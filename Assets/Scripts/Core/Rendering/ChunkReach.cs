using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How far a chunk is from a point, told from its coordinate alone (GL773). Looks over every chunk renderer read each
    /// one's transform, a call into the engine apiece; once the whole render distance loaded and meshed there were some
    /// 25,000 of them, so the looks test this first and only measure the chunks it lets through.
    /// </summary>
    public static class ChunkReach
    {
        /// <summary>
        /// False when the chunk's column is certainly further than <paramref name="radius"/> across the ground from
        /// <paramref name="focusLocal"/>; both in the world's own space. A chunk's width of slack keeps it on the safe side.
        /// </summary>
        public static bool MayBeWithin(Vector3Int coord, Vector3 focusLocal, Vector3 chunkSize, float radius)
        {
            float dx = (coord.x + 0.5f) * chunkSize.x - focusLocal.x;
            float dz = (coord.z + 0.5f) * chunkSize.z - focusLocal.z;
            float reach = radius + chunkSize.x + chunkSize.z;
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>A radius in metres as the world's own space measures it.</summary>
        public static float InWorldSpace(Transform world, float radius)
        {
            return radius / Mathf.Max(1e-4f, Mathf.Abs(world.lossyScale.x));
        }
    }
}
