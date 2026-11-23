using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A fixture standing in a building's work yard (GL713, GL714): an anvil, a tool rack, a spinning wheel. Its
    /// <see cref="Kind"/> names the model the game stands there; a worker of the shop stands at <see cref="Stand"/> to work it.
    /// </summary>
    [Serializable]
    public sealed class YardFixture
    {
        /// <summary>The model's name ("anvil", "tool-rack", "spinning-wheel", "loom", "furnace", "tub", ...).</summary>
        public string Kind;
        /// <summary>Its middle in the building's own frame (x across its front, z out of its front), in metres.</summary>
        public Vector2 Local;
        /// <summary>Its turn about y from the building's, in degrees.</summary>
        public float Yaw;
        /// <summary>True when a worker stands at it to work (an anvil, a wheel); false for a rack or a stack.</summary>
        public bool Worked = true;
        /// <summary>The duties (<see cref="Duty.Id"/>) done at it ("hammer" at an anvil); empty for any of the shop's yard duties.</summary>
        public string[] Duties = Array.Empty<string>();
        /// <summary>Where the worker stands, in the building's frame: in front of the fixture, facing it.</summary>
        public Vector2 Stand;
    }

    /// <summary>
    /// One building of the settlement catalog (GL710): its footprint, stories, roof pitch and work yard, and the
    /// settlements and shops it is built for. A row of <see cref="Defaults"/>; the layout, the voxels, the walkers and the
    /// game read these fields, never the building's name.
    /// </summary>
    [Serializable]
    public sealed class BuildingDesign
    {
        public string Id;
        public string Name;
        /// <summary>Metres across its front and from front to back. A shop keeps the trading post's frame (<see cref="TownLayout.ShopWidth"/>).</summary>
        public float Width = TownLayout.ShopWidth;
        public float Depth = TownLayout.ShopDepth;
        /// <summary>How many floors it has, 1 to 3.</summary>
        public int Stories = 1;
        /// <summary>Metres from one floor to the next.</summary>
        public float StoryHeight = TownLayout.ShopHeight;
        /// <summary>GL711: the roof's pitch, rise over run, picked between these per building so a row isn't uniform.</summary>
        public float PitchMin = 0.5f;
        public float PitchMax = 0.9f;
        /// <summary>True for a shop's open front with a counter (the keeper trades over it); false for a closed front with a door.</summary>
        public bool ShopFront;
        /// <summary>The settlement types (<see cref="SettlementType.Id"/>) it is built in; empty for any.</summary>
        public string[] Types = Array.Empty<string>();
        /// <summary>The shop specialties (<see cref="EconomySpecialty.Id"/>) it houses; empty for a house (no shop).</summary>
        public string[] Specialties = Array.Empty<string>();
        /// <summary>How often it is picked among the rows that fit.</summary>
        public float Weight = 1f;
        /// <summary>GL714: a canvas tarp on posts over the work yard in front of it.</summary>
        public bool Tarp;
        /// <summary>GL713: low stone walls round the work yard's sides (a courtyard).</summary>
        public bool YardWalls;
        /// <summary>How far the work yard reaches out of its front, in metres (the apron before the street).</summary>
        public float YardDepth = 2.6f;
        /// <summary>The fixtures in its work yard; its workers stand at the worked ones.</summary>
        public List<YardFixture> Fixtures = new List<YardFixture>();

        /// <summary>GL715: built all of stone (a castle, a guard tower) rather than boards over a stone plinth.</summary>
        public bool Masonry;
        /// <summary>GL715: a flat stone roof walled round by a crenellated parapet, in place of a gable roof.</summary>
        public bool Battlements;
        /// <summary>GL715: the side of a square turret at each corner, rising over the parapet, in metres (0: none).</summary>
        public float Turrets;
        /// <summary>
        /// GL715: a settlement's landmark (the capital's castle, a city's guard tower): never picked by <see cref="Choose"/>,
        /// only built where a settlement type names it (<see cref="SettlementType.Landmarks"/>).
        /// </summary>
        public bool Landmark;

        /// <summary>Metres a battlemented roof's parapet stands over the roof, and a turret over the parapet.</summary>
        public const float ParapetHeight = 1f;
        public const float TurretRise = 2.5f;

        /// <summary>True when it has a work yard to build: fixtures, a tarp or walls.</summary>
        public bool HasYard => Fixtures.Count > 0 || Tarp || YardWalls;

        /// <summary>The fixtures a worker stands at.</summary>
        public IEnumerable<YardFixture> WorkedFixtures()
        {
            foreach (YardFixture fixture in Fixtures)
            {
                if (fixture.Worked)
                {
                    yield return fixture;
                }
            }
        }

        /// <summary>True when it may be built in a settlement of this type.</summary>
        public bool BuiltIn(SettlementType type)
        {
            return Types.Length == 0 || (type != null && Array.IndexOf(Types, type.Id) >= 0);
        }

        /// <summary>True when it houses a shop of this specialty.</summary>
        public bool Houses(string specialty)
        {
            return specialty != null && Array.IndexOf(Specialties, specialty) >= 0;
        }

        // A fixture with its worker 0.8 m out in front of it (towards the street), facing it.
        private static YardFixture At(string kind, float x, float z, float yaw = 0f, bool worked = true, string duty = null)
        {
            return new YardFixture
            {
                Kind = kind, Local = new Vector2(x, z), Yaw = yaw, Worked = worked, Stand = new Vector2(x, z + 0.8f),
                Duties = duty != null ? new[] { duty } : Array.Empty<string>(),
            };
        }

        /// <summary>
        /// GL713/GL714: the fixture a shop's worker on a yard duty is stationed at, by its place among the shop's workers:
        /// one whose duties name it, else any worked one; null when the yard has none.
        /// </summary>
        public YardFixture StationFor(string duty, int worker)
        {
            var named = new List<YardFixture>();
            var worked = new List<YardFixture>();
            foreach (YardFixture fixture in WorkedFixtures())
            {
                worked.Add(fixture);
                if (duty != null && Array.IndexOf(fixture.Duties, duty) >= 0)
                {
                    named.Add(fixture);
                }
            }
            List<YardFixture> from = named.Count > 0 ? named : worked;
            return from.Count > 0 ? from[Mathf.Abs(worker) % from.Count] : null;
        }

        /// <summary>
        /// The catalog (GL710: "1, 2, 3 story buildings, houses, mills, tavern, stable, blacksmith workshop, wood workshop,
        /// smelting areas, the whole lot of typical medieval dwellings"). Houses by settlement type; shops by specialty, with
        /// the smithy's anvil courtyard (GL713) and the fabric maker's tarp-covered workshop (GL714).
        /// </summary>
        public static List<BuildingDesign> Defaults()
        {
            float front = TownLayout.ShopDepth * 0.5f;
            return new List<BuildingDesign>
            {
                // --- Houses ---
                new BuildingDesign { Id = "hut", Name = "Hut", Width = 5f, Depth = 4.5f, Stories = 1, StoryHeight = 2.5f, PitchMin = 0.8f, PitchMax = 1.1f,
                    Types = new[] { "camp", "thieves-camp", "outpost", "village" } },
                new BuildingDesign { Id = "cottage", Name = "Cottage", Width = 6f, Depth = 4.5f, Stories = 1, PitchMin = 0.6f, PitchMax = 1f,
                    Types = new[] { "outpost", "village", "town" }, Weight = 2f },
                new BuildingDesign { Id = "longhouse", Name = "Longhouse", Width = 7.5f, Depth = 5f, Stories = 1, PitchMin = 0.5f, PitchMax = 0.75f,
                    Types = new[] { "village", "town" } },
                new BuildingDesign { Id = "two-story-house", Name = "Two-story house", Width = 6f, Depth = 5f, Stories = 2, PitchMin = 0.6f, PitchMax = 1f,
                    Types = new[] { "village", "town", "city", "capital" }, Weight = 2f },
                new BuildingDesign { Id = "townhouse", Name = "Townhouse", Width = 5.5f, Depth = 5.5f, Stories = 3, StoryHeight = 2.5f, PitchMin = 0.9f, PitchMax = 1.2f,
                    Types = new[] { "town", "city", "capital" }, Weight = 1.5f },
                new BuildingDesign { Id = "tavern", Name = "Tavern", Width = 7.5f, Depth = 6f, Stories = 2, PitchMin = 0.55f, PitchMax = 0.8f,
                    Types = new[] { "village", "town", "city", "capital" }, Weight = 0.5f },
                new BuildingDesign { Id = "stable", Name = "Stable", Width = 7.5f, Depth = 5f, Stories = 1, StoryHeight = 2.5f, PitchMin = 0.4f, PitchMax = 0.6f,
                    Types = new[] { "outpost", "village", "town", "city" }, Weight = 0.5f,
                    Fixtures = { At("trough", 0f, front + 1.2f, 0f, false) } },
                new BuildingDesign { Id = "mill", Name = "Mill", Width = 6f, Depth = 6f, Stories = 2, StoryHeight = 3f, PitchMin = 0.8f, PitchMax = 1.1f,
                    Types = new[] { "village", "town", "city" }, Weight = 0.4f },

                // --- Shops: the trading post's frame (the keeper's counter), stories and a work yard by specialty ---
                new BuildingDesign
                {
                    Id = "smithy", Name = "Blacksmith workshop", ShopFront = true, Stories = 1, PitchMin = 0.45f, PitchMax = 0.7f,
                    Specialties = new[] { "blacksmith", "goldsmith", "jeweler" }, YardWalls = true, YardDepth = 2.8f,
                    Fixtures =
                    {
                        At("anvil", -1.6f, front + 1.1f, duty: "hammer"), At("anvil", 1.6f, front + 1.1f, duty: "hammer"),
                        At("tool-rack", -2.7f, front + 0.4f, 90f, false), At("tool-rack", 2.7f, front + 0.4f, -90f, false),
                    },
                },
                new BuildingDesign
                {
                    Id = "smelter", Name = "Smelting yard", ShopFront = true, Stories = 1, PitchMin = 0.4f, PitchMax = 0.6f,
                    Specialties = new[] { "smelting", "crushing" }, YardWalls = true,
                    Fixtures = { At("furnace", -1.8f, front + 1.1f, duty: "stoke"), At("campfire", -0.1f, front + 0.9f, 0f, false), At("anvil", 1.4f, front + 1.1f, duty: "hammer"), At("ore-pile", 2.6f, front + 0.5f, 0f, false) },
                },
                new BuildingDesign
                {
                    Id = "fabric-workshop", Name = "Fabric workshop", ShopFront = true, Stories = 2, PitchMin = 0.6f, PitchMax = 0.9f,
                    Specialties = new[] { "fabrics", "clothing" }, Tarp = true, YardDepth = 2.8f,
                    Fixtures =
                    {
                        At("spinning-wheel", -2f, front + 1.1f, duty: "spin"), At("spinning-wheel", 0f, front + 1.1f, duty: "spin"), At("loom", 2f, front + 1.1f, duty: "spin"),
                    },
                },
                new BuildingDesign
                {
                    Id = "tannery", Name = "Tannery", ShopFront = true, Stories = 1, PitchMin = 0.5f, PitchMax = 0.7f,
                    Specialties = new[] { "tannery", "leathery" }, Tarp = true,
                    Fixtures = { At("hide-frame", -1.6f, front + 1.1f, duty: "tan"), At("tub", 1.6f, front + 1.1f, duty: "tan") },
                },
                new BuildingDesign
                {
                    Id = "wood-workshop", Name = "Wood workshop", ShopFront = true, Stories = 1, PitchMin = 0.5f, PitchMax = 0.8f,
                    Specialties = new[] { "lumber-supplies", "carpentry", "tool-componentry" }, Tarp = true,
                    Fixtures = { At("sawhorse", -1.6f, front + 1.1f), At("workbench", 1.6f, front + 1.1f), At("log-pile", 2.7f, front + 0.4f, 90f, false) },
                },
                new BuildingDesign
                {
                    Id = "mason-yard", Name = "Mason's yard", ShopFront = true, Stories = 1, PitchMin = 0.4f, PitchMax = 0.6f,
                    Specialties = new[] { "masonry", "stone-quarry", "limestone-quarry", "aggregate-supplies", "mining", "sand-pit", "clay-pit" },
                    YardWalls = true, Fixtures = { At("workbench", 0f, front + 1.1f), At("ore-pile", -2.4f, front + 0.5f, 0f, false) },
                },
                new BuildingDesign { Id = "two-story-shop", Name = "Shop with rooms over", ShopFront = true, Stories = 2, PitchMin = 0.6f, PitchMax = 1f,
                    Types = new[] { "village", "town", "city", "capital" } },
                new BuildingDesign { Id = "shop", Name = "Shop", ShopFront = true, Stories = 1, PitchMin = 0.5f, PitchMax = 0.9f },

                // --- Landmarks (GL715): "The Capital needs to be more grand including a Castle ... Cities have at least one
                // main stone guard tower." Built only where a settlement type names them. ---
                new BuildingDesign { Id = "castle", Name = "Castle", Width = 8f, Depth = 8f, Stories = 3, StoryHeight = 3.5f, Masonry = true,
                    Battlements = true, Turrets = 2f, Landmark = true, Types = new[] { "capital" }, Weight = 0f },
                new BuildingDesign { Id = "guard-tower", Name = "Guard tower", Width = 4.5f, Depth = 4.5f, Stories = 3, StoryHeight = 4f, Masonry = true,
                    Battlements = true, Landmark = true, Types = new[] { "city", "capital" }, Weight = 0f },
            };
        }

        /// <summary>
        /// The design a building is built to (GL710): a shop's row by its specialty (else a plain shop that fits the
        /// settlement), a house's by the settlement type, picked by weight with <paramref name="pick"/> (0 to 1).
        /// </summary>
        public static BuildingDesign Choose(IReadOnlyList<BuildingDesign> catalog, SettlementType type, string specialty, bool shop, float pick)
        {
            var fits = new List<BuildingDesign>();
            foreach (BuildingDesign design in catalog)
            {
                if (!design.Landmark && design.ShopFront == shop && design.BuiltIn(type) && (!shop || design.Houses(specialty)))
                {
                    fits.Add(design);
                }
            }
            if (fits.Count == 0)
            {
                // A shop with no workshop of its own: a plain shop (no specialty rows).
                foreach (BuildingDesign design in catalog)
                {
                    if (!design.Landmark && design.ShopFront == shop && design.BuiltIn(type) && design.Specialties.Length == 0)
                    {
                        fits.Add(design);
                    }
                }
            }
            if (fits.Count == 0)
            {
                foreach (BuildingDesign design in catalog)
                {
                    if (!design.Landmark && design.ShopFront == shop)
                    {
                        fits.Add(design);
                    }
                }
            }
            if (fits.Count == 0)
            {
                return null;
            }
            float total = 0f;
            foreach (BuildingDesign design in fits)
            {
                total += Mathf.Max(0f, design.Weight);
            }
            float at = Mathf.Clamp01(pick) * total;
            foreach (BuildingDesign design in fits)
            {
                at -= Mathf.Max(0f, design.Weight);
                if (at < 0f)
                {
                    return design;
                }
            }
            return fits[fits.Count - 1];
        }

        /// <summary>The catalog row of this id, or null.</summary>
        public static BuildingDesign Find(IReadOnlyList<BuildingDesign> catalog, string id)
        {
            if (catalog != null)
            {
                foreach (BuildingDesign design in catalog)
                {
                    if (design.Id == id)
                    {
                        return design;
                    }
                }
            }
            return null;
        }

        /// <summary>The widest and deepest of a catalog's rows: the ground a layout keeps for any building.</summary>
        /// <param name="type">The settlement it is for: a landmark row counts only where this type builds it (GL715); null for all rows.</param>
        public static Vector2 LargestFootprint(IReadOnlyList<BuildingDesign> catalog, SettlementType type = null)
        {
            var largest = new Vector2(TownLayout.ShopWidth, TownLayout.ShopDepth);
            foreach (BuildingDesign design in catalog)
            {
                if (design.Landmark && type != null && (type.Landmarks == null || Array.IndexOf(type.Landmarks, design.Id) < 0))
                {
                    continue;
                }
                largest = Vector2.Max(largest, new Vector2(design.Width, design.Depth));
            }
            return largest;
        }
    }
}
