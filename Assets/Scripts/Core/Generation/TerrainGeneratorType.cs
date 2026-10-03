namespace Clube.Core
{
    /// <summary>The terrain generators available in <see cref="TerrainSettings"/> (K9).</summary>
    public enum TerrainGeneratorType
    {
        /// <summary>A level surface at the surface height.</summary>
        Flat,

        /// <summary>Rolling waves: a sine along x times a sine along z.</summary>
        Sine,

        /// <summary>A heightfield from one layer of 2D Perlin noise.</summary>
        Perlin2D,

        /// <summary>3D Perlin noise added to a ground level, which can make overhangs and caves.</summary>
        Perlin3D,

        /// <summary>A heightfield from several octaves of 2D Perlin noise (fractal Brownian motion).</summary>
        FractalPerlin2D,

        /// <summary>Fractal 2D Perlin noise remapped through an editable height curve.</summary>
        Spline,

        /// <summary>Heights read from a grayscale image or RAW file.</summary>
        Heightmap,
    }
}
