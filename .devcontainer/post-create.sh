#!/usr/bin/env bash
set -euo pipefail

mkdir -p "$HOME/.codex"
touch "$HOME/.codex/config.toml"

if ! grep -q '^\[mcp_servers\.unityMCP\]' "$HOME/.codex/config.toml"; then
  cat >> "$HOME/.codex/config.toml" <<'EOF'

[mcp_servers.unityMCP]
command = "uvx"
args = ["--from", "mcpforunityserver==9.7.3", "mcp-for-unity", "--transport", "stdio"]
startup_timeout_sec = 60
EOF
fi

codex --version
uvx --version
