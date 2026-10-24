# F-Droid preparation — 2026-10-24

This is a source export, not an accepted F-Droid submission.

## Verified locally

- Native AmneziaWG wrapper builds for arm64-v8a and x86_64 using Go and NDK r28c.
- Android client test project: 32 passed, 0 failed (Release).
- English and Russian Fastlane descriptions are present.
- Original client code has an MIT license. Third-party licenses are separate.

## Build blocker

With .NET SDK 10.0.401, Release APK restore fails with NU1102 for
`Microsoft.NETCore.App.Runtime.Mono.linux-x64` version `10.0.12`.
Passing `RunAOTCompilation=false` does not resolve it. No APK was built from
this export. Toolchain/workload compatibility must be resolved before claiming
the build instructions work or submitting a build recipe.

## Publication checklist

- Public repository: https://github.com/kazievnadir-max/blackeagle-android-
- Confirm redistribution rights for artwork and audit transitive dependencies.
- Resolve the APK build blocker and pin a working toolchain.
- Establish a source-build strategy for .NET/NuGet native dependencies acceptable
  to F-Droid; a successful local APK build alone does not establish compliance.
- Add licensed icon/screenshots and release changelog.
- Review service-related AntiFeatures (fixed endpoint and Telegram dependency).
- Test fdroiddata metadata in F-Droid's environment, then submit for review.
- Choose signing strategy before distributing a first F-Droid build.

Official guide: https://f-droid.org/docs/Submitting_to_F-Droid_Quick_Start_Guide/
