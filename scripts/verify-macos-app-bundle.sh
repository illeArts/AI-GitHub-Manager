#!/usr/bin/env bash
# verify-macos-app-bundle.sh
#
# Run this ON A MAC after pulling branch fix/v1.6.2-macos-app-bundle.
# It builds both macOS .app bundles locally exactly the way the
# release-v1.6.2.yml "macos-packages" job does, ad-hoc signs them,
# and runs every check from the acceptance criteria.
#
# Usage:
#   cd "AI GitHub Manager"          # repo root
#   bash scripts/verify-macos-app-bundle.sh
#
# Requirements on the Mac:
#   • .NET 8 SDK   — https://dotnet.microsoft.com/download
#   • Xcode CLT    — xcode-select --install   (provides codesign, spctl, plutil, ditto, file)

set -euo pipefail

APP_NAME="AI GitHub Manager"
VERSION="1.6.2"
BUNDLE_ID="com.illearts.AIGitHubManager"
PROJECT="src/AI.GitHubManager.App/AI.GitHubManager.App.csproj"

log()  { echo "  [→] $*"; }
ok()   { echo "  [✓] $*"; }
err()  { echo "  [✗] $*" >&2; exit 1; }

command -v dotnet   &>/dev/null || err "dotnet not found. Install .NET 8 SDK."
command -v codesign &>/dev/null || err "codesign not found. Run: xcode-select --install"
command -v spctl     &>/dev/null || err "spctl not found. Run: xcode-select --install"
command -v plutil    &>/dev/null || err "plutil not found. Run: xcode-select --install"
command -v ditto     &>/dev/null || err "ditto not found. Run: xcode-select --install"

for RID in osx-arm64 osx-x64; do
  case "$RID" in
    osx-arm64) ARCH_LABEL=arm64; EXPECT_ARCH=arm64 ;;
    osx-x64)   ARCH_LABEL=x64;   EXPECT_ARCH=x86_64 ;;
  esac

  echo ""
  echo "==================== $RID ===================="

  log "Publishing self-contained build for $RID ..."
  rm -rf "artifacts/publish/$RID"
  dotnet publish "$PROJECT" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None -p:DebugSymbols=false \
    -o "artifacts/publish/$RID"
  ok "Published"

  APP="artifacts/appbundle/$RID/${APP_NAME}.app"
  rm -rf "artifacts/appbundle/$RID"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

  cp "artifacts/publish/$RID/AI.GitHubManager.App" "$APP/Contents/MacOS/AI.GitHubManager.App"
  cp "artifacts/publish/$RID/"*.dylib "$APP/Contents/MacOS/" 2>/dev/null || true
  chmod +x "$APP/Contents/MacOS/AI.GitHubManager.App"
  cp src/AI.GitHubManager.App/Assets/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
  cp LICENSE "$APP/Contents/Resources/LICENSE.txt"

  cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>              <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>       <string>${APP_NAME}</string>
    <key>CFBundleExecutable</key>        <string>AI.GitHubManager.App</string>
    <key>CFBundleIdentifier</key>        <string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key>           <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundlePackageType</key>       <string>APPL</string>
    <key>CFBundleSignature</key>         <string>????</string>
    <key>CFBundleIconFile</key>          <string>AppIcon.icns</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key>    <string>12.0</string>
    <key>NSHighResolutionCapable</key>   <true/>
    <key>NSHumanReadableCopyright</key>  <string>© 2026 illeArts</string>
    <key>NSPrincipalClass</key>          <string>NSApplication</string>
    <key>LSApplicationCategoryType</key> <string>public.app-category.developer-tools</string>
</dict>
</plist>
PLIST
  ok "Bundle assembled → $APP"

  log "file check ..."
  file "$APP/Contents/MacOS/AI.GitHubManager.App"
  file "$APP/Contents/MacOS/AI.GitHubManager.App" | grep -q "$EXPECT_ARCH" \
    && ok "Architecture matches ($EXPECT_ARCH)" \
    || err "Architecture mismatch! Expected $EXPECT_ARCH"

  log "plutil -lint ..."
  plutil -lint "$APP/Contents/Info.plist" && ok "Info.plist valid"

  log "No Windows binaries check ..."
  if find "$APP" -iname "*.exe" -o -iname "*.msi" | grep -q .; then
    err "Windows binaries found inside $APP"
  fi
  ok "No Windows binaries"

  log "Ad-hoc code signing (NOT a Developer ID signature, NOT notarized) ..."
  codesign --force --deep --sign - "$APP"
  codesign --verify --deep --strict --verbose=2 "$APP" && ok "codesign --verify passed"

  log "spctl assessment (Gatekeeper) ..."
  spctl --assess --type execute --verbose=4 "$APP" || \
    echo "  [!] spctl rejected the app — EXPECTED for ad-hoc signing without Developer ID + notarization."

  ZIP_DIR="artifacts/dist"
  mkdir -p "$ZIP_DIR"
  ZIP_NAME="AI-GitHub-Manager-v${VERSION}-macos-${ARCH_LABEL}.zip"
  ZIP_PATH="$ZIP_DIR/$ZIP_NAME"
  rm -f "$ZIP_PATH"

  log "Zipping with ditto ..."
  ( cd "artifacts/appbundle/$RID" && ditto -c -k --sequesterRsrc --keepParent \
      "${APP_NAME}.app" "$OLDPWD/$ZIP_PATH" )
  shasum -a 256 "$ZIP_PATH" | awk -v n="$ZIP_NAME" '{print $1"  "n}' > "$ZIP_PATH.sha256"
  ok "Archive created → $ZIP_PATH"

  log "Unpack spot-check ..."
  rm -rf /tmp/verify-unzip && mkdir -p /tmp/verify-unzip
  ditto -x -k "$ZIP_PATH" /tmp/verify-unzip
  test -d "/tmp/verify-unzip/${APP_NAME}.app" || err "No .app after unzip — packaging is broken."
  test -x "/tmp/verify-unzip/${APP_NAME}.app/Contents/MacOS/AI.GitHubManager.App" || err "Executable bit lost after unzip."
  ok "Unpacked ZIP contains a real .app bundle with executable bit set"

  echo ""
  echo "  Now test manually:"
  echo "    open \"/tmp/verify-unzip/${APP_NAME}.app\""
  echo "    (and double-click ${APP_NAME}.app in Finder at $ (pwd)/$APP)"
done

echo ""
echo "==================== Summary ===================="
echo "Ad-hoc signing was used (codesign --sign -)."
echo "This is NOT a Developer ID signature and the app is NOT notarized."
echo "Other users' Macs will show an 'unidentified developer' Gatekeeper warning"
echo "until Developer ID signing + notarization secrets are added to CI"
echo "(see the commented-out steps in .github/workflows/release-v1.6.2.yml)."
