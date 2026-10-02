using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Walks a ray through a chunk's voxel grid one voxel at a time, in the order
    /// the ray enters them (Amanatides &amp; Woo), and returns the first voxel a
    /// caller-supplied test accepts. <see cref="SurfaceRaycast"/> builds on it to
    /// pick the voxel whose surface a mouse click hits (K5).
    /// </summary>
    public static class VoxelRaycast
    {
        // Nudges the start point just inside the voxel the ray enters, so a ray that
        // starts exactly on a voxel boundary is placed in the voxel it is heading into.
        private const float Epsilon = 1e-4f;

        /// <summary>
        /// Casts <paramref name="ray"/>, in chunk-local space (sample 0 at the origin),
        /// through a grid of <paramref name="voxelCount"/> voxels of size
        /// <paramref name="voxelSize"/>.
        /// </summary>
        /// <param name="accept">Returns true for the voxel to stop at.</param>
        /// <param name="voxel">The first accepted voxel along the ray.</param>
        /// <returns>False when the ray misses the chunk or no voxel along it is accepted.</returns>
        public static bool Cast(Ray ray, Vector3Int voxelCount, float voxelSize, Func<Vector3Int, bool> accept, out Vector3Int voxel)
        {
            voxel = default;
            Vector3 size = (Vector3)voxelCount * voxelSize;
            if (!new Bounds(size * 0.5f, size).IntersectRay(ray, out float entry))
            {
                return false;
            }

            Vector3 direction = ray.direction.normalized;
            Vector3 start = ray.origin + direction * (Mathf.Max(entry, 0f) + Epsilon);

            Vector3Int current = Vector3Int.Min(
                Vector3Int.Max(Vector3Int.FloorToInt(start / voxelSize), Vector3Int.zero),
                voxelCount - Vector3Int.one);

            var step = new Vector3Int(Sign(direction.x), Sign(direction.y), Sign(direction.z));
            Vector3 nextBoundary = Vector3.zero;
            Vector3 boundaryStep = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                if (step[axis] == 0)
                {
                    nextBoundary[axis] = float.PositiveInfinity;
                    boundaryStep[axis] = float.PositiveInfinity;
                    continue;
                }

                // Distance along the ray to the first voxel boundary on this axis, then between boundaries.
                float boundary = (current[axis] + (step[axis] > 0 ? 1 : 0)) * voxelSize;
                nextBoundary[axis] = (boundary - start[axis]) / direction[axis];
                boundaryStep[axis] = voxelSize / Mathf.Abs(direction[axis]);
            }

            while (IsInside(current, voxelCount))
            {
                if (accept(current))
                {
                    voxel = current;
                    return true;
                }

                // Step across whichever boundary the ray reaches first.
                int nextAxis = nextBoundary.x < nextBoundary.y
                    ? (nextBoundary.x < nextBoundary.z ? 0 : 2)
                    : (nextBoundary.y < nextBoundary.z ? 1 : 2);
                current[nextAxis] += step[nextAxis];
                nextBoundary[nextAxis] += boundaryStep[nextAxis];
            }

            return false;
        }

        private static int Sign(float value)
        {
            return value > 0f ? 1 : value < 0f ? -1 : 0;
        }

        private static bool IsInside(Vector3Int voxel, Vector3Int voxelCount)
        {
            return voxel.x >= 0 && voxel.y >= 0 && voxel.z >= 0 &&
                   voxel.x < voxelCount.x && voxel.y < voxelCount.y && voxel.z < voxelCount.z;
        }
    }
}
