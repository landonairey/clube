using System;
using System.Collections.Generic;
using System.Text;
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
        public const float PieceLength = 2f;

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

        // The piece's flat rectangle: the box round its stretch of road (sampled every half metre), widened by the bed.
        private Rect Bounds(int piece, out float low, out float high)
        {
            RoadLine line = Bed.Line;
            float from = piece * PieceLength;
            float to = Mathf.Min(line.Length, from + PieceLength);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            low = float.MaxValue;
            high = float.MinValue;
            for (float d = from; ; d = Mathf.Min(to, d + 0.5f))
            {
                Vector2 p = line.Flat(d);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                float ground = line.HeightAlong(d), bed = Bed.HeightAlong(d);
                low = Mathf.Min(low, Mathf.Min(ground, bed));
                high = Mathf.Max(high, Mathf.Max(ground, bed));
                if (d >= to)
                {
                    break;
                }
            }
            float reach = Bed.Reach;
            return Rect.MinMaxRect(min.x - reach, min.y - reach, max.x + reach, max.y + reach);
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
            VoxelMaterial surface = materials != null && !string.IsNullOrEmpty(bed.Definition.Surface) ? materials.Find(bed.Definition.Surface) : null;
            GroundLeveling.Level(world, materials, new GroundLeveling.Area
            {
                Centre = box.center,
                Half = box.size * 0.5f,
                FollowGround = true,
                Natural = natural,
                Headroom = Headroom,
                Paving = surface,
                Inside = (x, z) => bed.IsBed(new Vector2(x, z), stretches),
                Paved = (x, z) => bed.IsRoad(new Vector2(x, z), stretches),
                Surface = bed.FollowsGround ? (Func<float, float, float, float>)null : (x, z, ground) => bed.Surface(new Vector2(x, z), ground, stretches),
            }, iso);
        }

        /// <summary>Which pieces are written, as the piece count and hex digits (four pieces each), for the save.</summary>
        public string Save()
        {
            var text = new StringBuilder();
            text.Append(written.Length).Append(':');
            for (int i = 0; i < written.Length; i += 4)
            {
                int nibble = 0;
                for (int k = 0; k < 4 && i + k < written.Length; k++)
                {
                    nibble |= written[i + k] ? 1 << k : 0;
                }
                text.Append("0123456789abcdef"[nibble]);
            }
            return text.ToString();
        }

        /// <summary>Marks the pieces a save says are written (a road of another length is ignored).</summary>
        public void Restore(string text)
        {
            int colon = string.IsNullOrEmpty(text) ? -1 : text.IndexOf(':');
            if (colon < 0 || !int.TryParse(text.Substring(0, colon), out int pieces) || pieces != written.Length
                || text.Length - colon - 1 != (written.Length + 3) / 4)
            {
                return;
            }
            for (int i = 0; i < written.Length; i++)
            {
                int nibble = Convert.ToInt32(text[colon + 1 + i / 4].ToString(), 16);
                if ((nibble & (1 << (i % 4))) != 0 && !written[i])
                {
                    written[i] = true;
                    Written++;
                }
            }
        }
    }
}
