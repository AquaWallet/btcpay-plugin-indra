VERSION := $(shell sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' BTCPayServer.Plugins.Indra/BTCPayServer.Plugins.Indra.csproj)
RELEASE_PATH := ./release/BTCPayServer.Plugins.Indra/$(VERSION)
BOLTZ_REGTEST ?= $(CURDIR)/regtest
BOLTZ_REGTEST_PATH := $(abspath $(BOLTZ_REGTEST))

gh-release:
	./release.sh
	git commit -a -m "chore: bump version to v$(VERSION)"
	git tag -s v$(VERSION) -m "v$(VERSION)"
	git push
	git push --tags
	cd $(RELEASE_PATH) && gpg --detach-sig SHA256SUMS
	gh release create v$(VERSION) --title v$(VERSION) --draft --notes-file release-notes-template.md $(RELEASE_PATH)/*

btcpay-appsettings:
	echo "{ \
	\"DEBUG_PLUGINS\": \"$(PWD)/BTCPayServer.Plugins.Indra/bin/Debug/net10.0/BTCPayServer.Plugins.Indra.dll\" \
	}" > ./btcpayserver/BTCPayServer/appsettings.dev.json

build:
	dotnet build BTCPayServer.Plugins.Indra

run:
	cd ./btcpayserver/BTCPayServer && \
	BTCPAY_BTCLIGHTNING="type=lnd-rest;server=https://127.0.0.1:8081/;macaroonfilepath=$(BOLTZ_REGTEST_PATH)/data/lnd1/data/chain/bitcoin/regtest/admin.macaroon;certfilepath=$(BOLTZ_REGTEST_PATH)/data/lnd1/tls.cert;allowinsecure=true" \
	dotnet run --launch-profile "Bitcoin-Indra"

dev: btcpay-appsettings build run

test: btcpay-appsettings
	$(eval BTC_COOKIE := $(shell docker exec boltz-bitcoind cat /app/bitcoin/regtest/.cookie 2>/dev/null))
	TESTS_BTCRPCCONNECTION="server=http://127.0.0.1:18443;$(BTC_COOKIE)" \
	TESTS_BTCNBXPLORERURL="http://127.0.0.1:32838/" \
	TESTS_POSTGRES="User ID=boltz;Password=boltz;Include Error Detail=true;Host=127.0.0.1;Port=5432;Database=btcpayserver" \
	TESTS_EXPLORER_POSTGRES="User ID=boltz;Password=boltz;Include Error Detail=true;Host=127.0.0.1;Port=5432;Database=nbxplorer" \
	dotnet test --project ./BTCPayServer.Plugins.Indra.Tests -- --output Detailed

