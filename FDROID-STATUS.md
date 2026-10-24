# F-Droid preparation — 2026-10-24

This is a source export, not an accepted F-Droid submission.

## Verified locally

- Native AmneziaWG wrapper builds for arm64-v8a and x86_64 using Go and NDK r28c.
- Android client test project: 32 passed, 0 failed (Release).
- English and Russian Fastlane descriptions are present.
- Original client code has an MIT license. Third-party licenses are separate.
- Release ARM64 APK builds with `-r android-arm64`: version 0.5.1/code 11,
  min SDK 30, target SDK 36. ZIP alignment check with `zipalign -P 16` passed.
- Repeated after `dotnet clean` using build-release.sh: 0 warnings, 0 errors.
- 66 restored NuGet packages declare MIT or MIT AND Apache-2.0. See
  `docs/DEPENDENCIES.md`; this is not a complete embedded-component license audit.

## Local build findings

Building without an explicit runtime failed with NU1102 for Mono.linux-x64.
Using singular `-r android-arm64` resolved the build path; passing the plural
RuntimeIdentifiers property instead exposed an intermediate DLL copy failure.
The working invocation is in `clients/android/deploy/build-release.sh`.

Local ALT Linux's system code-signing certificate bundle lacked NuGet trust
roots (NU3042/NU3018). Verification was kept enabled: the SDK's codesignctl.pem
was read-only bind-mounted over that bundle in a temporary bwrap mount namespace
for the build. No system certificate files were modified.

Toolchain: SDK 10.0.401, Android workload 36.1.2, API/build-tools 36/36.0.0,
OpenJDK 17.0.20.1, NDK r28c, Go 1.27.1. SDK version is pinned in global.json.
This local build does not prove reproducibility or F-Droid build-server compliance.
No device was connected via ADB for runtime verification.

## Publication checklist

- Public repository: https://github.com/kazievnadir-max/blackeagle-android-
- Confirm redistribution rights for artwork and audit transitive dependencies.
- Validate runtime behaviour on a device and the x86_64 release build.
- Establish a source-build strategy for .NET/NuGet native dependencies acceptable
  to F-Droid; a successful local APK build alone does not establish compliance.
- Add licensed icon/screenshots and release changelog.
- Review service-related AntiFeatures (fixed endpoint and Telegram dependency).
- Test fdroiddata metadata in F-Droid's environment, then submit for review.
- Choose signing strategy before distributing a first F-Droid build.

`docs/FDROID-MAINTAINER-REQUEST.md` contains a draft packaging-policy enquiry.
No GitLab connector is available in this session; the request has not been sent.

Official guide: https://f-droid.org/docs/Submitting_to_F-Droid_Quick_Start_Guide/
