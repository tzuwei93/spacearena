# MCP Setup Notes

## Current Codex Session Status

Tool discovery in this Codex thread did not expose a callable Unity MCP or Godot MCP server. The available MCP servers were unrelated to Unity/Godot, so the game was implemented as Unity project files directly instead of through editor MCP calls.

This workspace already contains `.vscode/mcp.json` with a `godot-mcp` stdio entry using `npx -y @yanhuifair/godot-mcp -p ./GodotProject`. That is a useful editor-side setup stub, but Codex only sees the server after a session restart.

## Unity MCP Path

1. Open this folder in Unity `6000.5.0f1`.
2. Install or enable your Unity MCP bridge/package in the editor.
3. Confirm Codex can see resources like `mcpforunity://editor/state`.
4. Once connected, the intended workflow is:
   - Read `mcpforunity://editor/state`.
   - Run `Space Arena/Create Or Refresh Scene`.
   - Check console errors.
   - Capture a camera screenshot.
   - Run `Space Arena/Build WebGL`.

## Godot MCP Path

The existing VS Code config is:

```json
{
  "mcpServers": {
    "godot-mcp": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@yanhuifair/godot-mcp", "-p", "./GodotProject"]
    }
  }
}
```

The local project stub now lives at `GodotProject/project.godot`. If Godot itself is not installed yet, the server will still start for file-level setup, but engine control features need a Godot binary or `GODOT_PATH`.

This repo now keeps the design proposal engine-agnostic enough that the same rules can be ported to Godot 4 without changing the core game model.
