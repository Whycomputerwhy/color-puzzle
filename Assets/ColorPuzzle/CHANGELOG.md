# Color Puzzle Version History

This log is intended for people and AI agents continuing the same Unity project. Append new entries; do not rewrite past decisions without explaining the migration.

## 0.1.6 — 2026-09-17 — Top-down goal placement

### Fixed

- Players standing on the goal can now place the matching ball by looking down and right-clicking.
- Top-down placement checks the pedestal footprint, camera height, downward look, match identity, and line of sight.
- Added Inspector tuning for footprint padding, maximum camera height, and required downward look.

## 0.1.5 — 2026-09-17 — Authored-content protection and placement assist

### Safety

- Permanently disabled the destructive vertical-slice generator; legacy batch calls now fail before changing any asset.
- Replaced the build-preparation command with read-only build validation.
- Scene, prefab, material, HUD, camera, Player Settings, and Build Settings authoring is now preserved.

### Fixed

- Goal placement now uses a tunable sphere-cast assist after the exact center ray.
- Placement still requires a matching carried object and clear line of sight, preventing through-wall placement.
- Carry HUD now indicates when the goal is within the valid placement assist region.

## 0.1.4 — 2026-09-17 — Desktop build preparation

### Added

- `Color Puzzle → Prepare Desktop Build` editor command.
- Demo scene is enforced as the sole enabled scene at build index 0.
- Repeatable 1280×720 windowed, resizable desktop-player defaults.

## 0.1.3 — 2026-09-17 — Reliable whole-lens discovery

### Fixed

- Discovery now checks the mission object's projected screen bounds against the entire circular lens instead of using one ray through the lens center.
- Objects reveal when any visible portion enters the glass, with an adjustable pixel tolerance and an occlusion check.
- Active discoverable objects use a lightweight registry, avoiding repeated scene searches while the lens is held.

## 0.1.2 — 2026-09-17 — Stable camera-relative carrying

### Fixed

- Carried objects now follow the camera carry anchor in `LateUpdate`, after mouse-look has completed for the frame.
- Rigidbody interpolation and collision simulation are disabled while carried, preventing lag, jitter, and collisions with the player.
- Original Rigidbody interpolation is restored when the object is dropped.

## 0.1.1 — 2026-09-17 — Movable aligned magnifier

### Changed

- Holding Q now uses mouse delta to move the circular lens while player position and camera orientation remain locked.
- The reveal camera renders a full aspect-correct view; the UI counter-offsets that render beneath the moving circle so perspective and screen positions align with the regular view.
- Discovery rays now pass through the lens position instead of always using the screen center.
- The regular camera uses a solid white background and the color-reveal camera uses a solid blue background.
- Added Inspector controls for lens sensitivity, edge padding, recentering, and full-view render height.

## 0.1.0 — 2026-09-17 — Core vertical slice

### Added

- First-person WASD movement, mouse look, jump, cursor release/recapture.
- Hold-Q magnifier that freezes movement/look and shows a circular true-color render.
- Persistent discovery state for the red mission ball.
- Left-click pickup with a bottom-right carry anchor.
- Right-click placement on the matching goal or physical drop/toss.
- Goal completion: ball snaps to the socket and pedestal becomes red.
- Small graybox world with revealable colored floor, platforms, steps, walls, and column.
- Reusable player, world-block, red-ball, and goal prefabs.
- Centralized Inspector tuning and full design/file-structure documentation.

### Architecture decisions

- The grayscale world uses authored gray base materials instead of a post-process, keeping URP setup simple for the prototype.
- True-color objects are collider-free proxy children on the `ColorReveal` layer, rendered by a second camera to a circular UI texture.
- Mission identity is string-based (`red-ball`) for readability in this slice; migrate to ScriptableObject item/color definitions when multiple colors or mission types are introduced.

### Known prototype limits / next work

- Only one object/goal pair exists.
- No E-key/NPC interactions yet, as requested.
- No save system, pause menu, audio, animation, or final art.
- The magnifier is a circular color view with a simple rim; a modeled hand/glass overlay can replace the HUD later without changing discovery logic.
