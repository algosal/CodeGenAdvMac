#!/bin/bash

echo "🚀 Running CodeGenApp for Mac Catalyst..."

# ===== Config: Debug vs Release =====
CONFIG="Debug"

if [[ "$1" == "release" || "$1" == "--release" || "$1" == "Release" ]]; then
  CONFIG="Release"
fi

echo "🛠  Build configuration: $CONFIG"

# Navigate to project folder
cd "/Users/salmansaeed/vscode/AWS-Coud-Coding/CodeGen/CodeGenApp" || {
    echo "❌ Project folder not found!"
    exit 1
}

TFM="net8.0-maccatalyst18.0"

if [[ "$CONFIG" == "Debug" ]]; then
  echo "📦 Building (dotnet build $TFM, $CONFIG)..."
  dotnet build -f "$TFM" -c "$CONFIG"
  if [ $? -ne 0 ]; then
      echo "❌ Build failed."
      exit 1
  fi

  APP_PATH="bin/$CONFIG/$TFM/maccatalyst-arm64/CodeGenApp.app"

  echo "✅ Debug build succeeded!"

  if [ ! -d "$APP_PATH" ]; then
      echo "❌ App bundle not found at:"
      echo "   $APP_PATH"
      exit 1
  fi

  echo "📂 Opening app (Debug)..."
  open "$APP_PATH"

  echo ""
  echo "📍 App Path (Debug):"
  echo "   $APP_PATH"
  echo "🎉 Done!"
  exit 0
fi

# ===== Release path: use publish =====
echo "📦 Publishing (dotnet publish $TFM, $CONFIG)..."
dotnet publish -f "$TFM" -c "$CONFIG"
if [ $? -ne 0 ]; then
    echo "❌ Publish failed."
    exit 1
fi

echo "✅ Release publish succeeded!"

PUBLISH_DIR="bin/$CONFIG/$TFM/publish"

APP_PATH="$PUBLISH_DIR/CodeGenApp.app"
PKG_PATH=$(ls "$PUBLISH_DIR"/CodeGenApp-*.pkg 2>/dev/null | head -n 1)

if [ -d "$APP_PATH" ]; then
    echo "📂 Opening .app bundle (Release)..."
    open "$APP_PATH"

    echo ""
    echo "📍 App Path (Release .app):"
    echo "   $APP_PATH"
    echo ""
    echo "💡 Tip: You can drag this .app into /Applications."
    echo "🎉 Done!"
    exit 0
elif [ -f "$PKG_PATH" ]; then
    echo "📦 Found installer package:"
    echo "   $PKG_PATH"
    echo "📂 Opening .pkg installer (Release)..."
    open "$PKG_PATH"

    echo ""
    echo "📍 Installer Path (Release .pkg):"
    echo "   $PKG_PATH"
    echo ""
    echo "💡 Tip: Run through the installer to add CodeGenApp to your Mac."
    echo "🎉 Done!"
    exit 0
else
    echo "❌ Neither .app nor .pkg found in:"
    echo "   $PUBLISH_DIR"
    echo "   (run 'ls \"$PUBLISH_DIR\"' to inspect actual contents)"
    exit 1
fi
