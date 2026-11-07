using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Indra.Models.Api;

/// <summary>
/// Request to enable Indra for a store
/// </summary>
public class IndraSetupRequest
{
    /// <summary>
    /// The wallet name to use for receiving lightning payments
    /// </summary>
    [Required]
    public string WalletName { get; set; } = string.Empty;
}

/// <summary>
/// Response containing current Indra setup status
/// </summary>
public class IndraSetupData
{
    /// <summary>
    /// Whether Indra is enabled for this store
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The configured wallet, if any
    /// </summary>
    public IndraWalletData? Wallet { get; set; }
}

/// <summary>
/// Request to create or import a wallet
/// </summary>
public class CreateIndraWalletRequest
{
    /// <summary>
    /// The name for the wallet
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The currency: "BTC" or "LBTC". Defaults to "LBTC" if not specified.
    /// </summary>
    public string Currency { get; set; } = "LBTC";

    /// <summary>
    /// Optional: 12 or 24 word mnemonic seed phrase for import
    /// </summary>
    public string? Mnemonic { get; set; }

    /// <summary>
    /// Optional: Bitcoin Core descriptor for watch-only import
    /// </summary>
    public string? CoreDescriptor { get; set; }
}

/// <summary>
/// Wallet balance information
/// </summary>
public class IndraWalletBalance
{
    /// <summary>
    /// Total balance in satoshis
    /// </summary>
    public ulong Total { get; set; }

    /// <summary>
    /// Confirmed balance in satoshis
    /// </summary>
    public ulong Confirmed { get; set; }

    /// <summary>
    /// Unconfirmed balance in satoshis
    /// </summary>
    public ulong Unconfirmed { get; set; }
}

/// <summary>
/// Wallet data returned by the API
/// </summary>
public class IndraWalletData
{
    /// <summary>
    /// Unique wallet identifier
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Wallet name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Wallet currency: "BTC" or "LBTC"
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Wallet balance
    /// </summary>
    public IndraWalletBalance? Balance { get; set; }

    /// <summary>
    /// Whether this is a watch-only (read-only) wallet
    /// </summary>
    public bool Readonly { get; set; }
}

/// <summary>
/// Response when creating a new wallet (includes mnemonic for backup)
/// </summary>
public class CreateIndraWalletResponse : IndraWalletData
{
    /// <summary>
    /// The mnemonic seed phrase. Only returned when creating a new wallet.
    /// </summary>
    public string? Mnemonic { get; set; }
}
