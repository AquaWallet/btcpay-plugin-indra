#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Boltzrpc;
using BTCPayServer.Client.Models;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Tests;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace BTCPayServer.Plugins.Indra.Tests;

[Trait("Integration", "Integration")]
[Trait("Lightning", "Lightning")]
public class IndraMigrationFlowTests(ITestOutputHelper helper) : IndraTestBase(helper)
{
    [Fact(Timeout = TestUtils.TestTimeout)]
    [Trait("Playwright", "Playwright")]
    public async Task PreparesSwitchesAndRestoresSameStoreWithoutTouchingBoltz()
    {
        await using var browser = CreatePlaywrightTesterWithIndra();
        await browser.StartAsync();
        await browser.RegisterNewUser();
        await browser.CreateNewStore();
        var account = browser.AsTestAccount();
        var tester = browser.Server;
        await tester.EnsureIndraServerReady();
        var repository = tester.PayTester.StoreRepository;
        var store = (await repository.FindStore(account.StoreId))!;
        var previous = new JObject
        {
            ["ConnectionString"] = "type=boltz;server=https://127.0.0.1:9002;macaroon=fixture-old;walletId=9",
            ["AllowUnsafeConnection"] = false
        };
        store.SetPaymentMethodConfig(IndraMigration.PaymentMethod, previous);
        await repository.UpdateStore(store);
        var oldSettings = new JObject { ["wallet"] = "old", ["pendingSwap"] = "must-stay-in-boltz" };
        await repository.UpdateSetting(account.StoreId, "Boltz", oldSettings);
        var oldDirectory = Path.Combine(tester.PayTester.GetService<IOptions<DataDirectories>>().Value.DataDir, "Plugins", "Boltz");
        Directory.CreateDirectory(oldDirectory);
        var oldDatabase = Path.Combine(oldDirectory, "boltz.db");
        var oldBytes = new byte[] { 1, 7, 13, 24 };
        await File.WriteAllBytesAsync(oldDatabase, oldBytes);

        var service = await tester.GetIndraService();
        var settings = await service.InitializeStore(account.StoreId);
        var client = service.Daemon.GetClient(settings)!;
        settings.SetStandaloneWallet((await client.CreateWallet(new WalletParams { Name = "prepared", Currency = Currency.Lbtc })).Wallet);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Set(account.StoreId, settings));
        Assert.Null(service.GetSettings(account.StoreId));
        await service.Set(account.StoreId, settings, allowInactive: true);
        await AssertPrevious();
        Assert.False(await service.IsActive(account.StoreId));

        // Exercise the HTTP -> HTTPS startup repair path while another backend is active.
        settings.GrpcUrl = new UriBuilder(settings.GrpcUrl!) { Scheme = "http" }.Uri;
        await repository.UpdateSetting(account.StoreId, "Indra", settings);
        await tester.PayTester.RestartAsync();
        service = await tester.GetIndraService();
        repository = tester.PayTester.StoreRepository;
        await tester.WaitForIndraDaemon();
        await AssertPrevious();

        // A failed RPC readiness check must not save a backup or switch the connection.
        var validSettings = service.GetSettings(account.StoreId)!;
        var missingWallet = service.GetSettings(account.StoreId)!;
        missingWallet.StandaloneWallet!.Id = ulong.MaxValue;
        await service.Set(account.StoreId, missingWallet, allowInactive: true);
        var beforeFailure = await service.GetMigration(account.StoreId);
        await Assert.ThrowsAnyAsync<Exception>(() => service.Migrate(account.StoreId, beforeFailure.Revision, false));
        Assert.Equal(beforeFailure.Revision, (await service.GetMigration(account.StoreId)).Revision);
        await service.Set(account.StoreId, validSettings, allowInactive: true);

        // Compare-and-update must reject a core settings edit that races with migration.
        var staleStore = (await repository.FindStore(account.StoreId))!;
        var oldConfiguration = staleStore.DerivationStrategies;
        var oldBlob = staleStore.StoreBlob;
        IndraMigration.Switch(staleStore, new JObject { ["ConnectionString"] = "type=indra;server=https://fixture" }, IndraMigration.Revision(staleStore));
        var editedStore = (await repository.FindStore(account.StoreId))!;
        var editedBlob = editedStore.GetStoreBlob();
        editedBlob.AdditionalData["concurrent-edit"] = true;
        editedStore.SetStoreBlob(editedBlob);
        await repository.UpdateStore(editedStore);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateConnection(staleStore, oldConfiguration, oldBlob));
        var afterRace = (await repository.FindStore(account.StoreId))!;
        Assert.Null(IndraMigration.Backup(afterRace));
        Assert.True(afterRace.GetStoreBlob().AdditionalData["concurrent-edit"].Value<bool>());
        await AssertPrevious();

        var path = $"/plugins/{account.StoreId}/Indra/migration";
        await browser.GoToUrl(path);
        await Expect(browser.Page.Locator("h2")).ToHaveTextAsync("Move to Indra");
        var html = await browser.Page.ContentAsync();
        Assert.DoesNotContain("fixture-old", html);
        Assert.DoesNotContain(settings.Macaroon, html);
        Assert.Contains("Indra cannot verify Boltz", html);

        // Logged-in POSTs still require a CSRF token.
        var form = browser.Page.APIRequest.CreateFormData();
        form.Set("command", "switch");
        form.Set("ConfirmSettled", "true");
        form.Set("Revision", (await service.GetMigration(account.StoreId)).Revision);
        var withoutCsrf = await browser.Page.APIRequest.PostAsync(new Uri(new Uri(browser.Page.Url), path).ToString(),
            new APIRequestContextOptions { Form = form });
        Assert.Equal(400, withoutCsrf.Status);
        await AssertPrevious();

        // The same authenticated user and valid CSRF token cannot migrate another user's store.
        var other = tester.NewAccount();
        await other.GrantAccessAsync();
        var otherBefore = await service.GetMigration(other.StoreId);
        var csrf = await browser.Page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        form.Set("__RequestVerificationToken", csrf);
        var otherUrl = new Uri(new Uri(browser.Page.Url), $"/plugins/{other.StoreId}/Indra/migration").ToString();
        var forbidden = await browser.Page.APIRequest.PostAsync(otherUrl, new APIRequestContextOptions { Form = form, MaxRedirects = 0 });
        Assert.Equal(302, forbidden.Status);
        Assert.Contains("/errors/403", forbidden.Headers["location"]);
        Assert.Equal(otherBefore.Revision, (await service.GetMigration(other.StoreId)).Revision);

        await browser.Page.Locator("#confirm-switch").CheckAsync();
        await browser.Page.Locator("button[value=switch]").ClickAsync();
        await Expect(browser.Page.Locator("body")).ToContainTextAsync("New Lightning payments currently use Indra");
        Assert.True(await service.IsActive(account.StoreId));
        var api = await account.CreateClient();
        var invoice = await api.CreateInvoice(account.StoreId, new CreateInvoiceRequest { Amount = 5m, Currency = "USD" });
        var method = await IndraInvoiceFlowTests.WaitForLightningMethod(api, invoice.Id, "BTC-LN");
        await tester.PayWithBoltzRegtestLnd(method.Destination!);
        await IndraInvoiceFlowTests.AssertInvoiceSettled(api, account.StoreId, invoice.Id, "BTC-LN");
        await IndraInvoiceFlowTests.AssertIndraSettlementData(tester, account.StoreId, invoice.Id, "BTC-LN");

        await browser.Page.Locator("#confirm-restore").CheckAsync();
        await browser.Page.Locator("button[value=restore]").ClickAsync();
        await Expect(browser.Page.Locator("body")).ToContainTextAsync("previous Lightning configuration has been restored");
        await AssertPrevious();
        await service.Set(account.StoreId, service.GetSettings(account.StoreId), allowInactive: true);
        await AssertPrevious();
        await tester.PayTester.RestartAsync();
        service = await tester.GetIndraService();
        repository = tester.PayTester.StoreRepository;
        await tester.WaitForIndraDaemon();
        await AssertPrevious();
        Assert.False(await service.IsActive(account.StoreId));
        Assert.NotNull(service.GetSettings(account.StoreId)?.StandaloneWallet);
        Assert.True(JToken.DeepEquals(oldSettings, await repository.GetSettingAsync<JObject>(account.StoreId, "Boltz")));
        Assert.Equal(oldBytes, await File.ReadAllBytesAsync(oldDatabase));

        async Task AssertPrevious() => Assert.True(JToken.DeepEquals(previous,
            IndraMigration.Connection((await repository.FindStore(account.StoreId))!)));
    }
}
