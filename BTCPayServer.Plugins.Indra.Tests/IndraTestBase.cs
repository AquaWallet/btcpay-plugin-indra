using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Hosting;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.Bitcoin;
using BTCPayServer.Plugins.Indra;
using BTCPayServer.Plugins;
using BTCPayServer.Tests;
using BTCPayServer.Tests.Logging;
using Microsoft.Extensions.DependencyInjection;
using NBXplorer;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using NBitcoin;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests
{
    public class IndraTestBase : UnitTestBase
    {
        private static readonly string SharedBoltzClientCacheDir = Path.Combine(
            Path.GetTempPath(),
            "btcpayserver-boltz-client-cache");

        public IndraTestBase(ITestOutputHelper helper) : base(helper)
        {
        }

        public BTCPayNetworkProvider CreateNetworkProviderWithIndra(ChainName chainName)
        {
            var conf = new ConfigurationRoot(new List<IConfigurationProvider>()
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource()
                {
                    InitialData = new[] {
                        new KeyValuePair<string, string>("chains", "*"),
                        new KeyValuePair<string, string>("network", chainName.ToString())
                    }
                })
            });
            return CreateNetworkProviderWithIndra(conf);
        }

        public BTCPayNetworkProvider CreateNetworkProviderWithIndra(IConfiguration conf = null)
        {
            conf ??= new ConfigurationRoot(new List<IConfigurationProvider>()
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource()
                {
                    InitialData = new[] {
                        new KeyValuePair<string, string>("chains", "*"),
                        new KeyValuePair<string, string>("network", "regtest")
                    }
                })
            });
            var bootstrap = Startup.CreateBootstrap(conf);
            var services = new PluginServiceCollection(new ServiceCollection(), bootstrap);
            var plugins = new List<BaseBTCPayServerPlugin>() { new BitcoinPlugin() };

            foreach (var p in plugins)
            {
                p.Execute(services);
            }
            services.AddSingleton(services.BootstrapServices.GetRequiredService<SelectedChains>());
            services.AddSingleton(services.BootstrapServices.GetRequiredService<NBXplorerNetworkProvider>());
            services.AddSingleton(services.BootstrapServices.GetRequiredService<Logs>());
            services.AddSingleton(services.BootstrapServices.GetRequiredService<IConfiguration>());
            services.AddSingleton<BTCPayNetworkProvider>();

            var indraPlugin = new IndraPlugin();
            indraPlugin.Execute(services);

            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetService<BTCPayNetworkProvider>();

        }

        public ServerTester CreateServerTesterWithIndra([CallerMemberNameAttribute] string scope = null, bool newDb = true)
        {
            ConfigureSharedBoltzClientCacheDir();
            var provider = CreateNetworkProviderWithIndra();
            Assert.NotNull(provider);
            Assert.NotNull(provider.GetNetwork<BTCPayNetwork>("BTC"));
            return new ServerTester(scope, newDb, TestLogs, TestLogProvider, provider);
        }

        public PlaywrightTester CreatePlaywrightTesterWithIndra([CallerMemberNameAttribute] string scope = null, bool newDb = true)
        {
            ConfigureSharedBoltzClientCacheDir();
            var tester = new PlaywrightTester
            {
                Server = new ServerTester(scope, newDb, TestLogs, TestLogProvider, CreateNetworkProviderWithIndra())
            };

            typeof(BTCPayServerTester)
                .GetProperty(nameof(BTCPayServerTester.InContainer))!
                .SetValue(tester.Server.PayTester, true);

            return tester;
        }

        private static void ConfigureSharedBoltzClientCacheDir()
        {
            Environment.SetEnvironmentVariable("BOLTZ_CLIENT_CACHE_DIR", SharedBoltzClientCacheDir);
        }
    }
}
