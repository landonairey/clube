using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds where a ray first hits a chunk's Marching Cubes surface, and which
    /// voxel that piece of surface belongs to (K5), without needing the built mesh
    /// or a collider. Walks the voxels along the ray (<see cref="VoxelRaycast"/>)
    /// and polygonises each one that has a surface, stopping at the first whose
    /// own triangles the ray hits. Each triangle lies inside its voxel, so voxels
    /// visited in ray order give the nearest hit.
    /// </summary>
    public static class SurfaceRaycast
    {
        /// <param name="ray">In chunk-local space, with sample 0 at the origin.</param>
        /// <param name="voxel">The voxel whose surface was hit.</param>
        /// <param name="point">The hit point, chunk-local.</param>
        /// <returns>False when the ray misses the surface everywhere in the chunk.</returns>
        public static bool Cast(Ray ray, Chunk chunk, ChunkMeshSettings settings, out Vector3Int voxel, out Vector3 point)
        {
            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            var corners = new float[MarchingCubes.CornerCount];
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var writer = new FlatVertexWriter(vertices);
            float nearest = float.PositiveInfinity;

            bool HitsSurface(Vector3Int candidate)
            {
                for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                {
                    corners[corner] = chunk.GetDensity(candidate + MarchingCubes.CornerOffset(corner));
                }

                vertices.Clear();
                triangles.Clear();
                MarchingCubes.Polygonise(
                    corners, settings.IsoLevel, (Vector3)candidate * settings.VoxelSize, settings.VoxelSize,
                    placer, writer, triangles);

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    if (RayTriangle.Intersect(ray, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]], out float distance))
                    {
                        nearest = Mathf.Min(nearest, distance);
                    }
                }
                return !float.IsPositiveInfinity(nearest);
            }

            bool hit = VoxelRaycast.Cast(ray, chunk.VoxelCount, settings.VoxelSize, HitsSurface, out voxel);
            point = hit ? ray.origin + ray.direction.normalized * nearest : Vector3.zero;
            return hit;
        }
    }
}
