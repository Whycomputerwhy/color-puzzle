# Color Puzzle Version History

This log is intended for people and AI agents continuing the same Unity project. Append new entries; do not rewrite past decisions without explaining the migration.

## 0.2.21 — 2026-09-29 — White-circle placement targeting

### Changed

- Placement now targets the top-surface point beneath the goal's placement socket, matching the center of the visible white circle instead of accepting the pedestal's whole collider.
- The existing `Placement Cast Radius` now defines forgiving world-space room around that marked point; its `0.75 m` default remains unchanged.
- Direct, assisted, and standing-on-top placement paths all require the same target-proximity test and an unobstructed path to the marked surface.
- Aiming at the pedestal body outside the white-circle region now falls through to the normal object-drop behavior.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.20 — 2026-09-29 — Eased magnifier movement

### Changed

- Split magnifier motion into a raw mouse-driven target and a displayed lens position that eases toward it.
- Added frame-rate-independent exponential smoothing using unscaled time, keeping the lens responsive even if game time is altered.
- Added an Inspector `Lens Move Easing` control, defaulting to a subtle `18` response per second; zero restores immediate movement.
- Re-centering the magnifier now resets both motion states together, so opening it never glides in from a stale position.

### Safety

- Runtime script and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.19 — 2026-09-29 — Reliable pickup aim

### Fixed

- Replaced the fragile zero-width-only pickup test with an occlusion-aware two-stage aim query.
- An interactive exact center-ray hit remains authoritative; otherwise a small sphere cast provides forgiveness around the crosshair, including when the thin ray narrowly misses the object and reaches the floor behind it.
- Both aim stages ignore the player hierarchy and select only the nearest remaining collider, preventing assisted pickup through walls or props.
- Added a null-safe shared player-collider filter used by pickup and drop queries.

### Added

- Added an Inspector `Pickup Aim Radius` control, defaulting to `0.16 m`; setting it to zero restores exact-ray-only targeting.

### Safety

- Runtime script and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.18 — 2026-09-29 — Skyward drop player pass-through

### Fixed

- Downward floor probes now inspect all hits and explicitly skip the player hierarchy instead of accepting the first collider.
- A drop aimed into empty sky can no longer treat the CharacterController's head as a supporting surface, place the object above the player, or leave it frozen in midair.
- The probe continues straight through the player to the nearest valid world surface below; the existing temporary player-collision ignore remains active if the final object volume overlaps the player.

### Safety

- Runtime script and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.17 — 2026-09-29 — Drop surface and player-overlap exceptions

### Fixed

- A fallback animation whose start would be inside solid geometry or at/below the resolved horizontal resting center is now skipped; the object appears directly on that surface instead of floating upward out of a volume.
- Close focused walls retain player-side clearance but now animate from a short near-camera start down to the floor instead of placing instantly.
- A dropped object's world collision activates normally, while only its collision with an overlapping player is ignored until the player fully exits the object's expanded volume. This prevents dropped objects from pushing the player away.

### Added

- Added Inspector controls for close-wall animation start distance and player-exit collision clearance.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.16 — 2026-09-29 — Crosshair-aligned distant drops

### Fixed

- Corrected the interpretation of the requested Z-axis behavior from world-vertical Y to the camera's forward/local-Z focus point.
- Distant and empty-air drops now start with the object's bounds center exactly on the crosshair at `Drop Distance`, regardless of whether the player looks up or down.
- The fallback floor probe uses that same camera-forward point, so the object falls to the ground directly beneath its visible start instead of appearing above or below the focus before release.
- Removed the world-height animation control that caused the pitch-dependent screen offset.

### Preserved

- Immediate close-surface placement, close wall clearance, forward-only movement influence, collision validation, static final objects, and exact goal placement.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.15 — 2026-09-29 — Simplified near/far drop split

### Changed

- Replaced overlapping aimed-ground and forward-distance behavior with one center-focus distance rule.
- A usable focused surface at or inside `Drop Distance` receives the object immediately at the validated intersection, without animation or movement lead.
- A farther, absent, or unsuitable focused surface uses the forward fallback and animates from directly above the final point, producing a straight world-Y drop with no diagonal screen-center travel.
- Removed the separate aimed-surface range and screen-center animation-start controls; `Drop Distance` is now the sole near/far threshold.

### Preserved

- Exact goal placement, close wall clearance, forward-only lead on distant fallback drops, grounding, overlap rejection, static final objects, and manual scene/prefab edits.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.14 — 2026-09-29 — Aim-distance ground drops

### Changed

- Looking down at a floor or other supporting surface now drops the carried object at the actual center-ray hit point instead of forcing the normal forward drop distance.
- Very close aimed ground points remain very close; the fixed forward distance is now only a fallback when no supporting surface is under the crosshair.
- Wall-side placement, forward movement lead, downward grounding, overlap validation, and the quick kinematic drop animation remain active.
- Added an Inspector control for maximum aimed-supporting-surface recognition distance; it does not impose a minimum distance.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.13 — 2026-09-28 — Subtle first-person view motion

### Added

- Added a low-amplitude vertical camera bob driven by actual grounded horizontal speed.
- Added a very short smooth camera squat before physical jump launch, followed by a quick recovery to the manually authored camera-pivot position.
- View-motion offsets freeze while the magnifier locks movement, preserving the existing fixed camera position during inspection.
- Added Inspector controls for enabling view motion, walk amplitude/frequency/threshold/smoothing, and jump squat depth/anticipation/recovery timing.

### Safety

- The animation is an additive runtime offset from the authored camera-pivot position and never writes that position back to the prefab or scene.
- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.12 — 2026-09-28 — Forward-only drop lead

### Changed

- Restricted movement-matched drop displacement to the player's forward velocity component.
- Strafing and backward movement now produce the same stationary drop destination as standing still.
- Forward wall safety, grounding, overlap validation, animation, and exact goal placement remain unchanged.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.11 — 2026-09-28 — Movement-matched object drop

### Changed

- Ordinary drops now inherit a small bounded displacement from the player's current horizontal movement, giving the quick descent a matching directional drift.
- Movement lead is projected along an aimed wall instead of through it, and the shifted destination still passes through all existing grounding, wall-clearance, and overlap checks.
- Exact goal placement remains unchanged.
- Added Inspector controls for movement lead time, maximum offset, and minimum activation speed.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.10 — 2026-09-28 — Underfoot pickup and fall

### Changed

- Left-click pickup now retains the centered aim path as its first priority, then checks a short sphere probe directly beneath the player's feet.
- A mission object supporting the player can therefore be collected without aiming down at it.
- Pickup disables the supporting object's collider as before, allowing the existing CharacterController gravity to lower the player naturally onto the next surface.
- Added Inspector controls for underfoot probe radius, starting height, and distance.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.9 — 2026-09-28 — Facing-wall drop priority

### Fixed

- A mostly vertical surface directly under the crosshair now takes priority over the higher flat surface on top of that obstacle.
- The drop destination is moved to the player-facing side of the wall before the downward floor probe, so the ball lands beside and leans against the obstacle instead of jumping onto it.
- Existing overlap rejection, horizontal clearance, grounding, and quick kinematic drop animation remain active.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.8 — 2026-09-28 — Quick kinematic drop animation

### Changed

- Restored a brief visible drop from the viewport center to the collision-safe grounded destination.
- The animation uses frame-rate-independent, gravity-like interpolation rather than Rigidbody motion; collision remains disabled in transit and activates only after the object reaches its static resting point.
- Added Inspector controls for duration, viewport-center depth, minimum fall height, and fall-curve exponent.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

## 0.2.7 — 2026-09-28 — Collision-safe object drops

### Changed

- Dropping now sphere-casts along the horizontal path and stops objects on the player-facing side of walls and props instead of allowing placement through them.
- Final drop positions are grounded vertically, pushed horizontally out of intersecting colliders with bounded penetration-resolution passes, and rejected if any blocking overlap remains.
- Added Inspector controls for wall clearance, collision tolerance, resolution passes, and the wall-normal threshold.

### Safety

- Runtime scripts and documentation only; no scene, prefab, material, HUD, camera, or Build Settings asset was rewritten.

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
