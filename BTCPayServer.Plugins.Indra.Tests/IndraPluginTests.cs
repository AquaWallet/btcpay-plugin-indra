using System.Linq;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests;

public class IndraPluginTests
{
    [Fact]
    public void InstallsNextToTheBoltzPlugin()
    {
        var plugin = new IndraPlugin();

        Assert.Equal("BTCPayServer.Plugins.Indra", plugin.Identifier);
        Assert.DoesNotContain(plugin.Dependencies, d => d.Identifier == "BTCPayServer.Plugins.Boltz");
        var btcpay = Assert.Single(plugin.Dependencies, d => d.Identifier == nameof(BTCPayServer));
        Assert.Equal(">=2.4.7", btcpay.Condition);
    }

    [Fact]
    public void ReportsTheProjectVersion()
    {
        var plugin = new IndraPlugin();
        var assemblyVersion = typeof(IndraPlugin).Assembly.GetName().Version!;

        Assert.Equal(assemblyVersion.ToString(3), plugin.Version.ToString(3));
    }
}
