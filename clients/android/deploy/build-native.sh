#!/usr/bin/env bash
set -euo pipefail
android_project=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
sdk_root=${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}
ndk_root=${ANDROID_NDK_HOME:-$sdk_root/ndk/28.2.13676358}
go_command=${GO_CMD:-go}
toolchain="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64/bin"
[[ -x "$toolchain/aarch64-linux-android24-clang" ]] || { echo 'Установите Android NDK 28.2.13676358.' >&2; exit 1; }
cd "$android_project/native"
"$go_command" mod download
for abi in arm64-v8a x86_64; do
    if [[ "$abi" == arm64-v8a ]]; then arch=arm64; compiler=aarch64-linux-android24-clang; else arch=amd64; compiler=x86_64-linux-android24-clang; fi
    mkdir -p "$android_project/artifacts/native/$abi"
    GOOS=android GOARCH="$arch" CGO_ENABLED=1 CC="$toolchain/$compiler" \
        "$go_command" build -trimpath -buildmode=c-shared \
        -ldflags='-s -w -extldflags=-Wl,-z,max-page-size=16384' \
        -o "$android_project/artifacts/native/$abi/libblackeagle_awg.so" .
done
module_dir=$("$go_command" list -m -f '{{.Dir}}' github.com/amnezia-vpn/amneziawg-go/v3)
install -m 644 "$module_dir/LICENSE" "$android_project/artifacts/native/AWG-LICENSE"
