#!/bin/sh
set -eu

source_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
build_dir="$1"
app="$build_dir/Team1 Screen Time Capture.app/Contents"
binary="$app/MacOS/ScreenTimeCapture"

if [ ! -x "$binary" ] || [ "$source_dir/main.swift" -nt "$binary" ] || [ "$source_dir/UsageRecognition.swift" -nt "$binary" ] || [ "$source_dir/run.sh" -nt "$binary" ]; then
    mkdir -p "$app/MacOS"
    cat > "$app/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key><string>com.team1.screen-time-capture</string>
    <key>CFBundleExecutable</key><string>ScreenTimeCapture</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleName</key><string>Team1 Screen Time Capture</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSUIElement</key><true/>
    <key>NSScreenCaptureUsageDescription</key><string>選択したシステム設定のウィンドウを1枚撮影します。</string>
</dict>
</plist>
PLIST
    for arch in arm64 x86_64; do
        /usr/bin/xcrun swiftc -target "$arch-apple-macos15.2" -module-cache-path "$build_dir/ModuleCache-$arch" \
            "$source_dir/main.swift" "$source_dir/UsageRecognition.swift" -o "$build_dir/ScreenTimeCapture-$arch"
    done
    /usr/bin/xcrun lipo -create "$build_dir/ScreenTimeCapture-arm64" "$build_dir/ScreenTimeCapture-x86_64" -output "$binary"
    /usr/bin/codesign --force --sign - "${app%/Contents}"
fi

case "${2:-}" in
    --build-only) ;;
    --embed)
        player="$3"
        signing=$(/usr/bin/codesign --display --verbose=2 "$player" 2>&1 || :)
        case "$signing" in
            *Authority=*)
                printf '%s\n' '撮影用アプリは配布用署名の前に同梱してください。' >&2
                exit 1
                ;;
        esac
        mkdir -p "$player/Contents/Helpers"
        /usr/bin/ditto "${app%/Contents}" "$player/Contents/Helpers/Team1 Screen Time Capture.app"
        /usr/bin/codesign --force --sign - --preserve-metadata=identifier,entitlements,flags,runtime "$player"
        /usr/bin/codesign --verify --deep "$player"
        ;;
    *)
        shift
        exec "$binary" "$@"
        ;;
esac
