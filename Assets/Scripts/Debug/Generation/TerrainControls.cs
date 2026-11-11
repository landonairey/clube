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
            DrawLayers(frame, config, terrain.Layers);

            // Regenerating from the config also throws away every brush edit.
            if (GUILayout.Button("Reset terrain (undo all edits)", frame.Button))
            {
                config.NotifyChanged();
            }
        }

        // How deep the top layer and the band reach (M10); the materials themselves are set in the config asset.
        private static void DrawLayers(LabPanelFrame frame, WorldConfig config, TerrainLayers layers)
        {
            if (layers.IsEmpty)
            {
                return;
            }
            float top = frame.Slider($"{Name(layers.Top)} depth (m)", layers.TopDepth, 0f, 4f, "0.0#");
            float band = frame.Slider($"{Name(layers.Band)} down to (m)", layers.BandDepth, 0f, 16f, "0.0#");
            if (!Mathf.Approximately(top, layers.TopDepth) || !Mathf.Approximately(band, layers.BandDepth))
            {
                layers.TopDepth = top;
                layers.BandDepth = band;
                config.NotifyChanged();
            }

            // Bare rock on steep mountain slopes (GL21) and small surface rocks (GL22).
            float steep = frame.Slider($"{Name(layers.Base)} on slopes from (°, 90 off)", layers.SteepAngle, 10f, 90f, "0");
            float steepHeight = frame.Slider("…above height (m)", layers.SteepMinHeight, 0f, 64f, "0.#");
            float baseAbove = frame.Slider($"{Name(layers.Base)} everywhere above (m, 0 off)", layers.BaseAboveHeight, 0f, 128f, "0.#");
            float rocks = frame.Slider($"{Name(layers.Rock)} chance per column", layers.RockChance, 0f, 0.05f, "0.000");
            if (!Mathf.Approximately(baseAbove, layers.BaseAboveHeight))
            {
                layers.BaseAboveHeight = baseAbove;
                config.NotifyChanged();
            }
            if (!Mathf.Approximately(steep, layers.SteepAngle) || !Mathf.Approximately(steepHeight, layers.SteepMinHeight)
                || !Mathf.Approximately(rocks, layers.RockChance))
            {
                layers.SteepAngle = steep;
                layers.SteepMinHeight = steepHeight;
                layers.RockChance = rocks;
                config.NotifyChanged();
            }
        }

        // The eroded landscape's gullies (P12). Each change regenerates.
        private static void DrawErosion(LabPanelFrame frame, WorldConfig config, ErosionSettings erosion)
        {
            GUILayout.Label("Erosion", frame.Header);
            int baseOctaves = Mathf.RoundToInt(frame.Slider("Massif octaves", erosion.BaseOctaves, 1f, 8f, "0"));
            float size = frame.Slider("Gully size (m)", erosion.GullySize, 20f, 400f, "0");
            float strength = frame.Slider("Strength", erosion.Strength, 0f, 0.6f, "0.00");
            int octaves = Mathf.RoundToInt(frame.Slider("Gully octaves", erosion.Octaves, 1f, 7f, "0"));
            float weight = frame.Slider("Gully weight", erosion.GullyWeight, 0f, 1f, "0.00");
            float detail = frame.Slider("Detail", erosion.Detail, 0.5f, 4f, "0.00");
            float onset = frame.Slider("Onset", erosion.Onset, 0.25f, 4f, "0.00");
            float assumed = frame.Slider("Assumed slope", erosion.AssumedSlope, 0.1f, 2f, "0.00");
            float ridges = frame.Slider("Ridge rounding", erosion.RidgeRounding, 0f, 0.5f, "0.00");
            float creases = frame.Slider("Gully floor rounding", erosion.CreaseRounding, 0f, 0.5f, "0.00");
            int valleys = Mathf.RoundToInt(frame.Slider("River valley octaves", erosion.ValleyOctaves, 1f, 4f, "0"));
            float lift = frame.Slider("Peak lift", erosion.PeakLift, 0f, 1f, "0.00");
            float pond = frame.Slider("Smallest pond (m²)", erosion.MinPondArea, 0f, 5000f, "0");
            if (baseOctaves != erosion.BaseOctaves || !Mathf.Approximately(size, erosion.GullySize) || !Mathf.Approximately(strength, erosion.Strength)
                || octaves != erosion.Octaves || !Mathf.Approximately(weight, erosion.GullyWeight)
                || !Mathf.Approximately(detail, erosion.Detail) || !Mathf.Approximately(onset, erosion.Onset)
                || !Mathf.Approximately(assumed, erosion.AssumedSlope) || !Mathf.Approximately(ridges, erosion.RidgeRounding)
                || !Mathf.Approximately(creases, erosion.CreaseRounding) || valleys != erosion.ValleyOctaves
                || !Mathf.Approximately(pond, erosion.MinPondArea) || !Mathf.Approximately(lift, erosion.PeakLift))
            {
                erosion.BaseOctaves = baseOctaves;
                erosion.GullySize = size;
                erosion.Strength = strength;
                erosion.Octaves = octaves;
                erosion.GullyWeight = weight;
                erosion.Detail = detail;
                erosion.Onset = onset;
                erosion.AssumedSlope = assumed;
                erosion.RidgeRounding = ridges;
                erosion.CreaseRounding = creases;
                erosion.ValleyOctaves = valleys;
                erosion.MinPondArea = pond;
                erosion.PeakLift = lift;
                config.NotifyChanged();
            }
            GUILayout.Label("Gullies branch down the mountains, each octave half the size; flat land is left alone. Rivers start in the valleys of the biggest octaves.", frame.Hint);
        }

        // Rivers and lakes (GL307), valley rivers (P12) and soil water (WT1). Shape changes regenerate; soil water is read
        // by the water flow each step, so it changes live.
        private static void DrawWater(LabPanelFrame frame, WorldConfig config, WaterGeneration water, bool eroded)
        {
            GUILayout.Label("Rivers and lakes", frame.Header);
            float riverArea = frame.Slider("River from land drained (m²)", water.RiverArea, 1000f, 200000f, "0");
            float lakeDepth = frame.Slider("Pond from hollow depth (m)", water.LakeMinDepth, 0.1f, 3f, "0.0");
            bool valleyRivers = water.ValleyRivers;
            float valleyDepth = water.ValleyDepth;
            float headArea = water.ValleyHeadArea;
            if (eroded)
            {
                valleyRivers = frame.ToggleField("Rivers start in gully valleys", water.ValleyRivers);
                valleyDepth = frame.Slider("…valley depth (ridge map)", water.ValleyDepth, 0.05f, 0.9f, "0.00");
                headArea = frame.Slider("…land drained at the head (m²)", water.ValleyHeadArea, 200f, 20000f, "0");
            }
            if (!Mathf.Approximately(riverArea, water.RiverArea) || !Mathf.Approximately(lakeDepth, water.LakeMinDepth)
                || valleyRivers != water.ValleyRivers || !Mathf.Approximately(valleyDepth, water.ValleyDepth)
                || !Mathf.Approximately(headArea, water.ValleyHeadArea))
            {
                water.RiverArea = riverArea;
                water.LakeMinDepth = lakeDepth;
                water.ValleyRivers = valleyRivers;
                water.ValleyDepth = valleyDepth;
                water.ValleyHeadArea = headArea;
                config.NotifyChanged();
            }

            water.SoilWater = frame.Slider("Soil water (share of a chunk)", water.SoilWater, 0f, 1f, "0.00");
            water.HoldSoilWaterFull = frame.ToggleField("Keep soil water full (rain stand-in)", water.HoldSoilWaterFull);
            GUILayout.Label("Rivers top up from their chunk's soil water; with it kept full they never run dry. Off, a river drains its chunks and then stops.", frame.Hint);
        }

        private static string Name(VoxelMaterial material)
        {
            return material != null ? material.DisplayName : "None";
        }

        // The noise shape. Each real change regenerates, so dragging reshapes the terrain live.
        private static void DrawShape(LabPanelFrame frame, WorldConfig config, TerrainSettings terrain)
        {
            float worldHeight = config.ChunkSize.y * config.VoxelSize * config.WorldHeightInChunks;
            float surface = frame.Slider("Surface level", terrain.SurfaceLevel, 0f, Mathf.Max(1f, worldHeight), "0.0");
            bool eroded = terrain.Generator == TerrainGeneratorType.ErodedLandscape;
            bool landscape = terrain.Generator == TerrainGeneratorType.Landscape || eroded;
            float amplitude = frame.Slider(landscape ? "Mountain height (m)" : "Amplitude", terrain.Amplitude, 0f,
                Mathf.Max(1f, landscape ? worldHeight : worldHeight * 0.5f), "0.0");
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

            if (landscape)
            {
                GUILayout.Label("Landscape", frame.Header);
                float size = frame.Slider("Mountain width (m)", terrain.FeatureSize, 50f, 800f, "0");
                float coverage = frame.Slider("Mountain coverage", terrain.MountainCoverage, 0f, 1f, "0.00");
                if (!Mathf.Approximately(size, terrain.FeatureSize) || !Mathf.Approximately(coverage, terrain.MountainCoverage))
                {
                    terrain.FeatureSize = size;
                    terrain.MountainCoverage = coverage;
                    config.NotifyChanged();
                }
                GUILayout.Label("Surface level is the plains' height; Frequency isn't used. Mountains taller than the world are cut off: raise World height.", frame.Hint);

                // P7: the far terrain's horizon.
                float distantHeight = frame.Slider("Distant ranges height (m)", terrain.DistantRangeHeight, 0f, 4000f, "0");
                float distantWidth = frame.Slider("Distant ranges width (m)", terrain.DistantRangeWidth, 500f, 20000f, "0");
                float distantStart = frame.Slider("Distant ranges start (m)", terrain.DistantRangeStart, 0f, 20000f, "0");
                if (!Mathf.Approximately(distantHeight, terrain.DistantRangeHeight) || !Mathf.Approximately(distantWidth, terrain.DistantRangeWidth)
                    || !Mathf.Approximately(distantStart, terrain.DistantRangeStart))
                {
                    terrain.DistantRangeHeight = distantHeight;
                    terrain.DistantRangeWidth = distantWidth;
                    terrain.DistantRangeStart = distantStart;
                    config.NotifyChanged();
                }
                GUILayout.Label("Great ranges for the far terrain's horizon: none within Start of the origin, full height from twice it. 0 m: none.", frame.Hint);
            }

            if (eroded)
            {
                DrawErosion(frame, config, terrain.Erosion);
            }
            if (landscape)
            {
                DrawWater(frame, config, terrain.Water, eroded);
            }

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
