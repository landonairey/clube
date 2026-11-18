using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Where the yellow arrow over the family's parcel hangs (GL765): over the parcel's centre and above every tree on the
    /// parcel and just round it, so it can be seen from where the cart stops. Worked out from the map and the tree
    /// field, so it doesn't depend on the parcel's chunks having loaded.
    /// </summary>
    public static class ParcelArrowSpot
    {
        // Metres the tip hangs over the ground at least, over the tallest tree, and how far past the parcel's sides trees count.
        private const float LeastHeight = 9f;
        private const float OverTrees = 3f;
        private const float TreesReach = 8f;

        /// <summary>The arrow's tip in world space; <paramref name="fallbackY"/> is the ground's height where nothing else tells it.</summary>
        public static Vector3 TipOver(WorldView world, LandParcels land, Vector2Int parcel, float fallbackY)
        {
            Vector2 centre = land.CentreOf(parcel);
            float ground = GroundAt(world, centre, fallbackY);
            float tip = ground + LeastHeight;
            Rect area = land.AreaOf(parcel);
            float? tallest = world.Trees?.TallestTop(Rect.MinMaxRect(area.xMin - TreesReach, area.yMin - TreesReach, area.xMax + TreesReach, area.yMax + TreesReach));
            if (tallest.HasValue)
            {
                tip = Mathf.Max(tip, tallest.Value + OverTrees);
            }
            return world.transform.TransformPoint(new Vector3(centre.x, tip, centre.y));
        }

        private static float GroundAt(WorldView world, Vector2 centre, float fallbackY)
        {
            MapSession session = MapSession.Current;
            if (session != null && session.Map != null)
            {
                return session.Map.Height(centre.x, centre.y);
            }
            float top = world.Config.WorldHeightInChunks * world.World.Grid.ChunkWorldSize.y;
            Vector3 from = world.transform.TransformPoint(new Vector3(centre.x, top, centre.y));
            return world.Raycast(new Ray(from, -world.transform.up), out Vector3 hit) ? world.transform.InverseTransformPoint(hit).y : fallbackY;
        }
    }
}
