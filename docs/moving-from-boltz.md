---
description: How the Indra plugin relates to the Boltz BTCPay Plugin, and how to move a store from Boltz to Indra.
---

# 🔀 Moving from Boltz

Indra is a fork of the Boltz BTCPay Plugin, but the two plugins are fully separate:

- Indra has its own plugin identifier (`BTCPayServer.Plugins.Indra`), pages, Greenfield routes (`/indra/` instead of `/boltz/`) and settings.
- Indra runs its own boltz-client daemon, with its data in `Plugins/Indra` and its RPC on port 9102. Boltz keeps `Plugins/Boltz` and port 9002.
- Lightning connection strings use `type=indra`, not `type=boltz`.

Installing Indra never reads, changes or removes anything belonging to the Boltz plugin, and both can be installed on the same server.

## Moving a store

A store can use only one Lightning backend at a time. To move a store from Boltz to Indra:

1. Let any pending Boltz swaps finish. Check the Boltz plugin's "Swaps" page; anything still pending or refundable has to be dealt with in Boltz.
2. Install Indra, then set it up for the store, either with SamRock and AQUA or manually by importing the same Liquid wallet.
3. Indra replaces the store's Lightning connection with its own. Boltz's data and wallets stay where they were, so you can switch back at any time.
4. Once nothing on the server uses Boltz any more, you can uninstall it.
