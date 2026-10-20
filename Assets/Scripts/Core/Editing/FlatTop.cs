using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finds a flat working top chiselled into rock (GL88, GL92; first pass of WS1's large stone): a
    /// level patch of carved voxels, each standing on a flat floor of host material (the four samples
    /// of its bottom face solid, the voxel below not carved), holding at least a
    /// <see cref="MinSide"/> × <see cref="MinSide"/> square. Plain data over the world; the game turns
    /// the floor it finds into Flat Rock.
    /// </summary>
    public static class FlatTop
    {
        /// <summary>The smallest square of floored voxels that counts as a working surface.</summary>
        public const int MinSide = 3;

        private static readonly HashSet<Vector3Int> Candidates = new HashSet<Vector3Int>();
        private static readonly Queue<Vector3Int> Frontier = new Queue<Vector3Int>();

        /// <summary>
        /// Looks around a box of voxels (a chisel hit, widened by <paramref name="margin"/> across and
        /// two voxels up and down) for a flat top. Fills <paramref name="top"/> with the floor samples of
        /// the whole level patch joined to the first qualifying square found, and returns true; false
        /// when there's none.
        /// </summary>
        /// <param name="isHost">Which materials a top can be cut into, by id.</param>
        public static bool Find(World world, Vector3Int min, Vector3Int max, int margin, float isoLevel, Func<byte, bool> isHost, List<Vector3Int> top)
        {
            top.Clear();
            Candidates.Clear();
            for (int z = min.z - margin; z <= max.z + margin; z++)
            {
                for (int y = min.y - 2; y <= max.y + 2; y++)
                {
                    for (int x = min.x - margin; x <= max.x + margin; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        if (IsFloored(world, voxel, isoLevel, isHost))
                        {
                            Candidates.Add(voxel);
                        }
                    }
                }
            }

            foreach (Vector3Int corner in Candidates)
            {
                if (!HoldsSquare(corner))
                {
                    continue;
                }
                // The whole level patch joined to it, side to side.
                Frontier.Clear();
                Frontier.Enqueue(corner);
                var seen = new HashSet<Vector3Int> { corner };
                while (Frontier.Count > 0)
                {
                    Vector3Int voxel = Frontier.Dequeue();
                    AddFloor(voxel, top);
                    foreach (Vector3Int step in new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
                    {
                        Vector3Int next = voxel + step;
                        if (Candidates.Contains(next) && seen.Add(next))
                        {
                            Frontier.Enqueue(next);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        // A carved voxel standing on a flat floor of host ground: the voxel below isn't carved and the
        // four samples of its bottom face are solid host material.
        private static bool IsFloored(World world, Vector3Int voxel, float isoLevel, Func<byte, bool> isHost)
        {
            if (!world.IsCarved(voxel) || world.IsCarved(voxel + Vector3Int.down))
            {
                return false;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3Int sample = voxel + new Vector3Int(corner & 1, 0, corner >> 1);
                float? density = world.GetDensity(sample);
                byte? material = world.GetMaterial(sample);
                if (!density.HasValue || density.Value < isoLevel || !material.HasValue || !isHost(material.Value))
                {
                    return false;
                }
            }
            return true;
        }

        // A floored voxel's four floor samples, each once.
        private static void AddFloor(Vector3Int voxel, List<Vector3Int> top)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3Int sample = voxel + new Vector3Int(corner & 1, 0, corner >> 1);
                if (!top.Contains(sample))
                {
                    top.Add(sample);
                }
            }
        }

        // MinSide × MinSide floored voxels from this corner on, at its level.
        private static bool HoldsSquare(Vector3Int corner)
        {
            for (int z = 0; z < MinSide; z++)
            {
                for (int x = 0; x < MinSide; x++)
                {
                    if (!Candidates.Contains(corner + new Vector3Int(x, 0, z)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
