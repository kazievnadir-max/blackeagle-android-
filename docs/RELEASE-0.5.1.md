# Black Eagle VPN 0.5.1

Android 11+; ARM64 (arm64-v8a).

Updated settings icon for light and dark themes. Ad blocking is displayed as a
single label and switch, and the app version appears in smaller centred text.

## Installation

Download `BlackEagleVPN-0.5.1-arm64.apk` from the GitHub release assets.
Access requires a subscription or trial activated through
https://t.me/BlackEagleVPNbot.

This APK uses the project's new permanent signing key. Android cannot install
it as an update over an APK signed with the old debug key. Before changing
installations, ensure you can obtain a new activation link from the bot.
Uninstalling the old app removes its locally saved profile and settings.
Future updates signed with this permanent key can update this APK in place.

## Verification

APK SHA-256:
`345734e1d0c11f9594dbf485eb39934248b7800683ee1d40840ab061c99d0efb`

Signing certificate SHA-256:
`990b9d5a3fafd367facde05cceada668ba2f6210da84d2d07e5a2cfa820ce383`

Release build completed without warnings or errors. APK signature and 16 KB ZIP
alignment checks passed. An earlier debug-signed Release build was installed and
launched on Samsung SM-S911B / Android 16. The newly signed APK has not replaced
that installation because Android requires removal when the signing key changes.
