using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A kind of light a settlement stands in its streets (GL716): a torch, a bonfire, an oil lantern. A row of
    /// <see cref="Defaults"/>; the planner and the game read these fields, never the kind's name (the game picks its model by <see cref="Id"/>).
    /// </summary>
    [Serializable]
    public sealed class SettlementLightKind
    {
        public string Id;
        public string Name;
        /// <summary>The side of the square of ground it takes, in metres.</summary>
        public float Size = 0.4f;
        /// <summary>How high its flame burns over the ground, in metres.</summary>
        public float FlameHeight = 1.8f;
        /// <summary>Its light's reach in metres and its brightness.</summary>
        public float Range = 12f;
        public float Intensity = 4f;

        /// <summary>
        /// GL716: "smaller ones have a couple permanent torches lit. Medium sized ones have a mix of torches and large
        /// bonfires the size of 2x2 building squares. Larger ones have oil burning lanterns."
        /// </summary>
        public static List<SettlementLightKind> Defaults()
        {
            return new List<SettlementLightKind>
            {
                new SettlementLightKind { Id = "torch", Name = "Torch", Size = 0.4f, FlameHeight = 1.75f, Range = 12f, Intensity = 4f },
                new SettlementLightKind { Id = "bonfire", Name = "Bonfire", Size = 2f, FlameHeight = 0.7f, Range = 18f, Intensity = 8f },
                new SettlementLightKind { Id = "lantern", Name = "Oil lantern", Size = 0.5f, FlameHeight = 2.3f, Range = 13f, Intensity = 4.5f },
            };
        }
    }

    /// <summary>How many of a kind of light a settlement type stands (GL716): a fixed number, or one beside every so many buildings.</summary>
    [Serializable]
    public sealed class SettlementLightRule
    {
        /// <summary>The <see cref="SettlementLightKind.Id"/>.</summary>
        public string Kind;
        /// <summary>A fixed number in the settlement (0: none by count).</summary>
        public int Count;
        /// <summary>One beside every this many buildings, from the centre out (0: none by buildings).</summary>
        public int EveryBuildings;

        public static SettlementLightRule Fixed(string kind, int count) => new SettlementLightRule { Kind = kind, Count = count };

        public static SettlementLightRule Every(string kind, int buildings) => new SettlementLightRule { Kind = kind, EveryBuildings = buildings };
    }

    /// <summary>One light of a settlement (GL716): what it is, where it stands, and the building it is put up with.</summary>
    public sealed class SettlementLight
    {
        public SettlementLightKind Kind;
        /// <summary>The middle of its ground (x, z), and the ground's height there (its building's terrace).</summary>
        public Vector2 Position;
        public float Ground;
        /// <summary>The building beside it (an index into <see cref="SettlementSite.Buildings"/>): it is stood once that building stands.</summary>
        public int Building;
        /// <summary>Its turn about y in degrees: its building's.</summary>
        public float Yaw;
    }

    /// <summary>
    /// Places a settlement's lights (GL716) by its type's rules (<see cref="SettlementType.Lights"/>): beside its
    /// buildings' fronts, from the centre out, on their terrace, clear of every building, lane and other light. Bigger
    /// kinds are placed first, so a bonfire gets the wide gaps.
    /// </summary>
    public static class SettlementLights
    {
        // Metres kept between a light and a building's wall, and between two lights.
        private const float WallGap = 0.3f;
        private const float LightGap = 3f;

        /// <summary>Fills <see cref="SettlementSite.Lights"/> from its type's rules and the kinds in <paramref name="kinds"/>.</summary>
        public static void Plan(SettlementSite site, IReadOnlyList<SettlementLightKind> kinds)
        {
            site.Lights.Clear();
            List<SettlementLightRule> rules = site.Settlement.Type.Lights;
            if (rules == null || kinds == null || site.Buildings.Count == 0)
            {
                return;
            }
            var ordered = new List<(SettlementLightRule Rule, SettlementLightKind Kind)>();
            foreach (SettlementLightRule rule in rules)
            {
                SettlementLightKind kind = Find(kinds, rule.Kind);
                if (kind != null)
                {
                    ordered.Add((rule, kind));
                }
            }
            ordered.Sort((a, b) => b.Kind.Size.CompareTo(a.Kind.Size));
            foreach ((SettlementLightRule rule, SettlementLightKind kind) in ordered)
            {
                int placed = 0;
                for (int b = 0; b < site.Buildings.Count; b++)
                {
                    bool byBuildings = rule.EveryBuildings > 0 && b % rule.EveryBuildings == 0;
                    bool byCount = rule.Count > 0 && placed < rule.Count;
                    if (!byBuildings && !byCount)
                    {
                        if (rule.EveryBuildings <= 0)
                        {
                            break;
                        }
                        continue;
                    }
                    // By count, both sides of a building may take one (a camp's one shop has its couple of torches).
                    for (int spot = 0; spot < 4; spot++)
                    {
                        if (TryPlace(site, kind, b, spot))
                        {
                            placed++;
                            if (byBuildings || placed >= rule.Count)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static SettlementLightKind Find(IReadOnlyList<SettlementLightKind> kinds, string id)
        {
            foreach (SettlementLightKind kind in kinds)
            {
                if (kind.Id == id)
                {
                    return kind;
                }
            }
            return null;
        }

        // A light at one of a building's spots, if the ground there is free: beside its front on either side (spots 0
        // and 1), else on the apron before a front corner (2 and 3).
        private static bool TryPlace(SettlementSite site, SettlementLightKind kind, int index, int spot)
        {
            SettlementBuilding building = site.Buildings[index];
            float half = kind.Size * 0.5f;
            float side = spot % 2 == 0 ? 1f : -1f;
            float w = building.Width * 0.5f, d = building.Depth * 0.5f;
            Vector2 local = spot < 2
                ? new Vector2(side * (w + WallGap + half + 0.05f), d - half)
                : new Vector2(side * (w - half), d + WallGap + half + 0.05f);
            Vector2 at = building.ToWorld(local);
            if (!Free(site, at, half))
            {
                return false;
            }
            site.Lights.Add(new SettlementLight { Kind = kind, Position = at, Ground = building.Pad, Building = index, Yaw = building.Yaw });
            return true;
        }

        /// <summary>True when a square of half side <paramref name="half"/> at a point is on a terrace and clear of buildings (and their yards), lanes and lights.</summary>
        public static bool Free(SettlementSite site, Vector2 at, float half)
        {
            var square = new Rect(at.x - half, at.y - half, half * 2f, half * 2f);
            bool onTerrace = false;
            foreach (SettlementTerrace terrace in site.Terraces)
            {
                if (terrace.Area.Contains(square.min) && terrace.Area.Contains(square.max))
                {
                    onTerrace = true;
                    break;
                }
            }
            if (!onTerrace)
            {
                return false;
            }
            foreach (SettlementBuilding other in site.Buildings)
            {
                BuildingDesign design = SettlementStructures.DesignOf(other);
                // Its walls and its yard out front, turned into the world.
                float yard = design.HasYard ? design.YardDepth : 0f;
                Vector2 a = other.ToWorld(new Vector2(-other.Width * 0.5f - WallGap, -other.Depth * 0.5f - WallGap));
                Vector2 b = other.ToWorld(new Vector2(other.Width * 0.5f + WallGap, other.Depth * 0.5f + yard + WallGap));
                Rect footprint = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (footprint.Overlaps(square))
                {
                    return false;
                }
            }
            float clear = site.Rules.StreetWidth * 0.5f + half + 0.2f;
            foreach (SettlementLane lane in site.Lanes)
            {
                for (int i = 1; i < lane.Points.Count; i++)
                {
                    if (SettlementSite.Distance(at, lane.Points[i - 1], lane.Points[i]) < clear)
                    {
                        return false;
                    }
                }
            }
            foreach (SettlementLight light in site.Lights)
            {
                if (Vector2.Distance(light.Position, at) < LightGap + half + light.Kind.Size * 0.5f)
                {
                    return false;
                }
            }
            return site.RoadBeyond(at.x, at.y) > half;
        }
    }
}
