# Color Puzzle — Vertical Slice Guide

## Game design

Color Puzzle is a casual first-person 3D exploration platformer about restoring color. The prototype starts in **Greyscale** and uses **Move Lens** as its sole player-facing magnifier interaction: holding right click while grounded freezes the player/camera and moves the circular color viewport with the mouse. Magnifier inspection reveals the red ball only inside the lens; pickup is the commitment point that makes the ball permanently red in the grayscale world after a later drop or placement. The player stores the ball in a bottom inventory HUD and presses Q to place it on the matching red-marked goal. Completing the match restores the goal and reveals a green door on the map's right side. Full Color remains available as a developer comparison view inside the hidden Enter panel.

### Prototype controls

- **WASD** — move
- **Mouse** — look
- **Space** — jump
- **Hold right click while grounded (Move Lens)** — freeze the player/camera and move the color-revealing magnifier
- **Left click** — pick up the centered mission object, or the mission object directly beneath the player
- **Q** — place the carried object on a matching goal, or drop it in front of the player
- **R** — reset the current scene
- **Enter** — open/close the developer panel; opening releases the cursor and closing recaptures it
- **Greyscale / Full Color (developer panel)** — compare the intended monochrome presentation with an always-on color view
- **Escape** — release the cursor manually; click outside UI to recapture it

### Play paths

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity` and press Play. The scene starts in Greyscale with Move Lens. Find the gray ball and hold right click while grounded: the ball appears red only inside the circular lens and returns to gray when the lens closes. Left-click the ball to commit its color and store it in the bottom inventory HUD. If dropped afterward it remains red in the grayscale world. Approach the goal with the red top mark, aim at it, and press Q. The goal becomes red and the completion door appears beside the right wall. Press R at any time to restart. Press Enter only when a developer needs to compare Greyscale with Full Color.

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity` for the second demo level. It preserves the same player, HUD, Move Lens interaction, red-ball mission, goal, and completion door, then expands the playable greybox into a colored underwater basin. Layered sand shelves, trenches, ruins, arches, rocks, coral, kelp, bubble columns, treasure-colored markers, and fourteen non-pickable colored spheres create denser exploration landmarks and visual diversions without introducing detailed art or swimming controls. The underwater goal is a cylinder with a circular red placement target and a white perimeter. Gravity is reduced only in this scene, from `-22` to `-8 m/s²`, for a floatier platforming feel.

## Visual system recipe

### Visual intent

Greyscale is deliberately sparse and monochrome against a solid white sky. Holding right click while grounded adds a movable circle of saturated color, with a warm rim and blue color-view sky. The color pass renders the complete normal world first and draws color proxies afterward, so objects without proxies never disappear. Move Lens freezes the player and camera while the circle moves. Full Color routes the same complete color pass directly to the screen, but only when selected from the hidden developer panel. Lens inspection is temporary and does not mutate object state; successful pickup commits the mission object's red material for subsequent drops or placement. Grounded movement adds a very small vertical camera bob, and jumping begins with a quick downward anticipation dip before launch. A floor drop briefly reintroduces the object at the screen center, accelerates it toward its prevalidated resting point, and enables its static collision only on arrival. Goal completion restores the pedestal and reveals the right-side door.

### System breakdown

| Stage | Visible role | Inputs | Process | Output | Feeds into |
|---|---|---|---|---|---|
| Base world | Monochrome exploration | Gray materials, main camera mask | Main camera excludes `ColorReveal` layer | Gray scene | Screen base |
| Color proxies | True-color counterpart | Shared mesh transforms, color materials | Child proxy meshes render only on `ColorReveal` | Colored scene geometry | Reveal camera |
| Reveal camera | Complete perspective-matched color view | Main camera layers + color-proxy layers, pose/FOV, screen aspect | Renders all ordinary objects, then later-queued color proxies | Complete color frame | Circular UI or Full Color display |
| Moving circle mask | Magnifying-glass shape | Mouse delta, target coordinate, easing response, RenderTexture, alpha sprite | Accumulates a clamped target, eases the visible mask toward it, and counter-moves the full-screen RawImage from the same eased coordinate | Smooth aligned round viewport | Final composite |
| Developer panel | Hidden presentation comparison | Enter, Greyscale / Full Color buttons, reveal camera | Enter shows or hides the panel and releases or recaptures the cursor; buttons route color output to the lens or full screen | Optional developer-only color comparison | Final composite |
| Pickup persistence | Permanent color clue | Successful pickup | Calls `Discover()` as the item enters inventory; lens inspection never calls it | Red object after later drop or placement | Main view |
| Underfoot pickup | Recovery when standing on an item | Player-foot position, downward sphere probe | Uses centered aim first, then selects the nearest unobstructed collider beneath the player | Supporting mission object enters inventory and loses collision | Natural player fall |
| Assisted aim pickup | Reliable small-object targeting | Exact center ray, pickup radius, interaction distance, occluding colliders | Accepts an interactive exact-ray hit first; otherwise tests a small sphere cast and accepts only that query's nearest collider | Forgiving pickup without reaching through walls | Inventory pickup |
| Walk bob | Subtle grounded body motion | Actual planar controller speed, phase, amplitude, smoothing | Advances a speed-scaled sine wave only while grounded and moving; freezes while magnifier movement lock is active | Small vertical camera offset | Camera pivot composite |
| Jump anticipation | Quick pre-jump squat | Space press, depth, anticipation and recovery durations | Holds grounded force during a short smooth downward dip, then launches and eases the camera back to rest | Responsive visual squat followed by normal jump | Camera pivot composite and player velocity |
| Distance-split drop | Simple near/far object release | Center-focus ray, camera-local Z, Drop Distance, surface type, resolved volume overlap | Close horizontal surfaces place immediately; close walls animate from near the view; every downward support probe skips the player and continues to world geometry | Direct horizontal placement or validated animation that cannot rest on the player | Static dropped object |
| Deferred player collision | Prevent drop-induced player displacement | Final object volume, player collider, exit clearance | Enables object/world collision immediately but ignores only the object/player collision pair until their expanded bounds no longer intersect | Static object that cannot shove an overlapping player | Normal solid object after exit |
| Placement target aim | Intentional goal interaction | Camera ray, white-circle center, placement radius, line of sight | Accepts a goal only when the aim line passes within the forgiving radius around its marked top surface | Selected matching goal | Completion |
| Completion | Mission payoff | Matching object ID + goal | Snaps item and changes pedestal material | Restored red goal | Main view |
| Inventory HUD | Carried-object feedback | Carried object's display name and color | Hides the world mesh while carried and shows a bottom swatch/label | `CARRYING: Red Ball` | Drop/place decision |
| Door reward | Completion reveal | Successful red-ball placement | Activates a runtime gray/color-proxy door beside the right wall | Newly visible green door | Completion feedback |

### Formula map

`viewportPixels = min(screenWidth, screenHeight) × viewportScreenFraction`

`lensTarget(t+1) = clamp(lensTarget(t) + mouseDelta × lensMoveSensitivity, screenBounds − lensRadius − padding)`

`lensBlend = lensMoveEasing ≤ 0 ? 1 : 1 − exp(−lensMoveEasing × unscaledDeltaTime)`

`lens(t+1) = lerp(lens(t), lensTarget(t+1), lensBlend)`

`finalView = PuzzleView ? grayscaleBase + circularColorReveal : fullScreenColorCamera`

`magnifierActive = rightMouseHeld ∧ playerGrounded ∧ Greyscale ∧ ¬developerPanelOpen`

`revealImageOffset = −lensCanvasOffset`

`persistentRed = hasBeenPickedUp ∨ hasBeenPlaced`

`underfootPickup = nearestNonPlayerHit(sphereCast(playerFeet + up × startHeight, down, radius, distance))`

`closeFocusedSurface = focusHitExists ∧ focusHitDistance ≤ dropDistance`

`focusCenter = cameraPosition + cameraForward × dropDistance`

`dropPosition(t) = closeFocusedSurface ? safeFocusIntersection : lerp(focusCenter, safeRestingPosition, clamp(elapsed / duration, 0, 1)^fallExponent)`

`skipAnimation = focusStartInsideSolid ∨ focusStartCenterY ≤ restingCenterY + collisionSkin`

`playerCollisionEnabled = ¬ intersects(expand(objectBounds, playerExitClearance), playerBounds)`

`forwardSpeed = max(0, dot(playerPlanarVelocity, playerForward))`

`movementLead = playerForward × min(forwardSpeed × leadTime, maximumOffset)`

`wallMovementLead = projectOnPlane(movementLead, wallNormal)`

`walkBobY = sin(2π × phase) × amplitude, phase += deltaTime × frequency × (planarSpeed / moveSpeed)`

`jumpSquatY = -depth × smoothstep(0, 1, anticipationElapsed / anticipationDuration)`

`wallSideCenterXZ = hitPointXZ + normalize(hitNormalXZ) × (objectRadius + obstacleClearance)`

`placementAimDistance = length(placementTarget − closestPoint(cameraRay, placementTarget))`

`placementAccepted = placementAimDistance ≤ placementCastRadius ∧ targetForwardDistance ≤ placementDistance ∧ lineOfSightClear`

The reveal camera uses the main camera's world position, rotation, field of view, and aspect ratio. Its culling mask is the union of the normal-world mask and the original `ColorReveal` mask. Color-proxy materials receive a later runtime render queue so they replace corresponding gray surfaces, while ordinary objects remain visible underneath. The main camera still excludes `ColorReveal`. Showing a proxy through the magnifier is purely compositional and does not change the object's persistent state.

### Layer and execution order

1. Main camera renders grayscale objects (opaque replacement is not used; authored gray materials are cheap and explicit).
2. Reveal camera renders the complete normal world plus later-queued color proxy meshes into `MagnifierColorRevealRT` while right click is held on the ground.
3. Mouse delta moves a clamped target while the displayed lens eases toward it; the camera/player remain frozen.
4. Screen-space HUD clips that texture with `MagnifierCircle.png` and draws it over the base view.
5. In `LateUpdate`, the camera pivot receives the smoothed walk-bob offset plus any active jump-squat offset; mouse-look rotation remains independent.
6. When dropping, the object renderer moves from the screen-center world point to the collision-safe resting point; its collider remains disabled until the final animation frame.
7. When placing, the camera ray must pass through the forgiving radius around the marked top-surface center; pedestal-body hits outside that region remain ordinary drops.
8. Crosshair, controls, and bottom inventory HUD render on top; the top-right developer panel is added only while Enter has opened it.

In Full Color, the reveal camera bypasses the RenderTexture and renders directly to the display after matching the main camera pose. Returning to Greyscale reconnects the same camera to the circular reveal texture.

## Inspector variable inventory

All game-feel variables are serialized and editable in the Unity Inspector.

| Component / variable | Default | Units / range | Effect |
|---|---:|---|---|
| FirstPersonPlayer / Move Speed | 5 | m/s, ≥0 | Ground movement speed |
| FirstPersonPlayer / Air Control | 0.55 | multiplier, ≥0 | Horizontal authority while airborne |
| FirstPersonPlayer / Jump Height | 1.35 | meters, ≥0 | Jump apex |
| FirstPersonPlayer / Gravity | -22 | m/s² | Fall and jump timing |
| Underwater scene / FirstPersonPlayer / Gravity | -8 | m/s² | Scene-only override for slower underwater falls; the original player prefab remains `-22` |
| FirstPersonPlayer / Grounded Force | -2 | m/s | Keeps the controller grounded |
| FirstPersonPlayer / Mouse Sensitivity | 0.13 | multiplier, ≥0 | Look response |
| FirstPersonPlayer / Vertical Look Limit | 85 | degrees, 1–89 | Pitch clamp |
| FirstPersonPlayer / Lock Cursor On Start | true | toggle | FPS cursor capture |
| FirstPersonPlayer / Enable View Motion | true | toggle | Enables the walking bob and jump-squat camera offsets |
| FirstPersonPlayer / Walk Bob Amplitude | 0.025 | meters, ≥0 | Maximum vertical camera displacement while walking |
| FirstPersonPlayer / Walk Bob Frequency | 1.8 | cycles/s at Move Speed, ≥0 | Base rhythm of the walking up-down pattern |
| FirstPersonPlayer / Walk Bob Minimum Speed | 0.15 | m/s, ≥0 | Prevents idle/collision jitter from activating the bob |
| FirstPersonPlayer / Walk Bob Smoothing | 14 | response per second, ≥0 | Softens the transition into and out of walking motion; zero snaps immediately |
| FirstPersonPlayer / Jump Squat Depth | 0.055 | meters, ≥0 | Downward camera anticipation before launch |
| FirstPersonPlayer / Jump Squat Duration | 0.07 | seconds, ≥0 | Brief grounded delay used to perform the pre-jump dip |
| FirstPersonPlayer / Jump Squat Recovery Duration | 0.12 | seconds, ≥0 | Time for the camera to return from the dip after launch |
| MagnifierView / Reveal Mouse Button | Right | Right/Middle | Hold-to-reveal input; activation also requires grounded state |
| MagnifierView / Viewport Screen Fraction | 0.25 | 0.08–0.5 | Circle diameter relative to shorter screen side |
| MagnifierView / Render Texture Height | 720 | pixels, ≥128 | Full-view reveal sharpness and GPU cost; width follows screen aspect |
| MagnifierView / Regular View Sky Color | white | HDR color | Solid background for the grayscale camera |
| MagnifierView / Reveal View Sky Color | blue `(0.08, 0.34, 0.9)` | HDR color | Solid background visible through the lens |
| MagnifierView / Lens Move Sensitivity | 1 | multiplier, ≥0 | Lens response to mouse movement while magnifying |
| MagnifierView / Lens Move Easing | 18 | response per second, ≥0 | Exponential follow speed for subtle frame-rate-independent lens smoothing; zero snaps immediately |
| MagnifierView / Screen Edge Padding | 8 | screen pixels, ≥0 | Extra gap between the lens rim and screen edge |
| MagnifierView / Reset Lens To Center On Open | true | toggle | Starts each inspection at screen center |
| MagnifierView / Enable Developer Panel | true | toggle | Allows Enter to create and toggle the hidden comparison panel |
| MagnifierView / Developer Panel Toggle Key | Enter | key | Opens/closes the developer panel |
| MagnifierView / Unlock Cursor While Panel Open | true | toggle | Releases the cursor on open and recaptures it on close |
| MagnifierView / Mode Button Size | (140, 36) | canvas pixels | Width and height of each mode button |
| MagnifierView / Mode Button Gap | 8 | canvas pixels, ≥0 | Horizontal spacing between the two buttons |
| MagnifierView / Selector Group Padding | 8 | canvas pixels, ≥0 | Interior spacing around the developer group |
| MagnifierView / Selector Group Label Height | 20 | canvas pixels, ≥8 | Reserved height for the developer group title |
| MagnifierView / Mode Selector Margin | (20, 20) | canvas pixels | Developer panel offset from the top-right screen corner |
| MagnifierView / Mode Button Font Size | 16 | points, ≥8 | Developer button label size |
| MagnifierView / Selector Group Label Font Size | 13 | points, ≥8 | Developer group-title label size |
| MagnifierView / Selected Mode Color | blue `(0.12, 0.42, 0.92, 0.95)` | RGBA | Active-button background |
| MagnifierView / Unselected Mode Color | charcoal `(0.12, 0.12, 0.12, 0.82)` | RGBA | Inactive-button background |
| MagnifierView / Mode Button Text Color | white | RGBA | Selector label color |
| MagnifierView / Selector Group Label Color | white | RGBA | Group-title text color |
| MagnifierView / Selector Group Background Color | black `(0, 0, 0, 0.55)` | RGBA | Translucent developer-panel backing |
| PlayerInteraction / Interaction Distance | 4 | meters, ≥0.1 | Pick/place reach |
| PlayerInteraction / Pickup Aim Radius | 0.16 | meters, ≥0 | Small world-space radius around an otherwise empty center ray; zero restores exact-ray-only pickup |
| PlayerInteraction / Pickup Below Probe Radius | 0.3 | meters, ≥0 | Horizontal forgiveness for picking up an object beneath the player's feet |
| PlayerInteraction / Pickup Below Probe Start Height | 0.2 | meters, ≥0 | Raises the downward probe origin enough to include the current supporting object |
| PlayerInteraction / Pickup Below Probe Distance | 0.8 | meters, ≥0.1 | Maximum downward distance for underfoot pickup |
| PlayerInteraction / Drop Or Place Key | Q | key | Places on a valid goal or drops the carried object |
| PlayerInteraction / Reset Key | R | key | Reloads the active scene to its initial state |
| PlayerInteraction / Drop Distance | 1.4 | meters, ≥0 | Forward distance used to search for a resting surface |
| PlayerInteraction / Drop Ground Probe Height | 1.5 | meters, ≥0 | Height above the candidate position where the downward ground probe begins |
| PlayerInteraction / Drop Ground Probe Distance | 20 | meters, ≥0.1 | Maximum downward search distance for a safe drop surface |
| PlayerInteraction / Drop Surface Clearance | 0.02 | meters, ≥0 | Small gap between the object collider and its resting surface |
| PlayerInteraction / Drop Obstacle Clearance | 0.04 | meters, ≥0 | Gap left between a dropped object and a wall or prop it leans against |
| PlayerInteraction / Drop Collision Skin | 0.01 | meters, ≥0 | Tolerance for final overlap validation and horizontal wall probing |
| PlayerInteraction / Drop Collision Resolution Iterations | 4 | passes, 1–8 | Maximum horizontal push-out passes used to resolve intersecting geometry |
| PlayerInteraction / Drop Wall Normal Maximum Y | 0.65 | normalized Y, 0–1 | Distinguishes mostly vertical blockers from walkable/supporting surfaces |
| PlayerInteraction / Drop Movement Lead Time | 0.12 | seconds, ≥0 | Converts the player's forward velocity component into matching drop displacement |
| PlayerInteraction / Drop Movement Maximum Offset | 0.65 | meters, ≥0 | Caps how far forward movement can shift the drop destination |
| PlayerInteraction / Drop Movement Minimum Speed | 0.15 | m/s, ≥0 | Minimum forward speed required; strafing and backward motion never activate the lead |
| PlayerInteraction / Drop Animation Duration | 0.22 | seconds, ≥0 | Time from screen center to the final grounded position; zero completes immediately |
| PlayerInteraction / Drop Animation Fall Exponent | 2.2 | exponent, 1–4 | Acceleration curve; 1 is linear and larger values produce a faster late fall |
| PlayerInteraction / Wall Drop Animation Start Distance | 0.35 | meters from camera, ≥0.05 | Starts close-wall drops very near the player view before they descend to the floor |
| PlayerInteraction / Interaction Layers | Everything | layer mask | Pick/place raycast layers |
| PlayerInteraction / Placement Distance | 5.5 | camera-forward meters, ≥0.1 | Maximum forward reach to the white-circle placement target |
| PlayerInteraction / Placement Cast Radius | 0.75 | world-space meters, ≥0 | Forgiving radius around the white-circle center that the camera aim line may pass through |
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
| DiscoverableObject / Player Exit Clearance | 0.03 | meters, ≥0 | Extra volume around a dropped object that the player must fully leave before object/player collision is restored |
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
│   └── Underwater/            # Nine flat-color underwater palette materials
├── Prefabs/
│   ├── Gameplay/              # Discoverable ball and matching goal
│   ├── Player/                # Player, cameras, carry anchor, HUD, magnifier
│   ├── Underwater/            # Reusable rocks, coral, kelp, bubbles, decoy sphere, and cylinder goal
│   └── World/                 # Reusable gray block with color proxy child
├── Scenes/                    # Original vertical slice and underwater demo
├── Scripts/
│   ├── Editor/                # Read-only validator and one-shot underwater level creator
│   └── Runtime/               # Movement, reveal, carry, pickup persistence, and goal logic
├── CHANGELOG.md               # Agent-readable version history
└── README_GAME.md             # This design and architecture guide
```

The scene uses prefab instances for the player, ball, goal, floor, walls, steps, and platforms. Every revealable prefab pairs a normal grayscale renderer with a collider-free, same-transform `ColorProxy` child. That keeps the prototype modular without committing the future game to a custom render pipeline effect.

The underwater level adds a single `UnderwaterEnvironment` root while leaving inherited objects intact. Its broad terrain pieces remain instances of the existing `WorldBlock` prefab, while repeated detail silhouettes use nested underwater prefabs. Each diversion sphere is an instance of `UnderwaterDecoySphere`: a gray base mesh with a colored reveal proxy, a collider, and deliberately no `DiscoverableObject` or rigidbody, so it cannot be collected. The underwater-only goal prefab preserves the red-ball matching logic while changing its silhouette to a cylinder and layering a white disc beneath the smaller red target to form the outline. The palette is intentionally flat and saturated so this remains a readable colored-greybox rather than premature finished art.

Goal selection uses the top surface directly beneath the placement socket as its target, matching the white circle instead of the pedestal's whole collider. The camera aim line may miss that center by `Placement Cast Radius`, including during the standing-on-top exception, but still needs a clear path to the marked surface.

Carried objects no longer appear in the camera hierarchy. Pickup first checks the exact center ray and immediately accepts an interactive hit. If that ray misses the interactive object—including when it reaches the floor just beside the ball—a small sphere cast around the same aim line supplies forgiving selection for small or edge-on objects. Only the sphere cast's nearest non-player collider can be selected, so a wall or prop caught by that assisted volume still blocks anything behind it. If aim pickup finds nothing interactive, a short downward sphere probe still checks for a mission object directly beneath the player's feet. Pickup suspends that object's physics/collision and hides its world renderers; if it was supporting the player, the existing CharacterController gravity naturally lowers the player to the next surface while the bottom HUD becomes the authoritative carried-object display. Ordinary dropping retains the near/far focus split with three explicit exceptions. A close horizontal surface places immediately at its validated top with no animation. A close wall receives player-side radius clearance but still animates from a very near camera-forward point down to the floor. For a distant or empty-air fallback, the bounds center normally begins on the crosshair at `Drop Distance`; if that start would overlap solid geometry or sit at/below the resolved horizontal resting center, animation is skipped and the object appears directly on the resolved top surface instead of floating upward out of a volume. Every downward support probe checks all hits, ignores the carried object and the entire player hierarchy, and continues through the player to the nearest real world surface. A skyward drop therefore cannot select the CharacterController's head as a floor or freeze above the player. Every path rests the collider above the local floor and runs bounded penetration-resolution passes that push the object horizontally out of adjacent geometry. The final position must be free of non-player blocking overlaps; otherwise the object remains safely in inventory. On a successful drop, collision with the world activates immediately, but collision with the player's CharacterController is temporarily ignored when their volumes overlap. The object cannot push the player away, and that one collision pair is restored only after the player has fully left the object's expanded volume. Placement restores the mesh at the goal socket in world space without parenting it beneath the scaled pedestal, preserving the object's original proportions. Velocity is cleared only while a Rigidbody is dynamic, avoiding Unity's unsupported kinematic-velocity warnings during later interactions.

## Tuning guide

- For a more intimate search, reduce `Viewport Screen Fraction` to 0.18.
- Adjust `Lens Move Sensitivity` without changing player look sensitivity; the two input responses are intentionally independent.
- `Lens Move Easing` defaults to a subtle `18`. Use roughly `10–14` for a floatier follow or `25+` for a crisper response; set it to zero for immediate movement.
- Keep Greyscale for normal play. Press Enter to expose the developer panel only when a full-color comparison is needed.
- Full Color is developer-only; selecting Greyscale restores the intended puzzle loop and right-click lens behavior.
- Increase `Pickup Below Probe Radius` slightly if future collectible shapes are wider than the player capsule; keep the distance short so pickup cannot reach through a floor.
- Increase `Pickup Aim Radius` slightly if future interactive objects are smaller; keep it around `0.1–0.25` m so selection remains intentional. The assisted query's nearest collider remains authoritative, preserving occlusion.
- For faster platforming, raise `Move Speed` to 6–7 and preserve the current jump feel by keeping Jump Height and Gravity together.
- Keep `Walk Bob Amplitude` below roughly `0.04` m for a restrained first-person feel. Frequency follows actual speed automatically; adjust `Walk Bob Frequency` for cadence and `Walk Bob Smoothing` for softness.
- Keep `Jump Squat Duration` short because it also delays physical launch. Increase `Jump Squat Depth` and recovery together for a more animated jump, or disable all camera motion with `Enable View Motion` while retaining the normal movement controls.
- `Render Texture Height` is the main magnifier performance knob. Use 360 on weak hardware or 1080 for a sharper desktop build.
- Increase `Drop Ground Probe Distance` only if future levels contain drops taller than 20 meters. Increase `Drop Surface Clearance` slightly if a new object shape visually clips into floors.
- `Drop Distance` is the single near/far threshold and camera-local-Z depth: a focused surface inside it places immediately, while a farther or missing surface starts its animation exactly on the center crosshair at that depth.
- `Placement Cast Radius` controls the aiming room around the white circle. Lower it toward `0.45` for precision or raise it toward `1.0` for easier placement; the target remains the marked top surface rather than the pedestal body.
- `Wall Drop Animation Start Distance` controls how near the camera a close-wall release appears before descending. Keep it around `0.25–0.5` m for a quick handoff feel.
- Raise `Drop Obstacle Clearance` if dropped objects look too close to walls. Increase `Drop Collision Resolution Iterations` only for unusually crowded geometry; four passes is the normal performance/readability balance.
- Increase `Drop Movement Lead Time` for stronger forward matching. Keep `Drop Movement Maximum Offset` modest so fast forward movement cannot make a drop feel thrown; raise `Drop Movement Minimum Speed` if slow forward motion should remain visually stationary. Strafing and backward movement intentionally ignore these settings.
- Lower `Drop Wall Normal Maximum Y` if sloped surfaces should count as floors more often; raise it if steeper surfaces should trigger player-side wall leaning.
- Shorten `Drop Animation Duration` for a snappier distant release or increase `Drop Animation Fall Exponent` for a stronger gravity-like finish. The animation height now comes from camera pitch and the grounded surface beneath the crosshair-aligned focus point; these settings do not affect immediate close placement.
- Increase `Player Exit Clearance` if collision returns while the player still visually touches a dropped object. Larger values delay only player collision restoration; the object remains solid to the rest of the world.
- In the underwater scene, tune the scene instance's `FirstPersonPlayer / Gravity` between roughly `-6` and `-12`; this changes only the second level and does not modify the shared player prefab.

## Verification notes

The original prototype generator is permanently disabled. It must never be used to regenerate this ongoing project because the scene, prefabs, materials, HUD, and cameras now contain manually authored work. The project uses Unity's Input System package already present in the project.

The underwater creator at `Assets/ColorPuzzle/Scripts/Editor/UnderwaterDemoLevelBuilder.cs` is intentionally safe and one-shot: it copies the authored first scene, adds only new underwater assets and scene content, and refuses to run if `ColorPuzzleUnderwaterDemo.unity` already exists. Do not delete and rerun it as an update workflow; edit the underwater scene normally in Unity from this point onward.

## Desktop build

Run **Color Puzzle → Validate Demo Build (Read Only)** before producing a player build. This command:

- verifies that `ColorPuzzleUnderwaterDemo.unity` is enabled at startup index 0;
- verifies that the original `ColorPuzzleVerticalSlice.unity` remains enabled at index 1;
- reports an error if the configuration is wrong;
- does not save or modify scenes, prefabs, materials, HUD, cameras, Player Settings, or Build Settings.

Then open **File → Build Profiles**, choose the desired desktop platform, and build. Ordinary Unity player builds read authored assets and do not invoke the disabled prototype generator.

`ColorPuzzleUnderwaterDemo.unity` is the player-build startup scene at index 0. The original vertical slice remains included and enabled at index 1.
