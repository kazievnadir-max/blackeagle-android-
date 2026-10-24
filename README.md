# Black Eagle VPN for Android

Public source repository: https://github.com/kazievnadir-max/blackeagle-android-

Android 11+ client built with C#/.NET 10 and Avalonia UI, using AmneziaWG.
Access requires a Black Eagle VPN subscription or trial activated through the
Telegram bot https://t.me/BlackEagleVPNbot. The API endpoint is
https://blackeagle-vpn.com/.

This source export includes the Android client, shared client logic, UI library
and protocol contracts. The directory layout preserves existing project references.
It does not include the control plane, bot, node deployments or server secrets.

## Build

Install .NET SDK 10 with the Android workload, Android SDK/API 36, JDK 17,
Android NDK 28.2.13676358 and Go 1.25 or later. Set ANDROID_SDK_ROOT,
ANDROID_NDK_HOME and JAVA_HOME to their local installation paths.

```sh
bash clients/android/deploy/build-native.sh
dotnet build clients/android/src/BlackEagle.Android/BlackEagle.Android.csproj \
  -c Release -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:JavaSdkDirectory="$JAVA_HOME"
```

The native build script builds the AWG shared libraries and copies their upstream
license into the APK assets. Never commit signing keys or credentials.

## Licensing

Original client source in this repository is licensed under MIT; see LICENSE.
Third-party dependencies retain their own licenses. Their notices must be
preserved. Licensing/provenance of bundled artwork must be confirmed before
publication; the source license does not override third-party rights.

## F-Droid preparation status

Not submitted or accepted into the main F-Droid repository yet. Outstanding:

See [FDROID-STATUS.md](FDROID-STATUS.md) for verified checks and the current
Release APK build blocker. The commands above are not yet a validated F-Droid
build recipe.

- Validate a clean build and all dependency licenses/provenance.
- Agree on .NET SDK/workload and NuGet dependency handling with maintainers.
- Prepare and test fdroiddata build metadata and store artwork/descriptions.
- Declare the fixed service endpoint and Telegram dependency as appropriate.
- Decide signing/reproducible-build strategy before the first public release.
- Create version tags after validating the release build.
