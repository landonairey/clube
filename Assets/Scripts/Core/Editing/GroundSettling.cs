using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Ground that falls (GL308, GL327). A sample of a material that falls (<see cref="VoxelMaterial.Fall"/>) with air under it
    /// moves down one sample a step, unless it's held: ground that can hang (<see cref="GroundHold"/>) stays while a way of at
    /// most its overhang leads sideways, through solid samples in its own layer, to ground standing on a solid column
    /// (dirt holds 2 samples out, rock 5, and rock a sample further for each solid sample over it, so a cave under thick rock
    /// keeps its ceiling where a thin lip breaks off). Sand never hangs, and also slides down a side where the slope is
    /// steeper than 45°. Ground that lets go keeps falling until it lands, rather than catching on a wall on the way down.
    /// Only samples near a change are looked at: every density change wakes the samples round it, a sample dug out wakes the
    /// whole sheet of overhanging ground above it, and every move wakes the ones round where it left and landed, so a
    /// dug-out bank crumbles and stops once everything rests. Moves swap the two samples' densities and materials through
    /// the world's edit path (A7), so the ground keeps its volume and chunk borders and meshes follow.
    /// </summary>
    public sealed class GroundSettling : IDisposable
    {
        private static readonly Vector3Int[] Sides = { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        /// <summary>Solid samples under a sample, straight down, that make it stand on the ground (3 m at 0.25 m voxels).</summary>
        public const int GroundDepth = 12;

        /// <summary>Most samples of one overhanging sheet looked at in a step; past it the sheet is taken as held.</summary>
        public const int MaxSheet = 4096;

        private readonly GroundHold[] holds = new GroundHold[256];
        private readonly float iso;
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> next = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> falling = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> letGo = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> sheetSeen = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, bool> standing = new Dictionary<Vector3Int, bool>();
        private readonly List<Vector3Int> sheet = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, int> reachOut = new Dictionary<Vector3Int, int>();
        private readonly Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        private int turn;

        /// <param name="fallOf">How each material id falls; none of them hangs (the GL308 behaviour).</param>
        public GroundSettling(World world, Func<byte, GroundFall> fallOf, float iso)
            : this(world, id => new GroundHold(fallOf(id)), iso)
        {
        }

        /// <param name="holdOf">How each material id falls and holds over air.</param>
        public GroundSettling(World world, Func<byte, GroundHold> holdOf, float iso)
        {
            World = world;
            this.iso = iso;
            for (int id = 0; id < holds.Length; id++)
            {
                holds[id] = holdOf((byte)id);
            }
            world.DensityChanged += Wake;
        }

        public World World { get; }

        /// <summary>Samples waiting to be looked at.</summary>
        public int AwakeCount => awake.Count;

        /// <summary>Wakes a sample and the ones that could fall into it: those above, and those beside (sand slides).</summary>
        public void Wake(Vector3Int sample)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    awake.Add(sample + new Vector3Int(dx, 1, dz));
                    awake.Add(sample + new Vector3Int(dx, 0, dz));
                }
            }
        }

        /// <summary>
        /// Moves every awake sample that can fall by one sample, lowest first, at most <paramref name="maxMoves"/>
        /// (the rest wait); returns how many moved. Samples woken by these moves wait for the next step.
        /// </summary>
        public int Step(int maxMoves = 2000)
        {
            if (awake.Count == 0)
            {
                return 0;
            }
            batch.Clear();
            batch.AddRange(awake);
            HashSet<Vector3Int> stepping = awake;
            awake = next;
            awake.Clear();
            next = stepping;
            batch.Sort((a, b) => a.y.CompareTo(b.y));
            turn++;
            FindLetGo();
            int moves = 0;
            foreach (Vector3Int sample in batch)
            {
                if (moves >= maxMoves)
                {
                    awake.Add(sample);
                    continue;
                }
                if (TryFall(sample))
                {
                    moves++;
                }
            }
            return moves;
        }

        private bool TryFall(Vector3Int sample)
        {
            float? density = World.GetDensity(sample);
            if (!density.HasValue || density.Value < iso)
            {
                falling.Remove(sample);
                return false;
            }
            byte? material = World.GetMaterial(sample);
            GroundHold hold = material.HasValue ? holds[material.Value] : default;
            if (hold.Fall == GroundFall.Stays)
            {
                falling.Remove(sample);
                return false;
            }
            Vector3Int below = sample + Vector3Int.down;
            bool wasFalling = falling.Remove(sample);
            if (IsAir(below))
            {
                // Ground that can hang stays while it's held; once it lets go it falls on until it lands.
                if (hold.CanHang && !wasFalling && !letGo.Contains(sample))
                {
                    return false;
                }
                Move(sample, below, density.Value, material.Value);
                falling.Add(below);
                return true;
            }
            if (hold.Fall != GroundFall.Slides)
            {
                return false;
            }
            // Sides in a turning order, so a heap spreads evenly instead of always one way.
            int first = (sample.x * 7 + sample.z * 13 + turn) & 3;
            for (int k = 0; k < 4; k++)
            {
                Vector3Int side = Sides[(first + k) & 3];
                if (IsAir(sample + side) && IsAir(below + side))
                {
                    Move(sample, below + side, density.Value, material.Value);
                    return true;
                }
            }
            return false;
        }

        // Which awake samples that can hang have let go this step: for each sheet of overhanging ground (solid samples
        // with air under them, joined sideways in one layer) holding an awake one, how far each of its samples is from
        // ground standing on a column, counted out from that ground; a sample further than its reach lets go.
        private void FindLetGo()
        {
            letGo.Clear();
            sheetSeen.Clear();
            standing.Clear();
            foreach (Vector3Int sample in batch)
            {
                if (sheetSeen.Contains(sample) || falling.Contains(sample) || !IsHangingCandidate(sample))
                {
                    continue;
                }
                MeasureSheet(sample);
            }
        }

        private bool IsHangingCandidate(Vector3Int sample)
        {
            if (!IsSolid(sample, out byte material) || !holds[material].CanHang || holds[material].Fall == GroundFall.Stays)
            {
                return false;
            }
            return IsAir(sample + Vector3Int.down);
        }

        private void MeasureSheet(Vector3Int start)
        {
            // The sheet: every overhanging sample joined to this one in its layer.
            sheet.Clear();
            reachOut.Clear();
            frontier.Clear();
            sheetSeen.Add(start);
            sheet.Add(start);
            for (int i = 0; i < sheet.Count && sheet.Count < MaxSheet; i++)
            {
                foreach (Vector3Int side in Sides)
                {
                    Vector3Int next = sheet[i] + side;
                    if (sheetSeen.Contains(next) || !IsSolid(next, out _) || !IsAir(next + Vector3Int.down))
                    {
                        continue;
                    }
                    sheetSeen.Add(next);
                    sheet.Add(next);
                }
            }
            if (sheet.Count >= MaxSheet)
            {
                // Too big to measure in a step: taken as held.
                return;
            }
            // Out from the ground that stands: each sheet sample beside a standing one is 1 out, and so on.
            foreach (Vector3Int sample in sheet)
            {
                foreach (Vector3Int side in Sides)
                {
                    Vector3Int next = sample + side;
                    if (!reachOut.ContainsKey(sample) && IsStanding(next))
                    {
                        reachOut[sample] = 1;
                        frontier.Enqueue(sample);
                    }
                }
            }
            while (frontier.Count > 0)
            {
                Vector3Int sample = frontier.Dequeue();
                int distance = reachOut[sample];
                foreach (Vector3Int side in Sides)
                {
                    Vector3Int next = sample + side;
                    if (sheetSeen.Contains(next) && !reachOut.ContainsKey(next) && IsSolid(next, out _) && IsAir(next + Vector3Int.down))
                    {
                        reachOut[next] = distance + 1;
                        frontier.Enqueue(next);
                    }
                }
            }
            foreach (Vector3Int sample in sheet)
            {
                if (!IsSolid(sample, out byte material) || !holds[material].CanHang)
                {
                    continue;
                }
                GroundHold hold = holds[material];
                int reach = hold.Overhang + Mathf.Min(SolidAbove(sample, hold.ArchReach), hold.ArchReach);
                if (!reachOut.TryGetValue(sample, out int distance) || distance > reach)
                {
                    letGo.Add(sample);
                    awake.Add(sample);
                }
            }
        }

        // Solid samples stacked straight over a sample, counted up to most.
        private int SolidAbove(Vector3Int sample, int most)
        {
            int count = 0;
            for (Vector3Int up = sample + Vector3Int.up; count < most && IsSolid(up, out _); up += Vector3Int.up)
            {
                count++;
            }
            return count;
        }

        // Ground that stands: solid, on a solid column GroundDepth samples deep (or reaching unloaded ground).
        private bool IsStanding(Vector3Int sample)
        {
            if (standing.TryGetValue(sample, out bool known))
            {
                return known;
            }
            bool result = IsSolid(sample, out _);
            for (int i = 1; result && i <= GroundDepth; i++)
            {
                Vector3Int down = sample + Vector3Int.down * i;
                float? density = World.GetDensity(down);
                if (!density.HasValue)
                {
                    break;
                }
                result = density.Value >= iso;
            }
            standing[sample] = result;
            return result;
        }

        private bool IsSolid(Vector3Int sample, out byte material)
        {
            float? density = World.GetDensity(sample);
            if (density.HasValue && density.Value >= iso)
            {
                material = World.GetMaterial(sample) ?? 0;
                return true;
            }
            material = 0;
            return false;
        }

        // Air, and loaded: unloaded ground counts as solid, so nothing falls out of the world.
        private bool IsAir(Vector3Int sample)
        {
            float? density = World.GetDensity(sample);
            return density.HasValue && density.Value < iso;
        }

        private void Move(Vector3Int from, Vector3Int to, float density, byte material)
        {
            float left = World.GetDensity(to) ?? 0f;
            byte leftMaterial = World.GetMaterial(to) ?? material;
            World.SetMaterial(to, material);
            World.SetDensity(to, density);
            World.SetDensity(from, left);
            World.SetMaterial(from, leftMaterial);
        }

        public void Dispose()
        {
            World.DensityChanged -= Wake;
        }
    }
}
