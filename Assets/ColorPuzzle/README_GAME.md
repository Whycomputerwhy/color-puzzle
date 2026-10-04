# Color Puzzle — Vertical Slice Guide

## Game design

Color Puzzle is a casual first-person 3D exploration platformer about restoring color. The underwater prototype now supports two session-persistent puzzle variations in the hidden Enter panel. **Color Search** is the existing Move Lens loop: holding right click while grounded freezes the player/camera and moves a circular color viewport. **Value Search** removes the magnifier entirely and renders the full scene in greyscale; the player identifies the correct object by comparing the intrinsic lightness of a deliberately reduced six-color palette. Pickup stores the object in a neutral bottom inventory HUD, and only a correct placement completes the goal. Full Color remains a Color Search-only developer comparison.

The active underwater shell and pillar visuals use normalized Unity mesh assets generated from the supplied OBJ source exports. Their Y-forward/Z-up export space is converted to Unity's Y-up convention during normalization, while the resulting mesh bounds match the previous visuals so authored scene placement does not move.

### Prototype controls

- **WASD** — move
- **Mouse** — look
- **Space** — jump
- **Hold right click while grounded (Color Search only)** — freeze the player/camera and move the color-revealing magnifier
- **Left click** — pick up the centered mission object, or the mission object directly beneath the player
- **Q** — place the carried object on a matching goal, or drop it in front of the player
- **R** — reset the current scene
- **Enter** — open/close the developer panel; opening releases the cursor and closing recaptures it
- **Color Search / Value Search (developer panel)** — switch between magnifier-based hue discovery and full-screen greyscale value comparison
- **Greyscale / Full Color (developer panel)** — compare the intended monochrome presentation with an always-on color view
- **Back Glow / Center Text (developer panel)** — compare the two 0.75-second interaction-cue variations
- **Escape** — release the cursor manually; click outside UI to recapture it

### Play paths

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity` and press Play. The scene starts in Greyscale with Move Lens. Find the gray ball and hold right click while grounded: the ball appears red only inside the circular lens and returns to gray when the lens closes. Left-click the ball to store it in the bottom inventory HUD. An ordinary drop remains gray in the main view and can be inspected again through the magnifier. Approach the goal with the red top mark, aim at it, and press Q. Placement permanently turns the ball and goal red and reveals the completion door beside the right wall. Press R at any time to restart. Press Enter only when a developer needs to compare Greyscale with Full Color.

Open `Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity` for the active demo level. It starts in Color Search. Press Enter and choose Value Search to replace the lens loop with a full-screen greyscale comparison puzzle. The controls line immediately removes the magnifier instruction, and the top-left square objective box explains the new story/mechanic. The underwater goal is a pillar with one flat, collider-free target image: a red center inside a white perimeter ring. Gravity is reduced only in this scene, from `-22` to `-8 m/s²`, for a floatier platforming feel.

## Visual system recipe

### Visual intent

Color Search uses a sparse monochrome base plus a movable circle of saturated color. Value Search instead renders the same complete color pass through a Rec. 709 luminance filter and remaps reveal-layer materials to six hue families whose approximate values are `0.05, 0.12, 0.22, 0.44, 0.59, 0.80`. This makes hue disappear while preserving large, intentional brightness steps. The red solution and red pillar hint occupy the target band near `0.22`; decoys occupy the other bands. Value Search never activates, draws, or advertises the magnifier. Both modes retain pickup, placement, reset, interaction cues, subtle walking/jump motion, and the same level geometry.

### System breakdown

| Stage | Visible role | Inputs | Process | Output | Feeds into |
|---|---|---|---|---|---|
| Base world | Monochrome exploration | Gray materials, main camera mask | Main camera excludes `ColorReveal` layer | Gray scene | Screen base |
| Color proxies | True-color counterpart | Shared mesh transforms, color materials | Child proxy meshes render only on `ColorReveal` | Colored scene geometry | Reveal camera |
| Reveal camera | Complete perspective-matched color view | Main camera layers + color-proxy layers, pose/FOV, screen aspect | Renders all ordinary objects, then later-queued color proxies | Complete color frame | Circular UI or Full Color display |
| Value palette | Six readable brightness families | Original reveal material name, six Inspector colors | Temporarily clones and retints reveal-layer materials; restores originals on mode exit | Controlled hue/value scene | Greyscale value pass |
| Value greyscale pass | Full-screen value-comparison puzzle | Complete color RenderTexture | Converts RGB to Rec. 709 luminance in a screen-space UI shader | Monochrome frame with intrinsic hue values | HUD composite |
| Moving circle mask | Magnifying-glass shape | Mouse delta, target coordinate, easing response, RenderTexture, alpha sprite | Accumulates a clamped target, eases the visible mask toward it, and counter-moves the full-screen RawImage from the same eased coordinate | Smooth aligned round viewport | Final composite |
| Developer panel | Hidden mechanics/presentation comparison | Enter, puzzle-mode buttons, view buttons, cue buttons | Three grouped selectors choose puzzle mode, developer view, and interaction-cue variation; Full Color is disabled during Value Search | Session-persistent developer comparisons | Final composite |
| Cue dwell | Intentional focus threshold | Exact object aim or assisted goal aim, unscaled time, 0.75-second threshold | Resets when the aimed target/action changes and reveals only after uninterrupted focus | Active contextual cue | Glow or text variation |
| Back-glow cue | Soft, shape-matched collectible emphasis | Collectible renderer, full-resolution mask, stencil, width, softness, fade time | Draws an always-visible silhouette mask, performs horizontal and vertical screen-space expansion/softening, then composites only where stencil differs; after dwell, applies a smoothstep opacity envelope over 0.25 seconds | Black/white soft outline that follows the object, survives occluders, and cannot cover its center | Final composite |
| Center-text cue | Minimal interaction instruction | Current carried state and aimed target | Shows `LC PICKUP`, or `Q PLACE` only when a carried object is aimed at its valid goal; ordinary drops have no cue | Tiny contextual label | HUD composite |
| Flat goal marker | Reads as a painted target, not a raised button | Analytic red/white radial texture, transparent exterior, one quad | Samples one unlit transparent image flush above the pedestal; no collider, depth, shadows, or stacked meshes | Red center with white ring | Placement landmark |
| Placement restoration | Permanent color payoff | Successful matching placement | Calls `Discover()` only after snapping the item to its goal; inspection, pickup, and ordinary drops never call it | Permanently red placed object | Main view |
| Underfoot pickup | Recovery when standing on an item | Player-foot position, downward sphere probe | Uses centered aim first, then selects the nearest unobstructed collider beneath the player | Supporting mission object enters inventory and loses collision | Natural player fall |
| Assisted aim pickup | Reliable small-object targeting | Exact center ray, pickup radius, interaction distance, occluding colliders | Accepts an interactive exact-ray hit first; otherwise tests a small sphere cast and accepts only that query's nearest collider | Forgiving pickup without reaching through walls | Inventory pickup |
| Walk bob | Subtle grounded body motion | Actual planar controller speed, phase, amplitude, smoothing | Advances a speed-scaled sine wave only while grounded and moving; freezes while magnifier movement lock is active | Small vertical camera offset | Camera pivot composite |
| Jump anticipation | Quick pre-jump squat | Space press, depth, anticipation and recovery durations | Holds grounded force during a short smooth downward dip, then launches and eases the camera back to rest | Responsive visual squat followed by normal jump | Camera pivot composite and player velocity |
| Distance-split drop | Simple near/far object release | Center-focus ray, camera-local Z, Drop Distance, surface type, resolved volume overlap | Close horizontal surfaces place immediately; close walls animate from near the view; every downward support probe skips the player and continues to world geometry | Direct horizontal placement or validated animation that cannot rest on the player | Static dropped object |
| Deferred player collision | Prevent drop-induced player displacement | Final object volume, player collider, exit clearance | Enables object/world collision immediately but ignores only the object/player collision pair until their expanded bounds no longer intersect | Static object that cannot shove an overlapping player | Normal solid object after exit |
| Placement target aim | Intentional goal interaction | Camera ray, white-circle center, placement radius, line of sight | Accepts a goal only when the aim line passes within the forgiving radius around its marked top surface | Selected matching goal | Completion |
| Completion | Mission payoff | Matching object ID + goal | Snaps item and changes pedestal material | Restored red goal | Main view |
| Inventory HUD | Minimal neutral carried-object feedback | Carried object's color-neutral display name | Hides the world mesh while carried and shows a small neutral ball glyph plus `Ball`, both outlined in white | `● Ball` | Drop/place decision |
| Door reward | Completion reveal | Successful red-ball placement | Activates a runtime gray/color-proxy door beside the right wall | Newly visible green door | Completion feedback |

### Formula map

`viewportPixels = min(screenWidth, screenHeight) × viewportScreenFraction`

`lensTarget(t+1) = clamp(lensTarget(t) + mouseDelta × lensMoveSensitivity, screenBounds − lensRadius − padding)`

`lensBlend = lensMoveEasing ≤ 0 ? 1 : 1 − exp(−lensMoveEasing × unscaledDeltaTime)`

`lens(t+1) = lerp(lens(t), lensTarget(t+1), lensBlend)`

`finalView = PuzzleView ? grayscaleBase + circularColorReveal : fullScreenColorCamera`

`value = 0.2126R + 0.7152G + 0.0722B`

`ValueSearchView = grayscale(completeColorPass(remapToSixValueBands(colorProxies)))`

`magnifierActive = ColorSearch ∧ rightMouseHeld ∧ playerGrounded ∧ Greyscale ∧ ¬developerPanelOpen`

`revealImageOffset = −lensCanvasOffset`

`persistentRed = hasBeenPlacedOnMatchingGoal`

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

`cueVisible = sameAimTargetFor ≥ interactionCueDwellDuration`

`glowFadeU = clamp(glowFadeElapsed / interactionCueGlowFadeInDuration, 0, 1)`

`glowOpacity = glowFadeU² × (3 − 2 × glowFadeU)`

`horizontalDistance(x,y) = min(|dx| where mask(x+dx,y) > 0)`

`softFalloff(d) = smoothstep(1, 0, d / glowWidthPixels) ^ lerp(2.2, 0.7, glowSoftness)`

`outlineAlpha(x,y) = max_dy(horizontalFalloff(x,y+dy) × verticalFalloff(dy)) × glowOpacity`

`goalMarker(r) = red, r ≤ innerRadius; white, innerRadius < r ≤ outerRadius; transparent, r > outerRadius`

`goalMarkerAlpha = 1 − smoothstep(outerRadius − feather, outerRadius + feather, r)`

The reveal camera uses the main camera's world position, rotation, field of view, and aspect ratio. Its culling mask is the union of the normal-world mask and the original `ColorReveal` mask. Color-proxy materials receive a later runtime render queue so they replace corresponding gray surfaces, while ordinary objects remain visible underneath. The main camera still excludes `ColorReveal`. Showing a proxy through the magnifier is purely compositional and does not change the object's persistent state.

### Layer and execution order

1. Main camera renders grayscale objects (opaque replacement is not used; authored gray materials are cheap and explicit).
2. Reveal camera renders the complete normal world plus later-queued color proxy meshes into `MagnifierColorRevealRT` whenever Color Search needs the lens or Value Search needs its full-screen frame.
3. Color Search moves a clamped lens target from mouse delta, eases the visible circle toward it, and freezes the player/camera only while the grounded lens is held.
4. Value Search temporarily applies the six-color palette, converts the complete RenderTexture to luminance, and draws it full-screen beneath all HUD; its lens object remains inactive.
5. In `LateUpdate`, the camera pivot receives the smoothed walk-bob offset plus any active jump-squat offset; mouse-look rotation remains independent.
6. When dropping, the object renderer moves from the screen-center world point to the collision-safe resting point; its collider remains disabled until the final animation frame.
7. When placing, the camera ray must pass through the forgiving radius around the marked top-surface center; pedestal-body hits outside that region remain ordinary drops.
8. The unlit flat marker renders over the pedestal top without casting/receiving shadows or contributing collision.
9. After the uninterrupted dwell threshold, the selected cue renders: the URP RenderGraph feature draws an exact object mask, expands and softens it at full camera resolution, and composites only outside the stencil while easing to authored opacity over 0.25 seconds. The main camera uses black; the color camera uses white. The alternative contextual center text appears beneath the crosshair.
10. Crosshair, mode-specific controls, square objective box, and bottom inventory HUD render on top; the top-right developer panel is added only while Enter has opened it.

In Full Color, the reveal camera bypasses the RenderTexture and renders directly to the display after matching the main camera pose. Full Color is unavailable in Value Search. Returning to Color Search + Greyscale reconnects the camera to the circular reveal texture and restores every original reveal material.

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
| MagnifierView / Default Puzzle Mode | Color Search | Color Search/Value Search | Fresh-play mechanic variation; R preserves the current session selection |
| MagnifierView / Color Search Objective Text | story copy | string | Top-left objective shown in the magnifier puzzle |
| MagnifierView / Value Search Objective Text | story copy | string | Top-left objective shown in the value-comparison puzzle |
| MagnifierView / Value Dark Blue | `(0.03,0.05,0.12)` | RGBA; luminance ≈0.05 | Darkest Value Search hue family |
| MagnifierView / Value Purple | `(0.28,0.05,0.36)` | RGBA; luminance ≈0.12 | Second-darkest hue family |
| MagnifierView / Value Target Red | `(0.90,0.03,0.03)` | RGBA; luminance ≈0.22 | Solution/hint brightness family |
| MagnifierView / Value Green | `(0.05,0.58,0.18)` | RGBA; luminance ≈0.44 | Middle-light decoy family |
| MagnifierView / Value Orange | `(1.00,0.52,0.06)` | RGBA; luminance ≈0.59 | Light decoy family |
| MagnifierView / Value Cyan | `(0.35,0.92,1.00)` | RGBA; luminance ≈0.80 | Lightest decoy family |
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
| MagnifierView / Show Story Objective | true | toggle | Creates the persistent top-left objective box |
| MagnifierView / Story Card Size | (500, 96) | canvas pixels | Square-corner objective-box bounds |
| MagnifierView / Story Card Margin | (20, 20) | canvas pixels | Offset from the top-left screen corner |
| MagnifierView / Story Card Padding | (22, 14) | canvas pixels | Text inset inside the box |
| MagnifierView / Story Font Size | 19 | points, ≥8 | Objective copy size |
| MagnifierView / Story Card Fill Color | white `(1,1,1,0.94)` | RGBA | Objective-box fill |
| MagnifierView / Story Text Color | navy-black `(0.07,0.10,0.15,1)` | RGBA | Objective text color |
| MagnifierView / Story Card Shadow Color | navy-black `(0.03,0.08,0.14,0.32)` | RGBA | Separation shadow against pale geometry |
| MagnifierView / Story Card Shadow Offset | (3, -4) | canvas pixels | Shadow direction and distance |
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
| PlayerInteraction / Interaction Cue Mode | Back Glow | Back Glow/Center Text | Developer-selected interaction feedback variation |
| PlayerInteraction / Interaction Cue Dwell Duration | 0.75 | seconds, ≥0 | Required uninterrupted aim time before either cue appears |
| PlayerInteraction / Interaction Cue Glow Fade In Duration | 0.25 | seconds, ≥0 | Smoothstep time from transparent to the authored black/white halo opacity after dwell; zero is immediate |
| PlayerInteraction / Interaction Cue Font Size | 14 | points, ≥8 | Size of the contextual center label |
| PlayerInteraction / Interaction Cue Text Size | (220, 28) | canvas pixels | Bounds of the centered cue label |
| PlayerInteraction / Interaction Cue Text Offset | (0, -26) | canvas pixels | Places the label just below the crosshair |
| PlayerInteraction / Interaction Cue Greyscale Text Color | black | RGBA | Text color in Greyscale mode |
| PlayerInteraction / Interaction Cue Color Text Color | white | RGBA | Text color in Full Color mode |
| PlayerInteraction / Interaction Cue Text Outline Color | white `(1,1,1,0.8)` | RGBA | Edge contrast for the black Greyscale label |
| PlayerInteraction / Interaction Cue Glow Width Pixels | 18 | screen pixels, 2–60 | Full-resolution screen-space expansion radius; remains visually consistent across object distance |
| PlayerInteraction / Interaction Cue Glow Softness | 0.9 | blend, 0–1 | Controls the screen-space falloff exponent; higher values retain a broader, softer outer tail |
| PlayerInteraction / Interaction Cue Greyscale Glow Color | black `(0,0,0,0.85)` | RGBA | Strong but softened back-glow color in the base grayscale camera |
| PlayerInteraction / Interaction Cue Color Glow Color | white `(1,1,1,0.86)` | RGBA | Bright inner glow in the color camera and magnifier |
| PlayerInteraction / Show Inventory HUD | true | toggle | Enables the bottom carried-object display |
| PlayerInteraction / Inventory HUD Size | (150, 42) | canvas pixels | Bounds for the ball glyph and label |
| PlayerInteraction / Inventory HUD Offset | (0, 92) | canvas pixels | Position above the bottom-center anchor |
| PlayerInteraction / Inventory HUD Gap | 8 | canvas pixels, ≥0 | Space between the ball glyph and `Ball` label |
| PlayerInteraction / Inventory Ball Font Size | 28 | points, ≥8 | Size of the neutral circular ball glyph |
| PlayerInteraction / Inventory Font Size | 18 | points, ≥8 | Carried-object label size |
| PlayerInteraction / Inventory Fill Color | dark gray `(0.12,0.12,0.12,1)` | RGBA | Interior color shared by the ball glyph and label |
| PlayerInteraction / Inventory Outline Color | white | RGBA | Outline around both HUD elements |
| PlayerInteraction / Inventory Outline Distance | `(1,-1)` | canvas pixels | Thickness/direction of the UI outline effect |
| DiscoverableObject / Object Id | red-ball | string | Goal matching identity |
| DiscoverableObject / Display Name | Ball | string | Color-neutral bottom inventory label |
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

The flat goal image is an authored/generated asset rather than runtime game feel, so its centralized values live at the top of `FlatGoalTargetMigration.cs` instead of on a scene component.

| Flat goal asset parameter | Default | Units / range | Effect |
|---|---:|---|---|
| Texture Size | 256 | pixels | Square source-image resolution |
| Outer Radius | 0.46 | normalized UV radius, 0–0.5 | White ring's outer edge |
| Inner Radius | 0.31 | normalized UV radius, 0–0.5 | Boundary between red center and white ring |
| Edge Feather Pixels | 1.25 | pixels, ≥0 | Antialiasing width at both radial boundaries |
| Target Local Diameter | 0.36 | prefab-local units, >0 | Quad width and depth on the cylinder top |
| Target Local Height | 1.105 | prefab-local Y | Flush offset immediately above the pedestal surface |
| Target Red | RGB `(217,20,20)` | 8-bit color | Center-disc color |
| Target White | RGB `(255,255,255)` | 8-bit color | Perimeter-ring color |

## File structure

```text
Assets/ColorPuzzle/
├── Generated/                 # Generated magnifier mask and flat goal-marker texture
├── Materials/                 # Gray base and simple prototype color materials
│   └── Underwater/            # Flat-color underwater palette and goal-marker material
├── Prefabs/
│   ├── Gameplay/              # Discoverable ball and matching goal
│   ├── Player/                # Player, cameras, carry anchor, HUD, magnifier
│   ├── Underwater/            # Reusable rocks, coral, kelp, bubbles, decoy sphere, and cylinder goal
│   └── World/                 # Reusable gray block with color proxy child
├── Resources/Shaders/         # Runtime-loaded outline mask/dilation shaders and visual shaders
├── Scenes/                    # Original vertical slice and underwater demo
├── Scripts/
│   ├── Editor/                # Validator, renderer-feature installer, and targeted asset migrations
│   └── Runtime/               # Gameplay plus the URP RenderGraph interaction-outline feature
├── ThirdParty/                # Source attribution for adapted open-source reference code
├── CHANGELOG.md               # Agent-readable version history
└── README_GAME.md             # This design and architecture guide
```

The scene uses prefab instances for the player, ball, goal, floor, walls, steps, and platforms. Every revealable prefab pairs a normal grayscale renderer with a collider-free, same-transform `ColorProxy` child. That keeps the prototype modular without committing the future game to a custom render pipeline effect.

The underwater level adds a single `UnderwaterEnvironment` root while leaving inherited objects intact. Its broad terrain pieces remain instances of the existing `WorldBlock` prefab, while repeated detail silhouettes use nested underwater prefabs. Each diversion sphere is an instance of `UnderwaterDecoySphere`: a gray base mesh with a colored reveal proxy, a collider, and deliberately no `DiscoverableObject` or rigidbody, so it cannot be collected. The underwater-only goal prefab preserves the red-ball matching logic while changing its silhouette to a cylinder. Its old stacked red and white cylinders are replaced by one flat transparent TGA on a collider-free quad, so the marking reads as surface graphics rather than a pressable button. The palette is intentionally flat and saturated so this remains a readable colored-greybox rather than premature finished art.

Goal selection uses the top surface directly beneath the placement socket as its target, matching the white circle instead of the pedestal's whole collider. The camera aim line may miss that center by `Placement Cast Radius`, including during the standing-on-top exception, but still needs a clear path to the marked surface.

Interaction cues are created only at runtime. The glow variation applies exclusively to collectible objects; valid placement bases remain eligible for the `Q PLACE` Center Text cue but never receive a halo. A URP renderer feature adapted from Unity Technologies' official Unity 6 RenderGraph outline sample draws the collectible into a full-resolution mask, expands and softens that mask in two screen-space passes, and composites only outside the object's stencil. The mask uses `ZTest Always`, so intervening geometry cannot clip the cue, while the stencil keeps the object surface above the glow. The main camera composites black and the magnifier/Full Color camera composites white. After the 0.75-second dwell, an unscaled-time smoothstep envelope multiplies authored alpha from zero to full strength over 0.25 seconds; changing targets, changing cue modes, losing aim, or opening the developer panel clears it. With empty hands, a collectible produces `LC PICKUP`. While carrying, only a valid matching goal produces `Q PLACE`; looking at an ordinary drop surface produces no hint or glow.

Developer selections are session state rather than game state. Choosing Color Search/Value Search, Greyscale/Full Color, or Back Glow/Center Text updates a small static session record. Pressing R reloads the scene and resets the player, objects, goal, and door, then reapplies all three selections. Beginning a fresh play session clears the record and returns to Color Search, Greyscale, and Back Glow. Value Search forces Greyscale and disables Full Color until Color Search is restored. The developer panel itself always closes on reload.

Carried objects no longer appear in the camera hierarchy. Pickup first checks the exact center ray and immediately accepts an interactive hit. If that ray misses the interactive object—including when it reaches the floor just beside the ball—a small sphere cast around the same aim line supplies forgiving selection for small or edge-on objects. Only the sphere cast's nearest non-player collider can be selected, so a wall or prop caught by that assisted volume still blocks anything behind it. If aim pickup finds nothing interactive, a short downward sphere probe still checks for a mission object directly beneath the player's feet. Pickup suspends that object's physics/collision and hides its world renderers; if it was supporting the player, the existing CharacterController gravity naturally lowers the player to the next surface while the bottom HUD becomes the authoritative carried-object display. Ordinary dropping retains the near/far focus split with three explicit exceptions. A close horizontal surface places immediately at its validated top with no animation. A close wall receives player-side radius clearance but still animates from a very near camera-forward point down to the floor. For a distant or empty-air fallback, the bounds center normally begins on the crosshair at `Drop Distance`; if that start would overlap solid geometry or sit at/below the resolved horizontal resting center, animation is skipped and the object appears directly on the resolved top surface instead of floating upward out of a volume. Every downward support probe checks all hits, ignores the carried object and the entire player hierarchy, and continues through the player to the nearest real world surface. A skyward drop therefore cannot select the CharacterController's head as a floor or freeze above the player. Every path rests the collider above the local floor and runs bounded penetration-resolution passes that push the object horizontally out of adjacent geometry. The final position must be free of non-player blocking overlaps; otherwise the object remains safely in inventory. On a successful drop, collision with the world activates immediately, but collision with the player's CharacterController is temporarily ignored when their volumes overlap. The object cannot push the player away, and that one collision pair is restored only after the player has fully left the object's expanded volume. Placement restores the mesh at the goal socket in world space without parenting it beneath the scaled pedestal, preserving the object's original proportions. Velocity is cleared only while a Rigidbody is dynamic, avoiding Unity's unsupported kinematic-velocity warnings during later interactions.

## Tuning guide

- For a more intimate search, reduce `Viewport Screen Fraction` to 0.18.
- Adjust `Lens Move Sensitivity` without changing player look sensitivity; the two input responses are intentionally independent.
- `Lens Move Easing` defaults to a subtle `18`. Use roughly `10–14` for a floatier follow or `25+` for a crisper response; set it to zero for immediate movement.
- Keep Greyscale for normal play. Press Enter to expose the developer panel when comparing either view or interaction-cue variations.
- Use Value Search to assess the brightness puzzle without any magnifier input. Tune the six palette colors by their Rec. 709 luminance, preserving roughly 0.08 or more between adjacent bands after lighting.
- Full Color is developer-only; selecting Greyscale restores the intended puzzle loop and right-click lens behavior.
- `Interaction Cue Dwell Duration` is the shared timing control for both variations. Lower it toward `0.4` for faster feedback or raise it toward `1.0` for more deliberate focus. Back Glow applies only to collectible objects; use Center Text to inspect the valid-goal `Q PLACE` cue.
- `Interaction Cue Glow Fade In Duration` defaults to `0.25` seconds. Raise it toward `0.4` for a gentler bloom, lower it toward `0.1` for a snappier cue, or set it to zero for an immediate halo.
- Increase `Interaction Cue Glow Width Pixels` for a broader halo. Raise `Glow Softness` for a gentler screen-space falloff, or lower it for a tighter graphic outline. The stencil mask always protects the exact object interior.
- Increase `Pickup Below Probe Radius` slightly if future collectible shapes are wider than the player capsule; keep the distance short so pickup cannot reach through a floor.
- Increase `Pickup Aim Radius` slightly if future interactive objects are smaller; keep it around `0.1–0.25` m so selection remains intentional. The assisted query's nearest collider remains authoritative, preserving occlusion.
- For faster platforming, raise `Move Speed` to 6–7 and preserve the current jump feel by keeping Jump Height and Gravity together.
- Keep `Walk Bob Amplitude` below roughly `0.04` m for a restrained first-person feel. Frequency follows actual speed automatically; adjust `Walk Bob Frequency` for cadence and `Walk Bob Smoothing` for softness.
- Keep `Jump Squat Duration` short because it also delays physical launch. Increase `Jump Squat Depth` and recovery together for a more animated jump, or disable all camera motion with `Enable View Motion` while retaining the normal movement controls.
- `Render Texture Height` is the main magnifier performance knob. Use 360 on weak hardware or 1080 for a sharper desktop build.
- Increase `Drop Ground Probe Distance` only if future levels contain drops taller than 20 meters. Increase `Drop Surface Clearance` slightly if a new object shape visually clips into floors.
- `Drop Distance` is the single near/far threshold and camera-local-Z depth: a focused surface inside it places immediately, while a farther or missing surface starts its animation exactly on the center crosshair at that depth.
- `Placement Cast Radius` controls the aiming room around the white circle. Lower it toward `0.45` for precision or raise it toward `1.0` for easier placement; the target remains the marked top surface rather than the pedestal body.
- To adjust the target graphic itself, edit the centralized `FlatGoalTargetMigration` constants and rerun `Color Puzzle > Migrations > Convert Underwater Goal Target To Flat Image`. The migration is idempotent and touches only the underwater goal prefab plus its generated texture/material.
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
