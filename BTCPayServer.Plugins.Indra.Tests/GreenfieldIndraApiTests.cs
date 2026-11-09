using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Plugins.Indra.Models.Api;
using BTCPayServer.Services;
using BTCPayServer.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests
{
    [Trait("Integration", "Integration")]
    public class GreenfieldIndraApiTests : IndraTestBase
    {
        public GreenfieldIndraApiTests(ITestOutputHelper helper) : base(helper)
        {
        }

        private GreenfieldIndraController CreateController(ServerTester tester, TestAccount account)
        {
            var indraService = tester.PayTester.GetService<IndraService>();
            var policiesSettings = tester.PayTester.GetService<PoliciesSettings>();
            var controller = new GreenfieldIndraController(indraService, policiesSettings);

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, account.UserId) };
            if (account.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, Roles.ServerAdmin));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            };
            return controller;
        }

        [Fact]
        public async Task CanGetSetupStatus()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            var controller = CreateController(tester, account);

            var result = await controller.GetSetup(account.StoreId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var setup = Assert.IsType<IndraSetupData>(okResult.Value);
            Assert.False(setup.Enabled);
        }

        [Fact]
        public async Task CanCreateAndListWallets()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await account.MakeAdmin();
            var controller = CreateController(tester, account);

            var createResult = await controller.CreateWallet(account.StoreId, new CreateIndraWalletRequest { Name = "test-wallet", Currency = "LBTC" });
            var okCreate = Assert.IsType<OkObjectResult>(createResult);
            var wallet = Assert.IsType<CreateIndraWalletResponse>(okCreate.Value);
            Assert.Equal("test-wallet", wallet.Name);
            Assert.True(wallet.Id > 0);

            var listResult = await controller.ListWallets(account.StoreId);
            var okList = Assert.IsType<OkObjectResult>(listResult);
            var wallets = Assert.IsType<List<IndraWalletData>>(okList.Value);
            Assert.Contains(wallets, w => w.Name == "test-wallet");

            await controller.RemoveWallet(account.StoreId, wallet.Id);
        }

        [Fact]
        public async Task CanEnableAndDisableIndra()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await account.MakeAdmin();
            var controller = CreateController(tester, account);

            var createResult = await controller.CreateWallet(account.StoreId, new CreateIndraWalletRequest { Name = "test-wallet", Currency = "LBTC" });
            var wallet = Assert.IsType<CreateIndraWalletResponse>(Assert.IsType<OkObjectResult>(createResult).Value);

            var enableResult = await controller.EnableSetup(account.StoreId, new IndraSetupRequest { WalletName = "test-wallet" });
            var enabled = Assert.IsType<IndraSetupData>(Assert.IsType<OkObjectResult>(enableResult).Value);
            Assert.True(enabled.Enabled);

            var disableResult = await controller.DisableSetup(account.StoreId);
            var disabled = Assert.IsType<IndraSetupData>(Assert.IsType<OkObjectResult>(disableResult).Value);
            Assert.False(disabled.Enabled);

            await controller.RemoveWallet(account.StoreId, wallet.Id);
        }

        [Fact]
        public async Task CannotDeleteWalletInUse()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await account.MakeAdmin();
            var controller = CreateController(tester, account);

            var createResult = await controller.CreateWallet(account.StoreId, new CreateIndraWalletRequest { Name = "test-wallet", Currency = "LBTC" });
            var wallet = Assert.IsType<CreateIndraWalletResponse>(Assert.IsType<OkObjectResult>(createResult).Value);

            await controller.EnableSetup(account.StoreId, new IndraSetupRequest { WalletName = "test-wallet" });

            var deleteResult = await controller.RemoveWallet(account.StoreId, wallet.Id);
            Assert.IsNotType<OkResult>(deleteResult);

            await controller.DisableSetup(account.StoreId);
            Assert.IsType<OkResult>(await controller.RemoveWallet(account.StoreId, wallet.Id));
        }

        [Fact]
        public async Task FullFlow()
        {
            using var tester = CreateServerTesterWithIndra();
            var account = await tester.CreateTestStore();
            await account.MakeAdmin();
            var controller = CreateController(tester, account);

            var setup = Assert.IsType<IndraSetupData>(Assert.IsType<OkObjectResult>(await controller.GetSetup(account.StoreId)).Value);
            Assert.False(setup.Enabled);

            var wallet = Assert.IsType<CreateIndraWalletResponse>(Assert.IsType<OkObjectResult>(
                await controller.CreateWallet(account.StoreId, new CreateIndraWalletRequest { Name = "api-wallet", Currency = "LBTC" })).Value);

            var wallets = Assert.IsType<List<IndraWalletData>>(Assert.IsType<OkObjectResult>(
                await controller.ListWallets(account.StoreId)).Value);
            Assert.Contains(wallets, w => w.Id == wallet.Id);

            var enabled = Assert.IsType<IndraSetupData>(Assert.IsType<OkObjectResult>(
                await controller.EnableSetup(account.StoreId, new IndraSetupRequest { WalletName = "api-wallet" })).Value);
            Assert.True(enabled.Enabled);

            Assert.IsNotType<OkResult>(await controller.RemoveWallet(account.StoreId, wallet.Id));

            var disabled = Assert.IsType<IndraSetupData>(Assert.IsType<OkObjectResult>(await controller.DisableSetup(account.StoreId)).Value);
            Assert.False(disabled.Enabled);

            Assert.IsType<OkResult>(await controller.RemoveWallet(account.StoreId, wallet.Id));
        }
    }
}
