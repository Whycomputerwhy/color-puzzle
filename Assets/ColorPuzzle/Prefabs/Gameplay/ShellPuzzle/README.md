# Shell puzzle blockout

This folder contains two base prefabs and four variants of each: `Shell Keyhole 01–04` and `Shell Key 01–04`. Their `ShellPuzzlePiece` components expose the same Pair Number, Fit Diameter, and Pearl Color.

| Pair | Fit diameter | Pearl / keyhole rim |
| --- | ---: | --- |
| 01 | 0.42 | Red |
| 02 | 0.56 | Cyan |
| 03 | 0.70 | Violet |
| 04 | 0.84 | Gold |

Each key is a beige cube with a color pearl on top. Each keyhole is a stone cylinder with a differently sized dark circular slot marker and matching color rim. These are visual blockout slots, not holes cut into the mesh. The separate `Clam Casing` and `Reflective Shell` prefabs are a large transparent sphere and a small metallic sphere.

The four key variants inherit the same cube and pearl transforms from `Shell Key Base`. Their different sizes come only from uniform scale on each variant's root (01: 1, 02: 1.412, 03: 1.824, 04: 2.235). Edit the cube shape, height, or pearl placement in the base prefab to update all four keys; edit a variant root's scale to change only that key's overall size. The shared shape is grounded at its root, so floor drops and keyhole placement still rest on their surfaces.

All visible meshes have a `DualViewMaterial` component. In Prefab Mode, set **Greyscale Materials** and **Color Materials** on each mesh to tune the two camera views. The color materials are in `Assets/ColorPuzzle/Materials/ShellPuzzle/`.

In Stage 1, left-click picks up a key. Q drops it to the floor with a brief mid-air fall, or places it while aiming near a keyhole's slot. Any placed key can be picked up and rearranged until all four matches are correct. The key base prefab reuses `ItemPickupCardDraw.wav` and `ItemDropBookClose.wav` (including keyhole placement); their clips and volumes are adjustable on its `ShellPuzzlePiece`. Wrong numbered matches also play `WrongShellPlacement.wav`. Only the complete correct combination locks the keys, completes the stage, and plays `CorrectShellPlacementAndReveal.wav` once. The `ShellPuzzleController` component on the Stage 1 prefab owns that rule and exposes correct/wrong clips and volumes. The old Stage 1 gate beacon is removed by the campaign scene's prefab override.

Victory visual recipe: the four *actual pearl meshes* brighten at their edges and grow slightly together, return to normal, and then the transparent casing fades out. The glow uses each pearl's cel-material Fresnel term rather than a separate covering sphere; the fade modifies temporary copies of both casing view materials, never the source assets. In Stage 1's `ShellPuzzleController` Inspector, tune **Pearl Glow Duration**, **Pearl Glow Brightness**, **Pearl Glow Scale**, and **Casing Fade Duration**. A smooth half-sine shapes the pearl pulse; smoothstep shapes the casing fade. These four controls are the full victory-effect parameter set. The casing turns off only after the fade finishes.

The shared room's four invisible perimeter colliders now match the authored Ocean Floor bounds: X 64.273–105.873 and Z 261.828–309.128, with 12-unit-high walls beginning at the floor surface. To resize the room later, update the floor first, then align the colliders to its new edges; keep their transform scales at 1 so their Box Collider sizes stay literal world units.

The numbered prefabs are already placed in the campaign scene by the designer. Keep rearranging those scene instances freely; no puzzle code assumes fixed world positions. Changing a base prefab updates shared structure; changing a numbered variant updates only that pair's version. If you change a variant's fit size, also resize its `Slot Recess`/`Slot Rim` or `Beige Cube` child and update the matching variant's Fit Diameter.
