using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// A test ground for materials (GL89): levels a square of ground in front of the player at their
    /// feet (filled below, cleared above) and pours a pile of every material that drops an item onto
    /// it, in rows, each shaped as its material piles (<see cref="TerrainPile"/>). All through the
    /// world's edit paths, so the chunks rebuild like any edit.
    /// </summary>
    public static class DebugMaterialChunk
    {
        // Metres across the square, how far it's cleared above the ground and filled below.
        private const float Size = 10f;
        private const float Clear = 4f;
        private const float Fill = 1f;

        // Metres between piles, and samples poured into each.
        private const float Spacing = 1.75f;
        private const int PileSamples = 40;

        /// <summary>Makes the square in front of <paramref name="player"/>; returns what it did, for the panel.</summary>
        public static string Make(WorldView view, Transform player)
        {
            if (view == null || !view.IsReady || player == null)
            {
                return "No world to build on.";
            }
            World world = view.World;
            float voxel = world.Grid.VoxelSize;
            float iso = view.Config.IsoLevel;
            MaterialRegistry registry = view.Config.Materials;

            // The ground under the player's feet sets the level; the square starts a metre ahead.
            Vector3 feet = view.transform.InverseTransformPoint(player.position);
            float level = view.Raycast(new Ray(player.position + Vector3.up, Vector3.down), out Vector3 ground)
                ? view.transform.InverseTransformPoint(ground).y
                : feet.y;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 centre = feet + forward * (1f + Size * 0.5f);

            byte fillMaterial = FindId(registry, "Dirt");
            int levelSample = Mathf.RoundToInt(level / voxel);
            int half = Mathf.RoundToInt(Size * 0.5f / voxel);
            int below = Mathf.RoundToInt(Fill / voxel);
            int above = Mathf.RoundToInt(Clear / voxel);
            Vector3Int middle = world.Grid.WorldToNearestSample(new Vector3(centre.x, level, centre.z));
            for (int z = -half; z <= half; z++)
            {
                for (int x = -half; x <= half; x++)
                {
                    for (int y = -below; y <= above; y++)
                    {
                        var sample = new Vector3Int(middle.x + x, levelSample + y, middle.z + z);
                        bool solid = y <= 0;
                        float? density = world.GetDensity(sample);
                        if (!density.HasValue)
                        {
                            continue;
                        }
                        if (solid && density.Value < iso)
                        {
                            world.SetMaterial(sample, fillMaterial);
                        }
                        world.SetDensity(sample, solid ? 1f : 0f);
                    }
                }
            }

            // A pile of each material that drops an item, row by row across the square.
            var materials = new List<VoxelMaterial>();
            if (registry != null)
            {
                foreach (VoxelMaterial material in registry.Materials)
                {
                    if (material != null && material.Drop != null)
                    {
                        materials.Add(material);
                    }
                }
            }
            int perRow = Mathf.Max(1, Mathf.FloorToInt((Size - 1f) / Spacing) + 1);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 start = new Vector3(centre.x, level, centre.z) - right * (Spacing * (perRow - 1) * 0.5f) - forward * (Size * 0.5f - 0.75f);
            var grid = new BuildGrid(view.Config.BuildCellSize);
            int piled = 0;
            for (int i = 0; i < materials.Count; i++)
            {
                Vector3 at = start + right * (Spacing * (i % perRow)) + forward * (Spacing * (i / perRow)) + Vector3.up * voxel;
                if (TerrainPile.Place(world, at, materials[i].Id, PileSamples, iso, PileSettings.For(materials[i], grid)) > 0)
                {
                    piled++;
                }
            }
            return $"Levelled a {Size:0} m square and piled {piled} of {materials.Count} materials.";
        }

        private static byte FindId(MaterialRegistry registry, string name)
        {
            if (registry != null)
            {
                foreach (VoxelMaterial material in registry.Materials)
                {
                    if (material != null && material.DisplayName == name)
                    {
                        return material.Id;
                    }
                }
            }
            return 0;
        }
    }
}
