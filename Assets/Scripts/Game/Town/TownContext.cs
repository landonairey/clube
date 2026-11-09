using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the town's keepers need from the world (GL413-GL415): the town's plan, the way from the world's local metres
    /// to the scene, the height of the ground under a point (the terrain as it is now, the land as generated where it isn't
    /// loaded), and the means to mine a node. Made by <see cref="TownSite"/>.
    /// </summary>
    public sealed class TownContext
    {
        private readonly WorldView worldView;
        private readonly Dictionary<int, HashSet<byte>> groupOres;
        private readonly Transform root;

        public TownContext(TownPlan plan, WorldView worldView, Transform root, Dictionary<int, HashSet<byte>> groupOres)
        {
            Plan = plan;
            this.worldView = worldView;
            this.root = root;
            this.groupOres = groupOres;
        }

        public TownPlan Plan { get; }

        /// <summary>The town's root object in the scene.</summary>
        public Transform Root => root;

        /// <summary>A point in the world's local metres, in the scene.</summary>
        public Vector3 ToScene(Vector3 local)
        {
            return worldView.transform.TransformPoint(local);
        }

        /// <summary>A turn about y in the world's frame, in the scene.</summary>
        public Quaternion SceneRotation(float yaw)
        {
            return worldView.transform.rotation * Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>The world's up, in the scene.</summary>
        public Vector3 Up => worldView.transform.up;

        /// <summary>
        /// The height of the ground under (x, z) in the world's local metres: the terrain's surface found looking down from a
        /// little over <paramref name="near"/>, else the land as generated.
        /// </summary>
        public float GroundAt(float x, float z, float near)
        {
            if (worldView.IsReady)
            {
                Vector3 from = ToScene(new Vector3(x, near + 1.4f, z));
                if (worldView.Raycast(new Ray(from, -Up), out Vector3 hit))
                {
                    float y = worldView.transform.InverseTransformPoint(hit).y;
                    if (y > near - 2.6f)
                    {
                        return y;
                    }
                }
            }
            return Plan.GroundAt(Plan.ToTown(new Vector2(x, z)));
        }

        /// <summary>Takes one sample of an ore group's ore near a point (GL415); false when none is in reach.</summary>
        public bool MineAt(Vector3 local, int group)
        {
            if (!worldView.IsReady || !groupOres.TryGetValue(group, out HashSet<byte> ores))
            {
                return false;
            }
            return NodeMining.TakeNearest(worldView.World, local, 1.6f, ores, worldView.Config.IsoLevel).HasValue;
        }

        private readonly Dictionary<TownShaft, Queue<Vector3Int>> pitOrders = new Dictionary<TownShaft, Queue<Vector3Int>>();

        /// <summary>
        /// Digs a few more samples out of an open pit (GL474), in <see cref="OpenPit"/>'s order (worked out the first time);
        /// returns how many were its group's ore. 0 once it's dug out.
        /// </summary>
        public int DigPit(TownShaft pit, int samples)
        {
            if (!worldView.IsReady || pit == null || !pit.IsPit)
            {
                return 0;
            }
            float iso = worldView.Config.IsoLevel;
            if (!pitOrders.TryGetValue(pit, out Queue<Vector3Int> order))
            {
                // Up to a couple of metres over the ground as generated: the land there may stand a little higher.
                order = OpenPit.Plan(worldView.World, pit.PitCentre, pit.PitFloor, pit.Path[0].y + 2f, iso);
                pitOrders[pit] = order;
            }
            groupOres.TryGetValue(pit.Group, out HashSet<byte> ores);
            return OpenPit.Dig(worldView.World, order, samples, iso, ores);
        }
    }
}
