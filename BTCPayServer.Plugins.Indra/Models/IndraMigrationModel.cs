namespace BTCPayServer.Plugins.Indra.Models;

public class IndraMigrationModel
{
    public string Revision { get; set; } = "";
    public bool Ready { get; set; }
    public bool Active { get; set; }
    public bool CanRestore { get; set; }
    public bool HasBackup { get; set; }
    public bool Restored { get; set; }
    public bool HasPreviousConnection { get; set; }
    public string? WalletName { get; set; }
    public bool ConfirmSettled { get; set; }
}
