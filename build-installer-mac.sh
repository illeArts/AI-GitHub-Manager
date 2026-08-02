#!/usr/bin/env bash
# ============================================================
#  AI GitHub Manager — macOS Installer Build
#  Creates a signed .app bundle + .dmg for distribution.
#
#  Usage (on a Mac, from repo root):
#    bash build-installer-mac.sh            # auto-detects arch
#    bash build-installer-mac.sh --universal # arm64 + x64 (needs both SDKs)
#
#  Requirements:
#    • .NET 8 SDK   → https://dotnet.microsoft.com/download
#    • Xcode CLT    → xcode-select --install
#    • Homebrew gh  → brew install gh  (runtime, not build-time)
# ============================================================
set -euo pipefail

APP_NAME="AI GitHub Manager"
APP_VERSION="1.6.2"
BUNDLE_ID="com.illearts.AIGitHubManager"
PROJECT="src/AI.GitHubManager.App/AI.GitHubManager.App.csproj"
DIST_DIR="dist"
PUBLISH_BASE="publish"

UNIVERSAL=${1:-""}

# ── Helpers ──────────────────────────────────────────────────────────────────

log()  { echo "  [→] $*"; }
ok()   { echo "  [✓] $*"; }
err()  { echo "  [✗] $*" >&2; exit 1; }

check_tool() { command -v "$1" &>/dev/null || err "$1 not found. $2"; }

# ── Checks ───────────────────────────────────────────────────────────────────

echo ""
echo " ================================================"
echo "  $APP_NAME — macOS Installer Build"
echo " ================================================"
echo ""

check_tool dotnet  "Install from https://dotnet.microsoft.com/download"
check_tool hdiutil "This is part of macOS — should always be present."

ARCH=$(uname -m)
if [[ "$UNIVERSAL" == "--universal" ]]; then
    RIDS=("osx-arm64" "osx-x64")
else
    [[ "$ARCH" == "arm64" ]] && RIDS=("osx-arm64") || RIDS=("osx-x64")
fi

mkdir -p "$DIST_DIR"

# ── Build each architecture ───────────────────────────────────────────────────

for RID in "${RIDS[@]}"; do
    OUT_DIR="$PUBLISH_BASE/$RID"
    log "Publishing $RID ..."

    dotnet publish "$PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$OUT_DIR" 2>&1 | grep -v "^Build succeeded" | grep -v "^  " || true

    ok "Published → $OUT_DIR"

    # ── Locate or build .app bundle ──────────────────────────────────────────
    # We always assemble the bundle ourselves: dotnet publish for macOS RIDs
    # only produces the bare executable + native .dylib files side by side,
    # never a real .app bundle. A loose "AI.GitHubManager.App" file is not
    # something Finder can open — it must be wrapped in Contents/MacOS,
    # Contents/Resources and an Info.plist before it is distributable.
    log "Assembling .app bundle ..."

    APP_BUNDLE="$OUT_DIR/${APP_NAME}.app"
    MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
    RES_DIR="$APP_BUNDLE/Contents/Resources"
    rm -rf "$APP_BUNDLE"
    mkdir -p "$MACOS_DIR" "$RES_DIR"

    # Binary — keep the real assembly name so update checks / diagnostics
    # that shell out to "AI.GitHubManager.App" keep working inside the bundle.
    BINARY=$(find "$OUT_DIR" -maxdepth 1 -name "AI.GitHubManager.App" -type f | head -1)
    [[ -z "$BINARY" ]] && err "Could not find the published binary in $OUT_DIR"
    cp "$BINARY" "$MACOS_DIR/AI.GitHubManager.App"
    chmod +x "$MACOS_DIR/AI.GitHubManager.App"

    # Native Avalonia libraries must sit next to the executable to be found.
    for dylib in "$OUT_DIR"/*.dylib; do
        [[ -f "$dylib" ]] && cp "$dylib" "$MACOS_DIR/"
    done

    # Real .icns (generated ahead of time via iconutil/sips from Assets/logo.png
    # and checked in at src/AI.GitHubManager.App/Assets/AppIcon.icns).
    ICNS_SRC="src/AI.GitHubManager.App/Assets/AppIcon.icns"
    if [[ -f "$ICNS_SRC" ]]; then
        cp "$ICNS_SRC" "$RES_DIR/AppIcon.icns"
    else
        err "Missing $ICNS_SRC — regenerate it before building the installer."
    fi
    cp LICENSE "$RES_DIR/LICENSE.txt" 2>/dev/null || true

    # Info.plist
    cat > "$APP_BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>              <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>       <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>        <string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key>           <string>${APP_VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${APP_VERSION}</string>
    <key>CFBundleExecutable</key>        <string>AI.GitHubManager.App</string>
    <key>CFBundleIconFile</key>          <string>AppIcon.icns</string>
    <key>CFBundlePackageType</key>       <string>APPL</string>
    <key>CFBundleSignature</key>         <string>????</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key>    <string>12.0</string>
    <key>NSHighResolutionCapable</key>   <true/>
    <key>NSHumanReadableCopyright</key>  <string>© 2026 illeArts</string>
    <key>NSPrincipalClass</key>          <string>NSApplication</string>
</dict>
</plist>
PLIST

    if command -v plutil &>/dev/null; then
        plutil -lint "$APP_BUNDLE/Contents/Info.plist"
    fi

    # Ad-hoc sign so the bundle at least passes a basic codesign verification
    # locally. This is NOT a Developer ID signature and is NOT notarized —
    # see RELEASE_NOTES / scripts/verify-macos-app-bundle.sh for details.
    #
    # Finder/Spotlight can tag a freshly created .app bundle directory with a
    # com.apple.FinderInfo "has custom icon" extended attribute the moment it
    # notices the bundle — sometimes *after* a first xattr -cr but *before*
    # codesign runs. A single strip is not always enough, so retry a few
    # times right next to the codesign call.
    if command -v codesign &>/dev/null && command -v xattr &>/dev/null; then
        SIGN_OK=0
        for attempt in 1 2 3 4 5; do
            xattr -cr "$APP_BUNDLE"
            if codesign --force --deep --sign - "$APP_BUNDLE" 2>/tmp/codesign-err.log; then
                SIGN_OK=1
                break
            fi
            grep -q "resource fork\|FinderInfo\|detritus" /tmp/codesign-err.log || err "codesign failed: $(cat /tmp/codesign-err.log)"
            log "codesign attempt $attempt hit a stray Finder attribute — retrying ..."
            sleep 1
        done
        [[ "$SIGN_OK" == "1" ]] || err "codesign kept failing on extended attributes. Close any Finder window showing $APP_BUNDLE and re-run."
    fi

    ok "App bundle created → $APP_BUNDLE"

    # ── Create DMG ───────────────────────────────────────────────────────────
    ARCH_LABEL="${RID/osx-/}"   # arm64 or x64
    STAGING="$PUBLISH_BASE/${RID}-dmg-staging"
    DMG_NAME="AI_GitHub_Manager_${APP_VERSION}_macOS_${ARCH_LABEL}.dmg"
    DMG_PATH="$DIST_DIR/$DMG_NAME"

    log "Creating $DMG_NAME ..."

    rm -rf "$STAGING"
    mkdir -p "$STAGING"
    cp -R "$APP_BUNDLE" "$STAGING/"
    # Symlink to /Applications so the user can drag-install
    ln -s /Applications "$STAGING/Applications"

    # Remove existing dmg
    rm -f "$DMG_PATH"

    hdiutil create \
        -volname "$APP_NAME" \
        -srcfolder "$STAGING" \
        -ov \
        -format UDZO \
        -imagekey zlib-level=9 \
        "$DMG_PATH"

    rm -rf "$STAGING"
    ok "DMG ready → $DMG_PATH"
    echo ""
done

# ── Summary ───────────────────────────────────────────────────────────────────

echo " ================================================"
echo "  Done! Installers:"
for RID in "${RIDS[@]}"; do
    ARCH_LABEL="${RID/osx-/}"
    echo "    dist/AI_GitHub_Manager_${APP_VERSION}_macOS_${ARCH_LABEL}.dmg"
done
echo ""
echo "  Install:  Open the .dmg → drag app to Applications"
echo " ================================================"
echo ""
