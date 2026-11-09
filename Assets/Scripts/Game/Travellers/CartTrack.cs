using System;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Writes the trade cart's road into the terrain (GL477): a dirt track along the <see cref="CartRoute"/>, the land's
    /// shape kept and trees on it cut (<see cref="GroundLeveling"/> following the ground, like the town's square). The road
    /// is written a piece at a time, each once its chunks are loaded and only within a few milliseconds a frame, so a
    /// streamed world gets its far end when the player goes there. Which pieces are written is saved, so a loaded game
    /// doesn't write them again over the player's digging.
    /// </summary>
    public sealed class CartTrack
    {
        // Metres of headroom cleared over the track (a fallen rock).
        private const float Headroom = 3f;

        // Metres of road in a piece: short, so one piece is a few milliseconds (the whole road is about 1.5 ms a metre).
        private const float PieceLength = 2f;

        // Pieces looked at each frame for whether their chunks have loaded.
        private const int ChecksPerFrame = 16;

        private readonly CartRoute route;
        private readonly float halfWidth;
        private readonly bool[] written;
        private readonly float[] cornerDistances;
        private int cursor;

        public CartTrack(CartRoute route, float halfWidth)
        {
            this.route = route ?? throw new ArgumentNullException(nameof(route));
            this.halfWidth = halfWidth;
            written = new bool[Mathf.CeilToInt(route.Length / PieceLength)];
            cornerDistances = new float[route.Points.Count];
            for (int i = 1; i < route.Points.Count; i++)
            {
                cornerDistances[i] = cornerDistances[i - 1] + (route.Points[i] - route.Points[i - 1]).magnitude;
            }
        }

        /// <summary>Pieces of road (<see cref="PieceLength"/> metres each).</summary>
        public int Pieces => written.Length;

        /// <summary>Pieces written so far.</summary>
        public int Written { get; private set; }

        /// <summary>Milliseconds the longest piece took to write, and all of them together.</summary>
        public float LongestMilliseconds { get; private set; }

        public float TotalMilliseconds { get; private set; }

        /// <summary>
        /// Looks at a few pieces and writes those whose chunks are loaded, until <paramref name="budgetMilliseconds"/> is
        /// spent (always at least one piece once one is ready).
        /// </summary>
        public void Step(World world, WorldConfig config, Func<float, float, float> natural, VoxelMaterial paving, float budgetMilliseconds)
        {
            if (Written >= written.Length || written.Length == 0)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < ChecksPerFrame; i++)
            {
                int piece = cursor;
                cursor = (cursor + 1) % written.Length;
                if (written[piece] || !Loaded(world, config, piece))
                {
                    continue;
                }
                float before = Time.realtimeSinceStartup;
                Write(world, config, natural, paving, piece);
                float took = (Time.realtimeSinceStartup - before) * 1000f;
                LongestMilliseconds = Mathf.Max(LongestMilliseconds, took);
                TotalMilliseconds += took;
                if ((Time.realtimeSinceStartup - started) * 1000f >= budgetMilliseconds)
                {
                    return;
                }
            }
        }

        // The piece's flat rectangle: the box round its stretch of road (sampled every half metre), widened by the track.
        private Rect Bounds(int piece, out float low, out float high)
        {
            float from = piece * PieceLength;
            float to = Mathf.Min(route.Length, from + PieceLength);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            low = float.MaxValue;
            high = float.MinValue;
            for (float d = from; ; d = Mathf.Min(to, d + 0.5f))
            {
                Vector2 p = route.Flat(d);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                float h = route.HeightAlong(d);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
                if (d >= to)
                {
                    break;
                }
            }
            return Rect.MinMaxRect(min.x - halfWidth, min.y - halfWidth, max.x + halfWidth, max.y + halfWidth);
        }

        private bool Loaded(World world, WorldConfig config, int piece)
        {
            Rect box = Bounds(piece, out float low, out float high);
            // The ground ramps a metre or so round its height, and the headroom is cleared above it.
            low -= 3f;
            high += Headroom + 2f;
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToChunk(new Vector3(box.xMin, Mathf.Max(0f, low), box.yMin));
            Vector3Int max = grid.WorldToChunk(new Vector3(box.xMax, high, box.yMax));
            int layers = config.WorldHeightInChunks;
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(layers - 1, max.y); y++)
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

        // The corner at or before a distance along the road.
        private int CornerBefore(float distance)
        {
            int index = Array.BinarySearch(cornerDistances, distance);
            return index >= 0 ? index : Mathf.Max(0, ~index - 1);
        }

        private void Write(World world, WorldConfig config, Func<float, float, float> natural, VoxelMaterial paving, int piece)
        {
            written[piece] = true;
            Written++;
            Rect box = Bounds(piece, out _, out _);
            // The road's corners round this piece, so the track is full width at its ends too.
            int first = CornerBefore(piece * PieceLength) - 1;
            int last = CornerBefore((piece + 1) * PieceLength) + 2;
            GroundLeveling.Level(world, config.Materials, new GroundLeveling.Area
            {
                Centre = box.center,
                Half = box.size * 0.5f,
                FollowGround = true,
                Natural = natural,
                Headroom = Headroom,
                Paving = paving,
                Paved = (x, z) => route.DistanceTo(new Vector2(x, z), first, last) <= halfWidth,
            }, config.IsoLevel);
        }


        /// <summary>Which pieces are written, as hex digits (four pieces each), for the save.</summary>
        public string Save()
        {
            var text = new StringBuilder();
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
            if (string.IsNullOrEmpty(text) || text.Length != (written.Length + 3) / 4)
            {
                return;
            }
            Written = 0;
            for (int i = 0; i < written.Length; i++)
            {
                int nibble = Convert.ToInt32(text[i / 4].ToString(), 16);
                written[i] = (nibble & (1 << (i % 4))) != 0;
                Written += written[i] ? 1 : 0;
            }
        }
    }
}
