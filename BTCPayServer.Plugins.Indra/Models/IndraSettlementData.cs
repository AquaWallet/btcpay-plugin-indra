#nullable enable

namespace BTCPayServer.Plugins.Indra.Models;

public class IndraSettlementData
{
    public string? SwapId { get; set; }
    public string? SettlementCurrency { get; set; }
    public string? SettlementAddress { get; set; }
    public string? SettlementTransactionId { get; set; }
}
