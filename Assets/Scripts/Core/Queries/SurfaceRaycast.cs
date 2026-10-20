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
                if (chunk.IsCarved(candidate))
                {
                    // Carved out (GL92): empty, but walled where the ray leaves it into solid ground.
                    if (HitsWall(ray, chunk, settings, candidate, out float wall))
                    {
                        nearest = Mathf.Min(nearest, wall);
                    }
                    return !float.IsPositiveInfinity(nearest);
                }
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

        // Where the ray leaves a carved voxel: a hit when that face isn't shared with another carved
        // voxel and the ground there is solid (the face's four corners, blended), as its wall is drawn.
        private static bool HitsWall(Ray ray, Chunk chunk, ChunkMeshSettings settings, Vector3Int voxel, out float distance)
        {
            distance = float.PositiveInfinity;
            Vector3 direction = ray.direction.normalized;
            Vector3 min = (Vector3)voxel * settings.VoxelSize;
            int exitAxis = -1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-8f)
                {
                    continue;
                }
                float plane = direction[axis] > 0f ? min[axis] + settings.VoxelSize : min[axis];
                float t = (plane - ray.origin[axis]) / direction[axis];
                if (t < distance)
                {
                    distance = t;
                    exitAxis = axis;
                }
            }
            if (exitAxis < 0 || distance < 0f)
            {
                return false;
            }
            var step = Vector3Int.zero;
            step[exitAxis] = direction[exitAxis] > 0f ? 1 : -1;
            if (chunk.IsCarved(voxel + step))
            {
                return false;
            }

            // The density at the exit point, blended from the face's four corners.
            Vector3 local = (ray.origin + direction * distance - min) / settings.VoxelSize;
            int u = (exitAxis + 1) % 3;
            int v = (exitAxis + 2) % 3;
            float fu = Mathf.Clamp01(local[u]);
            float fv = Mathf.Clamp01(local[v]);
            float density = 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3Int sample = voxel;
                sample[exitAxis] += step[exitAxis] > 0 ? 1 : 0;
                int du = corner & 1;
                int dv = corner >> 1;
                sample[u] += du;
                sample[v] += dv;
                if (!chunk.TryGetDensity(sample, out float value))
                {
                    return false;
                }
                density += value * (du == 1 ? fu : 1f - fu) * (dv == 1 ? fv : 1f - fv);
            }
            return density >= settings.IsoLevel;
        }
    }
}
