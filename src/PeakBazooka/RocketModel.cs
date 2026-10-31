using System.Collections.Generic;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// The rocket you see in flight: PEAK's own <c>InertRocket</c> (the firework rocket the rocket pack drops), stripped
/// to its meshes, nose turned to +Z (the flight direction) and scaled to the bazooka's bore. Replaces the
/// AntiZooka's antigravity shell. A copy is kept under an inactive holder so its scripts never run. The model stands
/// upright (nose along its own +Y); its firework particles (coloured discs, black smoke) are removed.
/// </summary>
internal static class RocketModel
{
    private const string ResourceName = "InertRocket";

    /// <summary>How thick the rocket is drawn, in metres (about the AntiZooka's bore).</summary>
    private const float Thickness = 0.22f;

    private static GameObject? template;

    private static bool tried;

    /// <summary>An active rocket model facing +Z, or null when the game has no <c>InertRocket</c>.</summary>
    internal static GameObject? Create()
    {
        if (!tried)
        {
            tried = true;
            template = BuildTemplate();
        }

        return template != null ? Object.Instantiate(template) : null;
    }

    private static GameObject? BuildTemplate()
    {
        GameObject? prefab = Resources.Load<GameObject>(ResourceName);
        if (prefab == null)
        {
            Plugin.Log.LogWarning($"No '{ResourceName}' in the game resources; rockets keep the AntiZooka shell.");
            return null;
        }

        var holder = new GameObject("PeakBazooka rocket template");
        holder.SetActive(false);
        Object.DontDestroyOnLoad(holder);

        var root = new GameObject("BazookaRocket");
        root.transform.SetParent(holder.transform, false);
        GameObject model = Object.Instantiate(prefab, root.transform);
        model.name = ResourceName;
        Strip(model);

        if (!Measure(root.transform, model, model.transform.localRotation * Vector3.up, out Vector3 center, out float length, out float width))
        {
            Plugin.Log.LogWarning($"'{ResourceName}' has no mesh; rockets keep the AntiZooka shell.");
            Object.Destroy(holder);
            return null;
        }

        LogParts(root.transform, model);

        // Nose along the model's own up axis (it flew nose-up when its longest bounds side was taken instead).
        Vector3 up = model.transform.localRotation * Vector3.up;
        Quaternion turn = Quaternion.FromToRotation(up, Vector3.forward);
        float scale = width > 0.0001f ? Thickness / width : 1f;

        // A point p (rocket space) ends up at turn * (scale * (p - center)): nose forward, centred, bore-sized.
        Transform t = model.transform;
        t.localPosition = turn * ((t.localPosition - center) * scale);
        t.localRotation = turn * t.localRotation;
        t.localScale *= scale;

        Plugin.Log.LogInfo(
            $"Rocket model: {ResourceName}, {length * scale:0.00}m long x {Thickness:0.00}m (x{scale:0.00}), nose along its +Y.");
        return root;
    }

    /// <summary>What the model is made of, for checking its orientation from the log.</summary>
    private static void LogParts(Transform root, GameObject model)
    {
        var parts = new List<string>();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            MeshFilter? filter = renderer.GetComponent<MeshFilter>();
            Mesh? mesh = filter != null ? filter.sharedMesh : null;
            Vector3 size = mesh != null
                ? Vector3.Scale(mesh.bounds.size, renderer.transform.lossyScale) / Mathf.Max(0.0001f, root.lossyScale.x)
                : Vector3.zero;
            parts.Add($"{renderer.name} ({renderer.GetType().Name}{(mesh != null ? $", mesh {mesh.name} {size.x:0.00}x{size.y:0.00}x{size.z:0.00}" : "")})");
        }

        Plugin.Log.LogInfo($"Rocket model parts: {string.Join("; ", parts)}.");
    }

    /// <summary>
    /// Only meshes stay: physics, colliders and scripts would fight the rocket's own flight, and its firework
    /// particles drew coloured discs and black smoke around the shooter.
    /// </summary>
    private static void Strip(GameObject model)
    {
        var effects = new List<string>();
        foreach (ParticleSystemRenderer particles in model.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            effects.Add(particles.name);
            Object.DestroyImmediate(particles);
        }

        foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
        {
            Object.DestroyImmediate(particles);
        }

        foreach (Renderer line in model.GetComponentsInChildren<Renderer>(true))
        {
            if (line is TrailRenderer or LineRenderer)
            {
                effects.Add(line.name);
                Object.DestroyImmediate(line);
            }
        }

        if (effects.Count > 0)
        {
            Plugin.Log.LogInfo($"Rocket model effects removed: {string.Join(", ", effects)}.");
        }

        foreach (Joint joint in model.GetComponentsInChildren<Joint>(true))
        {
            Object.DestroyImmediate(joint);
        }

        // Twice: a script another one requires can only go once that one is gone.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script != null)
                {
                    Object.DestroyImmediate(script);
                }
            }
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (Rigidbody rig in model.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>Bounds of every mesh in <paramref name="root"/>'s space: length along <paramref name="up"/>, width across it.</summary>
    private static bool Measure(Transform root, GameObject model, Vector3 up, out Vector3 center, out float length, out float width)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        center = bounds.center;
        Vector3 size = bounds.size;
        Vector3 a = new(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
        int axis = a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        length = size[axis];
        width = Mathf.Max(size[(axis + 1) % 3], size[(axis + 2) % 3]);
        return any;
    }
}
