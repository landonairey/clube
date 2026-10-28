using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where one level of the coarse voxel terrain (P7c) lies around the focus. Level l has voxels
    /// 2^l times the world's, so its chunks are 2^l times as wide, and covers a square of
    /// 2 × <see cref="HalfChunks"/> of its chunks centred near the focus: each level is twice as wide
    /// as the one inside it, which draws over its middle. The centre snaps to every second chunk,
    /// so a level moves only once the focus has crossed one of its own chunks.
    /// </summary>
    public readonly struct TerrainLodRing
    {
        public readonly int Level;

        /// <summary>Chunks each way from the centre.</summary>
        public readonly int HalfChunks;

        /// <summary>The centre, in this level's chunks (even): the corner shared by its four middle chunks.</summary>
        public readonly Vector2Int Centre;

        /// <summary>The level's chunk width in metres.</summary>
        public readonly float ChunkWidth;

        public TerrainLodRing(int level, int halfChunks, Vector2Int centre, float chunkWidth)
        {
            Level = level;
            HalfChunks = halfChunks;
            Centre = centre;
            ChunkWidth = chunkWidth;
        }

        /// <summary>How many times the world's voxel size a level's voxels are.</summary>
        public static float ScaleOf(int level)
        {
            return 1 << level;
        }

        /// <summary>The level's square around a focus (x, z in metres), for chunks <paramref name="chunkWidth"/> wide.</summary>
        public static TerrainLodRing Around(Vector2 focus, int level, int halfChunks, float chunkWidth)
        {
            var centre = new Vector2Int(
                2 * Mathf.RoundToInt(focus.x / (2f * chunkWidth)),
                2 * Mathf.RoundToInt(focus.y / (2f * chunkWidth)));
            return new TerrainLodRing(level, halfChunks, centre, chunkWidth);
        }

        /// <summary>The first column of the square (its x, z min), in this level's chunks.</summary>
        public Vector2Int First => Centre - new Vector2Int(HalfChunks, HalfChunks);

        /// <summary>Columns per side.</summary>
        public int Width => HalfChunks * 2;

        /// <summary>Half the square's width in metres: how far the level reaches from its centre.</summary>
        public float Reach => HalfChunks * ChunkWidth;

        /// <summary>The square in metres (x, z), min to max.</summary>
        public Rect Area => new Rect((Vector2)First * ChunkWidth, new Vector2(Width, Width) * ChunkWidth);

        /// <summary>
        /// True for the columns this level builds: its square, less the four middle columns, which the
        /// next finer level always covers however the two centres fall (see <see cref="Covers"/>).
        /// </summary>
        public bool Builds(Vector2Int column)
        {
            Vector2Int offset = column - Centre;
            bool inSquare = offset.x >= -HalfChunks && offset.x < HalfChunks && offset.y >= -HalfChunks && offset.y < HalfChunks;
            bool middle = offset.x >= -1 && offset.x < 1 && offset.y >= -1 && offset.y < 1;
            return inSquare && !middle;
        }

        /// <summary>
        /// True when <paramref name="finer"/>'s square, shrunk by <paramref name="margin"/> metres, lies on
        /// columns this level builds, so the finer level's edge always has this level under it.
        /// </summary>
        public bool Covers(TerrainLodRing finer, float margin)
        {
            Rect inner = finer.Area;
            Rect area = Area;
            Rect middle = new Rect((Vector2)(Centre - Vector2Int.one) * ChunkWidth, Vector2.one * (2f * ChunkWidth));
            Rect shrunk = Rect.MinMaxRect(inner.xMin + margin, inner.yMin + margin, inner.xMax - margin, inner.yMax - margin);
            bool inside = inner.xMin >= area.xMin && inner.yMin >= area.yMin && inner.xMax <= area.xMax && inner.yMax <= area.yMax;
            bool clearOfMiddle = shrunk.xMin <= middle.xMin && shrunk.yMin <= middle.yMin && shrunk.xMax >= middle.xMax && shrunk.yMax >= middle.yMax;
            return inside && clearOfMiddle;
        }

        public bool Equals(TerrainLodRing other)
        {
            return Level == other.Level && HalfChunks == other.HalfChunks && Centre == other.Centre && ChunkWidth == other.ChunkWidth;
        }
    }
}
