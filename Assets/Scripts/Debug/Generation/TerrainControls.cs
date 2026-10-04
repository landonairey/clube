using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The terrain section of the in-game lab panels (K33, K31): pick the generator
    /// (K9), change the seed, or regenerate to undo every edit. Edits the runtime config
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
            int picked = GUILayout.SelectionGrid(current, GeneratorNames.ToArray(), 2, frame.Button);
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

            // Regenerating from the config also throws away every brush edit.
            if (GUILayout.Button("Reset terrain (undo all edits)", frame.Button))
            {
                config.NotifyChanged();
            }
        }
    }
}
