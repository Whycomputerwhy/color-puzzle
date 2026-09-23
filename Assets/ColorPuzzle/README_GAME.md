# Color Puzzle — Vertical Slice Guide

## Game design

Color Puzzle is a casual first-person 3D exploration platformer about restoring color. The prototype now starts in **Full Color** for direct comparison, while **Puzzle View** retains the intended monochrome world and magnifier reveal. The player holds **right click while grounded** to inspect through the circular magnifying glass. **Magnifier Control** switches between Move Lens (frozen camera, movable glass) and Fixed Lens (centered glass, mouse-look). The player discovers the red ball, stores it in a bottom inventory HUD, and presses Q to place it on the matching red-marked goal. Completing the match restores the goal and reveals a green door on the map's right side.

### Prototype controls

- **WASD** — move
- **Mouse** — look
- **Space** — jump
- **Hold right click while grounded (Move Lens)** — freeze the player/camera and move the color-revealing magnifier
- **Hold right click while grounded (Fixed Lens)** — freeze walking, keep the lens centered, and aim the full camera view
- **Puzzle View / Full Color** — compare the intended black-and-white puzzle presentation with an always-on color-only developer view
- **Left click** — pick up the centered mission object
- **Q** — place the carried object on a matching goal, or drop it in front of the player
- **R** — reset the current scene
- **Escape** — release the cursor to use the top-right mode buttons; click outside UI to recapture it

### Play path

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity` and press Play. Select Puzzle View to test discovery, find the gray ball, hold right click while grounded until the ball turns red, and left-click it. Its world mesh disappears and the bottom inventory HUD shows the carried Red Ball. Approach the gray box with the red top mark, aim at it, and press Q. The goal becomes red and the completion door appears beside the right wall. Press R at any time to restart.

## Visual system recipe

### Visual intent

Puzzle View is deliberately sparse and grayscale against a solid white sky. Holding right click while grounded adds a circle of saturated color, with a warm rim and blue color-view sky. The color pass now renders the complete normal world first and draws color proxies afterward, so objects without proxies never disappear. Move Lens freezes the player and camera while the circle moves; Fixed Lens freezes walking while the camera aims through a centered circle. Full Color routes this complete color pass directly to the screen and is the default developer view. A discovered mission object remains saturated after the glass closes. Goal completion restores the pedestal and reveals the right-side door.

### System breakdown

| Stage | Visible role | Inputs | Process | Output | Feeds into |
|---|---|---|---|---|---|
| Base world | Monochrome exploration | Gray materials, main camera mask | Main camera excludes `ColorReveal` layer | Gray scene | Screen base |
| Color proxies | True-color counterpart | Shared mesh transforms, color materials | Child proxy meshes render only on `ColorReveal` | Colored scene geometry | Reveal camera |
| Reveal camera | Complete perspective-matched color view | Main camera layers + color-proxy layers, pose/FOV, screen aspect | Renders all ordinary objects, then later-queued color proxies | Complete color frame | Circular UI or Full Color display |
| Moving circle mask | Magnifying-glass shape | Mouse delta, RenderTexture, alpha sprite | Moves the mask while counter-moving the full-screen RawImage | Aligned round viewport | Final composite |
| Fixed circle mode | Centered aim variation | Mouse delta, player camera pose, centered lens coordinate | Locks player translation, permits camera look, and pins the mask to screen center | Camera-driven reveal search | Reveal camera + discovery |
| Magnifier selector | Interchangeable mechanic comparison | Move Lens / Fixed Lens, current mode | Runtime UI routes selection to the shared control state and highlights the active choice | Selected interaction mapping | Player controls + lens motion |
| Dev view selector | Presentation comparison | Puzzle View / Full Color, reveal camera | Routes color output either into the circular RenderTexture or directly to the screen | Mixed grayscale/color or continuous color | Final composite |
| Discovery | Persistent color clue | Lens screen coordinate, distance, aim-dot threshold | Casts through the lens and changes the mission renderer from gray to red | Persistent red object | Main view |
| Completion | Mission payoff | Matching object ID + goal | Snaps item and changes pedestal material | Restored red goal | Main view |
| Inventory HUD | Carried-object feedback | Carried object's display name and color | Hides the world mesh while carried and shows a bottom swatch/label | `CARRYING: Red Ball` | Drop/place decision |
| Door reward | Completion reveal | Successful red-ball placement | Activates a runtime gray/color-proxy door beside the right wall | Newly visible green door | Completion feedback |

### Formula map

`viewportPixels = min(screenWidth, screenHeight) × viewportScreenFraction`

`lens(t+1) = clamp(lens(t) + mouseDelta × lensMoveSensitivity, screenBounds − lensRadius − padding)`

`fixedLens(t) = (screenWidth / 2, screenHeight / 2)`

`cameraAim(t+1) = cameraAim(t) + mouseDelta × playerMouseSensitivity`

`finalView = PuzzleView ? grayscaleBase + circularColorReveal : fullScreenColorCamera`

`magnifierActive = rightMouseHeld ∧ playerGrounded ∧ PuzzleView`

`revealImageOffset = −lensCanvasOffset`

`discover = intersects(projectedObjectBounds, lensCircle + discoveryScreenPadding) ∧ distance ≤ discoveryDistance ∧ lineOfSight`

The reveal camera uses the main camera's world position, rotation, field of view, and aspect ratio. Its culling mask is the union of the normal-world mask and the original `ColorReveal` mask. Color-proxy materials receive a later runtime render queue so they replace corresponding gray surfaces, while ordinary objects remain visible underneath. The main camera still excludes `ColorReveal`.

### Layer and execution order

1. Main camera renders grayscale objects (opaque replacement is not used; authored gray materials are cheap and explicit).
2. Reveal camera renders the complete normal world plus later-queued color proxy meshes into `MagnifierColorRevealRT` while right click is held on the ground.
3. The selected mode supplies either a movable lens coordinate or the fixed screen center.
4. Screen-space HUD clips that texture with `MagnifierCircle.png` and draws it over the base view.
5. Crosshair, controls, bottom inventory HUD, and grouped top-right selectors render on top.

In Full Color, the reveal camera bypasses the RenderTexture and renders directly to the display after matching the main camera pose. Returning to Puzzle View reconnects the same camera to the circular reveal texture.

## Inspector variable inventory

All game-feel variables are serialized and editable in the Unity Inspector.

| Component / variable | Default | Units / range | Effect |
|---|---:|---|---|
| FirstPersonPlayer / Move Speed | 5 | m/s, ≥0 | Ground movement speed |
| FirstPersonPlayer / Air Control | 0.55 | multiplier, ≥0 | Horizontal authority while airborne |
| FirstPersonPlayer / Jump Height | 1.35 | meters, ≥0 | Jump apex |
| FirstPersonPlayer / Gravity | -22 | m/s² | Fall and jump timing |
| FirstPersonPlayer / Grounded Force | -2 | m/s | Keeps the controller grounded |
| FirstPersonPlayer / Mouse Sensitivity | 0.13 | multiplier, ≥0 | Look response |
| FirstPersonPlayer / Vertical Look Limit | 85 | degrees, 1–89 | Pitch clamp |
| FirstPersonPlayer / Lock Cursor On Start | true | toggle | FPS cursor capture |
| MagnifierView / Reveal Mouse Button | Right | Right/Middle | Hold-to-reveal input; activation also requires grounded state |
| MagnifierView / Discovery Distance | 12 | meters, ≥0.1 | Maximum discovery ray distance |
| MagnifierView / Discovery Screen Padding | 4 | screen pixels, ≥0 | Small tolerance added around the circular lens for reliable edge overlap |
| MagnifierView / Discovery Layers | Everything | layer mask | Raycastable discovery layers |
| MagnifierView / Viewport Screen Fraction | 0.25 | 0.08–0.5 | Circle diameter relative to shorter screen side |
| MagnifierView / Render Texture Height | 720 | pixels, ≥128 | Full-view reveal sharpness and GPU cost; width follows screen aspect |
| MagnifierView / Regular View Sky Color | white | HDR color | Solid background for the grayscale camera |
| MagnifierView / Reveal View Sky Color | blue `(0.08, 0.34, 0.9)` | HDR color | Solid background visible through the lens |
| MagnifierView / Lens Move Sensitivity | 1 | multiplier, ≥0 | Lens response to mouse movement while magnifying |
| MagnifierView / Screen Edge Padding | 8 | screen pixels, ≥0 | Extra gap between the lens rim and screen edge |
| MagnifierView / Reset Lens To Center On Open | true | toggle | Starts each inspection at screen center |
| MagnifierView / Starting Mode | Move Lens | enum | Chooses the initial magnifier interaction variation |
| MagnifierView / Starting Dev View Mode | Full Color | enum | Chooses mixed puzzle rendering or continuous full color at startup |
| MagnifierView / Show Mode Selector | true | toggle | Creates both labeled top-right button groups |
| MagnifierView / Mode Button Size | (140, 36) | canvas pixels | Width and height of each mode button |
| MagnifierView / Mode Button Gap | 8 | canvas pixels, ≥0 | Horizontal spacing between the two buttons |
| MagnifierView / Selector Group Gap | 10 | canvas pixels, ≥0 | Vertical separation between Magnifier Control and Dev View groups |
| MagnifierView / Selector Group Padding | 8 | canvas pixels, ≥0 | Interior spacing around each labeled group |
| MagnifierView / Selector Group Label Height | 20 | canvas pixels, ≥8 | Reserved height for each group title |
| MagnifierView / Mode Selector Margin | (20, 20) | canvas pixels | Offset from the top-right screen corner |
| MagnifierView / Mode Button Font Size | 16 | points, ≥8 | Selector label size |
| MagnifierView / Selector Group Label Font Size | 13 | points, ≥8 | Group-title label size |
| MagnifierView / Selected Mode Color | blue `(0.12, 0.42, 0.92, 0.95)` | RGBA | Active-button background |
| MagnifierView / Unselected Mode Color | charcoal `(0.12, 0.12, 0.12, 0.82)` | RGBA | Inactive-button background |
| MagnifierView / Mode Button Text Color | white | RGBA | Selector label color |
| MagnifierView / Selector Group Label Color | white | RGBA | Group-title text color |
| MagnifierView / Selector Group Background Color | black `(0, 0, 0, 0.55)` | RGBA | Translucent backing that visually groups each button pair |
| PlayerInteraction / Interaction Distance | 4 | meters, ≥0.1 | Pick/place reach |
| PlayerInteraction / Drop Or Place Key | Q | key | Places on a valid goal or drops the carried object |
| PlayerInteraction / Reset Key | R | key | Reloads the active scene to its initial state |
| PlayerInteraction / Drop Distance | 1.4 | meters, ≥0 | Spawn distance when dropping |
| PlayerInteraction / Drop Impulse | 1.5 | m/s, ≥0 | Forward toss strength |
| PlayerInteraction / Interaction Layers | Everything | layer mask | Pick/place raycast layers |
| PlayerInteraction / Placement Distance | 5.5 | meters, ≥0.1 | Maximum assisted placement reach |
| PlayerInteraction / Placement Cast Radius | 0.75 | meters, ≥0 | Side-approach placement aim forgiveness |
| PlayerInteraction / Top Placement Horizontal Padding | 0.35 | meters, ≥0 | Extra footprint allowance when standing on the goal |
| PlayerInteraction / Top Placement Max Camera Height | 3 | meters, ≥0.1 | Highest camera position accepted above the pedestal top |
| PlayerInteraction / Top Placement Minimum Downward Look | 0.1 | downward Y component, 0–1 | Required downward look when placing from atop the goal |
| PlayerInteraction / Show Inventory HUD | true | toggle | Enables the bottom carried-object display |
| PlayerInteraction / Inventory HUD Size | (260, 52) | canvas pixels | Bottom inventory panel dimensions |
| PlayerInteraction / Inventory HUD Offset | (0, 92) | canvas pixels | Position above the bottom-center anchor |
| PlayerInteraction / Inventory HUD Padding | 10 | canvas pixels, ≥0 | Interior panel spacing |
| PlayerInteraction / Inventory Swatch Size | 28 | canvas pixels, ≥8 | Carried-object color square size |
| PlayerInteraction / Inventory Font Size | 18 | points, ≥8 | Carried-object label size |
| PlayerInteraction / Inventory Background Color | black `(0,0,0,0.72)` | RGBA | Inventory panel backing |
| PlayerInteraction / Inventory Text Color | white | RGBA | Inventory label color |
| DiscoverableObject / Object Id | red-ball | string | Goal matching identity |
| DiscoverableObject / Display Name | Red Ball | string | Bottom inventory label |
| DiscoverableObject / Inventory Color | red `(0.85,0.08,0.08,1)` | RGBA | Bottom inventory swatch |
| GoalPedestal / Accepted Object Id | red-ball | string | Required carried object |
| GoalPedestal / Create Completion Door | true | toggle | Enables the runtime completion reward |
| GoalPedestal / Completion Door World Position | (10.4, 1.5, 5.5) | world meters | Places the door beside the right wall |
| GoalPedestal / Completion Door World Euler Angles | (0, 0, 0) | degrees | Door orientation |
| GoalPedestal / Completion Door World Scale | (0.35, 3, 2) | world scale | Door dimensions |
| GoalPedestal / Completion Door Base Color | gray `(0.35,0.35,0.35,1)` | RGBA | Puzzle-view surface |
| GoalPedestal / Completion Door Reveal Color | green `(0.12,0.72,0.38,1)` | RGBA | Color-view surface |
| GoalPedestal / Completion Door Name | RestoredColorDoor | string | Runtime hierarchy name |
| RotateHint / Degrees Per Second | (0,45,0) | deg/s | Optional hint rotation; component is ready for later use |

## File structure

```text
Assets/ColorPuzzle/
├── Generated/                 # Generated circular mask texture
├── Materials/                 # Gray base and simple prototype color materials
├── Prefabs/
│   ├── Gameplay/              # Discoverable ball and matching goal
│   ├── Player/                # Player, cameras, carry anchor, HUD, magnifier
│   └── World/                 # Reusable gray block with color proxy child
├── Scenes/                    # Playable vertical-slice scene
├── Scripts/
│   ├── Editor/                # Idempotent scene/prefab builder
│   └── Runtime/               # Movement, reveal, carry, discovery, goal logic
├── CHANGELOG.md               # Agent-readable version history
└── README_GAME.md             # This design and architecture guide
```

The scene uses prefab instances for the player, ball, goal, floor, walls, steps, and platforms. Every revealable prefab pairs a normal grayscale renderer with a collider-free, same-transform `ColorProxy` child. That keeps the prototype modular without committing the future game to a custom render pipeline effect.

Carried objects no longer appear in the camera hierarchy. Pickup suspends physics/collision and hides the world renderers; the bottom HUD becomes the authoritative carried-object display. Dropping restores the mesh, physics, collision, and original interpolation mode. Placement restores the mesh at the goal socket.

## Tuning guide

- For a more intimate search, reduce `Viewport Screen Fraction` to 0.18 and `Discovery Distance` to 8.
- Adjust `Lens Move Sensitivity` without changing player look sensitivity; the two input responses are intentionally independent.
- Compare modes from the top-right selector. Move Lens emphasizes inspection of a frozen composition; Fixed Lens feels closer to ordinary first-person aiming.
- Fixed Lens reuses `FirstPersonPlayer / Mouse Sensitivity`; adjust that one value if camera aiming feels too fast or slow.
- Full Color is now the default developer view. Puzzle View remains the intended discovery loop and is the only view where holding right click activates the lens.
- For faster platforming, raise `Move Speed` to 6–7 and preserve the current jump feel by keeping Jump Height and Gravity together.
- If discovery near the lens edge feels fussy, increase `Discovery Screen Padding`; keep it low so objects outside the visible circle do not reveal early.
- `Render Texture Height` is the main magnifier performance knob. Use 360 on weak hardware or 1080 for a sharper desktop build.

## Verification notes

The original prototype generator is permanently disabled. It must never be used to regenerate this ongoing project because the scene, prefabs, materials, HUD, and cameras now contain manually authored work. The project uses Unity's Input System package already present in the project.

## Desktop build

Run **Color Puzzle → Validate Demo Build (Read Only)** before producing a player build. This command:

- verifies that `ColorPuzzleVerticalSlice.unity` is enabled at build index 0;
- reports an error if the configuration is wrong;
- does not save or modify scenes, prefabs, materials, HUD, cameras, Player Settings, or Build Settings.

Then open **File → Build Profiles**, choose the desired desktop platform, and build. Ordinary Unity player builds read authored assets and do not invoke the disabled prototype generator.
