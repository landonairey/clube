using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// A test ground for materials (GL89): levels a pad of ground in front of the player, sunk to
    /// the lowest ground under it (filled below, cleared above) and pours a pile of each material that drops an item onto it, in
    /// rows by kind: the ores lined up in the first row, then the other ground, then plants. Break
    /// stages (cracked, loose) and worktops are left out. Each pile is shaped as its material piles
    /// (<see cref="TerrainPile"/>). All through the world's edit paths, so the chunks rebuild like any edit.
    /// </summary>
    public static class DebugMaterialChunk
    {
        // Metres between piles in a row and between rows; the margin round them (the pad is sunk to its lowest
        // ground, so piles stay where they're poured); how far the pad is cleared
        // above the ground and filled below.
        // Two build cells apart (1 m each), so block piles (ores, stone) each fill their own cell with a gap between.
        private const float Spacing = 2f;
        private const float RowGap = 2.5f;
        private const float Margin = 2f;
        private const float Clear = 4f;
        private const float Fill = 1f;

        // Samples poured into each pile.
        private const int PileSamples = 32;

        /// <summary>Makes the pad in front of <paramref name="player"/>; returns what it did, for the panel.</summary>
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
            List<List<VoxelMaterial>> rows = Rows(registry);
            int longest = 1;
            foreach (List<VoxelMaterial> row in rows)
            {
                longest = Mathf.Max(longest, row.Count);
            }
            float width = (longest - 1) * Spacing + Margin * 2f;
            float depth = Mathf.Max(0, rows.Count - 1) * RowGap + Margin * 2f;

            // The pad starts a metre ahead of the player, at the lowest ground under it, so all round it the
            // ground is level or higher and a pile never finds a lower cell off the edge to settle in.
            Vector3 feet = view.transform.InverseTransformPoint(player.position);
            float level = view.Raycast(new Ray(player.position + Vector3.up, Vector3.down), out Vector3 ground)
                ? view.transform.InverseTransformPoint(ground).y
                : feet.y;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 near = new Vector3(feet.x, level, feet.z) + forward * 1f;
            float highest = level;
            if (view.Generator != null)
            {
                float lowest = float.PositiveInfinity;
                highest = float.NegativeInfinity;
                for (float across = -width * 0.5f; across <= width * 0.5f; across += 1f)
                {
                    for (float along = 0f; along <= depth; along += 1f)
                    {
                        Vector3 at = near + right * across + forward * along;
                        float height = TerrainSurface.HeightAt(view.Generator, at.x, at.z, 0f, level + 64f);
                        lowest = Mathf.Min(lowest, height);
                        highest = Mathf.Max(highest, height);
                    }
                }
                level = lowest;
                near.y = level;
            }

            Level(world, registry, near, forward, right, width, depth, level, Mathf.Max(Clear, highest - level + 2f), voxel, iso);

            // A pile of each material, row by row away from the player, each row centred.
            var grid = new BuildGrid(view.Config.BuildCellSize);
            int piled = 0;
            int total = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                List<VoxelMaterial> row = rows[r];
                Vector3 rowStart = near + forward * (Margin + r * RowGap) - right * ((row.Count - 1) * Spacing * 0.5f);
                for (int i = 0; i < row.Count; i++)
                {
                    total++;
                    Vector3 at = CellCentre(rowStart + right * (i * Spacing), grid.CellSize) + Vector3.up * voxel;
                    if (TerrainPile.Place(world, at, row[i].Id, PileSamples, iso, PileSettings.For(row[i], grid)) > 0)
                    {
                        piled++;
                    }
                }
            }
            string ores = rows.Count > 0 ? string.Join(", ", rows[0].ConvertAll(m => m.DisplayName)) : "none";
            return $"Levelled a {width:0} x {depth:0} m pad and piled {piled} of {total} materials. Front row, the ores: {ores}.";
        }

        // The materials to show, by row: ores, other ground, plants. Only those that drop an item, leaving
        // out break stages (what another material breaks into) and worktops.
        private static List<List<VoxelMaterial>> Rows(MaterialRegistry registry)
        {
            var stages = new HashSet<VoxelMaterial>();
            var ores = new List<VoxelMaterial>();
            var aggregates = new List<VoxelMaterial>();
            var plants = new List<VoxelMaterial>();
            if (registry != null)
            {
                foreach (VoxelMaterial material in registry.Materials)
                {
                    for (VoxelMaterial stage = material != null ? material.BreaksInto : null; stage != null && stages.Add(stage); stage = stage.BreaksInto)
                    {
                    }
                }
                foreach (VoxelMaterial material in registry.Materials)
                {
                    if (material == null || material.Drop == null || material.IsWorktop || stages.Contains(material))
                    {
                        continue;
                    }
                    switch (material.Category)
                    {
                        case VoxelMaterialCategory.Ore:
                            ores.Add(material);
                            break;
                        case VoxelMaterialCategory.Plant:
                            plants.Add(material);
                            break;
                        default:
                            aggregates.Add(material);
                            break;
                    }
                }
            }
            var rows = new List<List<VoxelMaterial>>();
            foreach (List<VoxelMaterial> row in new[] { ores, aggregates, plants })
            {
                if (row.Count > 0)
                {
                    rows.Add(row);
                }
            }
            return rows;
        }

        // Fills below the level and clears above it over the pad, a rectangle starting at near.
        private static void Level(World world, MaterialRegistry registry, Vector3 near, Vector3 forward, Vector3 right,
            float width, float depth, float level, float clear, float voxel, float iso)
        {
            byte fillMaterial = FindId(registry, "Dirt");
            int levelSample = Mathf.RoundToInt(level / voxel);
            int below = Mathf.RoundToInt(Fill / voxel);
            int above = Mathf.RoundToInt(clear / voxel);
            Vector3 centre = near + forward * (depth * 0.5f);
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            // Every sample whose column lies inside the (possibly turned) rectangle.
            float reach = Mathf.Sqrt(halfWidth * halfWidth + halfDepth * halfDepth);
            Vector3Int middle = world.Grid.WorldToNearestSample(centre);
            int span = Mathf.CeilToInt(reach / voxel);
            for (int z = -span; z <= span; z++)
            {
                for (int x = -span; x <= span; x++)
                {
                    Vector3 offset = new Vector3(middle.x + x, 0f, middle.z + z) * voxel - new Vector3(centre.x, 0f, centre.z);
                    if (Mathf.Abs(Vector3.Dot(offset, right)) > halfWidth || Mathf.Abs(Vector3.Dot(offset, forward)) > halfDepth)
                    {
                        continue;
                    }
                    for (int y = -below; y <= above; y++)
                    {
                        var sample = new Vector3Int(middle.x + x, levelSample + y, middle.z + z);
                        bool solid = y <= 0;
                        float? density = world.GetDensity(sample);
                        if (!density.HasValue)
                        {
                            continue;
                        }
                        // The floor's top layer is all one material, so the pad reads cleanly; below, only filled air takes it.
                        if (y == 0 || (solid && density.Value < iso))
                        {
                            world.SetMaterial(sample, fillMaterial);
                        }
                        world.SetDensity(sample, solid ? 1f : 0f);
                    }
                }
            }
        }

        // The middle of the build cell a point is in, so a block pile fills one cell.
        private static Vector3 CellCentre(Vector3 point, float cell)
        {
            return new Vector3((Mathf.Floor(point.x / cell) + 0.5f) * cell, point.y, (Mathf.Floor(point.z / cell) + 0.5f) * cell);
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
