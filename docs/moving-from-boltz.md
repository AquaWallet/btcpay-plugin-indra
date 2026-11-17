---
description:
  How the Indra plugin relates to the Boltz BTCPay Plugin, and how to move a
  store from Boltz to Indra.
---

# 🔀 Moving from Boltz

Indra is a fork of the Boltz BTCPay Plugin, but the two plugins are fully
separate:

- Indra has its own plugin identifier (`BTCPayServer.Plugins.Indra`), pages,
  Greenfield routes (`/indra/` instead of `/boltz/`) and settings.
- Indra runs its own indra-client daemon, with its data in `Plugins/Indra` and
  its RPC on port 9102. Boltz keeps `Plugins/Boltz` and port 9002.
- Lightning connection strings use `type=indra`, not `type=boltz`.

Installing Indra never reads, changes or removes anything belonging to the Boltz
plugin, and both can be installed on the same server.

## Moving a store

A store can use only one Lightning backend at a time. To move a store from Boltz
to Indra:

1. Open **Indra > Migration** and prepare a Liquid wallet. Creating or importing
   wallet credentials does not change the current Lightning connection. API and
   SamRock setup do not replace an existing backend; complete the switch here.
2. Pause new checkouts and automated payouts. Finish or expire old unpaid invoices,
   and complete pending swaps, refunds and payouts in Boltz. The migration page
   asks for your confirmation; it does not query Boltz or infer an empty queue
   from an offline daemon.
3. Select **Switch to Indra**. The page saves the complete previous Lightning
   configuration before switching new payments. Test a small payment before
   reopening checkout.
4. To switch back, first pause payments and finish Indra's pending work, then use
   **Restore previous Lightning connection**. If the connection was changed
   elsewhere, the page refuses to overwrite that change. Both plugins retain
   their wallets and history; restarting does not reactivate Indra after restore.

There is no database, swap, refund or payment-history import. Wallet import only
imports wallet credentials. Keep Boltz installed and running while any store or
unfinished payment still needs it. The same applies to Indra after switching back.

If you use a shared binary cache, configure `INDRA_CLIENT_CACHE_DIR` separately.
Indra does not read `BOLTZ_CLIENT_CACHE_DIR`.
