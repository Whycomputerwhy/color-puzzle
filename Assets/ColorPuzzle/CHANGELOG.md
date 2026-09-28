# Color Puzzle Version History

This log is intended for people and AI agents continuing the same Unity project. Append new entries; do not rewrite past decisions without explaining the migration.

## 0.2.6 — 2026-09-28 — Placement scale and kinematic warning fix

### Fixed

- Placed objects now use the goal socket's world position and rotation without becoming children of its non-uniformly scaled hierarchy, preventing the red ball from being flattened on the base.
- Rigidbody velocity is cleared only while a body is dynamic, eliminating Unity warnings about assigning linear or angular velocity to a kinematic body during repeated pickup/drop interactions.

### Safety

- Runtime code and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.5 — 2026-09-28 — Static grounded drops

### Changed

- Removed the forward velocity impulse from dropped interactive objects.
- Dropped objects now probe downward for a valid surface, align their collider above it, clear all velocity, disable gravity, and remain kinematic after appearing.
- Added a fallback probe beneath the player when the intended forward drop point is over an edge; the item remains carried if no safe surface exists.
- Added Inspector controls for ground-probe height, distance, and surface clearance.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.4 — 2026-09-28 — Player-facing magnifier mode locked

### Changed

- Made Greyscale the unconditional startup view and Move Lens the sole player-facing magnifier interaction.
- Removed the always-visible Move Lens / Fixed Lens selector and the Fixed Lens runtime path.
- Moved Greyscale / Full Color comparison controls into a developer panel hidden by default.
- Assigned Enter to open/close the developer panel; opening releases the cursor and closing recaptures it.

### Safety

- Runtime code and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.3 — 2026-09-24 — Underwater build startup

### Changed

- Moved `ColorPuzzleUnderwaterDemo.unity` to enabled build index 0 so desktop players launch directly into the underwater level.
- Retained `ColorPuzzleVerticalSlice.unity` as an enabled secondary scene at build index 1.
- Updated the read-only build validator to enforce and report the new two-scene order.

### Safety

- Only Build Settings, validator code, and documentation changed; no scene or gameplay asset was rewritten.

## 0.2.2 — 2026-09-24 — Temporary lens reveal and Greyscale label

### Changed

- Renamed the runtime `Puzzle View` button to `Greyscale` while retaining the internal enum for serialized compatibility.
- Magnifier inspection now remains temporary: the red ball appears colored only inside the lens and stays gray in the base view after the lens closes.
- Successful pickup is now the persistent color commitment point; a subsequently dropped or placed ball remains red in Greyscale.

### Fixed

- Bounded magnifier RenderTexture dimensions and added a safe 16:9 aspect fallback when Unity reports an invalid Game-view size during script reload.

### Safety

- Runtime code and documentation only; no scene, prefab, material, HUD layout, or camera asset was regenerated.

## 0.2.1 — 2026-09-24 — Underwater diversions and goal silhouette

### Added

- Added fourteen aqua, blue, purple, orange, pink, green, cyan, and gold diversion spheres throughout the underwater route.
- Added a reusable `UnderwaterDecoySphere` prefab with a collider and color-reveal proxy, but no pickup/discovery component or rigidbody.
- Added an underwater-only cylindrical goal prefab with a circular red placement target and a larger white backing disc that reads as an outline.

### Changed

- Reduced the underwater scene's gravity from `-10` to `-8 m/s²` without changing the shared player prefab.
- Replaced only the underwater goal instance; its matching, placement socket, completion material, and door settings remain intact.

### Safety

- Only the underwater scene changed. The original scene, shared player prefab, and shared `RedGoalPedestal` prefab were hash-verified unchanged.

## 0.2.0 — 2026-09-24 — Colored underwater demo level

### Added

- Added `ColorPuzzleUnderwaterDemo.unity` as a second playable demo scene at build index 1; the original vertical slice remains the startup scene at index 0.
- Expanded the inherited red-ball puzzle with a denser colored-greybox underwater basin made from terraces, trenches, ruins, arches, rocks, coral, kelp, bubble columns, and small marker objects.
- Added nine flat-color underwater materials and four reusable nested detail prefabs for rocks, coral, kelp, and bubbles.
- Added a one-shot editor creator with a centralized environment palette and layout parameter block.

### Changed

- The underwater scene overrides player gravity to `-10 m/s²` for a mildly floaty feel without adding swimming mechanics.
- Underwater ambient light, blue distance fog, and the color-camera background establish depth while retaining Full Color and Puzzle View comparisons.

### Safety

- The original scene, existing prefabs, and existing materials were not regenerated or modified; all 26 pre-existing scene/prefab/material asset hashes were verified unchanged after creation.
- The underwater creator refuses to overwrite an existing underwater scene. Future work should edit the authored scene directly.

## 0.1.9 — 2026-09-21 — Complete color pass, grounded magnifier, inventory and door

### Fixed

- The color camera now renders normal-world layers plus later-queued color proxies, so the red goal base and objects without active proxies stay visible.
- Magnifier activation now requires the CharacterController to be grounded, preventing mid-air control locking and hovering.

### Changed

- Full Color is the default developer view.
- Hold right click now activates the magnifier; Q drops or places the carried object; R reloads the scene.
- Carried objects are hidden from the world and represented by a bottom inventory panel instead of following a camera hold anchor.

### Added

- Successful red-ball placement reveals a simple runtime door beside the right wall.
- Added Inspector controls for the inventory HUD, input keys, complete-door transform/colors, and magnifier mouse button.

### Safety

- All additions are runtime-created; no authored scene, prefab, material, HUD, or camera asset is regenerated.

## 0.1.8 — 2026-09-21 — Grouped mechanic and rendering comparisons

### Added

- Grouped Move Lens and Fixed Lens beneath a labeled Magnifier Control panel.
- Added a separate Dev View group with Puzzle View and Full Color buttons.
- Full Color continuously routes the aligned color camera to the display, bypassing both the black-and-white base and hold-Q lens interaction.
- Added Inspector controls for group spacing, padding, label sizing, label color, and backing color.

### Safety

- Both selector groups are still created only at runtime; no authored scene, prefab, material, HUD, or camera asset is regenerated.

## 0.1.7 — 2026-09-21 — Interchangeable magnifier controls

### Added

- Added a Fixed Lens mode: the color circle stays at screen center, walking is locked, and mouse-look aims the complete viewport while Q is held.
- Added persistent Move Lens and Fixed Lens buttons at the top-right, with active-mode highlighting and Inspector-visible layout/colors.
- Added a runtime Input System event handler when the scene has none, so the selector works without editing the authored scene or player prefab.

### Changed

- Escape releases the cursor for UI selection, and clicking UI no longer immediately recaptures it.
- Magnifier camera synchronization and discovery now run in `LateUpdate` after player look, avoiding a frame of lag in Fixed Lens mode.

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
