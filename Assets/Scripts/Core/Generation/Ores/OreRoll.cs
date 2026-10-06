using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// One ore node as generation jobs read it (O1-O5, K35): plain values only, with its host
    /// materials as a bit mask (material ids fit in <see cref="MaterialRegistry.MaxMaterials"/> = 64 bits).
    /// Made from an <see cref="OreNode"/> by <see cref="OreField.ToJobNodes"/>.
    /// </summary>
    public struct OreNodeData
    {
        public float3 Centre;

        /// <summary>1 / σ per axis.</summary>
        public float3 InverseSpread;

        /// <summary>How far the node reaches on each axis: 3σ.</summary>
        public float3 Reach;

        public float Peak;
        public int Priority;
        public byte Material;

        /// <summary>Bit <c>id</c> set when the ore may replace material <c>id</c>; every bit for any host.</summary>
        public ulong Hosts;

        public bool MayReplace(byte host)
        {
            return host < 64 ? (Hosts & (1UL << host)) != 0 : Hosts == ulong.MaxValue;
        }
    }

    /// <summary>
    /// The ore replacement roll (O4), shared by the managed <see cref="OreField.Pick"/> and the
    /// generation jobs, so both give the same ore everywhere. A pure function of the node, the
    /// sample's position and its global sample index, so every chunk holding a border sample
    /// agrees on it (M2).
    /// </summary>
    public static class OreRoll
    {
        /// <summary>A node's reach in σ: beyond 3σ the chance is under 1.2% of the peak, and is taken as 0.</summary>
        public const float ReachInSigmas = 3f;

        private const float ReachSquared = ReachInSigmas * ReachInSigmas;

        /// <summary>
        /// Replacement chance at a position: peak × exp(−d² / 2), d in σ along each axis, and 0
        /// beyond the 3σ reach, so a chunk that didn't collect a node can't disagree with one that did.
        /// </summary>
        public static float Chance(float3 centre, float3 inverseSpread, float peak, float3 position)
        {
            float3 offset = (position - centre) * inverseSpread;
            float distanceSquared = math.dot(offset, offset);
            return distanceSquared > ReachSquared ? 0f : peak * math.exp(-0.5f * distanceSquared);
        }

        /// <summary>Whether the sample's hashed roll falls under the chance (O4).</summary>
        public static bool Rolls(int seed, int3 globalSample, byte material, float chance)
        {
            return chance > 0f && VoxelHash.Uniform(seed, globalSample.x, globalSample.y, globalSample.z, material) < chance;
        }

        /// <summary>
        /// Whether a candidate beats the ore picked so far for a sample (O5): higher priority
        /// wins, then the likelier ore.
        /// </summary>
        public static bool Beats(int priority, float chance, int bestPriority, float bestChance)
        {
            return priority > bestPriority || (priority == bestPriority && chance > bestChance);
        }
    }
}
