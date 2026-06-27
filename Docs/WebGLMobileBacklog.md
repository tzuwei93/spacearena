# Space Arena WebGL and Mobile Backlog

This backlog turns the current prototype assessment into a concrete shipping plan for the Unity WebGL build, with emphasis on phone usability and runtime stability.

## Goal

Ship a reliable WebGL build to GitHub Pages, then make the game usable and performant on mobile browsers, especially iPhone Safari and Android Chrome.

## Current Constraints

- Deployment depends on Unity WebGL builds and GitHub Pages automation.
- The current UI is generated in code with fixed pixel sizes and positions.
- Deck navigation still depends on mouse wheel input.
- Runtime asset loading relies on `Resources.LoadAll` scans.
- WebGL is configured as a prototype build rather than a production mobile web release.

## Phase 1: Deployment Hardening

### 1.1 Verify CI deployment

- Add `UNITY_LICENSE` secret in GitHub repository settings.
- Run `.github/workflows/deploy.yml` manually and confirm it publishes a working build.
- Confirm GitHub Pages is configured to serve the `gh-pages` branch.
- Verify gzip assets are served correctly and the build loads without console errors.

### 1.2 Replace prototype release assumptions

- Treat GitHub Actions as the primary deployment path.
- Keep `Tools/deploy.sh` as an escape hatch, not the normal release flow.
- Add a release checklist to the README or deployment docs.

### 1.3 Add a production WebGL shell

- Replace Unity's default WebGL template with a custom template.
- Add branded loading UI, progress state, and failure messaging.
- Add orientation guidance for phones before gameplay begins.
- Add a lightweight "best experienced in landscape" message for mobile devices.

## Phase 2: Mobile UX Refactor

### 2.1 Rebuild the deck builder for phones

- Replace the current fixed-size central panel with a responsive mobile layout.
- Move to a vertical flow:
  - alien preview and name at the top
  - alien picker in the middle
  - loadout and attack mode controls below
  - one primary `Start Match` button at the bottom
- Ensure the first viewport shows the actual playable setup, not dense menu chrome.

### 2.2 Replace desktop-only interaction patterns

- Remove mouse-wheel dependency for alien selection.
- Add tap-based alien selection.
- Add swipe or next/previous navigation for alien browsing.
- Ensure all interactive targets are thumb-sized and visually distinct.

### 2.3 Move combat controls into a thumb zone

- Redesign `Jump` and `Turn` controls for bottom-corner reachability.
- Increase touch target sizes to mobile-friendly dimensions.
- Keep core actions away from browser gesture edges.
- Add pressed/disabled states that remain readable outdoors and on lower-end screens.

### 2.4 Add safe-area-aware HUD layout

- Wrap HUD roots in a safe-area container.
- Respect iPhone notch and home indicator insets.
- Keep top banner, bottom feed, and action buttons out of unsafe edges.
- Validate landscape safe-area behavior on both iOS and Android.

### 2.5 Reduce UI density

- Collapse status into fewer signals:
  - wave
  - player HP
  - enemy HP
  - timer
- Convert dense text blocks into compact labels and stronger visual hierarchy.
- Ensure long labels do not clip on narrow mobile widths.

## Phase 3: Runtime Performance

### 3.1 Reduce first-load cost

- Replace broad `Resources.LoadAll` lookups with explicit references or a small indexed asset map.
- Preload only the selected fighter skin, common FX, and current wave assets.
- Add a warmup step before match start instead of loading lazily during first interactions.

### 3.2 Optimize sprite packaging

- Move alien assets into sprite atlases.
- Reduce texture switches during combat.
- Audit each alien texture import size and lower mobile WebGL variants where possible.
- Keep mipmaps disabled for these 2D assets unless a specific case benefits.

### 3.3 Add device-tier settings

- Keep 60 FPS as the preferred tier.
- Add a lower tier for weaker phones with:
  - reduced target frame rate
  - fewer decorative background elements
  - lighter UI effects
- Expose the tier internally through a simple runtime capability check or manual fallback setting.

### 3.4 Trim low-value draw and object costs

- Reduce or simplify the starfield for mobile builds.
- Review projectile and beam object reuse under sustained combat.
- Profile ring updates, HUD updates, and object counts on low-memory mobile browsers.

## Phase 4: WebGL-Specific Stability

### 4.1 Audit WebGL memory behavior

- Run a build-size analysis pass.
- Validate startup memory and growth behavior on mobile browsers.
- Test for crashes or tab reloads caused by large heap growth on iPhone Safari.
- Tune initial memory and growth settings only after real-device measurement.

### 4.2 Add browser test coverage

- Smoke test on:
  - iPhone Safari
  - Android Chrome
  - desktop Chrome
  - desktop Safari
- Check:
  - initial load time
  - orientation behavior
  - touch responsiveness
  - frame pacing
  - reload behavior after backgrounding the tab

### 4.3 Improve failure handling

- Add a clear unsupported-device or failed-load state in the WebGL shell.
- Surface retry guidance instead of leaving users on a blank or frozen canvas.
- Log build version visibly in the loader or footer for QA verification.

## Phase 5: Project Structure Decisions

### 5.1 Decide whether the scene remains fully procedural

- The current build pipeline recreates the scene before building.
- Decide whether that remains the long-term model.
- If UI and presentation become more authored, stop overwriting the scene on build.

### 5.2 Separate gameplay logic from presentation scaffolding

- Break `SpaceArenaBootstrap.cs` into smaller units:
  - bootstrapping
  - HUD construction
  - deck flow
  - combat flow
  - asset loading
- This should happen after the first mobile UX pass, not before.

## Recommended Order

1. Confirm CI deployment with Unity license and GitHub Pages.
2. Add a custom WebGL template and release checklist.
3. Refactor the deck builder and combat HUD for mobile.
4. Replace mouse-wheel input with touch-first selection.
5. Add safe-area support and landscape guidance.
6. Atlas sprites and reduce runtime `Resources` scanning.
7. Test and tune on real phones.

## Definition of Done

- A GitHub Pages build deploys reliably from `main`.
- The game loads and starts on iPhone Safari and Android Chrome.
- Core setup and combat flows are comfortable in landscape on phone screens.
- No required interaction depends on mouse, hover, or scroll wheel.
- Frame pacing remains stable on at least one mid-range mobile device per platform.
