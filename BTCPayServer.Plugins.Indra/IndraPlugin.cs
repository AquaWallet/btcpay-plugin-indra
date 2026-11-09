using System;
using System.Linq;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Hosting;
using BTCPayServer.Lightning;
using BTCPayServer.Payments;
using BTCPayServer.Services;
using BTCPayServer.Services.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBXplorer;

namespace BTCPayServer.Plugins.Indra;

public class IndraPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    [
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.4.7" }
    ];

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IndraDaemon>();
        services.AddSingleton<ILightningConnectionStringHandler>(provider =>
            provider.GetRequiredService<IndraLightningConnectionStringHandler>());
        services.AddSingleton<IndraLightningConnectionStringHandler>();
        services.AddSingleton<IndraService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<IndraService>());
        services.AddSingleton<ISwaggerProvider, IndraSwaggerProvider>();
        services.AddSingleton<IUIExtension>(new UIExtension("Indra/LNPaymentMethodSetupTab",
            "ln-payment-method-setup-tab"));
        services.AddSingleton<IUIExtension>(new UIExtension("Indra/LNPaymentMethodSetupTabhead",
            "ln-payment-method-setup-tabhead"));
        services.AddSingleton<IUIExtension>(new UIExtension("Indra/IndraInfo", "dashboard"));
        services.AddSingleton<IUIExtension>(new UIExtension("Indra/IndraNav", "store-integrations-nav"));

        var pluginServices = (PluginServiceCollection)services;
        ReplacePaymentsReportProvider(services);

        var nbxplorerNetworkProvider = pluginServices.BootstrapServices.GetRequiredService<NBXplorerNetworkProvider>();
        var btcNetwork = nbxplorerNetworkProvider.GetFromCryptoCode("BTC");

        var blockExplorerLink = btcNetwork.NBitcoinNetwork.ChainName == ChainName.Mainnet
            ? "https://liquid.network/tx/{0}"
            : "https://liquid.network/testnet/tx/{0}";
        services.AddTransactionLinkProvider(
            PaymentTypes.CHAIN.GetPaymentMethodId("LBTC"),
            new DefaultTransactionLinkProvider(blockExplorerLink));

        base.Execute(services);
    }

    private static void ReplacePaymentsReportProvider(IServiceCollection services)
    {
        var defaultPaymentsReportProvider = services.LastOrDefault(descriptor =>
            descriptor.ServiceType == typeof(ReportProvider) &&
            descriptor.ImplementationType == typeof(PaymentsReportProvider));
        if (defaultPaymentsReportProvider is not null)
        {
            services.Remove(defaultPaymentsReportProvider);
        }

        services.AddReportProvider<IndraPaymentsReportProvider>();
    }
}
