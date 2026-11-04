using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>Which materials make up trees, for felling (GL344): wood falls and holds things up, leaves only hang.</summary>
    public sealed class TreeMaterials
    {
        private readonly HashSet<byte> wood = new HashSet<byte>();
        private readonly HashSet<byte> leaves = new HashSet<byte>();

        public TreeMaterials(IEnumerable<VoxelMaterial> woods, IEnumerable<VoxelMaterial> leafs)
        {
            foreach (VoxelMaterial material in woods)
            {
                if (material != null)
                {
                    wood.Add(material.Id);
                }
            }
            foreach (VoxelMaterial material in leafs)
            {
                if (material != null)
                {
                    leaves.Add(material.Id);
                }
            }
        }

        /// <summary>Wood and leaves by material id.</summary>
        public TreeMaterials(IEnumerable<byte> woodIds, IEnumerable<byte> leafIds)
        {
            wood.UnionWith(woodIds);
            leaves.UnionWith(leafIds);
        }

        /// <summary>The birch and maple wood and leaves of a terrain's trees (stumps count as ground: they stay).</summary>
        public static TreeMaterials From(TreeGeneration trees)
        {
            return new TreeMaterials(new[] { trees.Wood, trees.MapleWood }, new[] { trees.Leaves, trees.MapleLeaves });
        }

        public bool IsWood(byte id)
        {
            return wood.Contains(id);
        }

        public bool IsLeaves(byte id)
        {
            return leaves.Contains(id);
        }

        public bool IsTree(byte id)
        {
            return wood.Contains(id) || leaves.Contains(id);
        }
    }

    /// <summary>One sample of a felled limb: where it was, what it was made of and how dense.</summary>
    public readonly struct FelledSample
    {
        public FelledSample(Vector3Int sample, byte material, float density, bool isWood)
        {
            Sample = sample;
            Material = material;
            Density = density;
            IsWood = isWood;
        }

        public Vector3Int Sample { get; }
        public byte Material { get; }
        public float Density { get; }
        public bool IsWood { get; }
    }

    /// <summary>A limb cut off its tree (GL344): what falls, and the cut it turns about.</summary>
    public sealed class FelledLimb
    {
        /// <summary>The solid samples that fall: the limb's wood, its branches and the leaves that hang from them.</summary>
        public readonly List<FelledSample> Solid = new List<FelledSample>();

        /// <summary>The thin, not-solid samples round them (the density ramp), so the limb's mesh is as smooth as the tree's.</summary>
        public readonly List<FelledSample> Fringe = new List<FelledSample>();

        /// <summary>The cut: the limb's wood sample nearest where it was chopped, which it turns about as it falls.</summary>
        public Vector3Int Pivot;

        /// <summary>The lowest and highest sample of the limb, fringe included.</summary>
        public Vector3Int Min, Max;

        public int WoodCount { get; internal set; }
    }

    /// <summary>
    /// Felling (GL344). After a chop on a tree, <see cref="FindSevered"/> looks for wood no longer joined to the ground
    /// through wood: everything above a limb cut through. With it go the leaves nearer (through leaves) to that wood than to
    /// any wood still standing, so cutting a branch takes its leaf balls and not the crown round it. <see cref="Land"/> puts
    /// it back as it lies once fallen: every sample turned 90° about the cut towards the fall direction, then dropped
    /// straight down its column onto the ground, wood first and leaves on top, passing through any tree on the way.
    /// Works on the <see cref="World"/>'s edit path, main thread.
    /// </summary>
    public static class TreeFelling
    {
        /// <summary>Most samples one wood search follows before it counts the wood as standing (a whole forest's worth).</summary>
        public const int MaxWood = 200000;

        /// <summary>How far through leaves, in samples, a felled limb takes them: past it they stay.</summary>
        public const int MaxLeafReach = 20;

        /// <summary>Share of a column's leaves that lands: the rest crush away, so a fallen crown lies flat.</summary>
        public const float LeafPacking = 0.1f;

        /// <summary>Deepest a landed column's leaves lie, in samples (1.5 m at 0.25 m voxels).</summary>
        public const int MaxLeafPile = 6;

        // Down last, so the search (a stack) follows wood downwards first and wood that stands finds the ground quickly.
        private static readonly Vector3Int[] DownLast =
        {
            Vector3Int.up, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1), Vector3Int.down,
        };

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>
        /// The limb cut off by a chop round <paramref name="from"/>-<paramref name="to"/> (global samples), or null when every
        /// piece of wood there still reaches the ground through wood.
        /// </summary>
        public static FelledLimb FindSevered(World world, Vector3Int from, Vector3Int to, TreeMaterials trees, float iso)
        {
            var standing = new HashSet<Vector3Int>();
            var fallingWood = new HashSet<Vector3Int>();
            var component = new HashSet<Vector3Int>();
            var stack = new Stack<Vector3Int>();
            Vector3Int low = Vector3Int.Min(from, to) - Vector3Int.one * 2;
            Vector3Int high = Vector3Int.Max(from, to) + Vector3Int.one * 2;
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var seed = new Vector3Int(x, y, z);
                        if (standing.Contains(seed) || fallingWood.Contains(seed) || !IsWood(world, seed, trees, iso))
                        {
                            continue;
                        }
                        bool anchored = Search(world, seed, trees, iso, component, stack);
                        (anchored ? standing : fallingWood).UnionWith(component);
                    }
                }
            }
            if (fallingWood.Count == 0)
            {
                return null;
            }

            var limb = new FelledLimb();
            var included = new HashSet<Vector3Int>();
            foreach (Vector3Int sample in fallingWood)
            {
                limb.Solid.Add(new FelledSample(sample, world.GetMaterial(sample) ?? 0, world.GetDensity(sample) ?? 1f, true));
                included.Add(sample);
            }
            limb.WoodCount = fallingWood.Count;
            foreach (Vector3Int leaf in HangingLeaves(world, fallingWood, trees, iso))
            {
                limb.Solid.Add(new FelledSample(leaf, world.GetMaterial(leaf) ?? 0, world.GetDensity(leaf) ?? 1f, false));
                included.Add(leaf);
            }
            AddFringe(world, limb, included, trees, iso);
            limb.Pivot = Nearest(fallingWood, (Vector3)(from + to) * 0.5f);
            return limb;
        }

        // Follows wood from a seed. True as soon as it touches ground (anything solid that isn't tree: soil, rock, a stump)
        // or unloaded land, or grows past MaxWood: the wood stands. False when it's all been followed and holds nothing up.
        private static bool Search(World world, Vector3Int seed, TreeMaterials trees, float iso, HashSet<Vector3Int> found, Stack<Vector3Int> stack)
        {
            found.Clear();
            stack.Clear();
            found.Add(seed);
            stack.Push(seed);
            while (stack.Count > 0)
            {
                Vector3Int sample = stack.Pop();
                foreach (Vector3Int step in DownLast)
                {
                    Vector3Int next = sample + step;
                    if (found.Contains(next))
                    {
                        continue;
                    }
                    float? density = world.GetDensity(next);
                    if (!density.HasValue)
                    {
                        return true;
                    }
                    if (density.Value < iso)
                    {
                        continue;
                    }
                    byte material = world.GetMaterial(next) ?? 0;
                    if (!trees.IsTree(material))
                    {
                        return true;
                    }
                    if (!trees.IsWood(material))
                    {
                        continue;
                    }
                    found.Add(next);
                    if (found.Count > MaxWood)
                    {
                        return true;
                    }
                    stack.Push(next);
                }
            }
            return false;
        }

        // The leaves that hang from the felled wood: reached through leaves (within MaxLeafReach) sooner from it than from any
        // solid that stays (standing wood, ground). Ties stay.
        private static List<Vector3Int> HangingLeaves(World world, HashSet<Vector3Int> wood, TreeMaterials trees, float iso)
        {
            var fromLimb = new Dictionary<Vector3Int, int>();
            var queue = new Queue<Vector3Int>();
            foreach (Vector3Int sample in wood)
            {
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (!fromLimb.ContainsKey(next) && IsLeaves(world, next, trees, iso))
                    {
                        fromLimb[next] = 1;
                        queue.Enqueue(next);
                    }
                }
            }
            while (queue.Count > 0)
            {
                Vector3Int sample = queue.Dequeue();
                int distance = fromLimb[sample];
                if (distance >= MaxLeafReach)
                {
                    continue;
                }
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (!fromLimb.ContainsKey(next) && IsLeaves(world, next, trees, iso))
                    {
                        fromLimb[next] = distance + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            // The same through the leaves found, from whatever solid stays beside them.
            var fromStanding = new Dictionary<Vector3Int, int>();
            foreach (Vector3Int leaf in fromLimb.Keys)
            {
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = leaf + step;
                    if (wood.Contains(next) || fromLimb.ContainsKey(next))
                    {
                        continue;
                    }
                    float? density = world.GetDensity(next);
                    if (density.HasValue && density.Value >= iso)
                    {
                        fromStanding[leaf] = 1;
                        queue.Enqueue(leaf);
                        break;
                    }
                }
            }
            while (queue.Count > 0)
            {
                Vector3Int sample = queue.Dequeue();
                int distance = fromStanding[sample];
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (fromLimb.ContainsKey(next) && !fromStanding.ContainsKey(next))
                    {
                        fromStanding[next] = distance + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            var hanging = new List<Vector3Int>();
            foreach (KeyValuePair<Vector3Int, int> leaf in fromLimb)
            {
                if (!fromStanding.TryGetValue(leaf.Key, out int stay) || leaf.Value < stay)
                {
                    hanging.Add(leaf.Key);
                }
            }
            return hanging;
        }

        // The not-solid tree samples beside the limb, so its mesh keeps the tree's smooth surface and none of it is left
        // behind as a film.
        private static void AddFringe(World world, FelledLimb limb, HashSet<Vector3Int> included, TreeMaterials trees, float iso)
        {
            Vector3Int min = Vector3Int.one * int.MaxValue;
            Vector3Int max = Vector3Int.one * int.MinValue;
            int count = limb.Solid.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3Int sample = limb.Solid[i].Sample;
                min = Vector3Int.Min(min, sample);
                max = Vector3Int.Max(max, sample);
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (included.Contains(next))
                    {
                        continue;
                    }
                    float density = world.GetDensity(next) ?? 0f;
                    byte material = world.GetMaterial(next) ?? 0;
                    if (density <= 0f || density >= iso || !trees.IsTree(material))
                    {
                        continue;
                    }
                    included.Add(next);
                    limb.Fringe.Add(new FelledSample(next, material, density, trees.IsWood(material)));
                    min = Vector3Int.Min(min, next);
                    max = Vector3Int.Max(max, next);
                }
            }
            limb.Min = min;
            limb.Max = max;
        }

        private static Vector3Int Nearest(HashSet<Vector3Int> samples, Vector3 point)
        {
            Vector3Int best = default;
            float bestDistance = float.MaxValue;
            foreach (Vector3Int sample in samples)
            {
                float distance = ((Vector3)sample - point).sqrMagnitude;
                if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && sample.y < best.y))
                {
                    bestDistance = distance;
                    best = sample;
                }
            }
            return best;
        }

        private static bool IsWood(World world, Vector3Int sample, TreeMaterials trees, float iso)
        {
            return (world.GetDensity(sample) ?? 0f) >= iso && trees.IsWood(world.GetMaterial(sample) ?? 0);
        }

        private static bool IsLeaves(World world, Vector3Int sample, TreeMaterials trees, float iso)
        {
            return (world.GetDensity(sample) ?? 0f) >= iso && trees.IsLeaves(world.GetMaterial(sample) ?? 0);
        }

        /// <summary>Takes the limb out of the world: every sample of it, fringe included, becomes air.</summary>
        public static void Remove(World world, FelledLimb limb)
        {
            foreach (FelledSample sample in limb.Solid)
            {
                world.SetDensity(sample.Sample, 0f);
            }
            foreach (FelledSample sample in limb.Fringe)
            {
                world.SetDensity(sample.Sample, 0f);
            }
        }

        /// <summary>
        /// An offset from the cut turned 90° over towards <paramref name="direction"/> (a unit step along x or z): what pointed
        /// up now points that way, and what pointed that way now points down. Exact on the sample grid.
        /// </summary>
        public static Vector3Int Turn(Vector3Int offset, Vector3Int direction)
        {
            int along = offset.x * direction.x + offset.z * direction.z;
            return new Vector3Int(
                offset.x - along * direction.x + offset.y * direction.x,
                -along,
                offset.z - along * direction.z + offset.y * direction.z);
        }

        /// <summary>
        /// Puts a fallen limb down (GL344): each solid sample turned about the cut towards <paramref name="direction"/>, then
        /// dropped down its column, which is searched for ground (solid and not tree) up to <paramref name="searchDepth"/>
        /// samples below the turned limb. Each column takes all its wood first, then <see cref="LeafPacking"/> of its leaves
        /// (at most <see cref="MaxLeafPile"/> deep), from the ground up; a sample
        /// already holding standing wood is passed over, leaves there are replaced. Returns the samples placed.
        /// </summary>
        public static int Land(World world, FelledLimb limb, Vector3Int direction, TreeMaterials trees, float iso, int searchDepth = 160)
        {
            var columns = new Dictionary<Vector2Int, List<(int Y, bool Wood, byte Material)>>();
            foreach (FelledSample sample in limb.Solid)
            {
                Vector3Int turned = limb.Pivot + Turn(sample.Sample - limb.Pivot, direction);
                var column = new Vector2Int(turned.x, turned.z);
                if (!columns.TryGetValue(column, out List<(int, bool, byte)> list))
                {
                    list = new List<(int, bool, byte)>();
                    columns[column] = list;
                }
                list.Add((turned.y, sample.IsWood, sample.Material));
            }

            int placed = 0;
            foreach (KeyValuePair<Vector2Int, List<(int Y, bool Wood, byte Material)>> pair in columns)
            {
                List<(int Y, bool Wood, byte Material)> list = pair.Value;
                // Wood first, then leaves; each kept in its order from the bottom of the turned limb.
                list.Sort((a, b) => a.Wood != b.Wood ? (a.Wood ? -1 : 1) : a.Y.CompareTo(b.Y));
                int top = int.MinValue;
                foreach ((int y, _, _) in list)
                {
                    top = Mathf.Max(top, y);
                }
                if (!FindGround(world, pair.Key, top + 2, searchDepth, trees, iso, out int ground))
                {
                    continue;
                }
                int at = ground + 1;
                // Leaves crush as they land: a column keeps a share of them, never deeper than MaxLeafPile, so the pile lies
                // flat instead of standing as deep as the crown was wide. Every sample of wood is kept.
                int leaves = 0;
                foreach ((_, bool wood, _) in list)
                {
                    leaves += wood ? 0 : 1;
                }
                int keptLeaves = Mathf.Min(MaxLeafPile, Mathf.CeilToInt(leaves * LeafPacking));
                int leafIndex = 0;
                foreach ((_, bool wood, byte material) in list)
                {
                    if (!wood)
                    {
                        // Every so many leaves of the column, spread over it, so its mix of leaf kinds is kept.
                        bool keep = leafIndex * keptLeaves / Mathf.Max(1, leaves) != (leafIndex + 1) * keptLeaves / Mathf.Max(1, leaves);
                        leafIndex++;
                        if (!keep)
                        {
                            continue;
                        }
                    }
                    var sample = new Vector3Int(pair.Key.x, at, pair.Key.y);
                    // Through standing trees: their wood is stepped over, their leaves give way.
                    while ((world.GetDensity(sample) ?? 0f) >= iso && trees.IsWood(world.GetMaterial(sample) ?? 0))
                    {
                        sample.y++;
                    }
                    if (world.GetDensity(sample) == null)
                    {
                        break;
                    }
                    world.SetDensity(sample, 1f);
                    world.SetMaterial(sample, material);
                    placed++;
                    at = sample.y + 1;
                }
            }
            return placed;
        }

        // The highest sample of a column that's solid and not tree, searched down from a height.
        private static bool FindGround(World world, Vector2Int column, int from, int depth, TreeMaterials trees, float iso, out int ground)
        {
            for (int y = from; y > from - depth; y--)
            {
                var sample = new Vector3Int(column.x, y, column.y);
                float? density = world.GetDensity(sample);
                if (density.HasValue && density.Value >= iso && !trees.IsTree(world.GetMaterial(sample) ?? 0))
                {
                    ground = y;
                    return true;
                }
            }
            ground = 0;
            return false;
        }
    }
}
