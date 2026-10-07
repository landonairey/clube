using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// <see cref="TerrainLayers"/> as plain values (M10): the material id at each depth below
    /// the surface, the steep-slope rule (GL21) and the surface rocks (GL22), without the
    /// asset references, so generation jobs can read it (K35).
    /// </summary>
    public struct LayerTable
    {
        /// <summary>Depth below the surface where the top layer ends, in metres.</summary>
        public float TopDepth;

        /// <summary>Depth below the surface where the band ends and the base begins, in metres.</summary>
        public float BandEnd;

        public byte Top;
        public byte Band;
        public byte Base;

        /// <summary>Surface gradient (rise over run) from which a column is steep; 0 turns steep slopes off.</summary>
        public float SteepGradient;

        /// <summary>Surface height, in metres, above which steep columns show the base material.</summary>
        public float SteepMinHeight;

        /// <summary>Material id of the surface rocks.</summary>
        public byte Rock;

        /// <summary>Chance that a surface column starts a rock cluster; 0 for no rocks.</summary>
        public float RockChance;

        /// <summary>Every depth gets material id 0: what terrain without layers has.</summary>
        public static LayerTable None => default;

        /// <summary>The table for a terrain's layers; <see cref="None"/> when it has none.</summary>
        public static LayerTable From(TerrainLayers layers)
        {
            if (layers == null || layers.IsEmpty)
            {
                return None;
            }
            return new LayerTable
            {
                TopDepth = layers.TopDepth,
                BandEnd = layers.BandDepth > layers.TopDepth ? layers.BandDepth : layers.TopDepth,
                Top = layers.Top != null ? layers.Top.Id : (byte)0,
                Band = layers.Band != null ? layers.Band.Id : (byte)0,
                Base = layers.Base != null ? layers.Base.Id : (byte)0,
                SteepGradient = layers.SteepAngle < 89.9f ? math.tan(math.radians(layers.SteepAngle)) : 0f,
                SteepMinHeight = layers.SteepMinHeight,
                Rock = layers.Rock != null ? layers.Rock.Id : (byte)0,
                RockChance = layers.Rock != null ? layers.RockChance : 0f,
            };
        }

        /// <summary>The material id at a depth below the surface (above it, the top layer's).</summary>
        public byte MaterialAt(float depth)
        {
            return depth < TopDepth ? Top : depth < BandEnd ? Band : Base;
        }

        /// <summary>The material id at a depth, in a column that is steep (GL21) or not.</summary>
        public byte MaterialAt(float depth, bool steep)
        {
            return steep && depth < BandEnd ? Base : MaterialAt(depth);
        }

        /// <summary>True for a column whose surface gradient and height make it bare rock (GL21).</summary>
        public bool IsSteep(float gradient, float surfaceHeight)
        {
            return SteepGradient > 0f && gradient >= SteepGradient && surfaceHeight >= SteepMinHeight;
        }
    }
}
