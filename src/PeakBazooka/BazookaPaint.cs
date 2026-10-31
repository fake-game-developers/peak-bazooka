using System.Collections.Generic;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// Repaints the cloned AntiZooka in the colours of PEAK's firework rocket, the bazooka's ammo: red fittings and a
/// white tube, with the grips and the spoked exhaust wheel keeping the AntiZooka's own tarnished-gold material. The
/// vanilla AntiZooka keeps its assets; the clone gets its own mesh copy and material copies.
/// <para>
/// The AntiZooka has two material slots: the brass parts and the starfield tube. To paint the brass parts apart, the
/// clone's mesh is a byte-for-byte copy (<see cref="MeshReadback"/>; the game's mesh is not CPU-readable) split into
/// one submesh per look. Same vertices, so nothing moves. The parts are the mesh's separate pieces: the barrel runs
/// along local Y with the muzzle at the low end, the grips hang furthest below it (−Z), and the widest piece is the
/// exhaust wheel. PEAK's item shader takes its base colour from <c>_MainTex</c>, so red and white are copies of the
/// brass material with a flat colour texture.
/// </para>
/// </summary>
internal static class BazookaPaint
{
    private static readonly Color32 RocketRed = new(0xE0, 0x2A, 0x1F, 0xFF);
    private static readonly Color32 PaperWhite = new(0xF2, 0xF0, 0xEA, 0xFF);

    // Share of the AntiZooka's tarnish/grime layers kept: a little texture, mostly clean cartoon colour.
    private const float GrimeKept = 0.3f;

    // Pieces reaching this far below the barrel are the grips (the bands and collars stop at about −0.6).
    private const float GripBelow = -1.2f;

    private const string BrassMaterialName = "M_Tarnished Gold Antizooka";

    internal const string ShellName = "Shell";

    private enum Paint
    {
        Red,
        Original,
        White,
    }

    private static readonly Dictionary<Paint, Material> materials = new();

    internal static void Apply(GameObject gun)
    {
        HideShell(gun);
        foreach (MeshRenderer renderer in gun.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            int brass = System.Array.FindIndex(slots, m => m != null && m.name.StartsWith(BrassMaterialName));
            MeshFilter? filter = renderer.GetComponent<MeshFilter>();
            if (brass < 0 || filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Material original = slots[brass];
            Mesh source = filter.sharedMesh;
            Mesh? mesh = source.subMeshCount == slots.Length ? MeshReadback.ReadableCopy(source) : null;
            if (mesh == null)
            {
                // Cannot split the mesh: the whole brass slot turns red (grips too), the tube white.
                var painted = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    painted[i] = Painted(original, i == brass ? Paint.Red : Paint.White);
                }

                renderer.sharedMaterials = painted;
                Plugin.Log.LogWarning(
                    $"Bazooka paint: mesh '{source.name}' could not be read; red fittings and grips, white tube.");
                return;
            }

            mesh.name = source.name + " (Bazooka)";
            SortedDictionary<Paint, List<int>> parts = Split(mesh, brass);
            mesh.subMeshCount = parts.Count;
            var looks = new List<Material>();
            var counts = new List<string>();
            int sub = 0;
            foreach (KeyValuePair<Paint, List<int>> part in parts)
            {
                // Same vertices, only regrouped: keep the copy's bounds, which match the original mesh.
                mesh.SetTriangles(part.Value, sub++, false);
                looks.Add(part.Key == Paint.Original ? original : Painted(original, part.Key));
                counts.Add($"{part.Key} {part.Value.Count / 3}");
            }

            filter.sharedMesh = mesh;
            renderer.sharedMaterials = looks.ToArray();
            Plugin.Log.LogInfo($"Bazooka paint: {string.Join(", ", counts)} triangles.");
            return;
        }

        Plugin.Log.LogWarning("Bazooka paint: no AntiZooka renderer found; keeping the vanilla look.");
    }

    /// <summary>Triangles of every submesh, regrouped by look.</summary>
    private static SortedDictionary<Paint, List<int>> Split(Mesh mesh, int brassSubmesh)
    {
        Vector3[] vertices = mesh.vertices;
        var parts = new SortedDictionary<Paint, List<int>>();

        void Add(Paint paint, IEnumerable<int> triangles)
        {
            if (!parts.TryGetValue(paint, out List<int> list))
            {
                list = new List<int>();
                parts[paint] = list;
            }

            list.AddRange(triangles);
        }

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] triangles = mesh.GetTriangles(s);
            if (s != brassSubmesh)
            {
                Add(Paint.White, triangles);
                continue;
            }

            // Brass parts: the grips hang below the barrel, the widest piece is the spoked exhaust wheel.
            List<List<int>> pieces = Pieces(vertices, triangles);
            List<int>? wheel = null;
            float wheelWidth = 0f;
            foreach (List<int> piece in pieces)
            {
                float width = 0f;
                foreach (int index in piece)
                {
                    width = Mathf.Max(width, Mathf.Abs(vertices[index].x));
                }

                if (width > wheelWidth)
                {
                    wheelWidth = width;
                    wheel = piece;
                }
            }

            foreach (List<int> piece in pieces)
            {
                float lowest = float.MaxValue;
                foreach (int index in piece)
                {
                    lowest = Mathf.Min(lowest, vertices[index].z);
                }

                Add(piece == wheel || lowest < GripBelow ? Paint.Original : Paint.Red, piece);
            }
        }

        return parts;
    }

    /// <summary>Connected pieces of a submesh (vertices at the same position count as joined), as triangle lists.</summary>
    private static List<List<int>> Pieces(Vector3[] vertices, int[] triangles)
    {
        var parent = new Dictionary<int, int>();

        int Find(int a)
        {
            while (parent[a] != a)
            {
                parent[a] = parent[parent[a]];
                a = parent[a];
            }

            return a;
        }

        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b)
            {
                parent[a] = b;
            }
        }

        var byPosition = new Dictionary<Vector3Int, int>();
        foreach (int index in triangles)
        {
            if (parent.ContainsKey(index))
            {
                continue;
            }

            parent[index] = index;
            Vector3 v = vertices[index];
            var key = new Vector3Int(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
            if (byPosition.TryGetValue(key, out int same))
            {
                Union(index, same);
            }
            else
            {
                byPosition[key] = index;
            }
        }

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Union(triangles[t], triangles[t + 1]);
            Union(triangles[t], triangles[t + 2]);
        }

        var pieces = new Dictionary<int, List<int>>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int root = Find(triangles[t]);
            if (!pieces.TryGetValue(root, out List<int> piece))
            {
                piece = new List<int>();
                pieces[root] = piece;
            }

            piece.Add(triangles[t]);
            piece.Add(triangles[t + 1]);
            piece.Add(triangles[t + 2]);
        }

        return new List<List<int>>(pieces.Values);
    }

    /// <summary>
    /// The AntiZooka shows its loaded antigravity sphere (a pale disc) in the muzzle. Hide its meshes;
    /// <see cref="Action_Antizooka"/> only scales and shakes its transform, and particle effects stay.
    /// </summary>
    private static void HideShell(GameObject gun)
    {
        foreach (Transform child in gun.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != ShellName)
            {
                continue;
            }

            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer or SkinnedMeshRenderer)
                {
                    renderer.enabled = false;
                }
            }
        }
    }

    /// <summary>A copy of the AntiZooka's brass material with a palette texture as its base colour.</summary>
    private static Material Painted(Material template, Paint paint)
    {
        if (materials.TryGetValue(paint, out Material cached) && cached != null)
        {
            return cached;
        }

        var material = new Material(template) { name = $"M_Bazooka_{paint}" };
        Texture2D texture = Flat(paint == Paint.White ? PaperWhite : RocketRed);
        texture.name = material.name + "_Tex";
        material.SetTexture("_MainTex", texture);
        material.SetTextureScale("_MainTex", Vector2.one);
        material.SetTextureOffset("_MainTex", Vector2.zero);

        // Clean cartoon colour: no tarnish hue shift (it turned colours yellow), most of the grime layers gone,
        // and painted cardboard instead of the AntiZooka's polished metal.
        foreach (string hue in new[] { "_HueShifts", "_HueShifts1" })
        {
            if (material.HasProperty(hue))
            {
                material.SetColor(hue, Color.clear);
            }
        }

        foreach (string layer in new[] { "_Color1", "_Color2", "_Color3", "_Color4" })
        {
            if (material.HasProperty(layer))
            {
                Color tint = material.GetColor(layer);
                tint.a *= GrimeKept;
                material.SetColor(layer, tint);
            }
        }

        SetIfPresent(material, "_AddSpecular", 1.5f);
        SetIfPresent(material, "_Metallic", 0.1f);
        SetIfPresent(material, "_BaseMetallic", 0.1f);
        SetIfPresent(material, "_Metalic", 0.1f);
        SetIfPresent(material, "_Smoothness", 0.45f);
        SetIfPresent(material, "_BaseSmooth", 0.45f);

        materials[paint] = material;
        return material;
    }

    private static Texture2D Flat(Color32 colour)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = colour;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static void SetIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }
}
