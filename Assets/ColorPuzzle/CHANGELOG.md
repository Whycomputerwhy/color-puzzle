# Color Puzzle Version History
This log is intended for people and AI agents continuing the same Unity project. Append new entries; do not rewrite past decisions without explaining the migration.

## 0.5.1 — 2026-10-15 — Forest distance camouflage

### Added

- Added the reusable `ForestCamouflage` runtime component for future Forest props and foliage. It blends an authored `_BaseColor` or `_Color` toward a configurable distant camouflage color without allocating material instances.
- Added camera/player override support so the component can be used in the current first-person setup or on a future alternate player rig.

### Safety

- The Forest scene remains an empty placeholder; no scene objects, shared prefabs, or Ocean assets were reassigned.

## 0.5.0 — 2026-10-13 — Playtest telemetry

### Added

- Added an automatically bootstrapped, persistent `TelemetryManager`, based on the supplied USC package's manager/metric pattern and updated for the current Input System.
- Added per-level measurements for time to portal unlock, magnifier activations, pickup actions, and time from portal unlock to first portal entry.
- Added an append-only `ColorPuzzle-Telemetry.txt` output under `Application.persistentDataPath/Telemetry`; final records save on portal entry, level exit, and quit, while F9 appends a manual snapshot.
- Added a trigger-only portal-entry reporter to the existing runtime-generated completion door without changing its visual transform.
- Added a concise telemetry usage guide to the Enter developer panel.

### Safety

- No authored scene hierarchy, manual object placement, materials, cameras, or level geometry were rewritten.
- The supplied package was used as an architectural reference rather than imported verbatim, avoiding its legacy input calls and timestamp-file proliferation.

## 0.3.0 — 2026-10-13 — Alpha level sequence and production HUD

### Added

- Established the alpha build order: Forest Tutorial (Level 1), Ocean (Level 2), and Village Ending (Level 3).
- Added intentionally empty Forest and Village scene placeholders with the shared player/dev panel but no authored level content.
- Added a Level Sequence group to the Enter developer panel for direct Forest/Ocean/Village loading.
- Updated the read-only build validator for the three-scene alpha sequence.

### Changed

- Disabled the persistent top-left objective card and bottom controls legend for the production HUD.
- Forest is now build index 0; the current underwater scene is preserved unchanged at index 1; Village is index 2.

### Safety

- No underwater scene transforms, objects, materials, camera settings, or manually authored level layout were modified.
- The previous vertical-slice scene remains in the project as reference history but is not included in the alpha build sequence.

## 0.2.44 — 2026-10-12 — Open-water ocean volume

### Added

- Added `UnderwaterOceanBackdrop`, a lightweight runtime ocean-volume layer for the two existing cameras.
- Each camera receives a camera-facing upper-to-lower water gradient, replacing the room-like sky/background with an open submerged volume.
- Converted the visible boundary walls into invisible collision boundaries at runtime: they continue to contain the player without reading as a room.

### Safety

- No authored level transforms, props, floor geometry, imported shell/pillar materials, or colliders were moved or replaced.
- All art-direction controls live on the `UnderwaterOceanBackdrop` component on `FirstPersonPlayer`.

## 0.2.43 — 2026-10-04 — OBJ ocean-model source migration

### Changed

- Replaced the shell and pillar visual meshes with assets generated from the supplied `shell.obj` and `pillar.obj` exports.
- Applied the OBJ export-space correction once during mesh normalization: the exported Z-up model axis becomes Unity Y-up, with forward compensated consistently.
- Matched each normalized OBJ mesh to the bounds of its outgoing visual mesh before swapping it, preserving every existing shell instance transform, scattered placement, pillar placement, and the goal's placement socket.

### Safety

- The active underwater scene was not regenerated or repositioned. The goal keeps its existing collider, target marker, socket, and gameplay identity; only the visible pillar renderer was exchanged.
- The original GLB/FBX assets remain in the project as source history. The new OBJ/MTL source files live under `Assets/ColorPuzzle/Models/Ocean/OBJ/` and normalized Unity meshes live under `Generated/Ocean/`.

## 0.2.42 — 2026-10-01 — Value Search puzzle variation

### Added

- Added a session-persistent `Color Search / Value Search` selector to the Enter developer panel.
- Value Search disables the magnifier completely and renders the complete scene through a full-screen Rec. 709 greyscale pass.
- Value Search temporarily reduces reveal-layer materials to six hue families with deliberately separated luminance values, then restores every original material when returning to Color Search.
- Added mode-specific controls text and story-objective copy; Value Search removes the magnifier hint and frames the mission as comparing light and shadow.

### Changed

- Converted the top-left objective HUD from a generated rounded rectangle to a normal square-corner white box.
- Full Color is disabled while Value Search is active so the variation remains a strictly greyscale mechanic.
- R continues to reset gameplay without resetting the selected puzzle mode, developer view, or interaction cue.

### Safety

- The palette remap uses temporary runtime material clones. It does not rewrite scene materials, prefabs, object transforms, or manually authored level geometry.

## 0.2.41 — 2026-10-01 — Persistent story objective card

### Added

- Added an always-visible, top-left story objective card with a white rounded fill, dark text, and a soft separation shadow.
- Objective copy now reads: “The ocean's colors have vanished. Peer through your magnifier to reveal them, then find the object that belongs atop the stone pillar.”
- Exposed the card text, visibility, size, margin, padding, corner radius, font size, fill/text colors, and shadow styling in the `MagnifierView` Inspector settings.

### Safety

- The card is created by the existing shared runtime HUD and appears in both Greyscale and Full Color modes. No scene objects, transforms, prefabs, or manually authored level geometry were regenerated.

## 0.2.40 — 2026-10-01 — Corrected ocean-model orientation

### Changed

- Corrected the imported shell axis so collectible shells rest on the ground instead of standing upright.
- Added deterministic yaw variation across the existing shells for a more naturally scattered layout without moving their authored positions.
- Corrected the pillar model axis and compensated for the former goal cylinder's nonuniform scale so the pillar remains upright instead of flattened.
- Re-derived the target marker and placement socket heights from the corrected pillar top.

### Safety

- The migration changes only ocean-model visual orientation and the target/socket alignment required by the corrected pillar. Existing shell positions and level geometry remain untouched.

## 0.2.39 — 2026-10-01 — Ocean pillar and collectible shells

### Changed

- Replaced the underwater cylinder goal's visible mesh with `pillar_ocean`, preserving its embedded colored texture in Greyscale, magnifier, and Full Color views.
- Replaced the red mission ball and every interactive decoy ball with a normalized `shell_ocean` mesh while retaining existing pickup, placement, outline, and discovery behavior.
- Extracted the GLBs' embedded PNG artwork into Unity texture assets; shell color variants multiply the original grey shell texture by the existing red/aqua/blue/purple/orange/pink/green/cyan/gold palette.
- Reduced the shell visual diameter and matching sphere-collider radius from 1.0 to 0.76 local units.
- Kept the flat white-outlined placement target above the new pillar and adjusted its socket for the smaller shell.

### Safety

- All authored scene object positions, rotations, scales, and level geometry remain unchanged. The underwater scene diff contains only per-instance color-material substitutions for existing decoys.
- Source GLBs, Unity-native FBXs, extracted textures, normalized mesh, generated materials, and an idempotent editor migration are stored under `Assets/ColorPuzzle` for future tuning.

## 0.2.38 — 2026-10-01 — Reduced color-view interaction glow

### Changed

- Added an Inspector-visible color-view glow scale, defaulting to 0.5.
- Color and magnifier cameras now render the white interaction back glow at half the greyscale view's screen-space width and half its composite opacity.
- The black greyscale glow retains its existing width, softness, color, and intensity.

### Safety

- Runtime outline parameters, player prefab serialization, and documentation only. No scene objects or authored level transforms were modified.

## 0.2.37 — 2026-10-01 — Natural close-range camera lens

### Changed

- Reduced both gameplay cameras from a 58° to a 50° vertical field of view so nearby spheres and greybox structures receive only mild perspective stretching.
- Reduced mouse sensitivity from 0.11 to 0.095 and pickup aim assist from 0.14 to 0.12 using the same approximate tangent ratio as the lens change.
- Kept both cameras on the matching 0.1 near clip plane so the greyscale and magnifier views remain aligned around nearby geometry.

### Safety

- Player prefab tuning and documentation only. No scene objects, transforms, or manually authored level changes were modified.

## 0.2.36 — 2026-10-01 — Complex surrounding routes

### Changed

- Added a second additive scene layer that gives the rear and both side perimeters the same multi-height structural density as the original forward route.
- Built a rear terrace and bridge network, stepped left-side walkway, raised right-side overlook, supporting pillars, screened pockets, and environmental landmarks.
- Added eight more interactive decoy balls across low, mid-height, elevated, under-bridge, and partially hidden perimeter placements.

### Safety

- New objects are isolated under `--- SURROUNDING EXPANSION 0.2.36 ---`; previous manual and generated scene objects are not moved, regenerated, or replaced.
- The pass is idempotent and refuses to add a second copy when its named root already exists.

## 0.2.35 — 2026-10-01 — Expanded underwater search space

### Changed

- Moved the player start close to the red goal while aiming into the main route.
- Relocated the red solution ball to a reachable rear-left grotto outside the initial sightline.
- Upgraded every underwater decoy ball to use the normal pickup, carry, drop, and interaction-cue mechanics.
- Wrong-color balls can now be placed on the target and remain pickable, but do not restore the goal or reveal the completion door.
- Added ten scattered decoy balls, including rear, side-route, elevated, and partially screened placements.
- Added twenty additive underwater greybox props across the rear, side, and far spaces so the composition continues behind the initial camera.

### Safety

- The scene expansion is additive and stored under `--- GAMEPLAY EXPANSION 0.2.35 ---`; existing environment objects are not rebuilt.
- Only the explicitly requested player and red-ball transforms are changed. Runtime scripts, the shared decoy prefab, and the underwater scene are updated in place.

## 0.2.34 — 2026-10-01 — Reduced perspective distortion

### Changed

- Reduced the main and magnifier color cameras from a 68° to a 58° vertical field of view to make nearby objects and level geometry look less stretched.
- Matched both cameras to a 0.1 near clip plane so the magnifier and regular view frame nearby geometry consistently.
- Reduced mouse sensitivity from 0.13 to 0.11 to preserve the previous on-screen look speed at the narrower field of view.
- Tightened pickup aim assist from 0.16 to 0.14 world units so its apparent screen-space generosity remains approximately unchanged.

### Safety

- Player prefab tuning and documentation only. No scene files or manually positioned level objects were modified.

## 0.2.33 — 2026-10-01 — Minimal outlined inventory HUD

### Changed

- Replaced the bottom inventory panel with only a small neutral ball glyph and the label `Ball`.
- Removed the background rectangle and `CARRYING:` prefix.
- Added a white outline to both the ball glyph and label for contrast in either view.
- Added Inspector controls for HUD gap, ball size, neutral fill, outline color, and outline distance.
- Serialized the color-neutral `Ball` display name directly on the collectible prefab so disabled scene/domain reload cannot retain the former runtime default.

### Safety

- Runtime HUD construction, player prefab tuning, and documentation only. No scene or authored HUD asset was saved or regenerated.

## 0.2.32 — 2026-10-01 — Placement-only color restoration

### Changed

- Picking up the mission object no longer permanently reveals its color.
- Ordinary drops return the object to its greyscale world appearance; the magnifier can still reveal its true color while inspecting it.
- Permanent object color is restored only by successful placement on the matching goal base.
- Removed the colored inventory swatch and color word from the object display name. The bottom HUD now shows only the neutral `CARRYING: Ball` label.

### Safety

- Runtime object/HUD logic, player prefab serialization, and documentation only. No scene, camera, level-layout, material, or Build Settings asset was saved or regenerated.

## 0.2.31 — 2026-10-01 — Unity RenderGraph soft outline

### Changed

- Replaced the visibly crescent-shaped mesh-shell cue with a full-resolution screen-space soft outline adapted from Unity Technologies' official Unity 6 Per-Object Outline RenderGraph example.
- The renderer feature draws the collectible's silhouette to a mask, expands/softens it in two screen-space passes, and composites only outside the object's stencil. The object therefore remains visually on top.
- The mask ignores scene depth so walls and nearby props cannot cut the interaction cue apart.
- Preserved the 0.75-second aim dwell and 0.25-second smoothstep fade. Greyscale receives black; the magnifier and Full Color camera receive white.
- Added automatic, idempotent installation into every URP renderer-data asset and included source attribution under `ThirdParty/UnityPerObjectOutline`.

### Safety

- No scene, prefab, camera, HUD layout, level layout, or Build Settings asset was saved or regenerated. The installer appends one renderer feature without removing existing features such as SSAO.

## 0.2.30 — 2026-10-01 — Recording-driven glow correction

### Fixed

- Reviewed the supplied 10.975-second QHD recording frame by frame. It showed that the interaction cue was only a narrow white edge around the ball; the large orange disc was the magnifier border, not the glow.
- Expanded the glow from 10 to 28 screen pixels and increased sampling from 6 to 12 nested shells, turning the hard outline into a broader soft falloff.
- Increased softness from `0.7` to `0.9` and slightly reduced peak alpha to remove the cut-out/sticker appearance.
- Added `Color Glow Width Ratio` at `0.72`: the white color-view core is now narrower than the underlying black falloff, preserving contrast when the ball sits over the level's nearly white floor.

### Safety

- Adjusted runtime glow parameters, shell-width calculation, player prefab defaults, and documentation only. No scene, camera, HUD layout, input, renderer-data asset, level layout, or build settings were saved or regenerated.

## 0.2.29 — 2026-10-01 — Mesh-conforming soft interaction glow

### Changed

- Replaced the 64-pixel billboard halo with a resolution-independent mesh-shell highlight based on the collectible's actual mesh and normals.
- Added an exact-silhouette stencil mask, preventing every glow layer from covering the highlighted object itself.
- Added six nested screen-pixel shells with normalized alpha distribution for a softer falloff, especially in the color view.
- Preserved the 0.25-second smoothstep fade, object-only eligibility, black Greyscale cue, white color-view cue, and visibility through intervening geometry.
- Replaced obsolete billboard controls with `Glow Width Pixels`, `Glow Layer Count`, and `Glow Softness` Inspector controls.

### Technique review

- Compared QuickOutline-style world-space shells, Unity's official per-object RenderGraph outline sample, URP Render Objects/stencil passes, and community URP 17 RenderGraph outline features.
- Chose runtime mesh shells plus stencil for this slice: it solves the current silhouette and resolution defects without importing a package or editing the project's renderer data asset.
- A RenderGraph edge/dilate outline remains the best later upgrade if highlights must support arbitrary skinned or hard-edged production meshes and many simultaneous targets.

### Safety

- Runtime scripts, two Resources shaders, player prefab cue defaults, and documentation only. No scene, camera, HUD layout, input, renderer-data asset, level layout, or build settings were saved or regenerated.

## 0.2.28 — 2026-10-01 — Back-glow fade-in and technique review

### Changed

- Added an Inspector-adjustable 0.25-second fade-in after the existing 0.75-second aim dwell.
- The fade uses unscaled time and a smoothstep envelope, so it remains frame-rate independent and starts/ends without a hard opacity corner.
- Reset the fade whenever aim is lost, the target changes, the cue mode changes, or the developer panel clears the cue.

### Technique decision

- Reviewed established Unity highlight patterns: material-property tint/emission, inverted-hull stencil outlines, and URP renderer-feature screen-space outlines.
- Retained the center-cutout billboard halo for this greybox slice because it preserves the requested behind-object glow, remains visible through nearby geometry, and avoids scene, renderer-asset, mesh-normal, or authored-material changes.
- If this cue becomes a production outline rather than a back glow, migrate it to an inverted-hull plus stencil implementation; that approach follows the object silhouette more accurately than a billboard.

### Safety

- Runtime interaction script, player prefab defaults, and documentation only. No scene, camera, HUD layout, input, level-layout, build-setting, or developer-setting asset was saved or regenerated.

## 0.2.27 — 2026-10-01 — Correct zero-alpha halo center

### Fixed

- Corrected the annular-mask calculation: Unity's `Mathf.SmoothStep(from, to, t)` had been given threshold edges as its output range, leaving roughly 70% mask opacity at the sprite center.
- The generator now normalizes radius with `InverseLerp(innerRadius, innerRadius + innerFeather, radius)` and smooths that zero-to-one value.
- Every pixel inside the configured inner radius now has exactly zero halo alpha, so the collectible renders unobscured while the outer overlay remains immune to world-depth clipping.

### Safety

- One runtime mask formula and documentation only. No prefab, scene, shader, camera, HUD, input, developer-setting, level-layout, or build-setting asset was changed.

## 0.2.26 — 2026-10-01 — Object-only halo and reset-safe developer settings

### Fixed

- Restricted Back Glow to collectible objects. Goal bases no longer glow; their valid `Q PLACE` feedback remains available in Center Text mode.
- Enlarged and tightened the halo's transparent center so the collectible itself remains completely unobscured above the overlay.
- Preserved the selected Greyscale/Full Color and Back Glow/Center Text developer modes when R reloads the gameplay scene.

### Reset behavior

- R still resets all gameplay state through the normal scene reload: player, carried object, object positions, goal completion, and completion door.
- A fresh play session restores the authored developer defaults. The panel's open/closed state is not preserved.

### Safety

- Runtime scripts, two existing player-prefab cue values, and documentation only. No scenes, level geometry, camera transforms, HUD layout, input bindings, shader assets, or build settings were rewritten.

## 0.2.25 — 2026-10-01 — Unobstructed interaction halo

### Changed

- Replaced the depth-dependent filled glow with a center-cutout annular halo and a dedicated transparent overlay shader.
- The shader uses an always-pass depth test, so walls, props, and other world geometry cannot cut off the halo.
- Added Inspector controls for the halo's normalized inner radius and feather so the overlay surrounds the target without painting across its center.
- Kept the paired layer behavior: black in Greyscale and later white on `ColorReveal`.

### Safety

- Runtime cue rendering, two player-prefab cue values, one Resources shader, and documentation only. No scenes, camera transforms, level geometry, HUD layout, input bindings, or build settings were rewritten.

## 0.2.24 — 2026-09-30 — Placement-only carry cue and stronger grayscale glow

### Changed

- Removed the ordinary `Q DROP` interaction suggestion. While carrying, feedback now appears only for the special valid-goal action `Q PLACE`; pickup feedback remains available while empty-handed.
- Corrected the player prefab's serialized cue colors so Greyscale uses black and the color pass uses white.
- Increased the halo scale and opacity, reduced its depth offset, and broadened its radial falloff so the black back glow stays clearly visible around the target silhouette in Greyscale.
- Restored the authored `0.75 s` dwell threshold on the player prefab.

### Safety

- Changed only the cue-selection runtime logic, the cue fields on the existing player prefab, and documentation. No scenes, level geometry, camera transforms, HUD layout, input bindings, or build settings were rewritten.

## 0.2.23 — 2026-09-30 — Flat underwater goal marker

### Changed

- Replaced the underwater goal's stacked red and white cylinder meshes with one flat transparent image containing a red center and white perimeter ring.
- The new marker is a single collider-free, unlit quad with shadows and light/reflection probes disabled, so it reads as a painted target instead of a raised button.
- Added an idempotent editor migration with centralized texture, color, feather, size, and height parameters; it creates the TGA and material and can safely update the same prefab again.

### Safety

- Preserved the existing placement socket, `GoalPedestal`, cylinder collider, root transform, and scene instance placement. No scene, player, camera, HUD, controls, Build Settings, or manually arranged level geometry was rewritten.

## 0.2.22 — 2026-09-30 — Interaction cue variations

### Added

- Added an `INTERACTION CUE` selector group to the Enter developer panel with `Back Glow` and `Center Text` options.
- Added a shared, Inspector-adjustable `0.75 s` uninterrupted aim dwell before interaction feedback appears.
- Added a runtime billboard glow behind aimed mission objects and valid goals. The base view receives a black halo, while `ColorReveal` receives a later white halo so the magnifier and Full Color mode show the correct variation.
- Added tiny contextual center labels: `LC PICKUP` for an aimed collectible, `Q PLACE` for an aimed valid goal, and `Q DROP` for another focused surface while carrying.
- Added centralized Inspector controls for cue timing, text layout/colors, glow size/depth, mask resolution/falloff, and grayscale/color glow colors.

### Safety

- Runtime scripts and documentation only. Glow textures, materials, sprites, and HUD text are generated transiently at play time; no scene, prefab, material, texture, camera, HUD, or Build Settings asset was rewritten.

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
# 0.4.0 - Start and Pause Menus

- Added `ColorPuzzleStartMenu.unity` as build index 0 with Play and Quit actions; Play enters the Forest tutorial placeholder.
- Added a reusable `PauseMenuController` to the shared player prefab. Escape pauses/resumes, halts time and gameplay input, exposes fixed control instructions and a persistent master-volume slider, and can return to the start menu.
- Added `GameAudioSettings` so the chosen master volume persists across scene changes and future launches.
- Added a real URP camera and audio listener to the start-menu scene so the menu no longer reports or renders as camera-less.
- Updated the alpha build validator for Start Menu -> Forest -> Ocean -> Village.
- Preserved the manually authored underwater scene and all scene-instance transforms; this change touches runtime scripts, the shared player prefab, a new menu scene, Build Settings, and documentation only.
