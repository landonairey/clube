using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Bands of rock by height (GL737), as plain values the fill jobs read: the canyons' layered limestone, sandstone,
    /// granite and stone. The bands repeat every <see cref="Period"/> metres up and down, and wander up and down a few
    /// metres across the land, so a wall shows level-ish lines that dip and rise. Only rock within <see cref="Depth"/>
    /// of the surface is banded: what a wall shows, while deeper ground keeps the base material its ores grow in.
    /// </summary>
    public struct RockStrata
    {
        /// <summary>At most this many bands (a <see cref="FixedList64Bytes{T}"/> of floats holds 15).</summary>
        public const int MaxBands = 15;

        /// <summary>Each band's material id, bottom first.</summary>
        public FixedList32Bytes<byte> Materials;

        /// <summary>Each band's top, in metres over the bottom of the repeat.</summary>
        public FixedList64Bytes<float> Tops;

        /// <summary>Metres of all the bands together: they repeat at this spacing.</summary>
        public float Period;

        /// <summary>Metres under the surface the bands reach; deeper rock is the base material.</summary>
        public float Depth;

        /// <summary>Metres the bands rise and fall either way across the land.</summary>
        public float Wander;

        public uint Seed;

        private const float WanderSize = 260f;

        /// <summary>No bands: rock stays the base material.</summary>
        public static RockStrata None => default;

        public bool IsCreated => Materials.Length > 0 && Period > 0f && Depth > 0f;

        /// <summary>Adds a band on top of those already there; thinner than 0.25 m or past <see cref="MaxBands"/> is skipped.</summary>
        public void Add(byte material, float thickness)
        {
            if (thickness < 0.25f || Materials.Length >= MaxBands)
            {
                return;
            }
            Period += thickness;
            Materials.Add(material);
            Tops.Add(Period);
        }

        /// <summary>The band's material at a world position (x, height, z).</summary>
        public byte MaterialAt(float x, float y, float z)
        {
            return BandAt(y + ShiftAt(x, z));
        }

        /// <summary>Metres the bands are lifted in the column at (x, z); worked out once per column.</summary>
        public float ShiftAt(float x, float z)
        {
            return Wander * GradientNoise.Fractal(Seed, x / WanderSize, z / WanderSize, 2) * 1.4f;
        }

        /// <summary>The band's material at a height already lifted by <see cref="ShiftAt"/>.</summary>
        public byte BandAt(float shiftedHeight)
        {
            float within = shiftedHeight - math.floor(shiftedHeight / Period) * Period;
            for (int i = 0; i < Materials.Length - 1; i++)
            {
                if (within < Tops[i])
                {
                    return Materials[i];
                }
            }
            return Materials[Materials.Length - 1];
        }
    }
}
