using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One mine of the town (GL414): which ore group it reaches, its head in the square and its floor line. A stair shaft, or
    /// an open pit over a node near the surface (GL474), whose path is the one point on the ground over its middle.
    /// </summary>
    public sealed class TownShaft
    {
        public TownShaft(int group, Vector2 head, Vector3 node, List<Vector3> path, float pitFloor = float.NaN)
        {
            Group = group;
            Head = head;
            Node = node;
            Path = path;
            PitFloor = pitFloor;
        }

        /// <summary>True for an open pit (GL474) rather than a stair shaft.</summary>
        public bool IsPit => !float.IsNaN(PitFloor);

        /// <summary>The height of a pit's floor when it's dug out (<see cref="OpenPit"/>); NaN for a shaft.</summary>
        public float PitFloor { get; }

        /// <summary>A pit's middle, in the world's local metres (x, z).</summary>
        public Vector2 PitCentre => new Vector2(Node.x, Node.z);

        /// <summary>How far a pit reaches from its middle at the ground; 0 for a shaft.</summary>
        public float PitRadius => IsPit ? OpenPit.RimRadius(Path[0].y - PitFloor) : 0f;

        /// <summary>The ore group it was dug to, in the site request's order (tin, copper, silver, gold).</summary>
        public int Group { get; }

        /// <summary>The head of its stairwell, in the town's frame.</summary>
        public Vector2 Head { get; }

        /// <summary>The node's centre, in the world's local metres.</summary>
        public Vector3 Node { get; }

        /// <summary>Its floor line, from the surface down to the node's face, in the world's local metres.</summary>
        public List<Vector3> Path { get; }
    }

    /// <summary>
    /// Everything about one town that follows from its site and the land (GL408-GL414), worked out once: where its frame
    /// sits and how it's turned, the height of each shop's pad, the wall's stones, and the mine shafts to its ore nodes.
    /// The builder, the levelling and the keepers all read it, so they agree. Plain data: positions are in the world's
    /// local metres unless named as in the town's frame.
    /// </summary>
    public sealed class TownPlan
    {
        private readonly Dictionary<ShopKind, float> padHeights = new Dictionary<ShopKind, float>();
        private readonly Func<float, float, float> natural;

        public TownPlan(TownPlacement site, float yaw, Func<float, float, float> natural, int seed, float voxel = 0.25f)
        {
            Site = site;
            Centre = site.Centre;
            Yaw = yaw;
            this.natural = natural;
            Voxel = Mathf.Max(0.01f, voxel);
            Seed = seed;
            foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
            {
                padHeights[plot.Kind] = PadHeightFor(plot);
            }
            Stones = TownStructures.GatePillars(p => GroundAt(p), ToWorld, yaw);
            Shafts = PlanShafts();
        }

        public TownPlacement Site { get; }

        /// <summary>The town's centre (x, z): the middle of its parcel.</summary>
        public Vector2 Centre { get; }

        /// <summary>The town's turn about y (a quarter turn: the gate faces the spawn).</summary>
        public float Yaw { get; }

        public int Seed { get; }

        /// <summary>The world's voxel size: pads and floors sit on its grid.</summary>
        public float Voxel { get; }

        /// <summary>The gate pillars' stones (GL409); the rest of the wall is solid (GL422).</summary>
        public List<WallStone> Stones { get; }

        /// <summary>The shafts dug to the ore nodes (GL414), one per ore group with a node.</summary>
        public List<TownShaft> Shafts { get; }

        /// <summary>A point of the town's frame, in the world's local metres (x, z).</summary>
        public Vector2 ToWorld(Vector2 town)
        {
            return Centre + TownLayout.Turn(town, Yaw);
        }

        /// <summary>A point in the world's local metres (x, z), in the town's frame.</summary>
        public Vector2 ToTown(Vector2 world)
        {
            return TownLayout.Turn(world - Centre, -Yaw);
        }

        /// <summary>The land's height at a point of the town's frame, as generated.</summary>
        public float GroundAt(Vector2 town)
        {
            Vector2 world = ToWorld(town);
            return natural(world.x, world.y);
        }

        /// <summary>The height of a shop's level pad (GL413): its floor stands <see cref="TownLayout.FloorHeight"/> over it.</summary>
        public float PadHeight(ShopKind kind)
        {
            return padHeights.TryGetValue(kind, out float height) ? height : Site.Ground;
        }

        /// <summary>The height a keeper stands at on a shop's floor.</summary>
        public float FloorHeight(ShopKind kind)
        {
            return PadHeight(kind) + TownLayout.FloorHeight;
        }

        /// <summary>The ground's height at the middle of the gate.</summary>
        public float GateGround => GroundAt(new Vector2(0f, -(TownLayout.InnerHalf + TownLayout.OuterHalf) * 0.5f));

        // The middle height of the land over the pad: its corners, edge middles and centre.
        private float PadHeightFor(TownLayout.ShopPlot plot)
        {
            Rect pad = plot.PadRect;
            var heights = new List<float>();
            for (int j = 0; j <= 2; j++)
            {
                for (int i = 0; i <= 2; i++)
                {
                    heights.Add(GroundAt(new Vector2(Mathf.Lerp(pad.xMin, pad.xMax, i * 0.5f), Mathf.Lerp(pad.yMin, pad.yMax, j * 0.5f))));
                }
            }
            heights.Sort();
            // On the voxel grid (GL417), so the board floor's top is exactly a voxel over it and what stands on it isn't sunk.
            return Mathf.Round(heights[heights.Count / 2] / Voxel) * Voxel;
        }

        private List<TownShaft> PlanShafts()
        {
            var shafts = new List<TownShaft>();
            Vector3[] nodes = Site.OreNodes;
            if (nodes == null)
            {
                return shafts;
            }
            var towards = new List<Vector2>();
            var groups = new List<int>();
            var pits = new List<(Vector2 Centre, float Radius)>();
            for (int g = 0; g < nodes.Length; g++)
            {
                if (Site.OreDistances == null || g >= Site.OreDistances.Length || float.IsInfinity(Site.OreDistances[g]))
                {
                    continue;
                }
                // The first group's (tin's) node near the surface is an open pit (GL474), where there's room for one.
                if (g == PitGroup && TryPlanPit(nodes[g], out TownShaft pit))
                {
                    shafts.Add(pit);
                    pits.Add((pit.Head, pit.PitRadius));
                    continue;
                }
                towards.Add(ToTown(new Vector2(nodes[g].x, nodes[g].z)));
                groups.Add(g);
            }
            List<Vector2> heads = TownLayout.ShaftHeads(towards, pits);
            for (int i = 0; i < heads.Count; i++)
            {
                Vector2 head = ToWorld(heads[i]);
                Vector3 node = nodes[groups[i]];
                shafts.Add(new TownShaft(groups[i], heads[i], node, TownStructures.ShaftPath(head, node, natural)));
            }
            return shafts;
        }

        /// <summary>The ore group whose node may be an open pit (GL474): the first, tin.</summary>
        public const int PitGroup = 0;

        // An open pit over the node (GL474): its floor a metre under the node's centre, no deeper than OpenPit.MaxDepth under
        // the ground, and with room round it (off the town's shops, paths and wall).
        private bool TryPlanPit(Vector3 node, out TownShaft pit)
        {
            pit = null;
            var centre = new Vector2(node.x, node.z);
            float ground = natural(centre.x, centre.y);
            float floor = node.y - 1f;
            float depth = ground - floor;
            if (depth > OpenPit.MaxDepth)
            {
                return false;
            }
            Vector2 head = ToTown(centre);
            if (!TownLayout.IsClearForPit(head, OpenPit.RimRadius(depth)))
            {
                return false;
            }
            pit = new TownShaft(PitGroup, head, node, new List<Vector3> { new Vector3(centre.x, ground, centre.y) }, Mathf.Min(floor, ground));
            return true;
        }

        /// <summary>The trees in the corner by the lumber shop, young ones of the world's own kinds (small enough for the corner), and
        /// the woodpile: logs of birch lying along it in a stack (GL423). Parts in the world's local metres.</summary>
        public List<TreePart> LumberYard(TreeGeneration trees)
        {
            var parts = new List<TreePart>();
            int seed = Seed;
            int cell = 0;
            foreach (Vector2 spot in TownLayout.Trees)
            {
                // A tree of the world's own making from a cell of its own, a young one so it fits the corner.
                for (int tries = 0; tries < 200; tries++, cell++)
                {
                    var treeCell = new Vector2Int(90000 + cell, 90000 + seed % 1000);
                    float height = TreeShape.HeightOf(seed, treeCell, trees);
                    if (height < 4.5f || height > 6.5f)
                    {
                        continue;
                    }
                    Vector2 world = this.ToWorld(spot);
                    var root = new float3(world.x, this.GroundAt(spot), world.y);
                    TreeShape.Build(seed, treeCell, root, trees, parts);
                    cell++;
                    break;
                }
            }
            // The woodpile: four rows of logs, each row one fewer, lying along the pile.
            const float Radius = 0.22f;
            const float Length = 2.2f;
            Vector2 pile = TownLayout.Woodpile;
            float ground = this.GroundAt(pile);
            for (int row = 0; row < 4; row++)
            {
                int logs = 4 - row;
                for (int i = 0; i < logs; i++)
                {
                    // A gap between logs, so each shows as its own log.
                    float across = (i - (logs - 1) * 0.5f) * Radius * 2.5f;
                    float up = ground + Radius + row * Radius * 2.2f;
                    Vector2 a = this.ToWorld(pile + new Vector2(across, -Length * 0.5f));
                    Vector2 b = this.ToWorld(pile + new Vector2(across, Length * 0.5f));
                    parts.Add(TreePart.Segment(new float3(a.x, up, a.y), new float3(b.x, up, b.y), Radius, Radius, trees.Wood.Id));
                }
            }
            return parts;
        }

        /// <summary>
        /// A shop's voxels in the world (GL412), by voxel (its lowest corner, a global sample), with the material of each:
        /// stone for the plinth and posts, <paramref name="boards"/> for floor, walls and roof, <paramref name="trim"/> for
        /// the counter, shelf and lintel.
        /// </summary>
        public Dictionary<Vector3Int, byte> ShopBlocks(ShopKind kind, float voxel, byte stone, byte boards, byte trim)
        {
            var blocks = new Dictionary<Vector3Int, byte>();
            TownLayout.ShopPlot plot = TownLayout.PlotOf(kind);
            float pad = PadHeight(kind);
            foreach ((Vector3 centre, TownStructures.ShopPart part) in TownStructures.ShopVoxels(voxel))
            {
                Vector2 world = ToWorld(plot.ToTown(new Vector2(centre.x, centre.z)));
                var at = new Vector3(world.x, pad + centre.y, world.y);
                Vector3Int cell = Vector3Int.FloorToInt(at / voxel);
                blocks[cell] = part == TownStructures.ShopPart.Stone ? stone : part == TownStructures.ShopPart.Trim ? trim : boards;
            }
            return blocks;
        }
    }
}
