using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Water that moves (GL313, GL315): a cellular flow over the water stored per sample in the <see cref="World"/>
    /// (<see cref="World.GetWater"/>, 0-1 per sample). Each step every awake sample's water first falls into the sample
    /// below while there's room, the way water falls down a drop; what's left spreads sideways, sharing out with lower
    /// neighbours, and only towards an edge with a drop under it when there is one, so a stream runs down the slope
    /// instead of fanning out over a hilltop, and keeps going until it meets other water or fills a hollow. Water that
    /// is too thin dries up. The generated rivers and lakes (<see cref="WaterFill"/>) are sources without end: their
    /// samples are kept topped up to the map's level, so a hole dug beside a lake fills up to the lake's surface.
    /// Ground put where water was takes its place: a sample that turns solid loses its water. Every density change
    /// wakes the samples round it; a sample that changed wakes its own neighbours for the next step.
    /// </summary>
    public sealed class LiveWater : IDisposable
    {
        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private static readonly Vector3Int[] Sideways =
        {
            Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>Less water than this, outside a source, dries up.</summary>
        public const float MinAmount = 0.03f;

        /// <summary>Thinner water than this doesn't spread sideways (it can still fall).</summary>
        public const float MinSpread = 0.06f;

        /// <summary>Sideways flow stops once neighbours are this close, so the water settles.</summary>
        public const float SettleDifference = 0.015f;

        private readonly WaterMap map;
        private readonly float iso;
        private readonly float voxel;
        private readonly HashSet<Vector3Int> flowing = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> stepping = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();
        private readonly List<Vector3Int> targets = new List<Vector3Int>();

        public LiveWater(World world, WaterMap map, float iso)
        {
            World = world;
            this.map = map;
            this.iso = iso;
            voxel = world.Grid.VoxelSize;
            world.DensityChanged += Wake;
        }

        public World World { get; }

        /// <summary>Most samples of moving (not generated) water, so a tunnel dug from a lake can't flood without end.</summary>
        public int MaxFlowing { get; set; } = 150000;

        /// <summary>Samples holding water that came from somewhere (not the generated rivers and lakes).</summary>
        public IReadOnlyCollection<Vector3Int> Flowing => flowing;

        /// <summary>Samples waiting to be looked at.</summary>
        public int AwakeCount => awake.Count;

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
        /// Wakes every sample of a chunk that holds water or is near the map's water: a chunk loaded with edits (a saved
        /// game, or one streamed back in) gets its flooded holes back this way.
        /// </summary>
        public void WakeChunk(Vector3Int coord)
        {
            if (!World.TryGetChunk(coord, out Chunk chunk))
            {
                return;
            }
            Vector3Int first = World.Grid.ChunkFirstSample(coord);
            Vector3Int size = World.Grid.ChunkSize;
            Vector3 middle = World.Grid.SampleToWorld(first + size / 2);
            bool near = map.IsCreated && map.TryCell(new Unity.Mathematics.float2(middle.x, middle.z), out int cell)
                && (map.Kind[cell] != 0 || map.LakeDistance[cell] <= size.x * voxel || map.RiverDistance[cell] <= size.x * voxel);
            if (!near && !chunk.HasWater)
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
        /// Looks at awake samples, lowest first, at most <paramref name="max"/>, and when <paramref name="budgetMs"/> is
        /// above 0 only for that long (a big spill then moves more slowly rather than stalling the frame); the rest wait.
        /// Returns how many samples changed.
        /// </summary>
        public int Step(int max = 5000, float budgetMs = 0f)
        {
            if (awake.Count == 0)
            {
                return 0;
            }
            timer.Restart();
            ownerCoord = new Vector3Int(int.MinValue, 0, 0);
            ownerChunk = null;
            batch.Clear();
            batch.AddRange(awake);
            HashSet<Vector3Int> now = awake;
            awake = stepping;
            awake.Clear();
            stepping = now;
            // Lowest first, so water lands before what's above it looks.
            batch.Sort(ByHeight);
            int changed = 0;
            int done = batch.Count;
            for (int i = 0; i < batch.Count; i++)
            {
                if (i >= max || (budgetMs > 0f && (i & 63) == 63 && timer.Elapsed.TotalMilliseconds > budgetMs))
                {
                    done = i;
                    break;
                }
                if (Look(batch[i]))
                {
                    changed++;
                }
            }
            for (int i = done; i < batch.Count; i++)
            {
                awake.Add(batch[i]);
            }
            return changed;
        }

        private static readonly Comparison<Vector3Int> ByHeight = (a, b) => a.y.CompareTo(b.y);
        private readonly System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        // The last chunk looked up, kept for one step (no chunk unloads during a step): most lookups are its neighbours.
        private Vector3Int ownerCoord;
        private Chunk ownerChunk;

        private bool Owner(Vector3Int sample, out Chunk chunk, out Vector3Int local)
        {
            Vector3Int size = World.Grid.ChunkSize;
            var coord = new Vector3Int(
                WorldGrid.FloorDiv(sample.x, size.x), WorldGrid.FloorDiv(sample.y, size.y), WorldGrid.FloorDiv(sample.z, size.z));
            local = sample - Vector3Int.Scale(coord, size);
            if (coord != ownerCoord || ownerChunk == null)
            {
                ownerCoord = coord;
                World.TryGetChunk(coord, out ownerChunk);
            }
            chunk = ownerChunk;
            return chunk != null;
        }

        /// <summary>How full of generated (source) water a sample is, 0 when none: the map's river or lake there.</summary>
        public float SourceAt(Vector3Int sample)
        {
            return WaterFill.GeneratedAt(map, World.Grid.SampleToWorld(sample), voxel);
        }

        private bool Look(Vector3Int sample)
        {
            if (!Owner(sample, out Chunk chunk, out Vector3Int local))
            {
                return false;
            }
            float a = chunk.GetWater(local);
            if (chunk.GetDensity(local) >= iso)
            {
                // Ground where water was takes its place.
                if (a > 0f)
                {
                    Set(sample, 0f, 0f);
                    WakeAround(sample);
                    return true;
                }
                return false;
            }
            float source = SourceAt(sample);
            float start = a;
            if (source > a)
            {
                a = source;
            }
            if (a <= 0f)
            {
                return false;
            }

            // Falls first, as much as fits below.
            Vector3Int below = sample + Vector3Int.down;
            bool belowOpen = IsOpen(below, out float underWater);
            if (belowOpen && underWater < 1f)
            {
                float fall = Mathf.Min(a, 1f - underWater);
                if (fall > 0.004f && CanFlowInto(below, underWater))
                {
                    Set(below, underWater + fall, SourceAt(below));
                    a = Mathf.Max(source, a - fall);
                    WakeAround(below);
                }
            }

            // Then spreads sideways, from what's left, once it stands on something.
            belowOpen = IsOpen(below, out underWater);
            if (a >= MinSpread && (!belowOpen || underWater > 0.98f))
            {
                Spread(sample, ref a, source);
            }

            if (source <= 0f && a < MinAmount)
            {
                a = 0f;
            }
            if (Mathf.Abs(a - start) < 0.002f)
            {
                return false;
            }
            Set(sample, a, source);
            WakeAround(sample);
            return true;
        }

        // Shares water with the lower neighbours beside it; when any of them is an edge with a drop under it, only with
        // those, so the water heads downhill.
        private void Spread(Vector3Int sample, ref float a, float source)
        {
            targets.Clear();
            bool anyDrop = false;
            foreach (Vector3Int step in Sideways)
            {
                Vector3Int next = sample + step;
                if (!IsOpen(next, out float there) || there >= a - SettleDifference || !CanFlowInto(next, there))
                {
                    continue;
                }
                bool drop = IsOpen(next + Vector3Int.down, out float under) && under < 0.98f;
                if (drop && !anyDrop)
                {
                    anyDrop = true;
                    targets.Clear();
                }
                if (drop == anyDrop)
                {
                    targets.Add(next);
                }
            }
            if (!anyDrop && targets.Count > 0 && TowardsDrop(sample))
            {
                anyDrop = true;
            }
            if (targets.Count == 0)
            {
                return;
            }
            float share = a;
            foreach (Vector3Int next in targets)
            {
                IsOpen(next, out float there);
                float give = Mathf.Min(1f - there, anyDrop ? share / (targets.Count + 1) : (share - there) / (targets.Count + 1));
                if (give <= 0.004f)
                {
                    continue;
                }
                Set(next, there + give, SourceAt(next));
                a = Mathf.Max(source, a - give);
                WakeAround(next);
            }
        }

        /// <summary>How far along the floor water looks for a drop to head for, like Minecraft's water.</summary>
        public const int DropSearch = 5;

        private readonly Queue<(Vector3Int sample, int first, int depth)> search = new Queue<(Vector3Int, int, int)>();
        private readonly HashSet<Vector3Int> searched = new HashSet<Vector3Int>();

        // Looks along the floor for the nearest drop within DropSearch samples; when there is one, keeps only the
        // targets that start the shortest ways to it, so the water heads for the edge instead of spreading.
        private bool TowardsDrop(Vector3Int sample)
        {
            search.Clear();
            searched.Clear();
            searched.Add(sample);
            for (int i = 0; i < Sideways.Length; i++)
            {
                Vector3Int next = sample + Sideways[i];
                if (targets.Contains(next))
                {
                    searched.Add(next);
                    search.Enqueue((next, i, 1));
                }
            }
            int found = -1;
            int firsts = 0;
            while (search.Count > 0)
            {
                (Vector3Int at, int first, int depth) = search.Dequeue();
                if (found >= 0 && depth > found)
                {
                    break;
                }
                if (IsOpen(at + Vector3Int.down, out float under) && under < 0.98f)
                {
                    found = depth;
                    firsts |= 1 << first;
                    continue;
                }
                if (depth >= DropSearch)
                {
                    continue;
                }
                foreach (Vector3Int step in Sideways)
                {
                    Vector3Int next = at + step;
                    if (searched.Add(next) && IsOpen(next, out _))
                    {
                        search.Enqueue((next, first, depth + 1));
                    }
                }
            }
            if (found < 0)
            {
                return false;
            }
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                int direction = System.Array.IndexOf(Sideways, targets[i] - sample);
                if ((firsts & (1 << direction)) == 0)
                {
                    targets.RemoveAt(i);
                }
            }
            return targets.Count > 0;
        }

        // True when a sample is loaded air; its water comes back in amount.
        private bool IsOpen(Vector3Int sample, out float amount)
        {
            if (Owner(sample, out Chunk chunk, out Vector3Int local) && chunk.GetDensity(local) < iso)
            {
                amount = chunk.GetWater(local);
                return true;
            }
            amount = 0f;
            return false;
        }

        // New water can go in while there's room under the cap on moving water.
        private bool CanFlowInto(Vector3Int sample, float amount)
        {
            return amount > 0f || flowing.Count < MaxFlowing;
        }

        private void Set(Vector3Int sample, float amount, float source)
        {
            World.SetWater(sample, amount);
            if (amount > 0f && amount > source + 0.002f)
            {
                flowing.Add(sample);
            }
            else
            {
                flowing.Remove(sample);
            }
        }

        private void WakeAround(Vector3Int sample)
        {
            Wake(sample);
            awake.Add(sample + Vector3Int.up + Vector3Int.right);
            awake.Add(sample + Vector3Int.up + Vector3Int.left);
            awake.Add(sample + Vector3Int.up + new Vector3Int(0, 0, 1));
            awake.Add(sample + Vector3Int.up + new Vector3Int(0, 0, -1));
        }

        public void Dispose()
        {
            World.DensityChanged -= Wake;
        }
    }
}
