# Color Puzzle — Vertical Slice Guide

## Game design

Color Puzzle is a casual first-person 3D exploration platformer set in a world that appears monochrome. The player holds **Q** to inspect the world through a circular magnifying-glass view, then moves that lens with the mouse while the regular camera remains fixed. The lens samples an aligned full-screen color render, so geometry keeps the same perspective and screen position across both views. When the red mission ball is covered by the glass, it becomes permanently colorful in the normal view. The player can then carry it to the box with the red mark; placing it there restores the whole box to red and completes the prototype mission.

### Prototype controls

- **WASD** — move
- **Mouse** — look
- **Space** — jump
- **Hold Q + move mouse** — freeze the player/camera and move the color-revealing magnifier
- **Left click** — pick up the centered mission object
- **Right click** — place the carried object on the centered matching goal, or drop it in front of the player
- **Escape** — release the cursor; left click recaptures it

### Play path

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity` and press Play. Find the gray ball, hold Q with the ball centered until it turns red, left-click it, approach the gray box with the red top mark, center the box, and right-click.

## Visual system recipe

### Visual intent

The base view is deliberately sparse and grayscale against a solid white sky. Holding Q adds a movable circle of saturated color (one quarter of the shorter screen dimension), with a warm rim and a solid blue color-view sky. The player and camera stop so discovery reads as deliberate observation. The color camera renders the full screen from the identical pose and projection; the moving mask reveals only the pixels directly beneath it. A discovered mission object remains saturated after the glass closes. Goal completion replaces the pedestal's grayscale surface with its mission color.

### System breakdown

| Stage | Visible role | Inputs | Process | Output | Feeds into |
|---|---|---|---|---|---|
| Base world | Monochrome exploration | Gray materials, main camera mask | Main camera excludes `ColorReveal` layer | Gray scene | Screen base |
| Color proxies | True-color counterpart | Shared mesh transforms, color materials | Child proxy meshes render only on `ColorReveal` | Colored scene geometry | Reveal camera |
| Reveal camera | Perspective-matched color layer | Main camera pose/FOV and screen aspect | Copies pose each frame; renders a full-view color texture | Aspect-correct Color RenderTexture | Circular UI |
| Moving circle mask | Magnifying-glass shape | Mouse delta, RenderTexture, alpha sprite | Moves the mask while counter-moving the full-screen RawImage | Aligned round viewport | Final composite |
| Discovery | Persistent color clue | Lens screen coordinate, distance, aim-dot threshold | Casts through the lens and changes the mission renderer from gray to red | Persistent red object | Main view |
| Completion | Mission payoff | Matching object ID + goal | Snaps item and changes pedestal material | Restored red goal | Main view |

### Formula map

`viewportPixels = min(screenWidth, screenHeight) × viewportScreenFraction`

`lens(t+1) = clamp(lens(t) + mouseDelta × lensMoveSensitivity, screenBounds − lensRadius − padding)`

`revealImageOffset = −lensCanvasOffset`

`discover = intersects(projectedObjectBounds, lensCircle + discoveryScreenPadding) ∧ distance ≤ discoveryDistance ∧ lineOfSight`

The reveal camera uses the main camera's world position, rotation, field of view, and aspect ratio. Its culling mask contains only the `ColorReveal` layer; the main camera excludes that layer. The circular UI alpha-composites the matching screen-space portion of the reveal texture over the base view.

### Layer and execution order

1. Main camera renders grayscale objects (opaque replacement is not used; authored gray materials are cheap and explicit).
2. Reveal camera renders colored proxy meshes into `MagnifierColorRevealRT` while Q is held.
3. Screen-space HUD clips that texture with `MagnifierCircle.png` and draws it over the base view.
4. Crosshair and status text render on top.

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
| MagnifierView / Reveal Key | Q | key | Hold-to-reveal input |
| MagnifierView / Discovery Distance | 12 | meters, ≥0.1 | Maximum discovery ray distance |
| MagnifierView / Discovery Screen Padding | 4 | screen pixels, ≥0 | Small tolerance added around the circular lens for reliable edge overlap |
| MagnifierView / Discovery Layers | Everything | layer mask | Raycastable discovery layers |
| MagnifierView / Viewport Screen Fraction | 0.25 | 0.08–0.5 | Circle diameter relative to shorter screen side |
| MagnifierView / Render Texture Height | 720 | pixels, ≥128 | Full-view reveal sharpness and GPU cost; width follows screen aspect |
| MagnifierView / Regular View Sky Color | white | HDR color | Solid background for the grayscale camera |
| MagnifierView / Reveal View Sky Color | blue `(0.08, 0.34, 0.9)` | HDR color | Solid background visible through the lens |
| MagnifierView / Lens Move Sensitivity | 1 | multiplier, ≥0 | Lens response to mouse movement while Q is held |
| MagnifierView / Screen Edge Padding | 8 | screen pixels, ≥0 | Extra gap between the lens rim and screen edge |
| MagnifierView / Reset Lens To Center On Open | true | toggle | Starts each inspection at screen center |
| PlayerInteraction / Interaction Distance | 4 | meters, ≥0.1 | Pick/place reach |
| PlayerInteraction / Drop Distance | 1.4 | meters, ≥0 | Spawn distance when dropping |
| PlayerInteraction / Drop Impulse | 1.5 | m/s, ≥0 | Forward toss strength |
| PlayerInteraction / Interaction Layers | Everything | layer mask | Pick/place raycast layers |
| PlayerInteraction / Placement Distance | 5.5 | meters, ≥0.1 | Maximum assisted placement reach |
| PlayerInteraction / Placement Cast Radius | 0.75 | meters, ≥0 | Side-approach placement aim forgiveness |
| PlayerInteraction / Top Placement Horizontal Padding | 0.35 | meters, ≥0 | Extra footprint allowance when standing on the goal |
| PlayerInteraction / Top Placement Max Camera Height | 3 | meters, ≥0.1 | Highest camera position accepted above the pedestal top |
| PlayerInteraction / Top Placement Minimum Downward Look | 0.1 | downward Y component, 0–1 | Required downward look when placing from atop the goal |
| DiscoverableObject / Object Id | red-ball | string | Goal matching identity |
| GoalPedestal / Accepted Object Id | red-ball | string | Required carried object |
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

Carried objects do not remain in the camera hierarchy. Their Rigidbody and collider simulation are suspended, then their world pose is matched to `CarryAnchor_BottomRight` during `LateUpdate`. This makes the object follow the final camera pose without physics interpolation lag or player collisions. Dropping restores physics and the original interpolation mode.

## Tuning guide

- For a more intimate search, reduce `Viewport Screen Fraction` to 0.18 and `Discovery Distance` to 8.
- Adjust `Lens Move Sensitivity` without changing player look sensitivity; the two input responses are intentionally independent.
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
