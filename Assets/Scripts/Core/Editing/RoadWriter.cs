using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes one road of the world plan into the terrain (GL613, GL633), a short piece at a time: trees on it cut, its
    /// bed shaped (<see cref="RoadBed"/>: a footpath follows the ground, a cart path is evened out and meets the ground
    /// at its shoulders) and its top layer changed to the class's surface, all through <see cref="GroundLeveling"/> and so
    /// through <see cref="World"/>'s edits. Whoever owns it decides which pieces to write and when (near the viewer, once
    /// their chunks are loaded, a few milliseconds a frame); which are written is kept for the save, so a loaded game
    /// doesn't write them again over the player's digging. Stretches under a bridge are never written.
    /// </summary>
    public sealed class RoadWriter
    {
        /// <summary>Metres of road in a piece: short, so one piece is a few milliseconds.</summary>
        public const float PieceLength = 1f;

        // Metres round a piece within which a tree standing on the road is cut: its trunk and the crown over the road.
        private const float TreeReach = 4f;

        // Metres of headroom cleared over the road (a fallen rock, a branch).
        private const float Headroom = 3f;

        private readonly bool[] written;

        public RoadWriter(RoadBed bed)
        {
            Bed = bed ?? throw new ArgumentNullException(nameof(bed));
            written = new bool[Mathf.Max(1, Mathf.CeilToInt(bed.Line.Length / PieceLength))];
            for (int piece = 0; piece < written.Length; piece++)
            {
                if (bed.OnBridge(piece * PieceLength, Mathf.Min(bed.Line.Length, (piece + 1) * PieceLength)))
                {
                    written[piece] = true;
                    Written++;
                    OnBridges++;
                }
            }
        }

        public RoadBed Bed { get; }

        /// <summary>Pieces of road (<see cref="PieceLength"/> metres each).</summary>
        public int Pieces => written.Length;

        /// <summary>Pieces written so far (those under bridges count from the start).</summary>
        public int Written { get; private set; }

        /// <summary>Pieces that lie under a bridge and are never written.</summary>
        public int OnBridges { get; }

        /// <summary>Leaves a piece unwritten for good (where the road runs onto the bed of the road it joins).</summary>
        public void Skip(int piece)
        {
            if (!written[piece])
            {
                written[piece] = true;
                Written++;
                Skipped++;
            }
        }

        /// <summary>Pieces left out by <see cref="Skip"/>.</summary>
        public int Skipped { get; private set; }

        public bool IsWritten(int piece)
        {
            return written[piece];
        }

        /// <summary>The middle of a piece (x, z).</summary>
        public Vector2 Middle(int piece)
        {
            return Bed.Line.Flat((piece + 0.5f) * PieceLength);
        }

        /// <summary>Adds the pieces not yet written whose middle is within <paramref name="range"/> metres of a point.</summary>
        public void Unwritten(Vector2 near, float range, List<int> into)
        {
            if (Written >= written.Length)
            {
                return;
            }
            float rangeSquared = range * range;
            for (int piece = 0; piece < written.Length; piece++)
            {
                if (!written[piece] && (Middle(piece) - near).sqrMagnitude <= rangeSquared)
                {
                    into.Add(piece);
                }
            }
        }

        // The piece's flat rectangle: the box round its own strip of the bed, from edge to edge across the road every half
        // metre along it (not the stretch's box widened all round: most of that is its neighbours' ground, and every
        // column of the rectangle costs a look). The road's two ends take the half circle beyond them as well.
        private Rect Bounds(int piece, out float low, out float high)
        {
            const float Margin = 0.5f;
            RoadLine line = Bed.Line;
            float from = piece * PieceLength;
            float to = Mathf.Min(line.Length, from + PieceLength);
            float reach = Bed.Reach;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            low = float.MaxValue;
            high = float.MinValue;
            for (float d = from; ; d = Mathf.Min(to, d + 0.5f))
            {
                Vector2 p = line.Flat(d);
                Vector2 way = line.Direction(d);
                Vector2 across = new Vector2(-way.y, way.x) * reach;
                bool end = (piece == 0 && d <= 0f) || (piece == written.Length - 1 && d >= to);
                Vector2 round = end ? Vector2.one * reach : new Vector2(Mathf.Abs(across.x), Mathf.Abs(across.y));
                min = Vector2.Min(min, p - round);
                max = Vector2.Max(max, p + round);
                float ground = line.HeightAlong(d), bed = Bed.HeightAlong(d);
                low = Mathf.Min(low, Mathf.Min(ground, bed));
                high = Mathf.Max(high, Mathf.Max(ground, bed));
                if (d >= to)
                {
                    break;
                }
            }
            return Rect.MinMaxRect(min.x - Margin, min.y - Margin, max.x + Margin, max.y + Margin);
        }

        /// <summary>True when every chunk a piece may change is loaded.</summary>
        public bool Loaded(World world, int heightInChunks, int piece)
        {
            Rect box = Bounds(piece, out float low, out float high);
            // The ground ramps a metre or so round its height, the land across the road may lie higher or lower than
            // along its middle, and the headroom is cleared above it.
            low -= 3f + Bed.Reach;
            high += Headroom + 2f + Bed.Reach;
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToChunk(new Vector3(box.xMin, Mathf.Max(0f, low), box.yMin));
            Vector3Int max = grid.WorldToChunk(new Vector3(box.xMax, high, box.yMax));
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

        /// <summary>Writes a piece into the world and marks it written.</summary>
        /// <param name="natural">The ground as generated at (x, z).</param>
        public void Write(World world, MaterialRegistry materials, Func<float, float, float> natural, float iso, int piece)
        {
            if (written[piece])
            {
                return;
            }
            written[piece] = true;
            Written++;
            Rect box = Bounds(piece, out _, out _);
            RoadBed bed = Bed;
            List<Vector2Int> stretches = bed.StretchesNear(box, bed.Reach);
            // This piece's own stretch, with a sample's overlap onto its neighbours; the road's ends take what lies beyond them.
            float from = piece == 0 ? float.MinValue : piece * PieceLength - 0.3f;
            float to = piece == written.Length - 1 ? float.MaxValue : (piece + 1) * PieceLength + 0.3f;
            VoxelMaterial surface = materials != null && !string.IsNullOrEmpty(bed.Definition.Surface) ? materials.Find(bed.Definition.Surface) : null;
            GroundLeveling.Level(world, materials, new GroundLeveling.Area
            {
                Centre = box.center,
                Half = box.size * 0.5f,
                FollowGround = true,
                Natural = natural,
                Headroom = Headroom,
                TreeReach = TreeReach,
                Paving = surface,
                Inside = (x, z) => bed.IsBed(new Vector2(x, z), stretches, from, to),
                Paved = (x, z) => bed.IsRoad(new Vector2(x, z), stretches),
                Surface = bed.FollowsGround ? (Func<float, float, float, float>)null : (x, z, ground) => bed.Surface(new Vector2(x, z), ground, stretches),
            }, iso);
        }

        /// <summary>Which pieces are written, as the piece count and hex digits (four pieces each), for the save.</summary>
        public string Save()
        {
            return PieceFlags.Save(written);
        }

        /// <summary>Marks the pieces a save says are written (a road of another length is ignored).</summary>
        public void Restore(string text)
        {
            Written += PieceFlags.Restore(text, written);
        }
    }
}
