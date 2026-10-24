#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
dotnet_command=${DOTNET_CMD:-dotnet}
runtime=${ANDROID_RUNTIME:-android-arm64}
case "$runtime" in
  android-arm64|android-x64) ;;
  *) echo 'ANDROID_RUNTIME must be android-arm64 or android-x64' >&2; exit 1 ;;
esac
: "${ANDROID_SDK_ROOT:?Set ANDROID_SDK_ROOT}"
: "${JAVA_HOME:?Set JAVA_HOME to an OpenJDK 17 installation}"
cd "$repo_root"
bash clients/android/deploy/build-native.sh
"$dotnet_command" build clients/android/src/BlackEagle.Android/BlackEagle.Android.csproj \
  -c Release -r "$runtime" \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:JavaSdkDirectory="$JAVA_HOME"
