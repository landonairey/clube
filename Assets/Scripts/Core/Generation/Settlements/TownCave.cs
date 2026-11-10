using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The secret cave under a trading post (GL475): a round room deep under the square and a tunnel sloping up from it to an
    /// opening outside the wall, on a side away from the gate and clear of the mines. Positions in the world's local metres.
    /// Worked out by <see cref="Plan"/> from the town's plan; carved with <see cref="Voxels"/>.
    /// </summary>
    public sealed class TownCave
    {
        /// <summary>The room's radius and height, in metres.</summary>
        public const float RoomRadius = 2.75f;
        public const float RoomHeight = 2.6f;

        /// <summary>Metres of ground left over the room's ceiling, at least.</summary>
        public const float Cover = 2f;

        // How far the room sits in from the wall, and how far outside the wall the tunnel opens.
        private const float RoomInset = 7f;
        private const float MouthOut = 4f;

        private TownCave(Vector3 room, List<Vector3> tunnel, Vector2 outward)
        {
            Room = room;
            Tunnel = tunnel;
            Outward = outward;
        }

        /// <summary>The middle of the room's floor.</summary>
        public Vector3 Room { get; }

        /// <summary>The tunnel's floor line, from the opening outside the wall down to the room's edge.</summary>
        public List<Vector3> Tunnel { get; }

        /// <summary>The way out of the room along the tunnel (x, z; a unit vector in the world).</summary>
        public Vector2 Outward { get; }

        /// <summary>
        /// The cave for a town: under the back of the square (or a side), on the first side whose room and tunnel keep clear of
        /// every mine (shaft heads and pits); null when none does.
        /// </summary>
        public static TownCave Plan(TownPlan plan)
        {
            // Away from the gate (-z): the back, then the sides.
            Vector2[] sides = { new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            foreach (Vector2 side in sides)
            {
                Vector2 room = side * (TownLayout.InnerHalf - RoomInset);
                Vector2 mouth = side * (TownLayout.OuterHalf + MouthOut);
                if (!ClearOfMines(plan, room, mouth))
                {
                    continue;
                }
                // The floor deep enough to leave cover over the whole room.
                float lowest = float.PositiveInfinity;
                for (int i = 0; i <= 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 at = room + (i == 8 ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RoomRadius);
                    lowest = Mathf.Min(lowest, plan.GroundAt(at));
                }
                float floor = Mathf.Floor((lowest - Cover - RoomHeight) / plan.Voxel) * plan.Voxel;
                Vector2 roomWorld = plan.ToWorld(room);
                Vector2 edgeWorld = plan.ToWorld(room + side * (RoomRadius - 0.5f));
                Vector2 mouthWorld = plan.ToWorld(mouth);
                var tunnel = new List<Vector3>
                {
                    new Vector3(mouthWorld.x, plan.GroundAt(mouth), mouthWorld.y),
                    new Vector3(edgeWorld.x, floor, edgeWorld.y),
                };
                Vector2 outward = (mouthWorld - roomWorld).normalized;
                return new TownCave(new Vector3(roomWorld.x, floor, roomWorld.y), tunnel, outward);
            }
            return null;
        }

        // The room and the line of the tunnel keep a stairwell's room (and a pit's) from every mine, in the town's frame.
        private static bool ClearOfMines(TownPlan plan, Vector2 room, Vector2 mouth)
        {
            foreach (TownShaft shaft in plan.Shafts)
            {
                float keep = (shaft.IsPit ? shaft.PitRadius : TownLayout.ShaftHeadRadius) + RoomRadius + 1f;
                Vector2 ab = mouth - room;
                float t = Mathf.Clamp01(Vector2.Dot(shaft.Head - room, ab) / ab.sqrMagnitude);
                if ((room + ab * t - shaft.Head).magnitude < keep)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True for a point inside the room (the world's local metres).</summary>
        public bool InRoom(Vector3 point)
        {
            return point.y >= Room.y && point.y <= Room.y + RoomHeight
                && (new Vector2(point.x - Room.x, point.z - Room.z)).sqrMagnitude <= RoomRadius * RoomRadius;
        }

        /// <summary>The voxels the room and tunnel take that have ground in them (the tunnel as a mine shaft's passage).</summary>
        public HashSet<Vector3Int> Voxels(World world, float iso)
        {
            HashSet<Vector3Int> voxels = TownStructures.ShaftVoxels(world, Tunnel, iso);
            float size = world.Grid.VoxelSize;
            Vector3Int low = Vector3Int.FloorToInt((Room - new Vector3(RoomRadius, 0f, RoomRadius)) / size);
            Vector3Int high = Vector3Int.FloorToInt((Room + new Vector3(RoomRadius, RoomHeight, RoomRadius)) / size);
            var room = new HashSet<Vector3Int>();
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        Vector3 centre = ((Vector3)voxel + Vector3.one * 0.5f) * size;
                        if (InRoom(centre))
                        {
                            room.Add(voxel);
                        }
                    }
                }
            }
            voxels.UnionWith(TownStructures.WithGround(world, room, iso));
            return voxels;
        }
    }
}
