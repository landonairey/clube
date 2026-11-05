using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One mine shaft of the town (GL414): which ore group it reaches, its head in the square and its floor line.</summary>
    public sealed class TownShaft
    {
        public TownShaft(int group, Vector2 head, Vector3 node, List<Vector3> path)
        {
            Group = group;
            Head = head;
            Node = node;
            Path = path;
        }

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

        public TownPlan(TownPlacement site, float yaw, Func<float, float, float> natural, int seed)
        {
            Site = site;
            Centre = site.Centre;
            Yaw = yaw;
            this.natural = natural;
            Seed = seed;
            foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
            {
                padHeights[plot.Kind] = PadHeightFor(plot);
            }
            Stones = TownStructures.WallStones(seed, p => GroundAt(p), ToWorld, yaw);
            Shafts = PlanShafts();
        }

        public TownPlacement Site { get; }

        /// <summary>The town's centre (x, z): the middle of its parcel.</summary>
        public Vector2 Centre { get; }

        /// <summary>The town's turn about y (a quarter turn: the gate faces the spawn).</summary>
        public float Yaw { get; }

        public int Seed { get; }

        /// <summary>The wall's stones (GL409).</summary>
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
            return heights[heights.Count / 2];
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
            for (int g = 0; g < nodes.Length; g++)
            {
                if (Site.OreDistances != null && g < Site.OreDistances.Length && !float.IsInfinity(Site.OreDistances[g]))
                {
                    towards.Add(ToTown(new Vector2(nodes[g].x, nodes[g].z)));
                    groups.Add(g);
                }
            }
            List<Vector2> heads = TownLayout.ShaftHeads(towards);
            for (int i = 0; i < heads.Count; i++)
            {
                Vector2 head = ToWorld(heads[i]);
                Vector3 node = nodes[groups[i]];
                shafts.Add(new TownShaft(groups[i], heads[i], node, TownStructures.ShaftPath(head, node, natural)));
            }
            return shafts;
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
