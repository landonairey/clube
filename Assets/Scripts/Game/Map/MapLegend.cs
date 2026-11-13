using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>What a row of the world map's legend shows beside its words.</summary>
    public enum MapLegendSymbol
    {
        Line,
        Bridge,
        Swatch,
        Icon,
        Player,
    }

    /// <summary>A row of the legend: a symbol drawn the way the map draws it, and its name.</summary>
    public readonly struct MapLegendRow
    {
        public MapLegendRow(MapLegendSymbol symbol, string label, MapLineStyle line = default, Color colour = default, SettlementType type = null)
        {
            Symbol = symbol;
            Label = label;
            Line = line;
            Colour = colour;
            Type = type;
        }

        public MapLegendSymbol Symbol { get; }
        public string Label { get; }
        public MapLineStyle Line { get; }
        public Color Colour { get; }
        public SettlementType Type { get; }
    }

    /// <summary>
    /// The world map's legend (GL635) as rows of data: the three road classes and the two water routes in their line
    /// types (<see cref="MapPicture.RoadStyle"/>), a bridge, the sea and fresh water, each settlement type the plan
    /// has with its icon, and the player. A new road class, route kind or settlement type is a new row with no new
    /// drawing code; <see cref="WorldMapScreen"/> draws each symbol with what it draws the map with.
    /// </summary>
    public static class MapLegend
    {
        public const float RowHeight = 22f;
        public const float SymbolWidth = 40f;
        public const float Width = 200f;

        public static List<MapLegendRow> For(WorldPlan plan)
        {
            var rows = new List<MapLegendRow>();
            for (RoadClass roadClass = RoadClass.PavedCartPath; roadClass >= RoadClass.Footpath; roadClass--)
            {
                MapLineStyle style = MapPicture.RoadStyle(roadClass);
                rows.Add(new MapLegendRow(MapLegendSymbol.Line, style.Name, style));
            }
            rows.Add(new MapLegendRow(MapLegendSymbol.Bridge, "Bridge", MapPicture.RoadStyle(RoadClass.DirtCartPath)));
            foreach (WaterRouteKind kind in new[] { WaterRouteKind.Sea, WaterRouteKind.River })
            {
                MapLineStyle style = MapPicture.RouteStyle(kind);
                rows.Add(new MapLegendRow(MapLegendSymbol.Line, style.Name, style));
            }
            rows.Add(new MapLegendRow(MapLegendSymbol.Swatch, "Ocean", colour: MapPicture.Sea));
            rows.Add(new MapLegendRow(MapLegendSymbol.Swatch, "Lake, river", colour: MapPicture.FreshWater));

            // The settlement types on this map, largest first.
            var types = new List<SettlementType>();
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                if (!types.Contains(settlement.Type))
                {
                    types.Add(settlement.Type);
                }
            }
            types.Sort((a, b) => b.Parcels.CompareTo(a.Parcels));
            foreach (SettlementType type in types)
            {
                rows.Add(new MapLegendRow(MapLegendSymbol.Icon, type.Name, type: type));
            }
            rows.Add(new MapLegendRow(MapLegendSymbol.Player, "You"));
            return rows;
        }

        /// <summary>
        /// Where the legend goes: beside the map on its right when the screen has the room, else inside the map's
        /// lower left corner.
        /// </summary>
        public static Rect Place(Rect map, int rows, float screenWidth, float scale)
        {
            float width = Width * scale, height = rows * RowHeight * scale + 12f;
            return screenWidth - map.xMax >= width + 16f
                ? new Rect(map.xMax + 10f, map.yMax - height, width, height)
                : new Rect(map.xMin + 8f, map.yMax - height - 36f, width, height);
        }
    }
}
