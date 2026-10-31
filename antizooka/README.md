# AntiZooka reference

Reference material for changing the bazooka's look. Nothing here is built into the mod.

| Path | What it is |
|---|---|
| `vanilla/textures/` | The AntiZooka's textures from PEAK. `Starboy_tex.png` is the brass atlas (every brass face samples its flat gold patch); `stars.png` / `star_edge.png` are the tube's starfield; `A_Texture_*.png` are the shader's tiling grime layers; `AntiZooka.png` is the vanilla hotbar icon |
| `vanilla/materials/` | The two materials' shader settings (`M_Tarnished_Gold_Antizooka`, `M_Scout_Star_Antizooka`) and `layout.json`, which maps submeshes to materials and texture slots |
| `vanilla/meshes/Anti-Zooka.obj` / `.mtl` / `.glb` | The AntiZooka mesh with its UVs. Unity's X axis is flipped in the export |
| `vanilla/_export.py` | Re-exports the above from the game files with [UnityPy](https://github.com/K0lb3/UnityPy) (it also writes a collision mesh, vertex colours and sounds that are not kept here) |
| `concepts/` | Concept art for the bazooka |

How the mod paints the bazooka (`src/PeakBazooka/BazookaPaint.cs`):

- The mesh is split by piece: the tube (the star material's submesh), the grips (brass pieces reaching below Z −1.2) and the spoked exhaust wheel (the widest brass piece). Every other brass piece is a fitting.
- Fittings are red and the tube white: copies of the brass material with a flat `_MainTex`. The grips and wheel keep the original brass material.
- The tube's UV U runs along its length and V halfway around it (mirrored), from 0.503 underneath to 0.602 on top. A texture that varies along U draws rings around the tube.
