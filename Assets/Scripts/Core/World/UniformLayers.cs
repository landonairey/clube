using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Which layers of a chunk column are proven all air or all rock from the column's heights, trees, ores and water
    /// (GL773), and what those chunks are made of. A proven layer needs no generating, storing or meshing until something
    /// writes to it (<see cref="World.ProveColumn"/>). Layers from <see cref="MaxLayers"/> up are never proven.
    /// </summary>
    public struct UniformLayers
    {
        /// <summary>The layers a column can hold proofs for; those above simply load as usual.</summary>
        public const int MaxLayers = 128;

        private ulong airLow;
        private ulong airHigh;
        private ulong rockLow;
        private ulong rockHigh;

        /// <summary>The material id of a proven air chunk's samples.</summary>
        public byte AirMaterial;

        /// <summary>The material id of a proven rock chunk's samples.</summary>
        public byte RockMaterial;

        /// <summary>How many layers are proven.</summary>
        public readonly int Count => math.countbits(airLow) + math.countbits(airHigh) + math.countbits(rockLow) + math.countbits(rockHigh);

        public readonly bool IsAir(int layer)
        {
            return IsIn(layer) && (((layer < 64 ? airLow : airHigh) >> (layer & 63)) & 1UL) != 0;
        }

        public readonly bool IsRock(int layer)
        {
            return IsIn(layer) && (((layer < 64 ? rockLow : rockHigh) >> (layer & 63)) & 1UL) != 0;
        }

        public readonly bool IsUniform(int layer)
        {
            return IsAir(layer) || IsRock(layer);
        }

        public void SetAir(int layer)
        {
            if (!IsIn(layer))
            {
                return;
            }
            Clear(layer);
            if (layer < 64)
            {
                airLow |= 1UL << layer;
            }
            else
            {
                airHigh |= 1UL << (layer - 64);
            }
        }

        public void SetRock(int layer)
        {
            if (!IsIn(layer))
            {
                return;
            }
            Clear(layer);
            if (layer < 64)
            {
                rockLow |= 1UL << layer;
            }
            else
            {
                rockHigh |= 1UL << (layer - 64);
            }
        }

        /// <summary>Takes back any proof of the layer.</summary>
        public void Clear(int layer)
        {
            if (!IsIn(layer))
            {
                return;
            }
            ulong keep = ~(1UL << (layer & 63));
            if (layer < 64)
            {
                airLow &= keep;
                rockLow &= keep;
            }
            else
            {
                airHigh &= keep;
                rockHigh &= keep;
            }
        }

        private static bool IsIn(int layer)
        {
            return layer >= 0 && layer < MaxLayers;
        }
    }
}
