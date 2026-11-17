# Indra BTCPay Plugin

Accept Lightning payments in BTCPay Server without running a Lightning node.
Incoming payments are swapped through [Indra](https://indra.aquabtc.com) and
settle to your own Liquid wallet, non-custodially. It is the Lightning backend
used by [SamRock Protocol](https://github.com/rockstardev/SamRockProtocol) when
you connect a store to [AQUA](https://aqua.net).

Indra is a fork of the
[Boltz BTCPay Plugin](https://github.com/BoltzExchange/boltz-btcpay-plugin). It
uses its own plugin identifier, routes, settings and data directory, so it can
be installed next to the Boltz plugin without touching Boltz's state.

## Documentation

See the [docs](https://aquawallet.github.io/btcpay-plugin-indra/).

## Swap engine

The plugin runs [indra-client](https://github.com/AquaWallet/indra-client)
(`boltzd`), JAN3's maintained fork of boltz-client, as its swap daemon and
points it at the Indra API on mainnet. The plugin downloads the pinned
indra-client release from its GitHub releases and checks it against the
embedded `boltz-client-manifest.txt` before starting it.

## Requirements

BTCPay Server 2.4.7 or later, on Linux (amd64 or arm64).

## Support

Open an issue on
[GitHub](https://github.com/AquaWallet/btcpay-plugin-indra/issues).

## Moving from Boltz

Open **Indra > Migration** in the store (or `/plugins/{storeId}/Indra/migration`).
Set up a Liquid wallet in Indra first. Preparing it leaves the existing Lightning
connection unchanged. Pause new checkouts and automated payouts, then finish or
expire unpaid invoices and complete swaps, refunds and payouts in the old plugin.
The page asks you to confirm this: it does not inspect Boltz or treat an offline
daemon as an empty queue.

**Switch to Indra** saves the previous Lightning configuration and changes new
payments to Indra. Test a small payment before reopening checkout. **Restore
previous Lightning connection** switches back, provided the connection has not
been changed elsewhere. First finish pending Indra payments. Both daemons, wallets,
settings and histories remain available; restarting does not undo a restore.

Wallet import accepts wallet credentials only. It does not move pending swaps,
refunds or history between engines. Keep the original plugin installed and running
until all its work is complete. There is no automatic database or pending-swap import.
API setup refuses to replace an existing Lightning backend: use the migration page
for that explicit switch. The optional binary cache is `INDRA_CLIENT_CACHE_DIR`;
Indra never uses `BOLTZ_CLIENT_CACHE_DIR`.
