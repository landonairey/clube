using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Ground that falls (GL308, GL327). A sample of a material that falls (<see cref="VoxelMaterial.Fall"/>) with air under it
    /// moves down one sample a step, unless it's held. Ground has a stability: 100% where it stands on a solid column, the
    /// same as the sample it rests on, and its material's loss (<see cref="GroundHold.StabilityLoss"/>) less for each sample
    /// it reaches out sideways or hangs under its neighbour; at 0% it lets go. Rock loses nothing, so it holds while it's
    /// joined to standing ground at all (any cave ceiling joined to its walls) and only a piece cut off falls, as one; dirt
    /// loses 34%, so it hangs two samples out; sand never hangs, and also slides down a side where the slope is steeper than
    /// 45°. Ground that lets go keeps falling until it lands, rather than catching on a wall on the way down.
    /// Only samples near a change are looked at: every density change wakes the samples round it, the loose ground joined
    /// to a woken sample is measured as a whole, and every move wakes the ones round where it left and landed, so a
    /// dug-out bank crumbles and stops once everything rests. Moves swap the two samples' densities and materials through
    /// the world's edit path (A7), so the ground keeps its volume and chunk borders and meshes follow.
    /// </summary>
    public sealed class GroundSettling : IDisposable
    {
        private static readonly Vector3Int[] Sides = { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        /// <summary>Solid samples under a sample, straight down, that make it stand on the ground (3 m at 0.25 m voxels).</summary>
        public const int GroundDepth = 12;

        /// <summary>
        /// Most samples of loose ground (not standing) gathered round one that could hang in a step; past it the ground is
        /// taken as held, being too big to be a loose piece.
        /// </summary>
        public const int MaxRegion = 4096;

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private readonly GroundHold[] holds = new GroundHold[256];
        private readonly float iso;
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> next = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> falling = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> letGo = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> regionSeen = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, bool> standing = new Dictionary<Vector3Int, bool>();
        private readonly List<Vector3Int> region = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, int> stability = new Dictionary<Vector3Int, int>();
        private readonly List<Vector3Int>[] buckets = NewBuckets();
        private int turn;

        private static List<Vector3Int>[] NewBuckets()
        {
            var lists = new List<Vector3Int>[101];
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = new List<Vector3Int>();
            }
            return lists;
        }

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

        // Which samples have let go this step (GL327). For each awake sample that could hang, the solid ground joined to it
        // is gathered, stopping at ground that stands (a solid column under it), which is 100% stable. Stability spreads
        // out from there, highest first: resting on a sample keeps its stability, reaching out sideways or hanging under
        // one costs the material's loss. Whatever ends at 0% (cut off from standing ground, or hanging too far out) lets go.
        private void FindLetGo()
        {
            letGo.Clear();
            regionSeen.Clear();
            standing.Clear();
            foreach (Vector3Int sample in batch)
            {
                if (regionSeen.Contains(sample) || falling.Contains(sample) || !IsHangingCandidate(sample))
                {
                    continue;
                }
                MeasureRegion(sample);
            }
        }

        private bool IsHangingCandidate(Vector3Int sample)
        {
            return IsSolid(sample, out byte material) && holds[material].CanHang && IsAir(sample + Vector3Int.down);
        }

        private void MeasureRegion(Vector3Int start)
        {
            region.Clear();
            stability.Clear();
            for (int i = 0; i <= 100; i++)
            {
                buckets[i].Clear();
            }
            regionSeen.Add(start);
            region.Add(start);
            for (int i = 0; i < region.Count; i++)
            {
                Vector3Int sample = region[i];
                if (IsStanding(sample))
                {
                    stability[sample] = 100;
                    buckets[100].Add(sample);
                    continue;
                }
                if (region.Count >= MaxRegion)
                {
                    // Too much loose ground to measure in a step: taken as held.
                    return;
                }
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (!regionSeen.Contains(next) && IsSolid(next, out _))
                    {
                        regionSeen.Add(next);
                        region.Add(next);
                    }
                }
            }
            for (int level = 100; level > 0; level--)
            {
                List<Vector3Int> bucket = buckets[level];
                for (int i = 0; i < bucket.Count; i++)
                {
                    Vector3Int from = bucket[i];
                    if (stability[from] != level)
                    {
                        continue;
                    }
                    foreach (Vector3Int step in Around)
                    {
                        Vector3Int to = from + step;
                        if (!regionSeen.Contains(to) || !IsSolid(to, out byte material))
                        {
                            continue;
                        }
                        // Resting on it costs nothing; reaching out or hanging under it costs the material's loss.
                        GroundHold hold = holds[material];
                        int loss = step == Vector3Int.up || hold.Fall == GroundFall.Stays ? 0 : hold.StabilityLoss;
                        int value = level - loss;
                        if (value > 0 && (!stability.TryGetValue(to, out int known) || value > known))
                        {
                            stability[to] = value;
                            buckets[value].Add(to);
                        }
                    }
                }
            }
            foreach (Vector3Int sample in region)
            {
                if (stability.ContainsKey(sample) || !IsSolid(sample, out byte material) || holds[material].Fall == GroundFall.Stays)
                {
                    continue;
                }
                letGo.Add(sample);
                awake.Add(sample);
            }
        }

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
