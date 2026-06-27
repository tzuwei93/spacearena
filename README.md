# Space Arena

`Space Arena` is a lightweight Unity WebGL prototype for a Crazy Arena-inspired 2D auto-battler.

Current prototype highlights:
- Three player squad archetypes.
- Automated arena combat with projectile pressure and a shrinking safe zone.
- Faction-based enemy waves with recurring boss encounters.
- Mid-run mutation drafting and rerolls.

Open this folder in Unity `6000.5.0f1`, then use `Space Arena/Create Or Refresh Scene` and press Play. To build the browser version, use `Space Arena/Build WebGL`.

To serve the WebGL build locally:

```bash
python3 Tools/serve_webgl.py --port 8080
```

The prototype uses a curated subset of the alien PNG assets from `design_assets/2d-Game-Alien-Character-Free-Sprite` under `Assets/Resources/Aliens`.
