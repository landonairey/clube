using System.Collections.Generic;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// How far round the viewer the far water's detailed tiles can stand in for its coarse sheets (GL761). The coarse
    /// sheets are cut away inside a square and the tiles drawn there, so the square may only reach as far as the tiles
    /// already built: a tile still waiting keeps the square short of it, and the coarse sheets go on drawing the water
    /// there. Positions are metres from the water map's origin; a tile's index is x + z * tiles across.
    /// </summary>
    public static class WaterDetailSquare
    {
        /// <summary>Metres from a point to a tile along the longer axis, the way the square measures: 0 inside it.</summary>
        public static float DistanceToTile(float2 centre, int tile, int tilesAcross, float tileWidth)
        {
            float2 low = new float2(tile % tilesAcross, tile / tilesAcross) * tileWidth;
            float2 away = math.max(math.max(low - centre, centre - (low + tileWidth)), 0f);
            return math.max(away.x, away.y);
        }

        /// <summary>The half width of the square: <paramref name="wanted"/>, or less to stop short of the nearest missing tile.</summary>
        public static float ReadyHalfWidth(float2 centre, HashSet<int> missing, int tilesAcross, float tileWidth, float wanted)
        {
            float halfWidth = wanted;
            foreach (int tile in missing)
            {
                halfWidth = math.min(halfWidth, DistanceToTile(centre, tile, tilesAcross, tileWidth));
            }
            return halfWidth;
        }

        /// <summary>The missing tile nearest the point, the one that lets the square grow first; -1 when none is missing.</summary>
        public static int Nearest(float2 centre, HashSet<int> missing, int tilesAcross, float tileWidth)
        {
            int nearest = -1;
            float best = float.MaxValue;
            foreach (int tile in missing)
            {
                float distance = DistanceToTile(centre, tile, tilesAcross, tileWidth);
                if (distance < best || (distance == best && tile < nearest))
                {
                    best = distance;
                    nearest = tile;
                }
            }
            return nearest;
        }
    }
}
