# Cel material hierarchy

`M_Cel_Master` is the shared style source. Adjust its light bands, rim light, and
outline settings to update every child material that has not overridden that setting.

`M_Cel_Environment_Base` is an organizational child for new ordinary static scene props.
The existing world, shell, and pillar palette materials are also now Material Variants of
`M_Cel_Master`; their only intended override is `_Color`. This means the current prefab and
scene assignments remain in place while inheriting the shared cel style.

The initial environment set preserves the current underwater palette: Aqua, DeepBlue,
KelpGreen, PurpleRock, Sand, CoralPink, CoralOrange, BubbleCyan, and TreasureGold.

## Greyscale type values

These Material Variants affect only the regular greyscale source meshes. The corresponding
colour proxies keep their existing palette materials for the magnifier and Full Color view.

| Material | Value | Used for |
| --- | ---: | --- |
| `M_Cel_Grey_Architecture` | 0.76 | Cubic ruins and world blocks |
| `M_Cel_Grey_Stone` | 0.68 | Rocks and non-imported stone props |
| `M_Cel_Grey_Kelp` | 0.46 | Kelp blades |
| `M_Cel_Grey_Coral` | 0.61 | Coral forms |
| `M_Cel_Grey_Bubble` | 0.88 | Bubble clusters |
| `M_Cel_Grey_Shell` | 0.56 | Reserved variation; not assigned to imported shells |

## Deep-water boundaries

The enclosing walls use a dedicated dark pair rather than the general type variations:

| Material | View | Value / colour |
| --- | --- | --- |
| `M_Cel_Grey_DeepWaterWall` | Greyscale | 0.22 neutral grey |
| `M_Cel_DeepWater` | Magnifier / Full Color | Deep blue `(0.025, 0.12, 0.22)` |

Imported shells and the imported pillar keep their existing source materials so this system
does not alter their appearance or their special proxy behaviour.

Do not use this hierarchy directly for the shells, pillar placement target, UI, outline
mask, or magnifier color proxies until those special render paths have been tested.

## Adjustable master values

| Property | Current value | Purpose |
| --- | ---: | --- |
| Ramp Levels | 3 | Number of discrete light bands; 2-5 is a useful game range. |
| Light Scalar | 1 | Overall direct-light response. |
| High Intensity | 1.35 | Bright-side strength. |
| Low Intensity | 0.72 | Shadow-side strength. |
| Outline Size | 1.25 | Silhouette expansion; use small changes. |
| Fresnel Brightness | 0.08 | Subtle edge light. |
| Fresnel Power | 0.35 | Tightness of that edge light. |
