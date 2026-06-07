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
APP_VERSION="1.0.0"
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
    APP_BUNDLE=$(find "$OUT_DIR" -name "*.app" -maxdepth 2 2>/dev/null | head -1)

    if [[ -z "$APP_BUNDLE" ]]; then
        log "No .app bundle from publish — creating manually ..."

        APP_BUNDLE="$OUT_DIR/${APP_NAME}.app"
        MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
        RES_DIR="$APP_BUNDLE/Contents/Resources"
        mkdir -p "$MACOS_DIR" "$RES_DIR"

        # Binary
        BINARY=$(find "$OUT_DIR" -maxdepth 1 -name "AI.GitHubManager.App" -type f | head -1)
        [[ -z "$BINARY" ]] && BINARY=$(find "$OUT_DIR" -maxdepth 1 -type f -perm /111 | head -1)
        [[ -z "$BINARY" ]] && err "Could not find the published binary in $OUT_DIR"
        cp "$BINARY" "$MACOS_DIR/AI GitHub Manager"
        chmod +x "$MACOS_DIR/AI GitHub Manager"

        # Copy logo as icns placeholder (proper icns should be generated separately)
        cp "src/AI.GitHubManager.App/Assets/logo.png" "$RES_DIR/AppIcon.png"

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
    <key>CFBundleExecutable</key>        <string>AI GitHub Manager</string>
    <key>CFBundleIconFile</key>          <string>AppIcon</string>
    <key>CFBundlePackageType</key>       <string>APPL</string>
    <key>LSMinimumSystemVersion</key>    <string>12.0</string>
    <key>NSHighResolutionCapable</key>   <true/>
    <key>NSHumanReadableCopyright</key>  <string>© 2026 illeArts</string>
    <key>NSPrincipalClass</key>          <string>NSApplication</string>
</dict>
</plist>
PLIST
        ok "App bundle created → $APP_BUNDLE"
    else
        ok "App bundle found  → $APP_BUNDLE"
    fi

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
