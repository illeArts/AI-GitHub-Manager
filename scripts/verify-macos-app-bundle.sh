#!/usr/bin/env bash
# verify-macos-app-bundle.sh
#
# Run this ON A MAC to verify the macOS app bundles used by release-v1.6.4.yml.
# It builds both macOS .app bundles locally exactly the way the
# release-v1.6.4.yml "macos-packages" job does, ad-hoc signs them, and
# runs every check from the acceptance criteria. It aborts hard (non-zero
# exit) the moment any signing or verification step fails — it never
# produces a ZIP from a bundle that isn't actually signed and verified.
#
# Why the bundle is assembled under /tmp instead of inside the repo:
# on a real Mac, codesign kept failing immediately AFTER a successful
# `codesign --sign` with "Disallowed xattr com.apple.FinderInfo" on the
# .app directory itself — i.e. something re-tagged the bundle with a
# "has custom icon" extended attribute in the few milliseconds between
# sign and verify. That is LaunchServices/Icon Services reacting to a
# newly created .app bundle, not Finder having a window open, and it
# happens fastest for bundles living inside an indexed, iCloud/Finder-
# visible location such as ~/Documents. /tmp is excluded from Spotlight
# indexing by default and is not iCloud-synced, so building, signing and
# verifying there removes the race entirely. Only the final, already
# verified ZIP (a plain file, not a bundle LaunchServices cares about)
# is copied back into the repo's artifacts/dist/.
#
# Order (per architecture):
#   publish -> assemble bundle (in /tmp) -> xattr audit + strip -> codesign
#   -> xattr audit + targeted re-strip if needed -> codesign --verify
#   -> ditto zip -> sha256 -> unzip into a fresh temp dir
#   -> re-verify the unpacked copy (bundle present, executable bit,
#      architecture, plutil -lint, codesign --verify, no Windows binaries)
#   -> copy the verified zip + sha256 into the repo's artifacts/dist/
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
VERSION="1.6.4"
BUNDLE_ID="com.illearts.AIGitHubManager"
PROJECT="src/AI.GitHubManager.App/AI.GitHubManager.App.csproj"
REPO_ROOT="$(pwd)"
BUILD_ROOT="/tmp/aigithubmanager-macos-build-$$"

log()  { echo "  [→] $*"; }
ok()   { echo "  [✓] $*"; }
err()  { echo "  [✗] $*" >&2; exit 1; }

command -v dotnet   &>/dev/null || err "dotnet not found. Install .NET 8 SDK."
command -v codesign &>/dev/null || err "codesign not found. Run: xcode-select --install"
command -v spctl    &>/dev/null || err "spctl not found. Run: xcode-select --install"
command -v plutil   &>/dev/null || err "plutil not found. Run: xcode-select --install"
command -v ditto    &>/dev/null || err "ditto not found. Run: xcode-select --install"
command -v xattr    &>/dev/null || err "xattr not found (should ship with macOS)."

cleanup() { rm -rf "$BUILD_ROOT"; }
trap cleanup EXIT

# Delete previously produced archives up front — a ZIP that survives from an
# earlier, failed run must never be mistaken for a valid one.
rm -f "$REPO_ROOT/artifacts/dist/AI-GitHub-Manager-v${VERSION}-macos-arm64.zip"*
rm -f "$REPO_ROOT/artifacts/dist/AI-GitHub-Manager-v${VERSION}-macos-x64.zip"*

# Print every extended attribute under $1 (recursively). Never fails the
# script by itself — it is a diagnostic, the caller decides what to do
# with what it finds.
show_xattrs() {
  local target="$1"
  xattr -lr "$target" 2>/dev/null || true
}

# Remove ALL extended attributes recursively (directory entry itself +
# every file inside).
strip_all_xattrs() {
  local target="$1"
  xattr -cr "$target" 2>/dev/null || true
  find "$target" -exec xattr -c {} \; 2>/dev/null || true
}

# Remove only com.apple.FinderInfo, recursively, from the directory entry
# itself and every file inside — used as a second, targeted pass if it
# reappears after a strip_all_xattrs + codesign round-trip.
strip_finderinfo_only() {
  local target="$1"
  xattr -d com.apple.FinderInfo "$target" 2>/dev/null || true
  find "$target" -exec xattr -d com.apple.FinderInfo {} \; 2>/dev/null || true
}

# Sign a bundle and prove there is nothing left to block it:
#   1. show + strip all extended attributes
#   2. confirm no code-signing-blocking attribute remains (hard fail otherwise)
#   3. codesign --force --deep --sign -
#   4. show attributes again; if FinderInfo reappeared, strip it
#      specifically and re-sign once
#   5. codesign --verify --deep --strict --verbose=4 — hard fail if this
#      does not pass; the caller must never proceed to zip a bundle for
#      which this step failed.
sign_and_verify_bundle() {
  local target="$1"

  log "Extended attributes BEFORE cleanup:"
  show_xattrs "$target"

  strip_all_xattrs "$target"

  # macOS 26 may immediately restore com.apple.provenance while copying an
  # executable. It is metadata, not a codesign detritus attribute, and cannot
  # reliably be removed by xattr. FinderInfo and resource forks are the
  # attributes that make codesign reject a bundle, so only those are fatal.
  if xattr -lr "$target" 2>/dev/null | grep -Eqi 'com\.apple\.(FinderInfo|ResourceFork)'; then
    echo "Code-signing-blocking extended attributes remain after strip:" >&2
    show_xattrs "$target"
    err "Could not remove code-signing-blocking extended attributes from '$target'."
  fi
  ok "No code-signing-blocking extended attributes remain before signing"

  log "codesign --force --deep --sign - ..."
  codesign --force --deep --sign - "$target"
  ok "codesign succeeded"

  log "Extended attributes AFTER codesign (checking for a FinderInfo re-tag):"
  show_xattrs "$target"

  if xattr -lr "$target" 2>/dev/null | grep -q "com.apple.FinderInfo"; then
    log "com.apple.FinderInfo reappeared after signing — removing it and re-signing once ..."
    strip_finderinfo_only "$target"
    codesign --force --deep --sign - "$target"
    ok "Re-signed after targeted FinderInfo removal"
  fi

  log "codesign --verify --deep --strict --verbose=4 ..."
  if ! codesign --verify --deep --strict --verbose=4 "$target" 2>/tmp/verify-err-$$.log; then
    cat /tmp/verify-err-$$.log >&2
    if grep -q "resource fork\|FinderInfo\|detritus" /tmp/verify-err-$$.log; then
      log "Extended attribute race hit codesign --verify — one more strip + sign + verify round ..."
      strip_all_xattrs "$target"
      strip_finderinfo_only "$target"
      codesign --force --deep --sign - "$target"
      codesign --verify --deep --strict --verbose=4 "$target" \
        || err "codesign --verify still fails after a second full strip+sign round. See output above."
    else
      err "codesign --verify failed for a reason other than extended attributes. See output above."
    fi
  fi
  ok "codesign --verify passed"
}

mdutil -i off "$BUILD_ROOT" &>/dev/null || true
mkdir -p "$BUILD_ROOT"

for RID in osx-arm64 osx-x64; do
  case "$RID" in
    osx-arm64) ARCH_LABEL=arm64; EXPECT_ARCH=arm64 ;;
    osx-x64)   ARCH_LABEL=x64;   EXPECT_ARCH=x86_64 ;;
  esac

  echo ""
  echo "==================== $RID ===================="

  PUBLISH_DIR="$BUILD_ROOT/publish/$RID"
  APP="$BUILD_ROOT/appbundle/$RID/${APP_NAME}.app"

  # ── 1. Publish ────────────────────────────────────────────────────────
  log "Publishing self-contained build for $RID ..."
  dotnet publish "$REPO_ROOT/$PROJECT" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None -p:DebugSymbols=false \
    -o "$PUBLISH_DIR"
  ok "Published (in $PUBLISH_DIR — outside the repo, no Spotlight/Finder attention)"

  # ── 2. Assemble the .app bundle ─────────────────────────────────────────
  # cp -X: never copy extended attributes from the source file, so nothing
  # can be inherited from the repo checkout into the freshly built bundle.
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

  cp -X "$PUBLISH_DIR/AI.GitHubManager.App" "$APP/Contents/MacOS/AI.GitHubManager.App"
  for dylib in "$PUBLISH_DIR/"*.dylib; do
    [[ -f "$dylib" ]] && cp -X "$dylib" "$APP/Contents/MacOS/"
  done
  chmod +x "$APP/Contents/MacOS/AI.GitHubManager.App"
  cp -X "$REPO_ROOT/src/AI.GitHubManager.App/Assets/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
  cp -X "$REPO_ROOT/LICENSE" "$APP/Contents/Resources/LICENSE.txt"

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

  # ── 3./4. xattr audit, strip, codesign, xattr audit, codesign --verify ──
  # Everything above is diagnosed/hard-checked inside sign_and_verify_bundle;
  # the script CANNOT reach the zip step below unless codesign --verify
  # actually passed.
  sign_and_verify_bundle "$APP"

  log "spctl assessment (Gatekeeper — informational only) ..."
  spctl --assess --type execute --verbose=4 "$APP" || \
    echo "  [!] spctl rejected the app — EXPECTED for ad-hoc signing without Developer ID + notarization. Not a failure of this script."

  # ── 5. ZIP + SHA-256 — only reached if signing and verification above
  # actually succeeded (sign_and_verify_bundle calls err()/exit 1 otherwise,
  # and set -e means we never fall through to here).
  ZIP_NAME="AI-GitHub-Manager-v${VERSION}-macos-${ARCH_LABEL}.zip"
  ZIP_PATH="$BUILD_ROOT/dist/$ZIP_NAME"
  mkdir -p "$BUILD_ROOT/dist"

  log "Zipping with ditto ..."
  ( cd "$BUILD_ROOT/appbundle/$RID" && ditto -c -k --sequesterRsrc --keepParent \
      "${APP_NAME}.app" "$ZIP_PATH" )
  shasum -a 256 "$ZIP_PATH" | awk -v n="$ZIP_NAME" '{print $1"  "n}' > "$ZIP_PATH.sha256"
  ok "Archive created → $ZIP_PATH"

  # ── 6. Unpack into a FRESH temp dir and re-verify the extracted copy ─────
  UNZIP_DIR="$BUILD_ROOT/unzip-check/$RID"
  mkdir -p "$UNZIP_DIR"
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

  codesign --verify --deep --strict --verbose=4 "$UNZIPPED_APP"
  ok "codesign --verify passed on the UNPACKED app — this ZIP is release-ready (ad-hoc signature only)"

  # ── 7. Only now copy the verified ZIP back into the repo ─────────────────
  mkdir -p "$REPO_ROOT/artifacts/dist" "$REPO_ROOT/artifacts/appbundle/$RID"
  cp -X "$ZIP_PATH" "$ZIP_PATH.sha256" "$REPO_ROOT/artifacts/dist/"
  # Keep a copy of the signed bundle around too, for the manual Finder/open
  # test below — this one is not re-verified, the ZIP above is the artifact
  # of record.
  rm -rf "$REPO_ROOT/artifacts/appbundle/$RID/${APP_NAME}.app"
  ditto "$APP" "$REPO_ROOT/artifacts/appbundle/$RID/${APP_NAME}.app"
  ok "Verified ZIP copied to $REPO_ROOT/artifacts/dist/$ZIP_NAME"

  echo ""
  echo "  Manual test:"
  echo "    open \"$REPO_ROOT/artifacts/appbundle/$RID/${APP_NAME}.app\""
done

echo ""
echo "==================== Summary ===================="
echo "Both archs reached this point only because, for each of them:"
echo "  - no code-signing-blocking extended attributes remained before signing,"
echo "  - codesign --force --deep --sign - succeeded,"
echo "  - codesign --verify --deep --strict --verbose=4 succeeded on the"
echo "    signed bundle AND again on the unpacked copy of the shipped ZIP."
echo "If any of that had failed the script would already have exited non-zero"
echo "and no ZIP would have been copied into artifacts/dist/."
echo ""
echo "Ad-hoc signing was used (codesign --sign -) and verified successfully."
echo "This is NOT a Developer ID signature and the app is NOT notarized."
echo "'spctl rejected' above is EXPECTED for ad-hoc signing without Developer ID"
echo "+ notarization — it is not an error in this script. Other users' Macs will"
echo "show an 'unidentified developer' Gatekeeper warning until Developer ID"
echo "signing + notarization secrets are added to CI (see the commented-out"
echo "steps in .github/workflows/release-v1.6.4.yml)."
