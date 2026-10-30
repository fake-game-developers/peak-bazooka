from __future__ import annotations

import json
from pathlib import Path

import UnityPy
from PIL import Image
from UnityPy.export.MeshExporter import export_mesh_obj
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = Path(r"D:/Program Files (x86)/Steam/steamapps/common/PEAK/PEAK_Data")
OUT = Path(__file__).resolve().parent
MESH_DIR = OUT / "meshes"
TEX_DIR = OUT / "textures"
MAT_DIR = OUT / "materials"
SND_DIR = OUT / "sounds"
for d in (MESH_DIR, TEX_DIR, MAT_DIR, SND_DIR):
    d.mkdir(parents=True, exist_ok=True)


def safe_name(name: str) -> str:
    return "".join(c if c.isalnum() or c in "-_." else "_" for c in name).strip("_")


def save_texture(obj, filename: str | None = None) -> Path:
    data = obj.read()
    img: Image.Image = data.image
    name = filename or f"{safe_name(data.m_Name)}.png"
    dest = TEX_DIR / name
    img.save(dest)
    print(f"texture {data.m_Name!r} {img.size} {img.mode} -> {dest.name}")
    return dest


env = UnityPy.load(str(ROOT / "resources.assets"))
by_id = {o.path_id: o for o in env.objects}

env0 = UnityPy.load(str(ROOT / "sharedassets0.assets"))
by_id0 = {o.path_id: o for o in env0.objects}

texture_map = {
    646: "AntiZooka.png",
    647: "Starboy_tex.png",
    835: "star_edge.png",
    644: "stars.png",
    608: "A_Texture_60.png",
    740: "A_Texture_77.png",
}
for pid, fname in texture_map.items():
    save_texture(by_id[pid], fname)

save_texture(by_id0[20], "A_Texture_23.png")


def material_dump(pid: int) -> dict:
    tree = by_id[pid].read_typetree()
    saved = tree.get("m_SavedProperties") or {}
    texs = {}
    for entry in saved.get("m_TexEnvs") or []:
        slot, val = entry
        tex = (val or {}).get("m_Texture") or {}
        texs[slot] = {
            "file_id": tex.get("m_FileID"),
            "path_id": tex.get("m_PathID"),
            "scale": (val or {}).get("m_Scale"),
            "offset": (val or {}).get("m_Offset"),
        }
    return {
        "name": tree.get("m_Name"),
        "path_id": pid,
        "shader": tree.get("m_Shader"),
        "textures": texs,
        "floats": {k: v for k, v in (saved.get("m_Floats") or [])},
        "colors": {k: v for k, v in (saved.get("m_Colors") or [])},
        "ints": {k: v for k, v in (saved.get("m_Ints") or [])},
    }


mat_gold = material_dump(147)
mat_star = material_dump(58)
(MAT_DIR / "M_Tarnished_Gold_Antizooka.json").write_text(
    json.dumps(mat_gold, indent=2), encoding="utf-8"
)
(MAT_DIR / "M_Scout_Star_Antizooka.json").write_text(
    json.dumps(mat_star, indent=2), encoding="utf-8"
)

slot_notes = {
    "submeshes": [
        {
            "index": 0,
            "material": "M_Tarnished Gold Antizooka",
            "triangles": 1846,
            "vertices": 2251,
            "layers": [
                {"slot": "_MainTex", "file": "textures/Starboy_tex.png"},
                {"slot": "_Texture1", "file": "textures/A_Texture_23.png", "source": "sharedassets0.assets"},
                {"slot": "_Texture2", "file": "textures/A_Texture_60.png"},
                {"slot": "_Texture3", "file": "textures/A_Texture_77.png"},
            ],
        },
        {
            "index": 1,
            "material": "M_Scout_Star_Antizooka",
            "triangles": 170,
            "vertices": 164,
            "layers": [
                {"slot": "_MainTex", "file": "textures/star_edge.png"},
                {"slot": "_Texture1", "file": "textures/A_Texture_23.png", "source": "sharedassets0.assets"},
                {"slot": "_Texture2", "file": "textures/stars.png"},
                {"slot": "_Texture3", "file": "textures/A_Texture_60.png"},
            ],
        },
    ],
    "named_texture": "textures/AntiZooka.png",
    "collision_mesh": "meshes/Anti-Zooka_Coll.obj",
    "visual_mesh": "meshes/Anti-Zooka.obj",
    "sounds": [
        "sounds/antizooka-Charge.wav",
        "sounds/antizooka-Fire.wav",
        "sounds/antizooka-Field_Spawn.wav",
    ],
    "notes": [
        "PEAK item shaders layer several triplanar textures and tint with vertex colors.",
        "Reworking the look usually means editing those layer textures and/or vertex colors, not a single baked albedo.",
        "Unity X axis is flipped in the exported OBJ/GLB (left-handed Unity -> right-handed).",
    ],
}
(MAT_DIR / "layout.json").write_text(json.dumps(slot_notes, indent=2), encoding="utf-8")
print("wrote materials")

visual = by_id[1126].read()
coll = by_id[1144].read()
mat_names = ["M_Tarnished_Gold_Antizooka", "M_Scout_Star_Antizooka"]
obj_txt = export_mesh_obj(visual, material_names=mat_names)
if "mtllib" not in obj_txt:
    obj_txt = "mtllib Anti-Zooka.mtl\n" + obj_txt
(MESH_DIR / "Anti-Zooka.obj").write_text(obj_txt, encoding="utf-8")
(MESH_DIR / "Anti-Zooka.mtl").write_text(
    "\n".join(
        [
            "newmtl M_Tarnished_Gold_Antizooka",
            "Kd 0.78 0.44 0.36",
            "map_Kd ../textures/Starboy_tex.png",
            "",
            "newmtl M_Scout_Star_Antizooka",
            "Kd 0.05 0.05 0.08",
            "map_Kd ../textures/star_edge.png",
            "",
        ]
    ),
    encoding="utf-8",
)
(MESH_DIR / "Anti-Zooka_Coll.obj").write_text(export_mesh_obj(coll), encoding="utf-8")
print("wrote obj")

handler = MeshHandler(visual)
handler.process()
print(
    "visual verts",
    handler.m_VertexCount,
    "uv0",
    None if handler.m_UV0 is None else len(handler.m_UV0),
    "n",
    None if handler.m_Normals is None else len(handler.m_Normals),
    "col",
    None if handler.m_Colors is None else len(handler.m_Colors),
    "uv1",
    None if handler.m_UV1 is None else len(handler.m_UV1),
)

if handler.m_Colors:
    lines = ["index,r,g,b,a"]
    for i, c in enumerate(handler.m_Colors):
        r, g, b, a = c[:4]
        lines.append(f"{i},{r},{g},{b},{a}")
    (MESH_DIR / "Anti-Zooka_vertex_colors.csv").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("wrote vertex colors", len(handler.m_Colors))

import numpy as np
import trimesh

verts = np.array([(-p[0], p[1], p[2]) for p in handler.m_Vertices], dtype=np.float32)
faces = []
for tris in handler.get_triangles():
    for a, b, c in tris:
        faces.append((c, b, a))
faces = np.array(faces, dtype=np.int32)

kwargs = {}
if handler.m_Normals:
    kwargs["vertex_normals"] = np.array(
        [(-n[0], n[1], n[2]) for n in handler.m_Normals], dtype=np.float32
    )
mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=False, **kwargs)
if handler.m_Colors:
    cols = np.array(handler.m_Colors, dtype=np.float32)
    if cols.max() <= 1.01:
        cols_u8 = np.clip(cols * 255.0, 0, 255).astype(np.uint8)
    else:
        cols_u8 = np.clip(cols, 0, 255).astype(np.uint8)
    mesh.visual.vertex_colors = cols_u8
if handler.m_UV0:
    mesh.visual.uv = np.array(handler.m_UV0, dtype=np.float32)

mesh.export(MESH_DIR / "Anti-Zooka.glb")
mesh.export(MESH_DIR / "Anti-Zooka.ply")
print("wrote glb/ply", mesh.vertices.shape, mesh.faces.shape)

counts = [len(t) for t in handler.get_triangles()]
start = 0
names = ["gold", "star"]
for i, count in enumerate(counts):
    part = mesh.submesh([np.arange(start, start + count)], append=True)
    part.export(MESH_DIR / f"Anti-Zooka_{names[i]}.glb")
    start += count
    print(" submesh", names[i], part.faces.shape)

audio_ids = (1950, 2162, 2378)
for pid in audio_ids:
    clip = by_id[pid].read()
    samples = clip.samples or {}
    for _name, blob in samples.items():
        dest = SND_DIR / f"{clip.m_Name}.wav"
        dest.write_bytes(blob)
        print(f"sound {clip.m_Name!r} -> {dest.name} ({len(blob)} bytes)")

print("OUT", OUT)
for p in sorted(OUT.rglob("*")):
    if p.is_file():
        print(f"  {p.relative_to(OUT)}  {p.stat().st_size}")
