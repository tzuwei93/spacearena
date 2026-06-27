#!/bin/bash
set -e

if [ ! -d "Builds/WebGL" ]; then
  echo "Error: Builds/WebGL directory not found. Please run the build script first: ./Tools/build_webgl.sh"
  exit 1
fi

echo "=========================================="
echo "Deploying WebGL build to GitHub Pages..."
echo "=========================================="

# Clean up temp deploy folder if exists
TEMP_DEPLOY_DIR="/tmp/spacearena-deploy"
rm -rf "$TEMP_DEPLOY_DIR"
mkdir -p "$TEMP_DEPLOY_DIR"

# Copy build files to temp deploy folder
cp -r Builds/WebGL/* "$TEMP_DEPLOY_DIR"

# Get remote origin URL of the current repo
REMOTE_URL=$(git remote get-url origin 2>/dev/null || echo "")

if [ -z "$REMOTE_URL" ]; then
  echo "Error: Git remote origin not found. Please make sure this project is a Git repository with a remote origin."
  exit 1
fi

cd "$TEMP_DEPLOY_DIR"

# Initialize temporary repository
git init
git checkout -b gh-pages

# Configure temp user if not set
if [ -z "$(git config user.name)" ]; then
  git config user.name "antigravity-agent"
fi
if [ -z "$(git config user.email)" ]; then
  git config user.email "agent@antigravity"
fi

git add .
git commit -m "Deploy WebGL build to GitHub Pages"
git remote add origin "$REMOTE_URL"

echo "Pushing build to origin/gh-pages branch..."
git push -f origin gh-pages

echo "=========================================="
echo "Deployment Complete!"
echo "Your game should be live shortly at GitHub Pages."
echo "=========================================="
