using System.Collections.Generic;
using Boltzrpc;

namespace BTCPayServer.Plugins.Indra.Models;

public class WalletsModel
{
    public List<Wallet> Wallets { get; set; } = new();
}
