#!/usr/bin/env bash
# publish-mac.sh — builds AI GitHub Manager as a macOS .app bundle
# Run this on a Mac from the repo root:  bash publish-mac.sh
set -euo pipefail

PROJECT="src/AI.GitHubManager.App/AI.GitHubManager.App.csproj"
OUT_BASE="publish"

echo "=== AI GitHub Manager — macOS publish ==="

# Detect architecture
ARCH=$(uname -m)
if [[ "$ARCH" == "arm64" ]]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi

OUT_DIR="$OUT_BASE/$RID"
echo "→ Architecture : $ARCH  (RID: $RID)"
echo "→ Output       : $OUT_DIR"
echo ""

# Build
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT_DIR"

echo ""
echo "✓ Build complete → $OUT_DIR"

# If Avalonia generated a .app bundle, show its location
APP=$(find "$OUT_DIR" -name "*.app" -maxdepth 2 2>/dev/null | head -1)
if [[ -n "$APP" ]]; then
    echo "✓ App bundle   → $APP"
    echo ""
    echo "To run:  open \"$APP\""
else
    BIN=$(find "$OUT_DIR" -name "AI.GitHubManager.App" -type f | head -1)
    echo ""
    echo "No .app bundle found (normal for self-contained single-file builds)."
    echo "To run:  \"$BIN\""
fi

echo ""
echo "Prerequisites on the Mac:"
echo "  • .NET 8 SDK        — https://dotnet.microsoft.com/download"
echo "  • Xcode CLT         — xcode-select --install"
echo "  • Homebrew + gh     — brew install gh"
