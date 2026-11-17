#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Indra;

// The backup belongs to Indra. Never read or write the Boltz plugin's settings or files.
internal static class IndraMigration
{
    internal const string BackupKey = "indraMigration";
    internal static readonly PaymentMethodId PaymentMethod = PaymentTypes.LN.GetPaymentMethodId("BTC");

    internal static JObject? Connection(StoreData store) => store.GetPaymentMethodConfig(PaymentMethod) as JObject;
    internal static JObject? Backup(StoreData store) => store.GetStoreBlob().AdditionalData
        .TryGetValue(BackupKey, out var value) ? value as JObject : null;

    internal static bool IsIndra(StoreData? store)
    {
        var connection = store is null ? null : Connection(store);
        var value = connection?.GetValue("connectionString", StringComparison.OrdinalIgnoreCase)?.Value<string>();
        return value?.Split(';').Any(part => string.Equals(part.Trim(), "type=indra", StringComparison.OrdinalIgnoreCase)) == true;
    }

    internal static string Revision(StoreData store) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(new JObject
        {
            ["connection"] = Connection(store)?.DeepClone(),
            ["backup"] = Backup(store)?.DeepClone()
        }.ToString(Formatting.None))));

    internal static bool CanRestore(StoreData store) => Backup(store) is { } backup &&
        backup["restored"]?.Value<bool>() != true &&
        JToken.DeepEquals(Connection(store), backup["applied"]);

    internal static void Switch(StoreData store, JObject connection, string revision)
    {
        CheckRevision(store, revision);
        if (IsIndra(store) || Backup(store) is { } backup && backup["restored"]?.Value<bool>() != true)
            throw new InvalidOperationException("This store has already switched. Its original backup has been kept.");

        var blob = store.GetStoreBlob();
        blob.AdditionalData[BackupKey] = new JObject
        {
            ["previous"] = Connection(store)?.DeepClone(),
            ["applied"] = connection.DeepClone(),
            ["restored"] = false
        };
        store.SetStoreBlob(blob);
        store.SetPaymentMethodConfig(PaymentMethod, connection);
    }

    internal static void Restore(StoreData store, string revision)
    {
        CheckRevision(store, revision);
        if (!CanRestore(store))
            throw new InvalidOperationException("The Lightning configuration has changed. Nothing was restored; review the store's Lightning settings.");

        var blob = store.GetStoreBlob();
        var backup = (JObject)blob.AdditionalData[BackupKey];
        store.SetPaymentMethodConfig(PaymentMethod, backup["previous"]?.DeepClone() as JObject);
        backup["restored"] = true;
        store.SetStoreBlob(blob);
    }

    // Credential repairs and wallet changes must not invalidate a still-current backup.
    internal static void RefreshActive(StoreData store, JObject connection)
    {
        if (CanRestore(store))
        {
            var blob = store.GetStoreBlob();
            ((JObject)blob.AdditionalData[BackupKey])["applied"] = connection.DeepClone();
            store.SetStoreBlob(blob);
        }
        store.SetPaymentMethodConfig(PaymentMethod, connection);
    }

    private static void CheckRevision(StoreData store, string revision)
    {
        if (!string.Equals(Revision(store), revision, StringComparison.Ordinal))
            throw new InvalidOperationException("The Lightning configuration changed since this page was opened. Reload the page and review it before switching.");
    }
}
