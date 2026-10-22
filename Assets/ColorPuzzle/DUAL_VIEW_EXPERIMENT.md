# Campaign dual-material views

The complete pre-conversion implementation is saved in the local Git branch `backup/pre-dual-appearance-2026-10-22` (commit `6437bae`). No scene transforms or hand-placed objects were changed.

## Visual recipe

1. One mesh renderer occupies the authored object's exact transform. Its original greyscale material(s) render in the main camera.
2. The same renderer uses its authored color material(s) for the aligned ColorReveal camera. Only its material array changes; the mesh, depth test, lighting, shadow reception, collider, and position stay the same.
3. The ColorReveal camera still renders a full-frame texture. The moving circular UI clips that texture at matching screen coordinates. World restoration still expands its existing spherical screen-space mask.
4. Existing `ColorProxy` objects remain as material references and recovery assets, but are disabled during play for converted objects. This removes the shell's 1.002-scale overlap and proxy-specific occlusion error. The ocean gradient likewise uses its one greyscale card for both views; its old color card supplies the color material.

Every current campaign MeshRenderer is set up: the floor, ocean gradient, intro pillar, magnifier pickup visual, and stage 1–3 gate beacons. Stage 4 currently contains no visible puzzle meshes. The shell prefabs are also converted for future use, although they are not currently placed in the four-stage campaign. The perimeter boundaries are collider-only, so there is no material to assign to them.

## Future-object workflow

New mesh objects placed under `OceanSharedRoom`, `OceanBackdropRig`, or any of the intro/four stage prefabs automatically receive a runtime dual-view component. Until a color is authored, they use their starting material in both cameras, so they remain visible rather than disappearing in the lens. This runtime fallback is **not saved to the prefab**.

For a persistent, individually tunable setup: open the stage/room prefab in Prefab Mode, select the new mesh object, choose **Color Puzzle → Set Up Selected Objects for Dual View**, then set its two material arrays in **Dual View Material** in the Inspector and save the prefab. You can also add that component manually. If the new object has a `ColorProxy` child, the menu copies that child's materials and records it as fallback. Use **Color Puzzle → Validate Campaign Dual Views** to find authored meshes without a saved setup. Never create a color proxy solely for this system.

The menu initially references the object's current material in both arrays when there is no color version. Change only **Color Materials** to a suitable existing cel material or a new material asset. If only one object should change, duplicate its material asset first; editing a shared material changes every object that uses it. Leave the geometry and transform alone.

## Tuning / parameter inventory

On a converted object's **Dual View Material** component:

| Inspector field | Effect |
| --- | --- |
| Greyscale Materials | Per-submesh materials for the main camera. Empty = renderer's authored starting materials. |
| Color Materials | Per-submesh materials for the ColorReveal camera. Empty = legacy ColorProxy materials (including per-instance prefab overrides), or the greyscale materials if no color has been authored. |
| Legacy Color Proxy | Optional earlier color object used only as a material source; hidden during play. |

On each campaign root's **Dual View Root** component:

| Inspector field | Effect |
| --- | --- |
| Scan Interval Seconds | How often runtime-spawned child meshes are discovered; default 1 second, minimum 0.1. |
| Include Inactive Children | Also prepare children currently hidden by puzzle state; enabled by default. |
| Log Missing Color Materials | Warn once when a new runtime object is visible in both views but has no authored color; enabled by default. |

Keep the two arrays aligned with the mesh's submesh slots. For an individually tuned object, fill both arrays explicitly and retain the proxy field only as backup. The `DualView` layer is selected automatically at runtime and both gameplay cameras render it. The magnifier's **Viewport Screen Fraction**, **Render Texture Height**, **Lens Move Sensitivity**, and **Lens Move Easing** remain on `MagnifierView`.

## Recovery and limits

To compare with the old rendering path, disable the **Dual View Material** component on a prefab; its `OnDisable` restores the original material and layer, and reactivates an existing color child. The backup branch is the exact complete pre-trial project. Value Search retains the old palette mapping for unconverted objects; converted objects use the actual authored color's luminance in its greyscale overlay.

The system changes renderer material arrays at each gameplay camera boundary. It creates no per-frame material instances, but profile CPU and batching cost if many hundreds of scenery objects are added. Unity's campaign-prefab validation and an initial greyscale Play-mode view pass. The color camera, magnifier crop, and future puzzle placements still need visual playtesting.
