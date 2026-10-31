# AntiZooka textures

PEAK's AntiZooka textures, kept as a reference for anyone changing the bazooka's look. They are not part of the build or the Thunderstore package; the mod reads the game's own textures at runtime.

| File | What it is |
|---|---|
| `Starboy_tex.png` | The brass atlas. Every brass face (fittings, grips, exhaust wheel) samples its flat gold patch |
| `stars.png`, `star_edge.png` | The tube's animated starfield |
| `A_Texture_23.png`, `A_Texture_60.png`, `A_Texture_77.png` | The item shader's tiling grime layers |
| `AntiZooka.png` | The vanilla hotbar icon; the bazooka's icons are this, recoloured red and white |

How the mod paints the bazooka is in `src/PeakBazooka/BazookaPaint.cs`: red fittings and a white tube are copies of the brass material with a flat `_MainTex`; the grips and wheel keep the original brass material.
