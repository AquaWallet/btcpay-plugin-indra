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

The plugin runs [boltz-client](https://github.com/BoltzExchange/boltz-client)
(`boltzd`), maintained by Boltz, as its swap daemon and points it at the Indra
API on mainnet. The plugin downloads the pinned boltz-client release from
Boltz's GitHub releases and checks it against the embedded, signed
`boltz-client-manifest.txt` before starting it.

## Requirements

BTCPay Server 2.4.7 or later, on Linux (amd64 or arm64).

## Support

Open an issue on
[GitHub](https://github.com/AquaWallet/btcpay-plugin-indra/issues).
