---
description:
  Known limitations of the Indra BTCPay Plugin, including the lack of support
  for 0-amount invoices in pull payments and admin enablement requirements.
---

# 🚧 Limitations

This page lists known limitations of the Indra BTCPay Plugin

## Known Limitations

One **major limitation** that we are aware of today, is the lack of support for
0-amount invoices for pull payments. That's because 0-amount invoices can’t be
handled non-custodially by swaps. Nothing we can change about this fact, but the
real UX issue with the plugin is that we can’t display an error to end-users
when a 0-amount invoice is being used.

The current UX for end-users is as follows: when they try to claim a pull
payment payout using a 0-amount invoice, the payout shows as “Awaiting Payment”:

<figure><img src="./assets/awaiting_payment.png" alt=""><figcaption></figcaption></figure>

Only after manually refreshing the page does it become visible that the payout
was canceled:

<figure><img src="./assets/cancelled_payment.png" alt=""><figcaption></figcaption></figure>

The user has no way of knowing that the payment was canceled due to the use of a
0-amount invoice. Therefore, we currently recommend to be on the lookout for
pending pull payments and advise users to use wallets like Phoenix or Aqua,
which allow setting amounts for Lightning invoices.

To properly display an error message, BTCPay needs reworking of quite some
things internally and this will take some time, here a non-exhaustive list of
issues we are working on to make this happen:

- [https://github.com/btcpayserver/btcpayserver/pull/6219](https://github.com/btcpayserver/btcpayserver/pull/6219)
- [https://github.com/btcpayserver/btcpayserver/issues/6131](https://github.com/btcpayserver/btcpayserver/issues/6131)
- [https://github.com/btcpayserver/BTCPayServer.Lightning/pull/163](https://github.com/btcpayserver/BTCPayServer.Lightning/pull/163)
- [https://github.com/btcpayserver/btcpayserver/discussions/6221](https://github.com/btcpayserver/btcpayserver/discussions/6221)
- [https://github.com/btcpayserver/btcpayserver/discussions/6220](https://github.com/btcpayserver/btcpayserver/discussions/6220)

Also, as a security measure, BTCPay Server admins have to manually enable the
plugin for their users after installation: open "Indra" in any store, go to the
"Admin" tab and turn on "Allow Plugin for non-admin users".
