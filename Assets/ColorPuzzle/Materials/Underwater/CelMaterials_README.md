# Cel material hierarchy

`M_Cel_Master` is the shared style source. Adjust its light bands, rim light, and
outline settings to update every child material that has not overridden that setting.

`M_Cel_Environment_Base` is an organizational child for ordinary static scene props.
Create color-specific variants from it, then change only `_Color` (and a texture when
one is required). Assign those variants in a prefab's `Mesh Renderer > Materials` slot.

The initial environment set preserves the current underwater palette: Aqua, DeepBlue,
KelpGreen, PurpleRock, Sand, CoralPink, CoralOrange, BubbleCyan, and TreasureGold.

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
