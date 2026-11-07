---
description:
  Install and configure the Indra BTCPay Plugin to accept Lightning payments
  non-custodially without running a Lightning node.
next:
  text: "🔀 Moving from Boltz"
  link: "/moving-from-boltz"
---

# 👋 Introduction

The Indra BTCPay Plugin lets any merchant accept Lightning payments without
running a Lightning node. Each Lightning payment is swapped through Indra into
L-BTC and paid to the merchant's own [Liquid](https://liquid.net/) wallet. The
plugin never holds the merchant's keys.

## How do I install the plugin?

**Merchants using a self-hosted server:** In BTCPay Server, go to "Manage
Plugins" and install "Indra" by AquaWallet. BTCPay Server 2.4.7 or later is
required.

**Merchants using a shared server:** Ask your server administrator to install
the plugin and enable it for tenants: as a server admin, open "Indra" in any
store, go to the "Admin" tab and turn on "Allow Plugin for non-admin users".

## Setting it up with AQUA

The easiest setup is through
[SamRock Protocol](https://github.com/rockstardev/SamRockProtocol). Install the
SamRock plugin, open SamRock in your store, and scan the QR code with AQUA.
SamRock creates a watch-only Liquid wallet in Indra from your AQUA wallet and
turns on Lightning for the store. Payments then arrive in AQUA.

## Setting it up manually

Open "Indra" in the store menu and follow the setup:

1. **Fees and swaps:** review the swap fees and how Lightning → Liquid swaps
   work.
2. **Wallet:** create a new Liquid wallet, or import an existing one. A
   watch-only (read-only) wallet is enough to receive payments.
3. **Chain swaps (optional):** set a maximum Liquid balance, above which the
   plugin swaps L-BTC to mainchain Bitcoin automatically.

When "Lightning" shows as active, make a small test purchase to check that
payments arrive. If something goes wrong, open an issue on
[GitHub](https://github.com/AquaWallet/btcpay-plugin-indra/issues).

## How does the plugin work under the hood?

The plugin is a UI around
[boltz-client](https://github.com/BoltzExchange/boltz-client), the swap daemon
maintained by Boltz. On startup it downloads the pinned boltz-client release,
checks it against the signed manifest bundled with the plugin, and runs it
pointed at the Indra API (`https://indra.aquabtc.com`) on mainnet.

The plugin also extends BTCPay's Greenfield API under
`/api/v1/stores/{storeId}/indra/`. You can access the merged server Swagger spec
at `https://<your-btcpay-server>/swagger/v1/swagger.json`.
