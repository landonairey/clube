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
    /// is thin stays put rather than spreading further, and never vanishes: volume is kept (GL323). The generated rivers and lakes (<see cref="WaterFill"/>) are sources without end: their
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

        /// <summary>Most samples water falls through empty air in one step (GL326): 2 m at 0.25 m voxels.</summary>
        public const int FallReach = 8;

        /// <summary>One step of water, a sixteenth of a sample (GL332): the least a sample holds or a move carries.</summary>
        public const float Quantum = 1f / Chunk.WaterLevels;

        /// <summary>Less water than this doesn't move sideways at all (it can still fall).</summary>
        public const float MinAmount = Quantum;

        /// <summary>
        /// Thinner water than this (two steps) doesn't spread over flat ground, and a spread never leaves a film thinner
        /// than it, so a puddle stops at its edge instead of thinning out for ever (GL332); one step of water joins the
        /// wettest water beside it.
        /// </summary>
        public const float MinSpread = 2f * Quantum;

        /// <summary>Sideways flow stops once neighbours are this close, so the water settles.</summary>
        public const float SettleDifference = 0.015f;

        private readonly WaterMap map;
        private readonly float iso;
        private readonly float voxel;
        private readonly HashSet<Vector3Int> flowing = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> stepping = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();
        private readonly List<Vector3Int> carry = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> carried = new HashSet<Vector3Int>();
        private Comparison<Vector3Int> byNearness;
        private readonly List<Vector3Int> targets = new List<Vector3Int>();

        public LiveWater(World world, WaterMap map, float iso)
        {
            World = world;
            this.map = map;
            this.iso = iso;
            voxel = world.Grid.VoxelSize;
            world.DensityChanged += OnGroundChanged;
        }

        public World World { get; }

        /// <summary>Most samples of moving (not generated) water, so a tunnel dug from a lake can't flood without end.</summary>
        public int MaxFlowing { get; set; } = 150000;

        /// <summary>Samples holding water that came from somewhere (not the generated rivers and lakes).</summary>
        public IReadOnlyCollection<Vector3Int> Flowing => flowing;

        /// <summary>Samples waiting to be looked at.</summary>
        public int AwakeCount => awake.Count + carry.Count;

        /// <summary>Wakes a sample and the six round it.</summary>
        public void Wake(Vector3Int sample)
        {
            awake.Add(sample);
            foreach (Vector3Int step in Around)
            {
                awake.Add(sample + step);
            }
        }

        // Ground dug or built: only water beside the change can move because of it, so dry ground wakes nothing (digging
        // a big pit once woke hundreds of thousands of dry samples, and sorting them each step cost 150 ms a frame).
        private void OnGroundChanged(Vector3Int sample)
        {
            if (HasWater(sample))
            {
                Wake(sample);
                return;
            }
            foreach (Vector3Int step in Around)
            {
                if (HasWater(sample + step))
                {
                    Wake(sample);
                    return;
                }
            }
        }

        private bool HasWater(Vector3Int sample)
        {
            return World.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) && chunk.HasWater && chunk.GetWater(local) > 0f;
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
            if (awake.Count == 0 && carry.Count == 0)
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
            // What the last step had no time for joins in; then nearest the focus first, lowest first (ByNearness).
            if (carry.Count > 0)
            {
                carried.Clear();
                carried.UnionWith(batch);
                foreach (Vector3Int sample in carry)
                {
                    if (carried.Add(sample))
                    {
                        batch.Add(sample);
                    }
                }
                carry.Clear();
            }
            batch.Sort(byNearness ??= ByNearness);
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
                carry.Add(batch[i]);
            }
            return changed;
        }

        // Nearest the focus first (in 8 m bands), so a long spill far off never holds up the water by the player; lowest
        // first within a band, so water lands before what's above it looks.
        private int ByNearness(Vector3Int a, Vector3Int b)
        {
            int da = Band(a), db = Band(b);
            return da != db ? da.CompareTo(db) : a.y.CompareTo(b.y);
        }

        private int Band(Vector3Int sample)
        {
            Vector3Int d = sample - Focus;
            return (Mathf.Abs(d.x) + Mathf.Abs(d.y) + Mathf.Abs(d.z)) / 32;
        }

        /// <summary>The sample the flow works out from first: the player's (set each frame by the water view).</summary>
        public Vector3Int Focus { get; set; }
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
                // Into empty air it drops up to FallReach samples at once (GL326), so water over a hole dug under it
                // collapses into it straight away, like sand; through water already falling it goes a sample a step,
                // so a waterfall stays one unbroken column.
                Vector3Int into = below;
                float there = underWater;
                if (there <= 0f)
                {
                    for (int i = 1; i < FallReach && IsOpen(into + Vector3Int.down, out float next) && next <= 0f; i++)
                    {
                        into += Vector3Int.down;
                    }
                }
                float fall = Steps(Mathf.Min(a, 1f - there));
                if (fall > 0.004f && CanFlowInto(into, there))
                {
                    Set(into, there + fall, SourceAt(into));
                    a = Mathf.Max(source, a - fall);
                    WakeAround(into);
                }
            }

            // Then spreads sideways, from what's left, once it stands on something.
            belowOpen = IsOpen(below, out underWater);
            bool standing = !belowOpen || underWater > 0.98f;
            if (a >= MinAmount && standing)
            {
                Spread(sample, ref a, source);
            }
            if (standing && a > 0f && a < MinSpread && source <= 0f)
            {
                Gather(sample, ref a);
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
                // A drop counts as downhill only where the ground falls steeply enough (GL332); a one-sample ledge in
                // ground close to flat is just a step of the staircase the voxels make of it.
                bool drop = IsOpen(next + Vector3Int.down, out float under) && under < 0.98f && IsSteep(sample, step, next);
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
            bool overEdge = anyDrop;
            if (!anyDrop && targets.Count > 0 && TowardsDrop(sample))
            {
                anyDrop = true;
            }
            // Thin water only follows a drop downhill; it spreads out over flat ground only while it has some depth.
            if (targets.Count == 0 || (!anyDrop && a < MinSpread))
            {
                return;
            }
            // Downhill, water holds together (GL325): it takes one way, not every lower one, so a spill runs in veins
            // like rain down a window instead of fanning out. Only when that way is full does the rest find another.
            if (anyDrop && targets.Count > 1)
            {
                Vector3Int path = PickPath();
                targets.Clear();
                targets.Add(path);
            }
            // On the flat, lowest neighbour first, each evening out with what's left.
            if (!anyDrop && targets.Count > 1)
            {
                targets.Sort(byWater ??= ByWater);
            }
            // A lone trickle moves whole down its way; water with more coming in behind it is a stream (GL330), which
            // evens out with the sample ahead and keeps its depth. Moved whole too, a fed trench's water leapfrogged
            // along the floor as beads with dry floor between them.
            bool whole = anyDrop && !IsFed(sample, targets, a);
            float share = a;
            for (int t = 0; t < targets.Count; t++)
            {
                Vector3Int next = targets[t];
                IsOpen(next, out float there);
                // A trickle heading downhill, over an edge or for one, goes all along its one way (GL323, GL325), so it
                // stays together: kept back, the rest found the way just wetted no lower than itself and started a
                // second vein. A stream, and water on the flat, evens out with the lower neighbours.
                // On the flat it evens out with one neighbour at a time, so neighbours end no more than a step apart
                // (shared out in one go, whole steps rounded every share to nothing while three steps apart, GL332).
                float give = Mathf.Min(1f - there, whole ? share / targets.Count : (a - there) / 2f);
                // A step apart, evening out would only swap the two, so water stops there; but water pressed from above
                // (more water over it, or a river's) pushes its last step on, so ponds and dug holes fill level to their
                // source instead of a step lower for each sample away from it (GL332).
                if (!whole && give < Quantum && a - there > Quantum * 0.5f && (source > 0f || IsOpen(sample + Vector3Int.up, out float pressing) && pressing > 0f))
                {
                    give = Mathf.Min(1f - there, Quantum);
                }
                // A trickle heading downhill takes a remainder too thin to move on its own along (GL324), so it leaves no
                // film behind it; a fed stream keeps its film (GL332).
                float rest = a - give - source;
                if (whole && t == targets.Count - 1 && rest > 0f && rest < MinSpread)
                {
                    give = Mathf.Min(1f - there, give + rest);
                }
                // On the flat, water spreads only into a sample it leaves with some depth, so a puddle's edge never
                // thins into specks.
                if (give <= 0.004f || (!anyDrop && there + give < MinSpread))
                {
                    continue;
                }
                // Over an edge the water goes straight down the face (GL320): into the open sample under the edge,
                // rather than out into the air beside the edge first, where it stood as a floating lip a step.
                Vector3Int into = next;
                if (overEdge && IsOpen(next + Vector3Int.down, out float under) && under < 1f)
                {
                    into = next + Vector3Int.down;
                    there = under;
                    give = Mathf.Min(give, 1f - under);
                }
                give = Steps(give);
                if (give <= 0f)
                {
                    continue;
                }
                Set(into, there + give, SourceAt(into));
                a = Mathf.Max(source, a - give);
                WakeAround(into);
            }
        }

        // True when more water comes in behind a sample: water above it, or beside it other than where it's going.
        // A neighbour feeds it only while it holds at least as much (GL332): thin leftovers side by side on a slope counted
        // each other as feeding and crept down as films instead of running on.
        private bool IsFed(Vector3Int sample, List<Vector3Int> going, float a)
        {
            if (IsOpen(sample + Vector3Int.up, out float above) && above > 0.004f)
            {
                return true;
            }
            foreach (Vector3Int step in Sideways)
            {
                Vector3Int next = sample + step;
                if (!going.Contains(next) && IsOpen(next, out float there) && there > 0.004f && there >= a)
                {
                    return true;
                }
            }
            return false;
        }

        // The one way downhill water takes (GL325): where water already runs (so streams gather into shared veins), then
        // the deepest fall (the steepest way down), then a fixed hash of the place, so veins wander but never flicker
        // between two ways.
        private Vector3Int PickPath()
        {
            Vector3Int best = targets[0];
            float bestScore = float.MinValue;
            foreach (Vector3Int next in targets)
            {
                IsOpen(next, out float there);
                float score = there > 0.004f ? 8f : 0f;
                int fall = 0;
                for (Vector3Int below = next + Vector3Int.down; fall < 4 && IsOpen(below, out float under); below += Vector3Int.down)
                {
                    if (fall == 0 && under > 0.004f)
                    {
                        score += 8f;
                    }
                    fall++;
                }
                score += fall + Hash01(next) * 0.9f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = next;
                }
            }
            return best;
        }

        // Water is stored in sixteenths (Chunk.WaterLevels, GL332). A move of whole steps changes both samples exactly; any
        // other was rounded on each side, and the many small moves of a spill made water out of nothing (GL325: a 100-unit
        // pour grew to 137).
        private static float Steps(float amount)
        {
            return amount <= 0f ? 0f : Mathf.Floor(amount * Chunk.WaterLevels + 1e-3f) * Quantum;
        }

        private static float Hash01(Vector3Int p)
        {
            unchecked
            {
                uint h = (uint)p.x * 73856093u ^ (uint)p.y * 19349663u ^ (uint)p.z * 83492791u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // Water too thin to spread joins the wettest standing water beside it (GL324), so what a spill leaves on the flat
        // gathers into puddles instead of lying about as specks. Ties go one fixed way, so two specks never swap back and
        // forth.
        private void Gather(Vector3Int sample, ref float a)
        {
            Vector3Int best = sample;
            float bestAmount = a;
            foreach (Vector3Int step in Sideways)
            {
                Vector3Int next = sample + step;
                if (!IsOpen(next, out float there) || there <= 0f || there >= 1f)
                {
                    continue;
                }
                if (IsOpen(next + Vector3Int.down, out float under) && under <= 0.98f)
                {
                    continue;
                }
                if (there > bestAmount || (there == bestAmount && Before(next, best)))
                {
                    best = next;
                    bestAmount = there;
                }
            }
            if (best == sample)
            {
                return;
            }
            float give = Steps(Mathf.Min(a, 1f - bestAmount));
            Set(best, bestAmount + give, SourceAt(best));
            a -= give;
            WakeAround(best);
        }

        private static bool Before(Vector3Int a, Vector3Int b)
        {
            return a.x != b.x ? a.x < b.x : a.z < b.z;
        }

        /// <summary>How far along the floor water looks for a drop to head for, like Minecraft's water.</summary>
        public const int DropSearch = 5;

        /// <summary>
        /// The gentlest ground water runs down (GL332), as fall over distance across <see cref="SlopeSpan"/> samples ahead:
        /// 0.25 is about 14°. Gentler ground counts as flat, where water evens out and stops in puddles once it's two steps
        /// deep, even where the voxels make the ground a staircase of one-sample ledges. A drop of two samples or more
        /// (a cliff) is always downhill.
        /// </summary>
        public float MinFlowSlope { get; set; } = 0.25f;

        /// <summary>How far ahead the ground's slope is measured: 8 samples, 2 m at 0.25 m voxels.</summary>
        public const int SlopeSpan = 8;

        private Comparison<Vector3Int> byWater;

        private int ByWater(Vector3Int a, Vector3Int b)
        {
            IsOpen(a, out float wa);
            IsOpen(b, out float wb);
            return wa.CompareTo(wb);
        }

        // True when water standing at sample, heading one step towards the drop at 'at', is going downhill: a cliff of two
        // samples or more there, or ground falling at least MinFlowSlope over the span ahead.
        private bool IsSteep(Vector3Int sample, Vector3Int step, Vector3Int at)
        {
            return FallUnder(at) >= 2 || Descent(sample, step) >= MinFlowSlope * SlopeSpan;
        }

        // How far the ground falls, in samples, along a direction from a sample of standing water, over the span ahead
        // (the most it falls; stops at ground rising to the water's level).
        private int Descent(Vector3Int sample, Vector3Int step)
        {
            int most = 0;
            for (int k = 1; k <= SlopeSpan; k++)
            {
                Vector3Int column = sample + step * k;
                if (!IsOpen(column, out _))
                {
                    break;
                }
                int fall = 0;
                for (Vector3Int below = column + Vector3Int.down; fall < SlopeSpan && IsOpen(below, out _); below += Vector3Int.down)
                {
                    fall++;
                }
                most = Mathf.Max(most, fall);
            }
            return most;
        }

        // Open samples under a sample, at most 4: how deep the drop there is.
        private int FallUnder(Vector3Int at)
        {
            int fall = 0;
            for (Vector3Int below = at + Vector3Int.down; fall < 4 && IsOpen(below, out float under) && under < 0.98f; below += Vector3Int.down)
            {
                fall++;
            }
            return fall;
        }

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
                    // Only ground falling at least MinFlowSlope is downhill (GL332): a drop too far off for its depth is
                    // ground close to flat, where water stands in puddles instead of running.
                    if (!IsSteep(sample, Sideways[first], at))
                    {
                        continue;
                    }
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
            World.DensityChanged -= OnGroundChanged;
        }
    }
}
