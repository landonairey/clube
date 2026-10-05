using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The terrain section of the in-game lab panels (K33, K31): pick the generator
    /// (K9), change the seed, tune the shape (surface level, amplitude, frequency and the
    /// fractal octaves, lacunarity and persistence, and the Spline generator's height curve),
    /// or regenerate to undo every edit.
    /// Edits the runtime config
    /// copy and calls <see cref="WorldConfig.NotifyChanged"/>, the same as an Inspector
    /// edit, so whatever owns the terrain regenerates it.
    /// </summary>
    public static class TerrainControls
    {
        private static readonly List<TerrainGeneratorType> GeneratorChoices = new List<TerrainGeneratorType>();
        private static readonly List<string> GeneratorNames = new List<string>();

        public static void Draw(LabPanelFrame frame, WorldConfig config)
        {
            TerrainSettings terrain = config.Terrain;

            // Heightmap needs an image asset, so it's only offered when one is assigned.
            GeneratorChoices.Clear();
            GeneratorNames.Clear();
            foreach (TerrainGeneratorType type in Enum.GetValues(typeof(TerrainGeneratorType)))
            {
                if (type != TerrainGeneratorType.Heightmap || terrain.HeightmapSource != null)
                {
                    GeneratorChoices.Add(type);
                    GeneratorNames.Add(type.ToString());
                }
            }

            int current = GeneratorChoices.IndexOf(terrain.Generator);
            int picked = GUILayout.SelectionGrid(current, GeneratorNames.ToArray(), 2, frame.Button, GUILayout.Width(frame.InnerWidth));
            if (picked != current && picked >= 0)
            {
                terrain.Generator = GeneratorChoices[picked];
                config.NotifyChanged();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seed {terrain.Seed}", frame.Label);
            int seed = terrain.Seed;
            if (GUILayout.Button("−", frame.Button))
            {
                seed--;
            }
            if (GUILayout.Button("+", frame.Button))
            {
                seed++;
            }
            if (GUILayout.Button("Random", frame.Button))
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }
            GUILayout.EndHorizontal();
            if (seed != terrain.Seed)
            {
                terrain.Seed = seed;
                config.NotifyChanged();
            }

            DrawShape(frame, config, terrain);

            // Regenerating from the config also throws away every brush edit.
            if (GUILayout.Button("Reset terrain (undo all edits)", frame.Button))
            {
                config.NotifyChanged();
            }
        }

        // The noise shape. Each real change regenerates, so dragging reshapes the terrain live.
        private static void DrawShape(LabPanelFrame frame, WorldConfig config, TerrainSettings terrain)
        {
            float worldHeight = config.ChunkSize.y * config.VoxelSize * config.WorldHeightInChunks;
            float surface = frame.Slider("Surface level", terrain.SurfaceLevel, 0f, Mathf.Max(1f, worldHeight), "0.0");
            float amplitude = frame.Slider("Amplitude", terrain.Amplitude, 0f, Mathf.Max(1f, worldHeight * 0.5f), "0.0");
            float frequency = frame.Slider("Frequency", terrain.Frequency, 0.005f, 0.5f, "0.000");
            int octaves = Mathf.RoundToInt(frame.Slider("Octaves", terrain.Octaves, 1f, 8f, "0"));
            float lacunarity = frame.Slider("Lacunarity", terrain.Lacunarity, 1f, 4f, "0.00");
            float persistence = frame.Slider("Persistence", terrain.Persistence, 0f, 1f, "0.00");

            if (!Mathf.Approximately(surface, terrain.SurfaceLevel) || !Mathf.Approximately(amplitude, terrain.Amplitude)
                || !Mathf.Approximately(frequency, terrain.Frequency) || octaves != terrain.Octaves
                || !Mathf.Approximately(lacunarity, terrain.Lacunarity) || !Mathf.Approximately(persistence, terrain.Persistence))
            {
                terrain.SurfaceLevel = surface;
                terrain.Amplitude = amplitude;
                terrain.Frequency = frequency;
                terrain.Octaves = octaves;
                terrain.Lacunarity = lacunarity;
                terrain.Persistence = persistence;
                config.NotifyChanged();
            }
            GUILayout.Label("Octaves, lacunarity and persistence shape the fractal generators.", frame.Hint);

            if (terrain.Generator == TerrainGeneratorType.Spline)
            {
                GUILayout.Label("Height curve (Spline)", frame.Header);
                if (HeightCurveControls.Draw(frame, terrain.HeightCurve))
                {
                    config.NotifyChanged();
                }
            }
        }
    }
}
