#!/bin/zsh
# Regenerate the MediaArchive icon and install it into the desktop app.
#
#   ./build.sh                 # build + install to ~/Applications/MediaArchive.app
#   ./build.sh /path/to.app    # install into a different bundle
#
# Requires macOS (sips, iconutil) and python3 with Pillow.
set -e
cd "$(dirname "$0")"

APP="${1:-$HOME/Applications/MediaArchive.app}"

echo "→ rendering PNGs (Cinzel Decorative → MA monogram)…"
python3 make_icon.py

echo "→ packing MediaArchive.icns…"
rm -rf MediaArchive.iconset && mkdir MediaArchive.iconset
specs=("16:icon_16x16" "32:icon_16x16@2x" "32:icon_32x32" "64:icon_32x32@2x" \
       "128:icon_128x128" "256:icon_128x128@2x" "256:icon_256x256" \
       "512:icon_256x256@2x" "512:icon_512x512" "1024:icon_512x512@2x")
for s in $specs; do
    sz="${s%%:*}"; name="${s##*:}"
    sips -z "$sz" "$sz" icon_1024.png --out "MediaArchive.iconset/${name}.png" >/dev/null
done
iconutil -c icns MediaArchive.iconset -o MediaArchive.icns
rm -rf MediaArchive.iconset

echo "→ updating the in-app favicon…"
cp icon_256.png ../wwwroot/favicon.png

if [[ -d "$APP" ]]; then
    echo "→ installing into $APP…"
    cp MediaArchive.icns "$APP/Contents/Resources/MediaArchive.icns"
    touch "$APP"
    /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$APP" || true
    echo "  (if the Dock/Finder icon doesn't refresh: killall Dock; killall Finder)"
else
    echo "! $APP not found — skipped install. MediaArchive.icns is ready to copy in."
fi

# keep the repo tidy: only the masters are versioned
rm -f icon_512.png icon_256.png icon_128.png icon_64.png icon_32.png icon_16.png
echo "✓ done"
