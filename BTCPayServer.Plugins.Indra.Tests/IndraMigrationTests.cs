#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Autoswaprpc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests;

public class IndraMigrationTests
{
    [Fact]
    public void KeepsExistingIntegrationEntryPoint()
    {
        Assert.NotNull(typeof(IndraService).GetMethod(nameof(IndraService.Set), new[] { typeof(string), typeof(IndraSettings) }));
    }

    [Fact]
    public void SetupTabsCannotReadOrClearAnotherStoreOrBoltzWizard()
    {
        var context = new DefaultHttpContext();
        var tempData = new TempDataDictionary(context, new EmptyTempDataProvider());
        tempData["setupSettings"] = "legacy-boltz-state";
        tempData["chainSetup"] = "legacy-boltz-chain";
        var controller = new IndraController(null!, null!, null!, null!, NullLogger<IndraController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = tempData
        };
        var setup = typeof(IndraController).GetProperty("SetupSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var chain = typeof(IndraController).GetProperty("ChainSetup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        context.SetStoreData(new StoreData { Id = "a" });
        Assert.Null(setup.GetValue(controller));
        Assert.Null(chain.GetValue(controller));
        setup.SetValue(controller, new IndraSettings { TenantId = 11 });
        chain.SetValue(controller, new ChainConfig { FromWallet = "wallet-a" });
        context.SetStoreData(new StoreData { Id = "b" });
        Assert.Null(setup.GetValue(controller));
        Assert.Null(chain.GetValue(controller));
        setup.SetValue(controller, new IndraSettings { TenantId = 22 });
        chain.SetValue(controller, new ChainConfig { FromWallet = "wallet-b" });
        context.SetStoreData(new StoreData { Id = "a" });
        Assert.Equal(11UL, ((IndraSettings)setup.GetValue(controller)!).TenantId);
        Assert.Equal("wallet-a", ((ChainConfig)chain.GetValue(controller)!).FromWallet);
        typeof(IndraController).GetMethod("ClearSetup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
        context.SetStoreData(new StoreData { Id = "b" });
        Assert.Equal(22UL, ((IndraSettings)setup.GetValue(controller)!).TenantId);
        Assert.Equal("wallet-b", ((ChainConfig)chain.GetValue(controller)!).FromWallet);
        Assert.Equal("legacy-boltz-state", tempData.Peek("setupSettings"));
        Assert.Equal("legacy-boltz-chain", tempData.Peek("chainSetup"));
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static JObject IndraConnection() => new() { ["ConnectionString"] = "type=indra;server=https://127.0.0.1:9102;macaroon=fixture;walletId=1" };

    private static StoreData ExistingStore()
    {
        var store = new StoreData { Id = "migration-test" };
        store.SetPaymentMethodConfig(IndraMigration.PaymentMethod, new JObject
        {
            ["connectionString"] = "type=boltz;server=https://127.0.0.1:9002;macaroon=old-fixture;walletId=9",
            ["AllowUnsafeConnection"] = false,
            ["futureOption"] = new JObject { ["preserve"] = true }
        });
        return store;
    }

    [Fact]
    public void RestoresCompleteOriginalConfiguration()
    {
        var store = ExistingStore();
        var previous = IndraMigration.Connection(store)!.DeepClone();
        IndraMigration.Switch(store, IndraConnection(), IndraMigration.Revision(store));
        Assert.True(IndraMigration.IsIndra(store));
        Assert.True(IndraMigration.CanRestore(store));
        IndraMigration.Restore(store, IndraMigration.Revision(store));
        Assert.True(JToken.DeepEquals(previous, IndraMigration.Connection(store)));
        Assert.False(IndraMigration.CanRestore(store));
        Assert.True(IndraMigration.Backup(store)!["restored"]!.Value<bool>());
    }

    [Fact]
    public void StaleAndRepeatedSwitchCannotReplaceBackup()
    {
        var store = ExistingStore();
        var revision = IndraMigration.Revision(store);
        IndraMigration.Switch(store, IndraConnection(), revision);
        var saved = IndraMigration.Revision(store);
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Switch(store, IndraConnection(), revision));
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Switch(store, IndraConnection(), saved));
        Assert.Equal(saved, IndraMigration.Revision(store));
    }

    [Fact]
    public void RestoreDoesNotOverwriteManualConnectionChange()
    {
        var store = ExistingStore();
        IndraMigration.Switch(store, IndraConnection(), IndraMigration.Revision(store));
        var revision = IndraMigration.Revision(store);
        store.SetPaymentMethodConfig(IndraMigration.PaymentMethod, new JObject { ["InternalNodeRef"] = "Internal Node" });
        var changed = IndraMigration.Revision(store);
        Assert.False(IndraMigration.CanRestore(store));
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Restore(store, revision));
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Restore(store, changed));
        Assert.Equal(changed, IndraMigration.Revision(store));
    }

    [Fact]
    public void CurrentCredentialRepairKeepsOriginalBackup()
    {
        var store = ExistingStore();
        var previous = IndraMigration.Connection(store)!.DeepClone();
        IndraMigration.Switch(store, IndraConnection(), IndraMigration.Revision(store));
        var repaired = IndraConnection();
        repaired["ConnectionString"] = repaired["ConnectionString"]!.Value<string>()!.Replace("macaroon=fixture", "macaroon=repaired");
        IndraMigration.RefreshActive(store, repaired);
        Assert.True(IndraMigration.CanRestore(store));
        IndraMigration.Restore(store, IndraMigration.Revision(store));
        Assert.True(JToken.DeepEquals(previous, IndraMigration.Connection(store)));
    }

    [Fact]
    public void EmptyConnectionAndRepeatedRestoreAreSafe()
    {
        var store = new StoreData { Id = "empty" };
        IndraMigration.Switch(store, IndraConnection(), IndraMigration.Revision(store));
        var revision = IndraMigration.Revision(store);
        IndraMigration.Restore(store, revision);
        Assert.Null(IndraMigration.Connection(store));
        var restored = IndraMigration.Revision(store);
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Restore(store, revision));
        Assert.Throws<InvalidOperationException>(() => IndraMigration.Restore(store, restored));
        Assert.Equal(restored, IndraMigration.Revision(store));
    }
}
