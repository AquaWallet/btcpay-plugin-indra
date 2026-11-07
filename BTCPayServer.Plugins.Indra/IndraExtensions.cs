using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Indra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBXplorer;
using Newtonsoft.Json.Linq;

public static class IndraExtensions
{
    public const string StoreBlobKey = "indra";

    public static IndraSettings GetIndraSettings(this StoreBlob storeBlob)
    {
        if (storeBlob.AdditionalData.TryGetValue(StoreBlobKey, out var rawS))
        {
            if (rawS is JObject rawObj)
            {
                return new Serializer(null).ToObject<IndraSettings>(rawObj);
            }
            else if (rawS.Type == JTokenType.String)
            {
                return new Serializer(null).ToObject<IndraSettings>(rawS.Value<string>());
            }
        }

        return null;
    }

    public static void SetIndraSettings(this StoreBlob storeBlob, IndraSettings settings)
    {
        if (settings is null)
        {
            storeBlob.AdditionalData.Remove(StoreBlobKey);
        }
        else
        {
            storeBlob.AdditionalData.AddOrReplace(StoreBlobKey, new Serializer(null).ToString(settings));
        }
    }
}
