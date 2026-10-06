using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How a world's terrain is generated (K8–K10), held in <see cref="WorldConfig"/>.
    /// Heights and frequencies are in world units, so the terrain keeps its shape
    /// when the voxel size changes; only the detail it's sampled at does.
    /// </summary>
    [Serializable]
    public class TerrainSettings
    {
        [Tooltip("Which generator fills the chunks (K9).")]
        [SerializeField]
        private TerrainGeneratorType generator = TerrainGeneratorType.FractalPerlin2D;

        [Tooltip("Height of the average ground surface above the chunk's base, in world units (K8).")]
        [SerializeField]
        private float surfaceLevel = 4f;

        [Tooltip("How far hills rise and valleys sink from the surface level, in world units (K8).")]
        [SerializeField, Min(0f)]
        private float amplitude = 2f;

        [Tooltip("Features per world unit. Higher gives smaller, busier features.")]
        [SerializeField, Min(0.001f)]
        private float frequency = 0.15f;

        [Tooltip("Fractal Perlin: how many noise layers are added together.")]
        [SerializeField, Range(1, 8)]
        private int octaves = 4;

        [Tooltip("Fractal Perlin: frequency multiplier from one octave to the next (usually about 2).")]
        [SerializeField, Min(1f)]
        private float lacunarity = 2f;

        [Tooltip("Fractal Perlin: amplitude multiplier from one octave to the next (usually about 0.5).")]
        [SerializeField, Range(0f, 1f)]
        private float persistence = 0.5f;

        [Tooltip("Seed for every noise generator (K10). The same seed always gives the same terrain.")]
        [SerializeField]
        private int seed = 1;

        [Tooltip("Which material the ground is made of by depth below the surface (M10): grass, then dirt, then stone.")]
        [SerializeField]
        private TerrainLayers layers = new TerrainLayers();

        [Tooltip("Ore nodes through the ground (3D): cell size and, per ore, how many, how deep, how rich and how spread.")]
        [SerializeField]
        private OreGeneration ores = new OreGeneration();

        [Header("Spline")]
        [Tooltip("Remaps the fractal noise (0-1 along the bottom) to a height (0 = level - amplitude, " +
                 "1 = level + amplitude). Flat stretches make plateaus, steep ones cliffs.")]
        [SerializeField]
        private AnimationCurve heightCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.4f, 0.15f), new Keyframe(0.55f, 0.75f), new Keyframe(1f, 1f));

        [Header("Heightmap")]
        [Tooltip("Grayscale heightmap image (PNG). Needs Read/Write enabled and no compression in its import settings. " +
                 "Assigning one (or switching to the Heightmap generator) resets the surface level, amplitude and " +
                 "units per pixel so the image maps onto the chunk as exported: black at the base, white at the top.")]
        [SerializeField]
        private Texture2D heightmap;

        [Tooltip("RAW heightmap (a square .bytes file of 8- or 16-bit heights). Used instead of the image when set.")]
        [SerializeField]
        private TextAsset heightmapRaw;

        [Tooltip("The RAW file stores two bytes (little-endian) per pixel.")]
        [SerializeField]
        private bool rawIs16Bit = true;

        [Tooltip("World units covered by one heightmap pixel. Pixel (0, 0) sits at the world origin.")]
        [SerializeField, Min(0.01f)]
        private float heightmapUnitsPerPixel = 1f;

        /// <summary>Settable so lab controls can switch it at runtime; call <see cref="WorldConfig.NotifyChanged"/> after.</summary>
        /// <summary>Material by depth below the surface (M10).</summary>
        public TerrainLayers Layers => layers;

        /// <summary>Ore generation (3D, O1-O5).</summary>
        public OreGeneration Ores => ores;

        public TerrainGeneratorType Generator
        {
            get => generator;
            set => generator = value;
        }

        // The shape values are settable for lab controls too; call WorldConfig.NotifyChanged after.

        public float SurfaceLevel
        {
            get => surfaceLevel;
            set => surfaceLevel = value;
        }

        public float Amplitude
        {
            get => amplitude;
            set => amplitude = Mathf.Max(0f, value);
        }

        public float Frequency
        {
            get => frequency;
            set => frequency = Mathf.Max(0.001f, value);
        }

        public int Octaves
        {
            get => octaves;
            set => octaves = Mathf.Clamp(value, 1, 8);
        }

        public float Lacunarity
        {
            get => lacunarity;
            set => lacunarity = Mathf.Max(1f, value);
        }

        public float Persistence
        {
            get => persistence;
            set => persistence = Mathf.Clamp01(value);
        }

        /// <summary>Settable so lab controls can reseed at runtime; call <see cref="WorldConfig.NotifyChanged"/> after.</summary>
        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public AnimationCurve HeightCurve => heightCurve;

        public Texture2D HeightmapTexture => heightmap;

        public TextAsset HeightmapRaw => heightmapRaw;

        public bool RawIs16Bit => rawIs16Bit;

        public float HeightmapUnitsPerPixel => heightmapUnitsPerPixel;

        /// <summary>The asset the heightmap comes from (the RAW file if set, else the image), or null.</summary>
        public UnityEngine.Object HeightmapSource => heightmapRaw != null ? heightmapRaw : heightmap;

        /// <summary>
        /// Maps the heightmap onto the chunk the way <see cref="HeightmapExport"/> writes
        /// it: black at the chunk's base, white at its top, one pixel per sample column.
        /// A freshly imported heightmap then shows its own terrain before any tweaking.
        /// </summary>
        public void FitHeightmapToChunk(float chunkHeight, float voxelSize)
        {
            surfaceLevel = chunkHeight / 2f;
            amplitude = chunkHeight / 2f;
            heightmapUnitsPerPixel = voxelSize;
        }

        /// <summary>The heightmap to generate from: the RAW file if set, else the image, else null.</summary>
        public Heightmap LoadHeightmap()
        {
            if (heightmapRaw != null)
            {
                return Heightmap.FromRaw(heightmapRaw.bytes, rawIs16Bit);
            }
            return heightmap != null ? Heightmap.FromTexture(heightmap) : null;
        }
    }
}
