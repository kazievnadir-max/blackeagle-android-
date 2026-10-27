# Request for packaging: Black Eagle VPN

Prepared 2026-10-27. Not submitted.
Destination: https://gitlab.com/fdroid/rfp/-/issues/new
Suggested title: `Black Eagle VPN — com.blackeaglevpn.client`

## Checklist

- [ ] Full inclusion-policy compliance confirmed (NuGet/native dependency provenance remains under review).
- [ ] Complete duplicate check across the catalog and all issue trackers.
- [x] No matching requests found in fdroid/rfp for "Black Eagle" or fdroiddata issues for "blackeagle" on 2026-10-27.
- [ ] Complete Fastlane images/screenshots (descriptions and changelog are present).
- [x] The original app owner requests inclusion and approves publication.

## Application ID

`com.blackeaglevpn.client`

```yaml
Categories:
  - Connectivity
License: MIT
WebSite: https://blackeagle-vpn.com/
SourceCode: https://github.com/kazievnadir-max/blackeagle-android-
IssueTracker: https://github.com/kazievnadir-max/blackeagle-android-/issues
AutoName: Black Eagle VPN
RepoType: git
Repo: https://github.com/kazievnadir-max/blackeagle-android-.git
```

## Summary

AmneziaWG VPN client for the Black Eagle service

## Description

Black Eagle VPN connects a device to the Black Eagle service using AmneziaWG.
Users import an activation link from the Telegram bot, select a server and
connect. Features include server latency checks, favourites, subscription
information, per-app split tunnelling, light/dark themes and Russian/English UI.
An active subscription or trial is required. Android 11 or later is supported.

## Packaging request and known open items

The owner requests packaging in the main F-Droid catalog. The client and both
eagle images are MIT-licensed; third-party components retain their licenses.
This request seeks initial review and packaging guidance. A tested fdroiddata
build-server recipe is not yet available.

The app uses C#/.NET 10, Avalonia and a Go AmneziaWG wrapper. A local ARM64
Release build succeeded, including a rebuild after cleaning; 32 tests passed.
The pinned SDK and build command are documented in README.md and
clients/android/deploy/build-release.sh. No production-signing secrets are
required to compile the client. Reproducibility has not been established.

The AWG wrapper builds from Go source. .NET SDK/workload and NuGet supply
managed libraries, AndroidX bindings and native SkiaSharp/HarfBuzz binaries.
docs/DEPENDENCIES.md inventories declared package licenses; embedded notices,
fonts and source provenance still require full review. Please advise which
components must be built from source and whether the SDK/workload and any
NuGet binaries can be accepted under the trusted-source policy.

Proposed AntiFeatures: TetheredNet (fixed Black Eagle endpoint) and NonFreeNet
(Telegram activation and non-public service backend). The backend is not
included in the client source repository.

Release tagging, store screenshots, F-Droid build validation and signing
strategy remain open. An F-Droid-signed release would not update an installed
copy signed with a different developer key in place.
