using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One kind of map outline (GL638), as data: how many sides are sea, how far the mainland reaches in from the
    /// others, and the lands, islets, bays and ranges laid in the map. The seed picks one by <see cref="Weight"/>
    /// and <see cref="MapLandBuilder"/> lays it out; a new kind is a new entry in
    /// <see cref="MapDefinition.ShapeTypes"/>, and nothing switches on the name. Sizes are shares of half the map's
    /// side, so an outline keeps its look at any map size.
    /// </summary>
    [Serializable]
    public class MapShapeType
    {
        [SerializeField]
        private string name = "Continent";

        [Tooltip("How often the seed picks this outline, against the others.")]
        [SerializeField, Min(0f)]
        private float weight = 1f;

        [Tooltip("Fewest and most sides that are sea.")]
        [SerializeField]
        private Vector2Int oceanSides = new Vector2Int(1, 2);

        [Tooltip("How far the mainland reaches in from a side that isn't sea, as a share of the map's width (least, most).")]
        [SerializeField]
        private Vector2 mainland = new Vector2(0.6f, 0.85f);

        [Tooltip("Fewest and most round lands laid in the map: islands out at sea, headlands on a mainland's coast.")]
        [SerializeField]
        private Vector2Int lands = new Vector2Int(2, 4);

        [Tooltip("Their radius (least, most), as a share of half the map.")]
        [SerializeField]
        private Vector2 landRadius = new Vector2(0.2f, 0.4f);

        [Tooltip("How far from the middle they are laid, as a share of half the map.")]
        [SerializeField, Range(0f, 1f)]
        private float spread = 0.7f;

        [Tooltip("Fewest and most arms the lands are strung along (a chain of islands, peninsulas off a mainland); 0 scatters them.")]
        [SerializeField]
        private Vector2Int arms = Vector2Int.zero;

        [Tooltip("Fewest and most small islands scattered round the lands.")]
        [SerializeField]
        private Vector2Int islets = new Vector2Int(1, 4);

        [Tooltip("Their radius (least, most), as a share of half the map.")]
        [SerializeField]
        private Vector2 isletRadius = new Vector2(0.04f, 0.09f);

        [Tooltip("Fewest and most bays the sea cuts into the lands.")]
        [SerializeField]
        private Vector2Int bays = new Vector2Int(1, 3);

        [Tooltip("How far the coast wanders from the round lands' own line, against the usual (1).")]
        [SerializeField, Range(0f, 2f)]
        private float coastWander = 1f;

        [Tooltip("Fewest and most mountain ranges besides the main one.")]
        [SerializeField]
        private Vector2Int ranges = new Vector2Int(0, 1);

        public MapShapeType()
        {
        }

        public MapShapeType(
            string name, float weight, Vector2Int oceanSides, Vector2 mainland, Vector2Int lands, Vector2 landRadius, float spread,
            Vector2Int arms, Vector2Int islets, Vector2 isletRadius, Vector2Int bays, float coastWander, Vector2Int ranges)
        {
            this.name = name;
            this.weight = weight;
            this.oceanSides = oceanSides;
            this.mainland = mainland;
            this.lands = lands;
            this.landRadius = landRadius;
            this.spread = spread;
            this.arms = arms;
            this.islets = islets;
            this.isletRadius = isletRadius;
            this.bays = bays;
            this.coastWander = coastWander;
            this.ranges = ranges;
        }

        public string Name => name;
        public float Weight => Mathf.Max(0f, weight);

        /// <summary>Fewest (x) and most (y) sides that are sea, each 1 to 4 (GL589: a map always touches an ocean).</summary>
        public Vector2Int OceanSides
        {
            get
            {
                int least = Mathf.Clamp(oceanSides.x, 1, 4);
                return new Vector2Int(least, Mathf.Clamp(oceanSides.y, least, 4));
            }
        }

        public Vector2 Mainland => new Vector2(Mathf.Clamp01(mainland.x), Mathf.Clamp01(Mathf.Max(mainland.x, mainland.y)));
        public Vector2Int Lands => Range(lands);
        public Vector2 LandRadius => Range(landRadius, 0.02f, 1f);
        public float Spread => Mathf.Clamp01(spread);
        public Vector2Int Arms => Range(arms);
        public Vector2Int Islets => Range(islets);
        public Vector2 IsletRadius => Range(isletRadius, 0.01f, 0.5f);
        public Vector2Int Bays => Range(bays);
        public float CoastWander => Mathf.Clamp(coastWander, 0f, 2f);
        public Vector2Int Ranges => Range(ranges);

        /// <summary>How far a count of sea sides is from what this outline allows: 0 when it fits.</summary>
        public int MisfitOf(int seaSides)
        {
            Vector2Int allowed = OceanSides;
            return seaSides < allowed.x ? allowed.x - seaSides : seaSides > allowed.y ? seaSides - allowed.y : 0;
        }

        private static Vector2Int Range(Vector2Int value)
        {
            int least = Mathf.Max(0, value.x);
            return new Vector2Int(least, Mathf.Max(least, value.y));
        }

        private static Vector2 Range(Vector2 value, float least, float most)
        {
            float low = Mathf.Clamp(value.x, least, most);
            return new Vector2(low, Mathf.Clamp(value.y, low, most));
        }

        public const string ContinentName = "Continent";
        public const string PeninsulasName = "Peninsulas";
        public const string IslandName = "Island and satellites";
        public const string IslandChainName = "Island chain";
        public const string ArchipelagoName = "Archipelago";

        /// <summary>
        /// The outlines the game starts with (GL638): mostly continent with the sea on a side or two, peninsulas
        /// reaching out from a mainland, a big island with satellites, a chain of islands, and an archipelago.
        /// </summary>
        public static List<MapShapeType> Defaults()
        {
            return new List<MapShapeType>
            {
                new MapShapeType(ContinentName, 3f, new Vector2Int(1, 2), new Vector2(0.6f, 0.82f), new Vector2Int(2, 4),
                    new Vector2(0.2f, 0.38f), 0.75f, Vector2Int.zero, new Vector2Int(1, 4), new Vector2(0.04f, 0.09f),
                    new Vector2Int(2, 3), 1f, new Vector2Int(1, 2)),
                new MapShapeType(PeninsulasName, 2f, new Vector2Int(2, 3), new Vector2(0.12f, 0.24f), new Vector2Int(7, 10),
                    new Vector2(0.11f, 0.2f), 0.7f, new Vector2Int(2, 3), new Vector2Int(2, 5), new Vector2(0.04f, 0.09f),
                    new Vector2Int(1, 2), 1f, new Vector2Int(0, 1)),
                new MapShapeType(IslandName, 2f, new Vector2Int(4, 4), new Vector2(0.05f, 0.1f), new Vector2Int(2, 3),
                    new Vector2(0.42f, 0.56f), 0.22f, Vector2Int.zero, new Vector2Int(4, 7), new Vector2(0.07f, 0.15f),
                    new Vector2Int(2, 3), 1f, new Vector2Int(0, 1)),
                new MapShapeType(IslandChainName, 1.5f, new Vector2Int(4, 4), new Vector2(0.05f, 0.1f), new Vector2Int(4, 6),
                    new Vector2(0.14f, 0.24f), 0.85f, new Vector2Int(1, 1), new Vector2Int(2, 4), new Vector2(0.04f, 0.09f),
                    new Vector2Int(0, 1), 1f, new Vector2Int(0, 0)),
                new MapShapeType(ArchipelagoName, 1.5f, new Vector2Int(4, 4), new Vector2(0.05f, 0.1f), new Vector2Int(8, 12),
                    new Vector2(0.11f, 0.24f), 0.82f, Vector2Int.zero, new Vector2Int(5, 8), new Vector2(0.04f, 0.09f),
                    new Vector2Int(0, 2), 1.2f, new Vector2Int(1, 2)),
            };
        }
    }
}
