using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What a digging duty takes out of the ground, voxel by voxel in the order to dig it (GL615): an open pit (a bowl,
    /// from the top down and from the middle out) or a tunnel (a bore from its mouth inwards, each slice from the top
    /// down). It holds no progress: <see cref="Next"/> finds the first voxel still solid in the world, so a pit dug
    /// yesterday, loaded from a save or helped along by the player goes on from where it is.
    /// </summary>
    public sealed class DigPlan
    {
        private readonly List<Vector3Int> voxels = new List<Vector3Int>();
        private int cursor;

        private DigPlan()
        {
        }

        /// <summary>The voxels, in digging order.</summary>
        public IReadOnlyList<Vector3Int> Voxels => voxels;

        /// <summary>
        /// A bowl under <paramref name="top"/> (the voxel at the ground's surface in its middle): <paramref name="radius"/>
        /// voxels wide at the top, one voxel narrower each <paramref name="stepDown"/> voxels down, <paramref name="depth"/> deep.
        /// A voxel above the top layer (ground that rises to one side) is part of its column, so a pit on a slope opens out.
        /// </summary>
        public static DigPlan Pit(Vector3Int top, int radius, int depth, int stepDown = 1, int above = 4)
        {
            var plan = new DigPlan();
            for (int layer = -above; layer < depth; layer++)
            {
                int r = layer <= 0 ? radius : radius - layer / Math.Max(1, stepDown);
                if (r < 0)
                {
                    break;
                }
                var ring = new List<Vector3Int>();
                for (int z = -r; z <= r; z++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + z * z <= r * r + r)
                        {
                            ring.Add(new Vector3Int(top.x + x, top.y - layer, top.z + z));
                        }
                    }
                }
                ring.Sort((a, b) => SqrFlat(a - top).CompareTo(SqrFlat(b - top)));
                plan.voxels.AddRange(ring);
            }
            return plan;
        }

        /// <summary>
        /// A bore from <paramref name="mouth"/> (the voxel on the floor in the middle of its opening) along
        /// <paramref name="direction"/> (x, z): <paramref name="width"/> voxels across, <paramref name="height"/> tall and
        /// <paramref name="length"/> long, its floor level.
        /// </summary>
        public static DigPlan Tunnel(Vector3Int mouth, Vector2 direction, int width, int height, int length)
        {
            var plan = new DigPlan();
            var seen = new HashSet<Vector3Int>();
            Vector2 along = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.up;
            var across = new Vector2(-along.y, along.x);
            float half = (width - 1) * 0.5f;
            for (int step = 0; step < length * 2; step++)
            {
                Vector2 middle = new Vector2(mouth.x, mouth.z) + along * (step * 0.5f);
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int w = 0; w < width * 2 - 1; w++)
                    {
                        Vector2 at = middle + across * (w * 0.5f - half);
                        var voxel = new Vector3Int(Mathf.FloorToInt(at.x + 0.5f), mouth.y + y, Mathf.FloorToInt(at.y + 0.5f));
                        if (seen.Add(voxel))
                        {
                            plan.voxels.Add(voxel);
                        }
                    }
                }
            }
            return plan;
        }

        /// <summary>
        /// The next voxel to dig: the first in order that <paramref name="isSolid"/> says is still there. Looks on from
        /// the last one found, so a finished stretch is not searched again; false once nothing is left.
        /// </summary>
        public bool Next(Func<Vector3Int, bool> isSolid, out Vector3Int voxel)
        {
            for (; cursor < voxels.Count; cursor++)
            {
                if (isSolid(voxels[cursor]))
                {
                    voxel = voxels[cursor];
                    return true;
                }
            }
            voxel = default;
            return false;
        }

        /// <summary>Starts the search over from the first voxel (the world changed under the plan).</summary>
        public void Rewind()
        {
            cursor = 0;
        }

        private static int SqrFlat(Vector3Int offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }
    }
}
