using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Peak.Bazooka;

/// <summary>
/// Readable copies of the game's meshes. PEAK ships most meshes without CPU-side data (<see cref="Mesh.isReadable"/>
/// is false), but their vertex and index buffers are still on the GPU: read those back byte for byte into a new mesh
/// with the same layout. The copy is the mod's own, so changing it never touches the game's asset.
/// </summary>
internal static class MeshReadback
{
    /// <summary>A readable copy of <paramref name="source"/>, or null when it cannot be read back.</summary>
    internal static Mesh? ReadableCopy(Mesh source)
    {
        if (source.isReadable)
        {
            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name;
            return copy;
        }

        try
        {
            VertexAttributeDescriptor[] attributes = source.GetVertexAttributes();
            int streams = 0;
            foreach (VertexAttributeDescriptor attribute in attributes)
            {
                streams = System.Math.Max(streams, attribute.stream + 1);
            }

            var copy = new Mesh { name = source.name };
            copy.SetVertexBufferParams(source.vertexCount, attributes);
            const MeshUpdateFlags quiet = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
                | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;
            for (int stream = 0; stream < streams; stream++)
            {
                using GraphicsBuffer vertices = source.GetVertexBuffer(stream);
                var data = new byte[vertices.count * vertices.stride];
                vertices.GetData(data);
                copy.SetVertexBufferData(data, 0, 0, data.Length, stream, quiet);
            }

            using (GraphicsBuffer indices = source.GetIndexBuffer())
            {
                copy.SetIndexBufferParams(indices.count, source.indexFormat);
                if (source.indexFormat == IndexFormat.UInt16)
                {
                    var data = new ushort[indices.count];
                    indices.GetData(data);
                    copy.SetIndexBufferData(data, 0, 0, data.Length, quiet);
                }
                else
                {
                    var data = new uint[indices.count];
                    indices.GetData(data);
                    copy.SetIndexBufferData(data, 0, 0, data.Length, quiet);
                }
            }

            copy.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                copy.SetSubMesh(i, source.GetSubMesh(i), quiet);
            }

            copy.bounds = source.bounds;

            // Some platforms hand back zeros instead of failing; then the copy is useless. Check size and centre.
            Vector3[] positions = copy.vertices;
            var read = new Bounds(positions.Length > 0 ? positions[0] : Vector3.zero, Vector3.zero);
            foreach (Vector3 position in positions)
            {
                read.Encapsulate(position);
            }

            float tolerance = source.bounds.size.magnitude * 0.02f + 0.001f;
            if (positions.Length == 0 || (read.size - source.bounds.size).magnitude > tolerance
                || (read.center - source.bounds.center).magnitude > tolerance)
            {
                Plugin.Log.LogWarning($"Mesh '{source.name}' read back from the GPU does not match its bounds; not used.");
                UnityEngine.Object.Destroy(copy);
                return null;
            }

            return copy;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not read mesh '{source.name}' back from the GPU: {e.Message}");
            return null;
        }
    }
}
