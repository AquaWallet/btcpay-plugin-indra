git submodule update --init
dotnet publish BTCPayServer.Plugins.Indra -o ./publish
dotnet run --project btcpayserver/BTCPayServer.PluginPacker ./publish BTCPayServer.Plugins.Indra ./release
