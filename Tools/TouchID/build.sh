#!/bin/bash
set -euo pipefail
tool_dir="$(cd "$(dirname "$0")" && pwd)"
project_dir="$(cd "$tool_dir/../.." && pwd)"
output="$project_dir/Assets/Plugins/macOS/libTeam1TouchIdEmbedded.dylib"
mkdir -p "$(dirname "$output")"
build_dir="$(mktemp -d /private/tmp/team1-touchid.XXXXXX)"
build_output="$build_dir/libTeam1TouchIdEmbedded.dylib"
xcrun clang -fobjc-arc -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 \
    -framework Foundation -framework AppKit -framework LocalAuthentication -weak_framework LocalAuthenticationEmbeddedUI \
    -install_name @rpath/libTeam1TouchIdEmbedded.dylib \
    "$tool_dir/TouchIdBridge.m" -o "$build_output"
codesign --force --sign - "$build_output"
# 読み込み済みの Unity が参照するファイルを直接上書きしない。
mv -f "$build_output" "$output"
rmdir "$build_dir"
