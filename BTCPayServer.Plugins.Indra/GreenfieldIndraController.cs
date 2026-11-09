using System;
using System.Linq;
using System.Threading.Tasks;
using Boltzrpc;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Indra.Models.Api;
using BTCPayServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Grpc.Core;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.Indra;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldIndraController(
    IndraService indraService,
    PoliciesSettings policiesSettings
) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Roles.ServerAdmin);
    private bool AllowCreateHot => IsAdmin || policiesSettings.AllowHotWalletForAll;


    private IActionResult IndraUnavailableError(string? message = null)  {
        return this.CreateAPIError("indra-unavailable", message ?? "Indra swap client is not available, ensure Indra is properly configured.");
    }

    private IActionResult IndraError(RpcException e) {
        return this.CreateAPIError("indra-error", e.Status.Detail);
    }

    [HttpGet("~/api/v1/stores/{storeId}/indra/setup")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> GetSetup(string storeId)
    {
        var settings = indraService.GetSettings(storeId);
        var response = new IndraSetupData
        {
            Enabled = settings is not null
        };

        var client = indraService.GetClient(storeId);
        if (settings?.StandaloneWallet != null && client != null)
        {
            try
            {
                var wallet = await client.GetWallet(settings.StandaloneWallet.Name);
                response.Wallet = ToWalletData(wallet);
            }
            catch (RpcException e)
            {
                return IndraError(e);
            }
        }

        return Ok(response);
    }

    [HttpPost("~/api/v1/stores/{storeId}/indra/setup")]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> EnableSetup(string storeId, [FromBody] IndraSetupRequest request)
    {
        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        try
        {
            var client = await indraService.GetOrCreateClient(storeId);
            if (client is null)
            {
                return IndraUnavailableError();

            }

            var settings = indraService.GetSettings(storeId)!;
            var wallet = await client.GetWallet(request.WalletName);
            settings.SetStandaloneWallet(wallet);
            await indraService.Set(storeId, settings);

            return Ok(new IndraSetupData
            {
                Enabled = true,
                Wallet = ToWalletData(wallet)
            });
        }
        catch (RpcException e)
        {
                return IndraError(e);
        }
        catch (InvalidOperationException e)
        {
            return IndraUnavailableError(e.Message);
        }
        catch (Exception e)
        {
            return this.CreateAPIError("setup-failed", e.Message);
        }
    }

    [HttpDelete("~/api/v1/stores/{storeId}/indra/setup")]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> DisableSetup(string storeId)
    {
        try
        {
            await indraService.Set(storeId, null);
            return Ok(new IndraSetupData { Enabled = false });
        }
        catch (Exception e)
        {
            return this.CreateAPIError("disable-failed", e.Message);
        }
    }

    [HttpGet("~/api/v1/stores/{storeId}/indra/wallets")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> ListWallets(string storeId)
    {
        try
        {
            var client = indraService.GetClient(storeId);
            if (client == null)
            {
                return IndraUnavailableError();
            }

            var wallets = await client.GetWallets(true);
            var result = wallets.Wallets_.Select(ToWalletData).ToList();
            return Ok(result);
        }
        catch (RpcException e)
        {
                return IndraError(e);
        }
    }

    [HttpPost("~/api/v1/stores/{storeId}/indra/wallets")]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> CreateWallet(string storeId, [FromBody] CreateIndraWalletRequest request)
    {
        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
        {
            ModelState.AddModelError(nameof(request.Currency), "Invalid currency. Use 'BTC' or 'LBTC'.");
            return this.CreateValidationError(ModelState);
        }

        var walletParams = new WalletParams
        {
            Name = request.Name,
            Currency = currency
        };

        try
        {
            var client = await indraService.GetOrCreateClient(storeId);
            if (client == null)
            {
                return IndraUnavailableError();
            }

            bool isImport = !string.IsNullOrEmpty(request.Mnemonic) || !string.IsNullOrEmpty(request.CoreDescriptor);

            if (!isImport)
            {
                if (!AllowCreateHot)
                {
                    return this.CreateAPIError(403, "hot-wallet-disabled", "Hot wallet creation is not allowed on this server");
                }

                var createResponse = await client.CreateWallet(walletParams);
                var wallet = await client.GetWallet(request.Name);

                return Ok(new CreateIndraWalletResponse
                {
                    Id = wallet.Id,
                    Name = wallet.Name,
                    Currency = wallet.Currency.ToString().ToUpper(),
                    Balance = wallet.Balance != null ? new IndraWalletBalance
                    {
                        Total = wallet.Balance.Total,
                        Confirmed = wallet.Balance.Confirmed,
                        Unconfirmed = wallet.Balance.Unconfirmed
                    } : null,
                    Readonly = wallet.Readonly,
                    Mnemonic = createResponse.Mnemonic
                });
            }
            else
            {
                var credentials = new WalletCredentials();
                if (!string.IsNullOrEmpty(request.Mnemonic))
                {
                    if (!AllowCreateHot)
                    {
                        return this.CreateAPIError(403, "mnemonic-import-disabled", "Mnemonic import is not allowed on this server");
                    }
                    credentials.Mnemonic = request.Mnemonic;
                }
                if (!string.IsNullOrEmpty(request.CoreDescriptor))
                {
                    credentials.CoreDescriptor = request.CoreDescriptor;
                }

                var wallet = await client.ImportWallet(walletParams, credentials);

                return Ok(new IndraWalletData
                {
                    Id = wallet.Id,
                    Name = wallet.Name,
                    Currency = wallet.Currency.ToString().ToUpper(),
                    Balance = wallet.Balance != null ? new IndraWalletBalance
                    {
                        Total = wallet.Balance.Total,
                        Confirmed = wallet.Balance.Confirmed,
                        Unconfirmed = wallet.Balance.Unconfirmed
                    } : null,
                    Readonly = wallet.Readonly
                });
            }
        }
        catch (RpcException e)
        {
                return IndraError(e);
        }
        catch (Exception e)
        {
            return this.CreateAPIError("wallet-creation-failed", e.Message);
        }
    }

    [HttpDelete("~/api/v1/stores/{storeId}/indra/wallets/{walletId}")]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> RemoveWallet(string storeId, ulong walletId)
    {
        if (indraService.IsWalletInUse(storeId, walletId))
        {
            return this.CreateAPIError("wallet-in-use", "Cannot delete the wallet currently configured for lightning payments. Disable Indra first or switch to a different wallet.");
        }

        try
        {
            var client = await indraService.GetOrCreateClient(storeId);
            if (client == null)
            {
                return IndraUnavailableError();
            }

            await client.RemoveWallet(walletId);
            return Ok();
        }
        catch (RpcException e)
        {
                return IndraError(e);
        }
    }

    private static IndraWalletData ToWalletData(Wallet wallet)
    {
        return new IndraWalletData
        {
            Id = wallet.Id,
            Name = wallet.Name,
            Currency = wallet.Currency.ToString().ToUpper(),
            Balance = wallet.Balance != null ? new IndraWalletBalance
            {
                Total = wallet.Balance.Total,
                Confirmed = wallet.Balance.Confirmed,
                Unconfirmed = wallet.Balance.Unconfirmed
            } : null,
            Readonly = wallet.Readonly
        };
    }
}
