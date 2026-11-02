using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Water that answers digging and building (GL313), over the generated rivers and lakes of a <see cref="WaterMap"/>.
    /// A sample is water when it's air and either under the generated water's surface in a wet cell (the map's water), or
    /// flooded: water came into it. Every density change in the <see cref="World"/> wakes the samples round it; an air
    /// sample beside water and below that water's surface floods to that surface, and wakes its own neighbours, so water
    /// runs into a dug hole a sample a step and fills it up to the level it came from (a lake is a source without end).
    /// Ground put where water was takes its place: a flooded sample that turns solid is no longer water, and a wet cell of
    /// the map whose ground is built up to its surface stops being water (<see cref="IsFilled"/>). Nothing here changes
    /// the terrain; the water is only what the water view (Rendering) draws.
    /// </summary>
    public sealed class LiveWater : IDisposable
    {
        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private readonly WaterMap map;
        private readonly float iso;
        private readonly float voxel;
        private readonly Dictionary<Vector3Int, float> flooded = new Dictionary<Vector3Int, float>();
        private readonly Dictionary<int, HashSet<Vector3Int>> floodedByCell = new Dictionary<int, HashSet<Vector3Int>>();
        private readonly HashSet<int> filled = new HashSet<int>();
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> stepping = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();

        public LiveWater(World world, WaterMap map, float iso)
        {
            World = world;
            this.map = map;
            this.iso = iso;
            voxel = world.Grid.VoxelSize;
            world.DensityChanged += Wake;
        }

        public World World { get; }

        /// <summary>Most samples that can flood, so a tunnel dug from a lake can't flood without end.</summary>
        public int MaxFlooded { get; set; } = 150000;

        /// <summary>A map cell (index) whose water changed: something to draw again.</summary>
        public event Action<int> CellChanged;

        /// <summary>Samples water came into, with the height of its surface there.</summary>
        public IReadOnlyDictionary<Vector3Int, float> Flooded => flooded;

        /// <summary>The flooded samples in a map cell, or null for none.</summary>
        public IReadOnlyCollection<Vector3Int> FloodedIn(int cell)
        {
            return floodedByCell.TryGetValue(cell, out HashSet<Vector3Int> samples) ? samples : null;
        }

        /// <summary>Samples waiting to be looked at.</summary>
        public int AwakeCount => awake.Count;

        /// <summary>True when a wet cell of the map has been built up to its water's surface: no water is drawn there.</summary>
        public bool IsFilled(int cell)
        {
            return filled.Contains(cell);
        }

        /// <summary>Wakes a sample and the six round it.</summary>
        public void Wake(Vector3Int sample)
        {
            awake.Add(sample);
            foreach (Vector3Int step in Around)
            {
                awake.Add(sample + step);
            }
        }

        /// <summary>
        /// Wakes every sample of a chunk, when it's near water: a chunk loaded with edits (a saved game) gets its
        /// flooded holes back this way.
        /// </summary>
        public void WakeChunk(Vector3Int coord)
        {
            Vector3Int first = World.Grid.ChunkFirstSample(coord);
            Vector3Int size = World.Grid.ChunkSize;
            Vector3 middle = World.Grid.SampleToWorld(first + size / 2);
            if (!map.TryCell(new Unity.Mathematics.float2(middle.x, middle.z), out int cell)
                || (map.Kind[cell] == 0 && map.LakeDistance[cell] > size.x * voxel && map.RiverDistance[cell] > size.x * voxel))
            {
                return;
            }
            for (int z = 0; z <= size.z; z++)
            {
                for (int y = 0; y <= size.y; y++)
                {
                    for (int x = 0; x <= size.x; x++)
                    {
                        awake.Add(first + new Vector3Int(x, y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Looks at every awake sample, at most <paramref name="max"/> (the rest wait): floods air beside water, dries
        /// flooded samples that turned solid. Returns how many samples changed. Water that came in wakes its neighbours for
        /// the next step, so it spreads a sample a step.
        /// </summary>
        public int Step(int max = 6000)
        {
            if (awake.Count == 0)
            {
                return 0;
            }
            batch.Clear();
            batch.AddRange(awake);
            HashSet<Vector3Int> now = awake;
            awake = stepping;
            awake.Clear();
            stepping = now;
            int changed = 0;
            for (int i = 0; i < batch.Count; i++)
            {
                if (i >= max)
                {
                    awake.Add(batch[i]);
                    continue;
                }
                if (Look(batch[i]))
                {
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>The height of the water's surface over a sample, or null when the sample isn't water.</summary>
        public float? LevelAt(Vector3Int sample)
        {
            if (flooded.TryGetValue(sample, out float level))
            {
                return level;
            }
            Vector3 position = World.Grid.SampleToWorld(sample);
            if (!map.TryCell(new Unity.Mathematics.float2(position.x, position.z), out int cell) || map.Kind[cell] == 0
                || filled.Contains(cell) || position.y >= map.Surface[cell])
            {
                return null;
            }
            float? density = World.GetDensity(sample);
            return density.HasValue && density.Value < iso ? map.Surface[cell] : (float?)null;
        }

        private bool Look(Vector3Int sample)
        {
            float? density = World.GetDensity(sample);
            if (!density.HasValue)
            {
                return false;
            }
            Vector3 position = World.Grid.SampleToWorld(sample);
            int cell = CellOf(position);
            bool changed = CheckFilled(cell);
            if (density.Value >= iso)
            {
                // Ground where water was takes its place.
                if (flooded.Remove(sample))
                {
                    if (floodedByCell.TryGetValue(cell, out HashSet<Vector3Int> inCell))
                    {
                        inCell.Remove(sample);
                    }
                    CellChanged?.Invoke(cell);
                    return true;
                }
                return changed;
            }
            if (flooded.ContainsKey(sample) || flooded.Count >= MaxFlooded || LevelAt(sample).HasValue)
            {
                return changed;
            }
            float best = float.NegativeInfinity;
            foreach (Vector3Int step in Around)
            {
                float? level = LevelAt(sample + step);
                if (level.HasValue && level.Value > position.y && level.Value > best)
                {
                    best = level.Value;
                }
            }
            if (float.IsNegativeInfinity(best))
            {
                return changed;
            }
            flooded[sample] = best;
            if (!floodedByCell.TryGetValue(cell, out HashSet<Vector3Int> samples))
            {
                samples = new HashSet<Vector3Int>();
                floodedByCell[cell] = samples;
            }
            samples.Add(sample);
            foreach (Vector3Int step in Around)
            {
                awake.Add(sample + step);
            }
            if (cell >= 0)
            {
                CellChanged?.Invoke(cell);
            }
            return true;
        }

        // A wet cell of the map is filled once the ground at its middle, just under the water's surface, is solid.
        private bool CheckFilled(int cell)
        {
            if (cell < 0 || map.Kind[cell] == 0)
            {
                return false;
            }
            float cellSize = map.CellSize;
            var middle = new Vector3(
                map.Origin.x + (cell % map.Size.x + 0.5f) * cellSize,
                map.Surface[cell] - voxel * 0.5f,
                map.Origin.y + (cell / map.Size.x + 0.5f) * cellSize);
            float? density = World.GetDensity(World.Grid.WorldToNearestSample(middle));
            bool full = density.HasValue && density.Value >= iso;
            bool was = filled.Contains(cell);
            if (full == was)
            {
                return false;
            }
            if (full)
            {
                filled.Add(cell);
            }
            else
            {
                filled.Remove(cell);
            }
            CellChanged?.Invoke(cell);
            return true;
        }

        /// <summary>The map cell holding a position, or -1 outside the map.</summary>
        public int CellOf(Vector3 position)
        {
            return map.TryCell(new Unity.Mathematics.float2(position.x, position.z), out int cell) ? cell : -1;
        }

        public void Dispose()
        {
            World.DensityChanged -= Wake;
        }
    }
}
