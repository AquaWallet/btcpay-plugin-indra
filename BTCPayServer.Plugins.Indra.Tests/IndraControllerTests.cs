using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Indra;
using BTCPayServer.Plugins.Indra.Models;
using BTCPayServer.Tests;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests
{
    [Trait("Integration", "Integration")]
    public class IndraControllerTests : IndraTestBase
    {
        public IndraControllerTests(ITestOutputHelper helper) : base(helper)
        {
        }

        [Fact]
        public async Task CanAccessStatusPage()
        {
            using var serverTester = CreateServerTesterWithIndra();
            var account = await serverTester.CreateTestStore();
            await serverTester.SetupIndraForStore(account.StoreId);
            var indraService = await serverTester.GetIndraService();
            Assert.NotNull(indraService.GetSettings(account.StoreId));

            var controller = serverTester.PayTester.GetController<IndraController>(account.UserId, account.StoreId, account.IsAdmin);
            Assert.NotNull(controller);
            Assert.NotNull(controller.Settings);
            var result = await controller.Status(account.StoreId);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<IndraInfo>(viewResult.Model);
            Assert.NotNull(model.Info);
            Assert.NotNull(model.Stats);
            Assert.NotNull(model.StandaloneWallet);
        }

        [Fact]
        public void AdminShowsNewestLogFilesFirst()
        {
            var logDirectory = Directory.CreateTempSubdirectory("indra-log-order-");
            try
            {
                var oldest = WriteLogFile(logDirectory, "boltz.log", DateTimeOffset.UtcNow.AddMinutes(-3));
                var newest = WriteLogFile(logDirectory, "indra-2.log", DateTimeOffset.UtcNow.AddMinutes(-1));
                var middle = WriteLogFile(logDirectory, "indra-1.log", DateTimeOffset.UtcNow.AddMinutes(-2));

                var logFiles = IndraController.GetLogFilesPage(logDirectory.GetFiles("indra*.log"), 0);

                Assert.Equal(new[] { newest.Name, middle.Name, oldest.Name }, logFiles.Select(file => file.Name));
            }
            finally
            {
                logDirectory.Delete(true);
            }
        }

        [Fact]
        public void IncludesLiquidWalletSyncIntervalInClientConfig()
        {
            var config = IndraDaemon.BuildConfig(
                new DaemonConfig { LiquidWalletSyncInterval = 60 },
                "regtest",
                new Uri("https://127.0.0.1:9002"));

            Assert.Contains("liquidWalletSyncInterval = 60", config);
        }

        [Fact]
        public void IncludesApiUrlInClientConfig()
        {
            var config = IndraDaemon.BuildConfig(
                new DaemonConfig { ApiUrl = new Uri("https://swaps.example.com/") },
                "regtest",
                new Uri("https://127.0.0.1:9002"));

            Assert.Contains("[Boltz]\nurl = \"https://swaps.example.com\"", config.ReplaceLineEndings("\n"));
        }

        [Fact]
        public void OmitsApiUrlFromClientConfigByDefault()
        {
            var config = IndraDaemon.BuildConfig(
                new DaemonConfig(),
                "regtest",
                new Uri("https://127.0.0.1:9002"));

            Assert.DoesNotContain("[Boltz]", config);
        }

        [Fact]
        public void DefaultsToIndraOnMainnet()
        {
            var config = IndraDaemon.BuildConfig(
                new DaemonConfig(),
                "mainnet",
                new Uri("https://127.0.0.1:9002"));

            Assert.Contains("[Boltz]\nurl = \"https://indra.aquabtc.com\"", config.ReplaceLineEndings("\n"));
        }

        private static FileInfo WriteLogFile(DirectoryInfo directory, string fileName, DateTimeOffset lastWriteTime)
        {
            var file = new FileInfo(Path.Combine(directory.FullName, fileName));
            File.WriteAllText(file.FullName, fileName);
            file.LastWriteTimeUtc = lastWriteTime.UtcDateTime;
            file.Refresh();
            return file;
        }
    }
}
