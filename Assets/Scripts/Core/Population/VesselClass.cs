using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A class of vessel (GL618), as a row: the water it sails ("smaller boats can use rivers and larger vessels use the
    /// sea"), how fast, how long it lies at each end, and the measures and fittings its hull is built from
    /// (<see cref="BoatMeshes"/>). A new class is a new row; a <see cref="NamedVessel"/> names the class it is one of.
    /// </summary>
    public sealed class VesselClass
    {
        public string Id;
        public string Name;

        /// <summary>The water routes it sails.</summary>
        public WaterRouteKind Kind;

        /// <summary>Metres a real second under way, at the clock's normal rate.</summary>
        public float Speed = 2.5f;

        /// <summary>Real seconds it lies at each end of its route, loading.</summary>
        public float RestSeconds = 150f;

        /// <summary>How many of the class sail each route of its kind; 0 for a class only named vessels and raiders are of.</summary>
        public int PerRoute = 1;

        /// <summary>True for thieves' boats (GL617): they sail from a thieves' camp, not on a route of their own.</summary>
        public bool Raider;

        /// <summary>The hull's length, greatest width, the deck's height over the water and the keel's depth under it, in metres.</summary>
        public float Length = 6f, Beam = 1.8f, Freeboard = 0.5f, Draught = 0.35f;

        /// <summary>Masts with a square sail each; none for a boat that is rowed or poled.</summary>
        public int Masts;

        /// <summary>A deckhouse aft, a wheelhouse forward, a smoke stack on the deckhouse.</summary>
        public bool Deckhouse, Wheelhouse, Stack;

        /// <summary>Oars over the sides.</summary>
        public bool Oars;

        public Color Hull = new Color(0.42f, 0.29f, 0.17f), Trim = new Color(0.3f, 0.2f, 0.12f), Sail = new Color(0.86f, 0.82f, 0.7f);

        /// <summary>Hands aboard, the first at the helm.</summary>
        public int Crew = 1;

        /// <summary>Crates of goods on deck (real storage).</summary>
        public int Crates = 2;

        /// <summary>How far it rolls on the water, in degrees, and how far it bobs, in metres.</summary>
        public float Roll = 3f, Bob = 0.06f;

        public const string RiverBoat = "river-boat", SeaTrader = "sea-trader", OreFreighter = "ore-freighter";
        public const string RiverRaider = "river-raider", SeaRaider = "sea-raider";

        /// <summary>The classes of the default world: a rowed river boat, a single-masted sea trader, the long ore freighter, and the thieves' two.</summary>
        public static List<VesselClass> Defaults()
        {
            return new List<VesselClass>
            {
                new VesselClass
                {
                    Id = RiverBoat, Name = "River boat", Kind = WaterRouteKind.River, Speed = 1.5f, RestSeconds = 120f,
                    Length = 4.6f, Beam = 1.5f, Freeboard = 0.32f, Draught = 0.18f, Oars = true, Crew = 1, Crates = 2, Roll = 2f, Bob = 0.03f,
                },
                new VesselClass
                {
                    Id = SeaTrader, Name = "Sea trader", Kind = WaterRouteKind.Sea, Speed = 3.4f, RestSeconds = 180f,
                    Length = 13f, Beam = 3.8f, Freeboard = 1.1f, Draught = 0.9f, Masts = 1, Deckhouse = true, Crew = 3, Crates = 4, Roll = 4f, Bob = 0.12f,
                },
                // The Edmund Fitzgerald's class: a long, low ore carrier, red hull, white houses fore and aft.
                new VesselClass
                {
                    Id = OreFreighter, Name = "Ore freighter", Kind = WaterRouteKind.Sea, Speed = 4.2f, RestSeconds = 240f, PerRoute = 0,
                    Length = 30f, Beam = 5.2f, Freeboard = 1.5f, Draught = 1.4f, Masts = 2, Deckhouse = true, Wheelhouse = true, Stack = true,
                    Hull = new Color(0.5f, 0.16f, 0.12f), Trim = new Color(0.9f, 0.88f, 0.82f), Sail = new Color(0.8f, 0.76f, 0.66f),
                    Crew = 4, Crates = 6, Roll = 1.5f, Bob = 0.08f,
                },
                new VesselClass
                {
                    Id = RiverRaider, Name = "Thieves' skiff", Kind = WaterRouteKind.River, Speed = 2.1f, RestSeconds = 60f, PerRoute = 0, Raider = true,
                    Length = 4.2f, Beam = 1.4f, Freeboard = 0.3f, Draught = 0.16f, Oars = true, Hull = new Color(0.16f, 0.15f, 0.15f),
                    Trim = new Color(0.32f, 0.12f, 0.12f), Crew = 2, Crates = 1, Roll = 2.5f, Bob = 0.03f,
                },
                new VesselClass
                {
                    Id = SeaRaider, Name = "Pirate cutter", Kind = WaterRouteKind.Sea, Speed = 4.4f, RestSeconds = 60f, PerRoute = 0, Raider = true,
                    Length = 9.5f, Beam = 2.8f, Freeboard = 0.8f, Draught = 0.7f, Masts = 1, Hull = new Color(0.14f, 0.13f, 0.14f),
                    Trim = new Color(0.32f, 0.12f, 0.12f), Sail = new Color(0.2f, 0.19f, 0.2f), Crew = 3, Crates = 2, Roll = 5f, Bob = 0.12f,
                },
            };
        }

        /// <summary>The class with an id, or null.</summary>
        public static VesselClass Find(IReadOnlyList<VesselClass> classes, string id)
        {
            foreach (VesselClass each in classes)
            {
                if (each.Id == id)
                {
                    return each;
                }
            }
            return null;
        }
    }
}
