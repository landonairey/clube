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

        public TerrainGeneratorType Generator => generator;

        public float SurfaceLevel => surfaceLevel;

        public float Amplitude => amplitude;

        public float Frequency => frequency;

        public int Octaves => octaves;

        public float Lacunarity => lacunarity;

        public float Persistence => persistence;

        public int Seed => seed;
    }
}
