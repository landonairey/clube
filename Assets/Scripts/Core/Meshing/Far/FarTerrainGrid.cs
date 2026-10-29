using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One level of the far terrain (P7): a square grid of <see cref="Quads"/> × <see cref="Quads"/>
    /// quads, <see cref="Spacing"/> metres apart, centred near the focus. Level l is twice as
    /// coarse as level l - 1 and twice as wide, so ten levels of 128 quads from 2 m reach 65 km
    /// (a geometry clipmap). Each level is centred on a multiple of twice its spacing, which
    /// puts the finer level's edges exactly on this level's grid lines: the finer level fills
    /// this level's <see cref="Hole"/>, and their shared edge has no gaps (see
    /// <see cref="FarTerrainMeshJob"/> for the seam).
    /// </summary>
    public readonly struct FarTerrainGrid
    {
        /// <summary>Its level, 0 the finest.</summary>
        public readonly int Level;

        /// <summary>Metres between vertices (a power of two times the base spacing).</summary>
        public readonly float Spacing;

        /// <summary>Quads per side; a multiple of 4 so the finer level's edges land on whole quads.</summary>
        public readonly int Quads;

        /// <summary>The centre, in vertices of this level from the world origin (always even).</summary>
        public readonly int2 Centre;

        public FarTerrainGrid(int level, float spacing, int quads, int2 centre)
        {
            Level = level;
            Spacing = spacing;
            Quads = quads;
            Centre = centre;
        }

        /// <summary>The level-<paramref name="level"/> grid around a focus (x, z in metres).</summary>
        public static FarTerrainGrid Around(float2 focus, int level, float baseSpacing, int quads)
        {
            float spacing = baseSpacing * (1 << level);
            // Snapped to every second vertex, so the next coarser level's grid lines meet this one's edges.
            int2 centre = 2 * (int2)math.round(focus / (2f * spacing));
            return new FarTerrainGrid(level, spacing, quads, centre);
        }

        /// <summary>Levels needed for the coarsest to reach <paramref name="range"/> metres from its centre.</summary>
        public static int LevelsFor(float range, float baseSpacing, int quads)
        {
            float finest = quads * 0.5f * baseSpacing;
            return range <= finest ? 1 : Mathf.CeilToInt(Mathf.Log(range / finest, 2f)) + 1;
        }

        /// <summary>Vertex (0, 0), counted in vertices of this level from the world origin.</summary>
        public int2 First => Centre - Quads / 2;

        public int VertexCount => (Quads + 1) * (Quads + 1);

        /// <summary>Half the grid's width, in metres.</summary>
        public float HalfExtent => Quads * 0.5f * Spacing;

        /// <summary>World (x, z) of the centre, in metres.</summary>
        public float2 CentrePosition => (float2)Centre * Spacing;

        /// <summary>
        /// The quads of this grid that <paramref name="finer"/> covers, as (x min, z min, x max, z max),
        /// max exclusive. <paramref name="finer"/> must be the next finer level around the same focus.
        /// </summary>
        public int4 Hole(FarTerrainGrid finer)
        {
            // The finer grid's first vertex is even (its centre is, and so is half its width), so
            // it falls on one of this grid's vertices.
            int2 start = finer.First / 2 - First;
            return new int4(start, start + Quads / 2);
        }

        /// <summary>Quads drawn: all of them but the hole's.</summary>
        public static int QuadsDrawn(int quads, int4 hole)
        {
            int2 size = math.max(0, hole.zw - hole.xy);
            return quads * quads - size.x * size.y;
        }

        public bool Equals(FarTerrainGrid other)
        {
            return Level == other.Level && Quads == other.Quads && Spacing == other.Spacing && math.all(Centre == other.Centre);
        }
    }
}
