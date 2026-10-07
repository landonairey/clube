using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The surface mesh inside a voxel, rebuilt on demand from the world's densities: the
    /// same marching cubes triangles the chunk mesh has there (V3's edge placement, flat
    /// triangles), for outlining what a tool reaches (GL4). Small and managed: meant for a
    /// handful of voxels a frame, not for meshing chunks.
    /// </summary>
    public static class SurfaceOutline
    {
        // Reused between calls; the outline only runs on the main thread.
        private static readonly float[] Corners = new float[MarchingCubes.CornerCount];
        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static readonly List<int> Indices = new List<int>();
        private static readonly FlatVertexWriter Writer = new FlatVertexWriter(Vertices);

        /// <summary>
        /// Adds the voxel's surface triangle edges to <paramref name="lines"/> as pairs of
        /// points (relative to the world origin), three pairs per triangle. Adds nothing for a
        /// voxel without surface or with a corner that isn't loaded.
        /// </summary>
        public static void AddEdges(World world, Vector3Int voxel, ChunkMeshSettings settings, List<Vector3> lines)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float? density = world.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                if (!density.HasValue)
                {
                    return;
                }
                Corners[corner] = density.Value;
            }

            Vertices.Clear();
            Indices.Clear();
            MarchingCubes.Polygonise(
                Corners, settings.IsoLevel, world.Grid.SampleToWorld(voxel), world.Grid.VoxelSize,
                EdgeVertexPlacers.For(settings.EdgePlacement), Writer, Indices);

            for (int i = 0; i < Indices.Count; i += 3)
            {
                Vector3 a = Vertices[Indices[i]];
                Vector3 b = Vertices[Indices[i + 1]];
                Vector3 c = Vertices[Indices[i + 2]];
                lines.Add(a);
                lines.Add(b);
                lines.Add(b);
                lines.Add(c);
                lines.Add(c);
                lines.Add(a);
            }
        }
    }
}
