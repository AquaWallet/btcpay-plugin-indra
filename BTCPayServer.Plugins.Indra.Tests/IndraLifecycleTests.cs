#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Boltzrpc;
using BTCPayServer.Client.Models;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using BTCPayServer.Tests;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests
{
    /// <summary>
    /// What a merchant relies on beyond a single happy-path payment: a fresh setup that
    /// stays out of the Boltz plugin's way, payments after a restart or a crash of
    /// boltzd, and failed swaps being refunded.
    /// </summary>
    [Trait("Integration", "Integration")]
    [Trait("Lightning", "Lightning")]
    public class IndraLifecycleTests : IndraTestBase
    {
        private static readonly PaymentMethodId LightningPaymentMethodId = PaymentTypes.LN.GetPaymentMethodId("BTC");

        public IndraLifecycleTests(ITestOutputHelper helper) : base(helper)
        {
        }

        [Fact(Timeout = TestUtils.TestTimeout)]
        public async Task FreshSetupUsesIndrasOwnDaemonAndConnectionType()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await tester.SetupIndraForStore(account.StoreId);

            var store = await tester.PayTester.StoreRepository.FindStore(account.StoreId);
            var config = store!.GetPaymentMethodConfig(LightningPaymentMethodId);
            var connectionString = config?["connectionString"]?.Value<string>() ??
                                   config?["ConnectionString"]?.Value<string>();
            Assert.NotNull(connectionString);
            Assert.StartsWith("type=indra;server=https://127.0.0.1:9102", connectionString);

            var dataDir = tester.PayTester.GetService<IOptions<DataDirectories>>().Value.DataDir;
            var configFile = Path.Combine(dataDir, "Plugins", "Indra", "boltz.toml");
            Assert.True(File.Exists(configFile), $"boltzd config not found at {configFile}");
            var daemonConfig = (await File.ReadAllTextAsync(configFile)).ReplaceLineEndings("\n");
            Assert.Contains("port = 9102", daemonConfig);
            Assert.Contains("restDisabled = true", daemonConfig);
            Assert.False(Directory.Exists(Path.Combine(dataDir, "Plugins", "Boltz")),
                "Indra created the Boltz plugin's data directory");

            var client = await account.CreateClient();
            var invoice = await client.CreateInvoice(account.StoreId, new CreateInvoiceRequest { Amount = 5m, Currency = "USD" });
            var method = await IndraInvoiceFlowTests.WaitForLightningMethod(client, invoice.Id, LightningPaymentMethodId.ToString());
            await tester.PayWithBoltzRegtestLnd(method.Destination!);
            await IndraInvoiceFlowTests.AssertInvoiceSettled(client, account.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            await IndraInvoiceFlowTests.AssertIndraSettlementData(tester, account.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            await AssertWalletReceivedLbtc(tester, account.StoreId);
        }

        [Fact(Timeout = TestUtils.TestTimeout)]
        public async Task SettlesPaymentsAfterGracefulRestart()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await tester.SetupIndraForStore(account.StoreId);
            var indraService = await tester.GetIndraService();
            var settingsBefore = indraService.GetSettings(account.StoreId)!;

            var daemon = await tester.GetIndraDaemon();
            await daemon.Stop();
            Assert.False(daemon.Running);
            daemon.InitiateStart();
            await tester.WaitForIndraDaemon();

            // The store keeps its macaroon and wallet across the restart
            var settingsAfter = indraService.GetSettings(account.StoreId)!;
            IndraTestUtils.AssertIndraSettingsEqual(settingsBefore, settingsAfter);
            Assert.Equal(settingsBefore.StandaloneWallet!.Id, settingsAfter.StandaloneWallet!.Id);

            var client = await account.CreateClient();
            var invoice = await client.CreateInvoice(account.StoreId, new CreateInvoiceRequest { Amount = 5m, Currency = "USD" });
            var method = await IndraInvoiceFlowTests.WaitForLightningMethod(client, invoice.Id, LightningPaymentMethodId.ToString());
            await tester.PayWithBoltzRegtestLnd(method.Destination!);
            await IndraInvoiceFlowTests.AssertInvoiceSettled(client, account.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            await AssertWalletReceivedLbtc(tester, account.StoreId);
        }

        [Fact(Timeout = TestUtils.TestTimeout)]
        public async Task RecoversSwapWhenDaemonCrashesMidPayment()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await tester.SetupIndraForStore(account.StoreId);

            var client = await account.CreateClient();
            var invoice = await client.CreateInvoice(account.StoreId, new CreateInvoiceRequest { Amount = 5m, Currency = "USD" });
            var method = await IndraInvoiceFlowTests.WaitForLightningMethod(client, invoice.Id, LightningPaymentMethodId.ToString());

            // The swap exists; boltzd dies before the payer pays, so the lockup happens while it is down
            await tester.CrashIndraDaemon();
            var payment = tester.PayWithBoltzRegtestLnd(method.Destination!);

            // The plugin restarts boltzd on a non-zero exit, and boltzd recovers the pending swap
            await tester.WaitForIndraDaemon();
            await payment;
            await IndraInvoiceFlowTests.AssertInvoiceSettled(client, account.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            await IndraInvoiceFlowTests.AssertIndraSettlementData(tester, account.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            await AssertWalletReceivedLbtc(tester, account.StoreId);
        }

        [Fact(Timeout = TestUtils.TestTimeout)]
        public async Task RefundsSwapWhenLightningPaymentFails()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await tester.SetupIndraForStore(account.StoreId);
            await tester.FundIndraStandaloneWallet(account.StoreId, 0.01m);

            var indraService = await tester.GetIndraService();
            var settings = indraService.GetSettings(account.StoreId)!;
            var client = indraService.GetClient(account.StoreId)!;
            var walletId = settings.StandaloneWallet!.Id;

            // A hold invoice lets the swap's Lightning payment arrive and then be failed on purpose
            var holdInvoice = await tester.CreateBoltzRegtestLndHoldInvoice(50_000);
            var swap = await client.CreateSwap(new CreateSwapRequest
            {
                Invoice = holdInvoice.PaymentRequest,
                Pair = new Pair { From = Currency.Lbtc, To = Currency.Btc },
                SendFromInternal = true,
                WalletId = walletId
            });
            Assert.False(string.IsNullOrEmpty(swap.Id));

            await TestUtils.EventuallyAsync(async () =>
            {
                await tester.MineLiquidBlocks();
                var lndInvoice = await tester.GetBoltzRegtestLndInvoice(holdInvoice.PaymentHash);
                Assert.Equal("ACCEPTED", lndInvoice["state"]?.Value<string>());
            });

            await tester.CancelBoltzRegtestLndInvoice(holdInvoice.PaymentHash);

            GetSwapInfoResponse? info = null;
            await TestUtils.EventuallyAsync(async () =>
            {
                await tester.MineLiquidBlocks();
                info = await client.GetSwapInfo(swap.Id);
                Assert.Equal(SwapState.Refunded, info.Swap.State);
                Assert.False(string.IsNullOrEmpty(info.Swap.RefundTransactionId));
            }, TestUtils.TestTimeout);

            // The refund goes back to the store's own wallet
            await TestUtils.EventuallyAsync(async () =>
            {
                await tester.MineLiquidBlocks();
                var transactions = await client.ListWalletTransactions(new ListWalletTransactionsRequest { Id = walletId });
                Assert.Contains(transactions.Transactions, tx => tx.Id == info!.Swap.RefundTransactionId);
            });
        }

        [Fact(Timeout = TestUtils.TestTimeout)]
        public async Task LeavesBoltzPluginStateUntouched()
        {
            using var tester = CreateServerTesterWithIndra();
            var boltzAccount = await tester.CreateTestStore();
            var boltzStoreId = boltzAccount.StoreId;

            // What an existing Boltz plugin install leaves behind
            var settingsRepository = tester.PayTester.GetService<SettingsRepository>();
            var storeRepository = tester.PayTester.GetService<StoreRepository>();
            var serverSettings = new JObject { ["LogLevel"] = "info", ["AllowTenants"] = true };
            var storeSettings = new JObject
            {
                ["grpcUrl"] = "https://127.0.0.1:9002",
                ["macaroon"] = "0201036c6e64",
                ["standaloneWallet"] = new JObject { ["id"] = 1, ["name"] = "boltz-wallet" }
            };
            const string boltzConnectionString =
                "type=boltz;server=https://127.0.0.1:9002/;macaroon=0201036c6e64;walletId=1;allowinsecure=true";
            await settingsRepository.UpdateSetting(serverSettings, "Boltz");
            await storeRepository.UpdateSetting(boltzStoreId, "Boltz", storeSettings);

            var boltzStore = await storeRepository.FindStore(boltzStoreId);
            var blob = boltzStore!.GetStoreBlob();
            blob.AdditionalData["boltz"] = new JObject { ["walletName"] = "boltz-wallet" };
            boltzStore.SetStoreBlob(blob);
            boltzStore.SetPaymentMethodConfig(LightningPaymentMethodId,
                new JObject { ["ConnectionString"] = boltzConnectionString });
            await storeRepository.UpdateStore(boltzStore);

            var dataDir = tester.PayTester.GetService<IOptions<DataDirectories>>().Value.DataDir;
            var boltzDir = Path.Combine(dataDir, "Plugins", "Boltz");
            Directory.CreateDirectory(Path.Combine(boltzDir, "macaroons"));
            await File.WriteAllTextAsync(Path.Combine(boltzDir, "boltz.toml"), "network = \"regtest\"\n[RPC]\nport = 9002\n");
            await File.WriteAllBytesAsync(Path.Combine(boltzDir, "boltz.db"), RandomNumberGenerator.GetBytes(4096));
            await File.WriteAllBytesAsync(Path.Combine(boltzDir, "macaroons", "admin.macaroon"), RandomNumberGenerator.GetBytes(64));
            var filesBefore = HashDirectory(boltzDir);

            // Indra set up, used and restarted on the same server
            var indraAccount = await tester.CreateTestStore();
            await tester.SetupIndraForStore(indraAccount.StoreId);
            var client = await indraAccount.CreateClient();
            var invoice = await client.CreateInvoice(indraAccount.StoreId, new CreateInvoiceRequest { Amount = 5m, Currency = "USD" });
            var method = await IndraInvoiceFlowTests.WaitForLightningMethod(client, invoice.Id, LightningPaymentMethodId.ToString());
            await tester.PayWithBoltzRegtestLnd(method.Destination!);
            await IndraInvoiceFlowTests.AssertInvoiceSettled(client, indraAccount.StoreId, invoice.Id, LightningPaymentMethodId.ToString());
            var daemon = await tester.GetIndraDaemon();
            await daemon.Stop();
            daemon.InitiateStart();
            await tester.WaitForIndraDaemon();

            Assert.True(JToken.DeepEquals(serverSettings, await settingsRepository.GetSettingAsync<JObject>("Boltz")));
            Assert.True(JToken.DeepEquals(storeSettings, await storeRepository.GetSettingAsync<JObject>(boltzStoreId, "Boltz")));
            boltzStore = await storeRepository.FindStore(boltzStoreId);
            Assert.True(JToken.DeepEquals(new JObject { ["walletName"] = "boltz-wallet" },
                boltzStore!.GetStoreBlob().AdditionalData["boltz"]));
            Assert.Equal(boltzConnectionString,
                boltzStore.GetPaymentMethodConfig(LightningPaymentMethodId)?["ConnectionString"]?.Value<string>());
            Assert.Equal(filesBefore, HashDirectory(boltzDir));

            // Indra keeps its own settings under its own names
            Assert.NotNull(await settingsRepository.GetSettingAsync<IndraServerSettings>("Indra"));
            Assert.NotNull(await storeRepository.GetSettingAsync<IndraSettings>(indraAccount.StoreId, "Indra"));
        }

        private static async Task AssertWalletReceivedLbtc(ServerTester tester, string storeId)
        {
            var indraService = await tester.GetIndraService();
            var settings = indraService.GetSettings(storeId)!;
            var client = indraService.GetClient(storeId)!;
            await TestUtils.EventuallyAsync(async () =>
            {
                await tester.MineLiquidBlocks();
                var wallet = await client.GetWallet(settings.StandaloneWallet!.Id);
                Assert.True(wallet.Balance.Total > 0, "The settlement never reached the store's Liquid wallet");
            });
        }

        private static Dictionary<string, string> HashDirectory(string directory)
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(directory, path),
                    path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        }
    }
}
