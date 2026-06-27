#!/bin/bash
set -e

echo "=========================================="
echo "Starting Unity WebGL Headless Build..."
echo "=========================================="

# Locate unity-editor binary
UNITY_BIN="unity-editor"
if ! command -v "$UNITY_BIN" &> /dev/null; then
    # Fallback to standard installation path in GameCI container
    if [ -f "/opt/unity/Editor/Unity" ]; then
        UNITY_BIN="/opt/unity/Editor/Unity"
    else
        echo "Error: Unity Editor binary not found in PATH or at /opt/unity/Editor/Unity."
        echo "Please verify that Unity is correctly installed and activated."
        exit 1
    fi
fi

# Run Unity WebGL build method
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -projectPath . \
  -executeMethod SpaceArenaBuild.BuildWebGL \
  -logFile - \
  -quit

echo "=========================================="
echo "Build Completed Successfully!"
echo "Output path: Builds/WebGL"
echo "=========================================="
