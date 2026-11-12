using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One ore node (O2): a centroid with a 3D Gaussian falloff.</summary>
    public readonly struct OreNode
    {
        public OreNode(int spec, byte material, Vector3 centre, Vector3 spread, float peak, int priority, Composition contents = null)
        {
            Spec = spec;
            Material = material;
            Centre = centre;
            Spread = spread;
            Peak = peak;
            Priority = priority;
            Contents = contents;
        }

        /// <summary>What ore mined from it is made of (GL32): its grade of ore, the rest rock.</summary>
        public Composition Contents { get; }

        /// <summary>Index of the <see cref="OreSpec"/> it came from.</summary>
        public int Spec { get; }

        /// <summary>The ore's material id.</summary>
        public byte Material { get; }

        /// <summary>World position of the centroid, relative to the world origin.</summary>
        public Vector3 Centre { get; }

        /// <summary>σ per axis in metres.</summary>
        public Vector3 Spread { get; }

        /// <summary>Replacement chance at the centre.</summary>
        public float Peak { get; }

        public int Priority { get; }

        /// <summary>How far the node reaches on each axis: 3σ, past which the chance is taken as 0 (O3).</summary>
        public Vector3 Reach => Spread * OreField.ReachInSigmas;

        /// <summary>Replacement chance at a position (O4): peak × exp(−d² / 2), d in σ along each axis; 0 beyond 3σ.</summary>
        public float Probability(Vector3 position)
        {
            return OreRoll.Chance(Centre, 1f / (float3)(Vector3)Spread, Peak, position);
        }
    }

    /// <summary>
    /// Places ore through a world (3D). The world is cut into cubic cells (O2); each cell's
    /// node centroids come from the world seed and the cell coordinate alone, so they don't
    /// depend on which chunks are loaded or in what order. A chunk gathers every node whose
    /// 3σ reach touches it, from its own and neighbouring cells (O3), and each host sample in
    /// reach rolls a position hash against the node's falloff (O4); where nodes overlap, the
    /// higher priority wins, then the likelier ore (O5).
    /// </summary>
    /// <remarks>
    /// <para>Centroids sit at their spec's depth below the surface, using the terrain generator's
    /// depth, which is also a pure function of position. Cells are cached as chunks need them.</para>
    /// <para>Placing nodes runs on the main thread (it's cheap and cached); the rolls run in the
    /// generation jobs (<see cref="ChunkFillKernel"/>) on the nodes <see cref="ToJobNodes"/>
    /// hands them, through the same <see cref="OreRoll"/> as <see cref="Pick"/>. Ore only forms
    /// below the surface.</para>
    /// </remarks>
    public sealed class OreField
    {
        /// <summary>A node's reach in σ: beyond 3σ the chance is under 1.2% of the peak.</summary>
        public const float ReachInSigmas = OreRoll.ReachInSigmas;

        // Cells kept before the cache is cleared; enough for a large loaded area.
        private const int MaxCachedCells = 8192;

        // Tries per expected node at finding a centroid inside the depth range.
        private const int PlacementTries = 4;

        private readonly OreGeneration settings;
        private readonly ITerrainGenerator generator;
        private readonly int seed;

        // Per spec: bit id set when the ore may replace material id (every bit: any host).
        private readonly ulong[] hosts;
        private readonly Dictionary<Vector3Int, OreNode[]> cells = new Dictionary<Vector3Int, OreNode[]>();
        private readonly List<OreNode> scratch = new List<OreNode>();
        private readonly List<OreNode> nearby = new List<OreNode>();
        private readonly Vector3 maxReach;

        public OreField(OreGeneration settings, int seed, ITerrainGenerator generator)
        {
            this.settings = settings;
            this.seed = seed;
            this.generator = generator;
            hosts = new ulong[settings.Ores.Count];
            for (int i = 0; i < settings.Ores.Count; i++)
            {
                OreSpec spec = settings.Ores[i];
                hosts[i] = ulong.MaxValue;
                if (spec?.Ore == null || spec.Hosts.Count == 0)
                {
                    continue;
                }
                hosts[i] = 0;
                foreach (VoxelMaterial host in spec.Hosts)
                {
                    if (host != null && host.Id < 64)
                    {
                        hosts[i] |= 1UL << host.Id;
                    }
                }
            }
            foreach (OreSpec spec in settings.Ores)
            {
                if (spec?.Ore != null)
                {
                    maxReach = Vector3.Max(maxReach, spec.Spread * ReachInSigmas);
                }
            }
        }

        /// <summary>An ore field for the terrain's settings, or null when it has no ores.</summary>
        public static OreField Create(TerrainSettings terrain, ITerrainGenerator generator)
        {
            return generator != null && terrain.Ores.HasOres ? new OreField(terrain.Ores, terrain.Seed, generator) : null;
        }

        public float CellSize => settings.CellSize;

        /// <summary>The world seed the rolls hash with.</summary>
        public int Seed => seed;

        /// <summary>The nodes whose centroids lie in a cell (O2), the same every time.</summary>
        public IReadOnlyList<OreNode> NodesInCell(Vector3Int cell)
        {
            if (cells.TryGetValue(cell, out OreNode[] nodes))
            {
                return nodes;
            }
            if (cells.Count >= MaxCachedCells)
            {
                cells.Clear();
            }
            nodes = PlaceNodes(cell);
            cells.Add(cell, nodes);
            return nodes;
        }

        /// <summary>
        /// The nodes centred in a cell, worked out without touching the cache: for wide one-off surveys (the town's site,
        /// GL399) that would otherwise push out the cells generation is using.
        /// </summary>
        public OreNode[] NodesInCellUncached(Vector3Int cell)
        {
            return cells.TryGetValue(cell, out OreNode[] nodes) ? nodes : PlaceNodes(cell);
        }

        /// <summary>The deepest any ore's nodes lie below the surface, in metres.</summary>
        public float MaxNodeDepth
        {
            get
            {
                float deepest = 0f;
                foreach (OreSpec spec in settings.Ores)
                {
                    if (spec?.Ore != null)
                    {
                        deepest = Mathf.Max(deepest, spec.MaxDepth);
                    }
                }
                return deepest;
            }
        }

        /// <summary>
        /// Every node whose reach overlaps the box (O3): nodes centred in neighbouring cells
        /// spill into it. <paramref name="results"/> is cleared first.
        /// </summary>
        public void CollectNodes(Bounds box, List<OreNode> results)
        {
            results.Clear();
            float size = CellSize;
            Vector3Int from = Vector3Int.FloorToInt((box.min - maxReach) / size);
            Vector3Int to = Vector3Int.FloorToInt((box.max + maxReach) / size);
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        foreach (OreNode node in NodesInCell(new Vector3Int(x, y, z)))
                        {
                            if (new Bounds(node.Centre, node.Reach * 2f).Intersects(box))
                            {
                                results.Add(node);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>The nodes as generation jobs read them, in the same order (the order breaks no ties, but keeps runs identical).</summary>
        public NativeArray<OreNodeData> ToJobNodes(List<OreNode> nodes, Allocator allocator)
        {
            var data = new NativeArray<OreNodeData>(nodes.Count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < nodes.Count; i++)
            {
                OreNode node = nodes[i];
                data[i] = new OreNodeData
                {
                    Centre = node.Centre,
                    InverseSpread = 1f / (float3)(Vector3)node.Spread,
                    Reach = node.Reach,
                    Peak = node.Peak,
                    Priority = node.Priority,
                    Material = node.Material,
                    Hosts = hosts[node.Spec],
                };
            }
            return data;
        }

        /// <summary>
        /// The material a sample ends up with (O4, O5): an ore if one of the nodes rolls it and
        /// may replace <paramref name="host"/>, otherwise the host. Generation rolls the same way
        /// in its jobs, and only for samples below the surface.
        /// </summary>
        /// <param name="globalSample">The sample's world-wide coordinate, which seeds its roll, so every chunk holding it agrees.</param>
        /// <param name="position">The sample's position relative to the world origin.</param>
        public byte Pick(byte host, Vector3Int globalSample, Vector3 position, IReadOnlyList<OreNode> nodes)
        {
            byte picked = host;
            int bestPriority = int.MinValue;
            float bestChance = 0f;
            var sample = new int3(globalSample.x, globalSample.y, globalSample.z);
            foreach (OreNode node in nodes)
            {
                ulong allowed = hosts[node.Spec];
                bool mayReplace = host < 64 ? (allowed & (1UL << host)) != 0 : allowed == ulong.MaxValue;
                if (!mayReplace)
                {
                    continue;
                }
                float chance = node.Probability(position);
                if (!OreRoll.Rolls(seed, sample, node.Material, chance) || !OreRoll.Beats(node.Priority, chance, bestPriority, bestChance))
                {
                    continue;
                }
                picked = node.Material;
                bestPriority = node.Priority;
                bestChance = chance;
            }
            return picked;
        }

        /// <summary>
        /// What ore mined at a position is made of (GL32): the contents of the node of its spec
        /// likeliest to have placed it there, or the spec's typical contents when no node
        /// reaches (ore poured back out of an inventory). Null when no spec makes the material.
        /// </summary>
        /// <param name="mined">The ore, or a stage it breaks into (cracked or loose copper).</param>
        /// <param name="position">Relative to the world origin.</param>
        public Composition ContentsAt(VoxelMaterial mined, Vector3 position)
        {
            int specIndex = SpecFor(mined);
            if (specIndex < 0)
            {
                return null;
            }
            CollectNodes(new Bounds(position, Vector3.zero), nearby);
            Composition best = null;
            float bestChance = 0f;
            foreach (OreNode node in nearby)
            {
                float chance = node.Spec == specIndex ? node.Probability(position) : 0f;
                if (chance > bestChance)
                {
                    best = node.Contents;
                    bestChance = chance;
                }
            }
            nearby.Clear();
            return best ?? settings.Ores[specIndex].TypicalContents;
        }

        // The spec whose ore is the material or breaks into it; -1 for none.
        private int SpecFor(VoxelMaterial mined)
        {
            for (int i = 0; i < settings.Ores.Count; i++)
            {
                // Break chains are a few stages long; the bound guards a looped chain.
                VoxelMaterial stage = settings.Ores[i]?.Ore;
                for (int depth = 0; stage != null && depth < 8; depth++, stage = stage.BreaksInto)
                {
                    if (stage == mined)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        // Keeps grade rolls apart from the placement rolls.
        private const int GradeSalt = 0x47524144;

        private OreNode[] PlaceNodes(Vector3Int cell)
        {
            scratch.Clear();
            float size = CellSize;
            Vector3 cellOrigin = (Vector3)cell * size;
            for (int specIndex = 0; specIndex < settings.Ores.Count; specIndex++)
            {
                OreSpec spec = settings.Ores[specIndex];
                if (spec?.Ore == null || spec.NodesPerCell <= 0f || spec.PeakProbability <= 0f)
                {
                    continue;
                }

                // Salts keep each ore's (and each node's) numbers independent.
                int salt = spec.Ore.Id * 1000;
                int count = NodeCount(spec, cell, salt);
                int tries = count * PlacementTries;
                for (int attempt = 0, placed = 0; attempt < tries && placed < count; attempt++)
                {
                    int nodeSalt = salt + 1 + attempt * 3;
                    Vector3 centre = TryCentre(cell, cellOrigin, size, nodeSalt);
                    float depth = generator.Depth(centre);
                    if (depth < spec.MinDepth || depth > spec.MaxDepth)
                    {
                        continue;
                    }
                    int gradeSalt = GradeSalt + salt + attempt * 8;
                    Vector3Int at = cell;
                    Composition contents = spec.ContentsFor(
                        VoxelHash.Uniform(seed, at.x, at.y, at.z, gradeSalt),
                        i => VoxelHash.Uniform(seed, at.x, at.y, at.z, gradeSalt + 1 + i));
                    scratch.Add(new OreNode(specIndex, spec.Ore.Id, centre, spec.Spread, spec.PeakProbability, spec.Priority, contents));
                    placed++;
                }
            }
            return scratch.ToArray();
        }

        // How many nodes of a spec the cell holds: the whole part of its count, and one more by a roll for the rest.
        private int NodeCount(OreSpec spec, Vector3Int cell, int salt)
        {
            int whole = Mathf.FloorToInt(spec.NodesPerCell);
            bool extra = VoxelHash.Uniform(seed, cell.x, cell.y, cell.z, salt) < spec.NodesPerCell - whole;
            return whole + (extra ? 1 : 0);
        }

        // Where one try at placing a node lands in its cell.
        private Vector3 TryCentre(Vector3Int cell, Vector3 cellOrigin, float size, int nodeSalt)
        {
            return cellOrigin + new Vector3(
                VoxelHash.Uniform(seed, cell.x, cell.y, cell.z, nodeSalt),
                VoxelHash.Uniform(seed, cell.x, cell.y, cell.z, nodeSalt + 1),
                VoxelHash.Uniform(seed, cell.x, cell.y, cell.z, nodeSalt + 2)) * size;
        }

        /// <summary>The ores' materials, by spec index (null where a spec has none).</summary>
        public VoxelMaterial OreOf(int spec)
        {
            return settings.Ores[spec]?.Ore;
        }

        /// <summary>How many specs there are (<see cref="OreNode.Spec"/> counts through them).</summary>
        public int SpecCount => settings.Ores.Count;

        /// <summary>
        /// Every centroid placing a cell's nodes would try, in order, added to <paramref name="tries"/>: the first half
        /// of placing nodes, for wide surveys (GL592) that then sample the ground under all of them in one job instead
        /// of one managed call each, and finish with <see cref="MarkPlaced"/>.
        /// </summary>
        public void AppendTries(Vector3Int cell, List<OreTry> tries)
        {
            float size = CellSize;
            Vector3 cellOrigin = (Vector3)cell * size;
            for (int specIndex = 0; specIndex < settings.Ores.Count; specIndex++)
            {
                OreSpec spec = settings.Ores[specIndex];
                if (spec?.Ore == null || spec.NodesPerCell <= 0f || spec.PeakProbability <= 0f)
                {
                    continue;
                }
                int salt = spec.Ore.Id * 1000;
                int count = NodeCount(spec, cell, salt);
                for (int attempt = 0; attempt < count * PlacementTries; attempt++)
                {
                    tries.Add(new OreTry { Spec = specIndex, Budget = count, Centre = TryCentre(cell, cellOrigin, size, salt + 1 + attempt * 3) });
                }
            }
        }

        /// <summary>
        /// Which of one cell's tries (<paramref name="count"/> of them from <paramref name="first"/>, as
        /// <see cref="AppendTries"/> listed them) become nodes, given the ground's height over each
        /// (<paramref name="ground"/>, indexed like the tries less <paramref name="groundOffset"/>): the same rule as
        /// placing them one by one. Their indices are added to <paramref name="placed"/>.
        /// </summary>
        public void MarkPlaced(List<OreTry> tries, int first, int count, NativeArray<float> ground, int groundOffset, List<int> placed)
        {
            int spec = -1, made = 0;
            for (int i = first; i < first + count; i++)
            {
                OreTry attempt = tries[i];
                if (attempt.Spec != spec)
                {
                    spec = attempt.Spec;
                    made = 0;
                }
                if (made >= attempt.Budget)
                {
                    continue;
                }
                float depth = ground[i - groundOffset] - attempt.Centre.y;
                if (depth < settings.Ores[spec].MinDepth || depth > settings.Ores[spec].MaxDepth)
                {
                    continue;
                }
                placed.Add(i);
                made++;
            }
        }
    }

    /// <summary>One try at placing an ore node (<see cref="OreField.AppendTries"/>).</summary>
    public struct OreTry
    {
        /// <summary>Index of the ore's spec.</summary>
        public int Spec;

        /// <summary>How many nodes of this spec the cell holds at most.</summary>
        public int Budget;

        public Vector3 Centre;
    }
}
