# Summary

TODO

**Do not attempt to install on BTCPay v1**

# Uploading to BTCPay

You can upload the `BTCPayServer.Plugins.Indra.btcpay` file to your btcpay server by navigating to `Plugins` and scrolling all the way down to `Upload Plugin`.

# Verifying the Release

In order to verify the release, you'll need to have `gpg` or `gpg2` installed on your system. You'll first need to import the keys that have signed this release if you haven't done so already:

```
TODO: URL of the AQUA release signing key | gpg --import
```

Once you have the required PGP keys, you can verify the release (assuming `SHA256SUMS` and `SHA256SUMS.sig` are in the current directory) with:

```
gpg --verify SHA256SUMS.sig
```

You should see the following if the verification was successful:

```
gpg: Good signature from "TODO: AQUA release signing key"
```

You should also verify that the hashes still match with the archive you've downloaded.

```
sha256sum --ignore-missing -c SHA256SUMS
```

If your archive is valid, you should see the following output:

```
BTCPayServer.Plugins.Indra.btcpay.json: OK
BTCPayServer.Plugins.Indra.btcpay: OK
```
