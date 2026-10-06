namespace Clube.Core
{
    /// <summary>
    /// <see cref="TerrainLayers"/> as plain values (M10): the material id at each depth below
    /// the surface, without the asset references, so generation jobs can read it (K35).
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
            };
        }

        /// <summary>The material id at a depth below the surface (above it, the top layer's).</summary>
        public byte MaterialAt(float depth)
        {
            return depth < TopDepth ? Top : depth < BandEnd ? Band : Base;
        }
    }
}
