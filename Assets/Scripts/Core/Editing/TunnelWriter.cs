using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Bores one tunnel of a road through the terrain (GL642), a metre at a time: an arched passage carved out in whole
    /// voxels (<see cref="SquareVoxels.Carve"/>, lined with stone where the ground would fall), its floor surfaced as
    /// the road is. Only the part with ground enough overhead is bored (<see cref="BoreStart"/> to <see cref="BoreEnd"/>);
    /// the road's own writer cuts the open approach at each end, and a portal stands where the bore begins. Which
    /// metres are bored is kept for the save, like a road's pieces.
    /// </summary>
    public sealed class TunnelWriter
    {
        /// <summary>Metres of ground over the floor from which the tunnel is bored rather than cut open.</summary>
        public const float BoreCover = 4.5f;

        private readonly bool[] written;
        private readonly Vector2 way;

        /// <param name="width">The passage's width in metres, and <paramref name="height"/> its height at the middle.</param>
        /// <param name="surface">The floor's material, by name (the road's own surface).</param>
        /// <param name="natural">The ground as generated at (x, z).</param>
        public TunnelWriter(RoadTunnel tunnel, float width, float height, string surface, Func<float, float, float> natural)
        {
            Tunnel = tunnel;
            Width = width;
            Height = height;
            Surface = surface;
            way = (tunnel.To - tunnel.From) / Mathf.Max(0.01f, tunnel.Length);
            // The bore runs from the first metre with ground enough overhead to the last.
            int length = Mathf.FloorToInt(tunnel.Length);
            int first = -1, last = -1;
            for (int i = 0; i < length; i++)
            {
                Vector2 at = tunnel.From + way * (i + 0.5f);
                if (natural(at.x, at.y) - tunnel.FloorAt(i + 0.5f) >= BoreCover)
                {
                    first = first < 0 ? i : first;
                    last = i;
                }
            }
            BoreStart = first < 0 ? 0f : first;
            BoreEnd = first < 0 ? 0f : last + 1f;
            written = new bool[first < 0 ? 0 : last - first + 1];
        }

        public RoadTunnel Tunnel { get; }
        public float Width { get; }
        public float Height { get; }
        public string Surface { get; }

        /// <summary>Metres along the tunnel where the bore begins and ends; before and after, the road is an open cutting.</summary>
        public float BoreStart { get; }
        public float BoreEnd { get; }

        /// <summary>Metres of bore, each a piece.</summary>
        public int Pieces => written.Length;

        public int Written { get; private set; }

        public bool IsWritten(int piece)
        {
            return written[piece];
        }

        /// <summary>True for a point (x, z) over the bore: the road's writer leaves the ground there alone.</summary>
        public bool IsBored(Vector2 point)
        {
            return Tunnel.DistanceTo(point, out float along) < Width && along >= BoreStart && along <= BoreEnd;
        }

        /// <summary>The middle of a piece's floor (x, height, z).</summary>
        public Vector3 Middle(int piece)
        {
            return At(BoreStart + piece + 0.5f);
        }

        /// <summary>The point of the floor's middle line a distance along the tunnel (x, height, z).</summary>
        public Vector3 At(float along)
        {
            Vector2 at = Tunnel.From + way * along;
            return new Vector3(at.x, Tunnel.FloorAt(along), at.y);
        }

        /// <summary>Adds the pieces not yet bored whose middle is within <paramref name="range"/> metres of a point (x, z).</summary>
        public void Unwritten(Vector2 near, float range, List<int> into)
        {
            for (int i = 0; i < written.Length; i++)
            {
                Vector3 middle = Middle(i);
                if (!written[i] && (new Vector2(middle.x, middle.z) - near).sqrMagnitude <= range * range)
                {
                    into.Add(i);
                }
            }
        }

        /// <summary>True when every chunk a piece may change is loaded.</summary>
        public bool Loaded(World world, int heightInChunks, int piece)
        {
            Vector3 middle = Middle(piece);
            float reach = Width * 0.5f + 2f;
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToChunk(new Vector3(middle.x - reach, Mathf.Max(0f, middle.y - 2f), middle.z - reach));
            Vector3Int max = grid.WorldToChunk(new Vector3(middle.x + reach, middle.y + Height + 2f, middle.z + reach));
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(heightInChunks - 1, max.y); y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!world.IsLoaded(new Vector3Int(x, y, z)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>Bores a piece and marks it written.</summary>
        /// <param name="lining">What ground that would fall round the passage is turned to; null leaves it.</param>
        public void Write(World world, MaterialRegistry materials, VoxelMaterial lining, float iso, int piece)
        {
            if (written[piece])
            {
                return;
            }
            written[piece] = true;
            Written++;
            float voxel = world.Grid.VoxelSize;
            float from = BoreStart + piece, to = from + 1f;
            float half = Width * 0.5f;
            Vector3 middle = Middle(piece);
            float reach = half + 1f;
            var voxels = new HashSet<Vector3Int>();
            var floor = new HashSet<Vector3Int>();
            Vector3Int low = Vector3Int.FloorToInt(new Vector3(middle.x - reach, middle.y - 1f, middle.z - reach) / voxel);
            Vector3Int high = Vector3Int.FloorToInt(new Vector3(middle.x + reach, middle.y + Height + 1f, middle.z + reach) / voxel);
            var side = new Vector2(-way.y, way.x);
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    // Each column of voxels belongs to the one metre of bore its middle lies along.
                    var column = new Vector2((x + 0.5f) * voxel, (z + 0.5f) * voxel);
                    float along = Vector2.Dot(column - Tunnel.From, way);
                    float across = Mathf.Abs(Vector2.Dot(column - Tunnel.From, side));
                    if (along < from || along >= to || across > half)
                    {
                        continue;
                    }
                    // An arch: full height along the middle, a third lower at the walls.
                    float share = across / half;
                    int y0 = Mathf.RoundToInt(Tunnel.FloorAt(along) / voxel);
                    float top = y0 * voxel + Height * (1f - 0.33f * share * share);
                    for (int y = y0; (y + 0.5f) * voxel < top; y++)
                    {
                        voxels.Add(new Vector3Int(x, y, z));
                    }
                    floor.Add(new Vector3Int(x, y0, z));
                }
            }
            SquareVoxels.Carve(world, voxels, materials, lining, iso);
            VoxelMaterial paving = materials != null && !string.IsNullOrEmpty(Surface) ? materials.Find(Surface) : null;
            if (paving == null)
            {
                return;
            }
            foreach (Vector3Int voxelAt in floor)
            {
                // The four samples under a floor voxel are the floor's surface.
                for (int corner = 0; corner < 4; corner++)
                {
                    var sample = new Vector3Int(voxelAt.x + (corner & 1), voxelAt.y, voxelAt.z + (corner >> 1));
                    if ((world.GetDensity(sample) ?? 0f) >= iso)
                    {
                        world.SetMaterial(sample, paving.Id);
                    }
                }
            }
        }

        /// <summary>Which pieces are bored, for the save.</summary>
        public string Save()
        {
            return PieceFlags.Save(written);
        }

        /// <summary>Marks the pieces a save says are bored (a tunnel of another length is ignored).</summary>
        public void Restore(string text)
        {
            Written += PieceFlags.Restore(text, written);
        }
    }
}
