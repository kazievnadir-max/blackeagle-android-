# Draft: packaging guidance for Black Eagle VPN (.NET / Avalonia)

Not submitted. This is a request for build-policy guidance, not a claim of
F-Droid compliance or a ready-to-merge build recipe.

Source: https://github.com/kazievnadir-max/blackeagle-android-

Application ID: `com.blackeaglevpn.client`

Version: 0.5.1 (versionCode 11), Android 11+, original client code MIT.

The application is a C#/.NET 10 and Avalonia client with an AmneziaWG Go native
wrapper. The repository contains Android sources and required shared code,
tests, build scripts and English/Russian Fastlane descriptions.

Access requires a subscription or trial obtained through a Telegram bot.
The client uses a fixed Black Eagle service endpoint. We propose declaring
`NonFreeNet` and `TetheredNet`; backend sources are not part of this repository.

The native AWG wrapper is built from Go module sources with Android NDK r28c.
The .NET Android SDK/workload, managed assemblies, AndroidX bindings, SkiaSharp
and HarfBuzz native assets currently come from NuGet/SDK packages. An inventory
of package-declared licenses is in `docs/DEPENDENCIES.md`; this does not establish
source provenance for every embedded component. Artwork permission confirmation,
embedded font/native notice review and F-Droid build-server validation remain open.

Before proposing an fdroiddata build, could maintainers advise which .NET and
NuGet dependencies can be accepted as trusted prebuilt components, and which
must be rebuilt from source using pinned upstream commits? We do not propose
blanket scanignore or deletion of license/provenance checks.

We have not claimed reproducible builds. Signing strategy needs agreement before
the first F-Droid release; an F-Droid signature would not update existing APKs
signed with the developer's different key in place.
