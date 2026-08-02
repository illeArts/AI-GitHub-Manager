#!/usr/bin/env bash
# verify-macos-app-bundle.sh
#
# Run this ON A MAC after pulling branch fix/v1.6.2-macos-app-bundle.
# It builds both macOS .app bundles locally exactly the way the
# release-v1.6.2.yml "macos-packages" job does, ad-hoc signs them, and
# runs every check from the acceptance criteria. It aborts hard (non-zero
# exit) the moment any signing or verification step fails — it never
# produces a ZIP from a bundle that isn't actually signed and verified.
#
# Order (per architecture):
#   publish -> assemble bundle -> xattr -cr -> codesign -> codesign --verify
#   -> zip with ditto -> sha256 -> unzip into a fresh temp dir
#   -> re-verify the UNPACKED copy (bundle present, executable bit,
#      architecture, plutil -lint, codesign --verify, no Windows binaries)
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
command -v spctl    &>/dev/null || err "spctl not found. Run: xcode-select --install"
command -v plutil   &>/dev/null || err "plutil not found. Run: xcode-select --install"
command -v ditto    &>/dev/null || err "ditto not found. Run: xcode-select --install"
command -v xattr    &>/dev/null || err "xattr not found (should ship with macOS)."

# Delete previously produced archives up front — a ZIP that survives from an
# earlier, failed run must never be mistaken for a valid release artifact.
rm -f artifacts/dist/AI-GitHub-Manager-v${VERSION}-macos-arm64.zip*
rm -f artifacts/dist/AI-GitHub-Manager-v${VERSION}-macos-x64.zip*

# Strip a com.apple.FinderInfo / resource-fork extended attribute from a
# path. Finder/Spotlight can (re-)tag a freshly created .app bundle
# directory with a "has custom icon" FinderInfo attribute the instant it
# notices the bundle type — codesign refuses to sign anything carrying one
# ("resource fork, Finder information, or similar detritus not allowed").
strip_xattrs() {
  local target="$1"
  xattr -cr "$target"
  find "$target" -exec xattr -c {} \; 2>/dev/null || true
}

# codesign --force --deep --sign - a bundle, retrying the xattr strip a
# bounded number of times if Finder/Spotlight re-tags it in between, but
# hard-failing (non-zero exit, via set -e) if it never succeeds. This never
# swallows a real signing failure — only the specific FinderInfo race is
# retried, anything else aborts immediately.
sign_bundle() {
  local target="$1"
  local attempt
  for attempt in 1 2 3 4 5; do
    strip_xattrs "$target"
    if codesign --force --deep --sign - "$target" 2>/tmp/codesign-err.log; then
      return 0
    fi
    if ! grep -q "resource fork\|FinderInfo\|detritus" /tmp/codesign-err.log; then
      cat /tmp/codesign-err.log >&2
      err "codesign failed for a reason other than a stray Finder attribute."
    fi
    log "  codesign attempt $attempt hit a stray Finder/resource-fork attribute — stripping again and retrying ..."
    sleep 1
  done
  cat /tmp/codesign-err.log >&2
  err "codesign kept failing on extended attributes after 5 attempts. Close any Finder window showing '$target' and re-run."
}

mdutil -i off "$(pwd)/artifacts" &>/dev/null || true

for RID in osx-arm64 osx-x64; do
  case "$RID" in
    osx-arm64) ARCH_LABEL=arm64; EXPECT_ARCH=arm64 ;;
    osx-x64)   ARCH_LABEL=x64;   EXPECT_ARCH=x86_64 ;;
  esac

  echo ""
  echo "==================== $RID ===================="

  # ── 1. Publish ────────────────────────────────────────────────────────
  log "Publishing self-contained build for $RID ..."
  rm -rf "artifacts/publish/$RID"
  dotnet publish "$PROJECT" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None -p:DebugSymbols=false \
    -o "artifacts/publish/$RID"
  ok "Published"

  # ── 2. Assemble the .app bundle ─────────────────────────────────────────
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
  plutil -lint "$APP/Contents/Info.plist"
  ok "Info.plist valid"

  log "No Windows binaries check ..."
  if find "$APP" -iname "*.exe" -o -iname "*.msi" | grep -q .; then
    err "Windows binaries found inside $APP"
  fi
  ok "No Windows binaries"

  # ── 3. xattr -cr, then codesign — hard fail on error ─────────────────────
  log "Signing (ad-hoc — NOT a Developer ID signature, NOT notarized) ..."
  sign_bundle "$APP"
  ok "codesign succeeded"

  # ── 4. codesign --verify — hard fail on error ─────────────────────────────
  log "Verifying signature ..."
  codesign --verify --deep --strict --verbose=2 "$APP"
  ok "codesign --verify passed"

  log "spctl assessment (Gatekeeper — informational only) ..."
  spctl --assess --type execute --verbose=4 "$APP" || \
    echo "  [!] spctl rejected the app — EXPECTED for ad-hoc signing without Developer ID + notarization. Not a failure of this script."

  # ── 5. ZIP + SHA-256 — only reached if steps 3+4 above actually succeeded ─
  ZIP_DIR="artifacts/dist"
  mkdir -p "$ZIP_DIR"
  ZIP_NAME="AI-GitHub-Manager-v${VERSION}-macos-${ARCH_LABEL}.zip"
  ZIP_PATH="$ZIP_DIR/$ZIP_NAME"
  rm -f "$ZIP_PATH" "$ZIP_PATH.sha256"

  log "Zipping with ditto ..."
  ( cd "artifacts/appbundle/$RID" && ditto -c -k --sequesterRsrc --keepParent \
      "${APP_NAME}.app" "$OLDPWD/$ZIP_PATH" )
  shasum -a 256 "$ZIP_PATH" | awk -v n="$ZIP_NAME" '{print $1"  "n}' > "$ZIP_PATH.sha256"
  ok "Archive created → $ZIP_PATH"

  # ── 6. Unpack into a FRESH temp dir and re-verify the extracted copy ─────
  UNZIP_DIR="/tmp/verify-unzip-${RID}"
  rm -rf "$UNZIP_DIR" && mkdir -p "$UNZIP_DIR"
  ditto -x -k "$ZIP_PATH" "$UNZIP_DIR"
  UNZIPPED_APP="$UNZIP_DIR/${APP_NAME}.app"

  log "Re-checking the UNPACKED archive contents ..."
  test -d "$UNZIPPED_APP" || err "No .app after unzip — packaging is broken."
  ok "Unpacked archive contains a real .app bundle"

  test -x "$UNZIPPED_APP/Contents/MacOS/AI.GitHubManager.App" || err "Executable bit lost after unzip."
  ok "Executable bit preserved after unzip"

  file "$UNZIPPED_APP/Contents/MacOS/AI.GitHubManager.App" | grep -q "$EXPECT_ARCH" \
    || err "Architecture wrong after unzip! Expected $EXPECT_ARCH"
  ok "Architecture correct after unzip ($EXPECT_ARCH)"

  plutil -lint "$UNZIPPED_APP/Contents/Info.plist"
  ok "Info.plist valid after unzip"

  if find "$UNZIPPED_APP" -iname "*.exe" -o -iname "*.msi" | grep -q .; then
    err "Windows binaries found in the unpacked archive"
  fi
  ok "No Windows binaries after unzip"

  codesign --verify --deep --strict --verbose=2 "$UNZIPPED_APP"
  ok "codesign --verify passed on the UNPACKED app — this ZIP is release-ready (ad-hoc signature only)"

  echo ""
  echo "  Manual test:"
  echo "    open \"$UNZIPPED_APP\""
  echo "    (and double-click ${APP_NAME}.app in Finder at $(pwd)/$APP)"
done

echo ""
echo "==================== Summary ===================="
echo "Both archs reached this point only because codesign, codesign --verify,"
echo "and codesign --verify on the unpacked ZIP all succeeded for each of them —"
echo "if any of that had failed the script would already have exited non-zero."
echo ""
echo "Ad-hoc signing was used (codesign --sign -) and verified successfully."
echo "This is NOT a Developer ID signature and the app is NOT notarized."
echo "'spctl rejected' above is EXPECTED for ad-hoc signing without Developer ID"
echo "+ notarization — it is not an error in this script. Other users' Macs will"
echo "show an 'unidentified developer' Gatekeeper warning until Developer ID"
echo "signing + notarization secrets are added to CI (see the commented-out"
echo "steps in .github/workflows/release-v1.6.2.yml)."
