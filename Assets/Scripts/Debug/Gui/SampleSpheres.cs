using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// A sphere on every density sample of a chunk, all in one mesh with vertex colours
    /// (one draw call), shaded by density from black (0) to white (1). Used for the
    /// chunk's sample view (V6, <see cref="ChunkDebugView"/>) and the step-through's
    /// density field (K17), which reveals the spheres a few at a time.
    /// </summary>
    /// <remarks>
    /// Spheres are in storage order (x fastest, then y, then z), the order the field is
    /// revealed in. The geometry is only rebuilt when the sample count, voxel size or
    /// radius changes; a density edit only rewrites the colours.
    /// </remarks>
    public sealed class SampleSpheres : IDisposable
    {
        /// <summary>Above this many samples the spheres aren't drawn, to keep the mesh and its rebuilds light.</summary>
        public const int MaxSamples = 40000;

        private readonly LabMeshObject meshObject;
        private readonly List<Vector3> sphereVertices = new List<Vector3>();
        private readonly List<int> sphereTriangles = new List<int>();
        private readonly List<Vector3> scratchVertices = new List<Vector3>();
        private readonly List<int> scratchIndices = new List<int>();
        private readonly List<Color32> colors = new List<Color32>();

        private Vector3Int builtCount;
        private float builtVoxelSize;
        private float builtRadius;
        private int revealed = -1;

        /// <param name="material">A material that shows vertex colours (and alpha, if the spheres are see-through).</param>
        public SampleSpheres(Transform parent, string name, Material material)
        {
            meshObject = new LabMeshObject(parent, name, material);
            LabMeshes.Icosphere(sphereVertices, sphereTriangles);
        }

        public bool Visible
        {
            get => meshObject.Visible;
            set => meshObject.Visible = value;
        }

        public static int Total(Vector3Int sampleCount)
        {
            return sampleCount.x * sampleCount.y * sampleCount.z;
        }

        /// <summary>The sample at a storage-order index (x fastest).</summary>
        public static Vector3Int SampleAt(int index, Vector3Int sampleCount)
        {
            return new Vector3Int(
                index % sampleCount.x,
                index / sampleCount.x % sampleCount.y,
                index / (sampleCount.x * sampleCount.y));
        }

        /// <summary>Lays out one sphere per sample, unless it already is for these values.</summary>
        /// <param name="radius">Sphere radius as a fraction of the voxel size.</param>
        public void SetGeometry(Vector3Int sampleCount, float voxelSize, float radius)
        {
            if (sampleCount == builtCount && voxelSize == builtVoxelSize && radius == builtRadius)
            {
                return;
            }

            float worldRadius = radius * voxelSize;
            scratchVertices.Clear();
            scratchIndices.Clear();
            int total = Total(sampleCount);
            for (int i = 0; i < total; i++)
            {
                Vector3 centre = (Vector3)SampleAt(i, sampleCount) * voxelSize;
                int first = scratchVertices.Count;
                foreach (Vector3 vertex in sphereVertices)
                {
                    scratchVertices.Add(centre + vertex * worldRadius);
                }
                foreach (int index in sphereTriangles)
                {
                    scratchIndices.Add(first + index);
                }
            }

            Mesh mesh = meshObject.Mesh;
            mesh.Clear();
            mesh.indexFormat = scratchVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(scratchVertices);
            mesh.SetTriangles(scratchIndices, 0);
            mesh.RecalculateBounds();

            builtCount = sampleCount;
            builtVoxelSize = voxelSize;
            builtRadius = radius;
            revealed = total;
        }

        /// <summary>Shades each sphere by its sample's density. Call after <see cref="SetGeometry"/>.</summary>
        public void SetDensities(Func<Vector3Int, float> densityAt, float opacity)
        {
            int perSphere = sphereVertices.Count;
            byte alpha = (byte)(Mathf.Clamp01(opacity) * 255f);
            colors.Clear();
            int total = Total(builtCount);
            for (int i = 0; i < total; i++)
            {
                byte grey = (byte)(Mathf.Clamp01(densityAt(SampleAt(i, builtCount))) * 255f);
                var color = new Color32(grey, grey, grey, alpha);
                for (int v = 0; v < perSphere; v++)
                {
                    colors.Add(color);
                }
            }
            meshObject.Mesh.SetColors(colors);
        }

        /// <summary>
        /// Draws only the first <paramref name="count"/> spheres in storage order, by
        /// shortening the index range drawn rather than rebuilding the mesh.
        /// </summary>
        public void Reveal(int count)
        {
            count = Mathf.Clamp(count, 0, Total(builtCount));
            if (count == revealed)
            {
                return;
            }

            revealed = count;
            meshObject.Mesh.SetSubMesh(
                0,
                new SubMeshDescriptor(0, count * sphereTriangles.Count),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        public void Dispose()
        {
            meshObject.Dispose();
        }
    }
}
