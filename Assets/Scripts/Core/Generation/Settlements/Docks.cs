using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A kind of dock (GL726), as a row: "a set of marina docks that match the size of town. Larger capital, a more grand
    /// set of docks with paved connected to the downtown... Smaller village or outpost only need a small foot dock
    /// connected to the couple buildings". A new kind is a new row.
    /// </summary>
    public sealed class DockDesign
    {
        public string Id;
        public string Name;

        /// <summary>The settlement types (<see cref="SettlementType.Id"/>) that get this dock.</summary>
        public string[] Types = Array.Empty<string>();

        /// <summary>The pier's width, and the longest it runs out from the shore, in metres.</summary>
        public float Width = 1.2f, MaxLength = 20f;

        /// <summary>The head across the pier's end (a T), its length and width; 0 for none.</summary>
        public float HeadLength, HeadWidth;

        /// <summary>How many vessels can lie alongside at once.</summary>
        public int Berths = 1;

        /// <summary>True for a stone quay with a paved way up to the town; false for planks and a trodden path.</summary>
        public bool Stone;

        /// <summary>The way from the nearest building down to the pier: its width in metres.</summary>
        public float PathWidth = 1.2f;

        /// <summary>The deck's height over the water, in metres.</summary>
        public float DeckRise = 0.8f;

        public static List<DockDesign> Defaults()
        {
            return new List<DockDesign>
            {
                new DockDesign
                {
                    Id = "foot-dock", Name = "Foot dock", Types = new[] { "outpost", "village" }, Width = 1.2f, MaxLength = 22f, Berths = 1, PathWidth = 1.2f,
                    DeckRise = 0.6f,
                },
                new DockDesign
                {
                    Id = "pier", Name = "Pier", Types = new[] { "town" }, Width = 2.4f, MaxLength = 32f, HeadLength = 12f, HeadWidth = 3f, Berths = 2,
                    PathWidth = 2f,
                },
                new DockDesign
                {
                    Id = "harbour", Name = "Harbour", Types = new[] { "city" }, Width = 3.5f, MaxLength = 42f, HeadLength = 20f, HeadWidth = 4f, Berths = 3,
                    PathWidth = 3f, DeckRise = 1f,
                },
                new DockDesign
                {
                    Id = "grand-docks", Name = "Grand docks", Types = new[] { "capital" }, Width = 6f, MaxLength = 50f, HeadLength = 34f, HeadWidth = 7f,
                    Berths = 4, Stone = true, PathWidth = 5f, DeckRise = 1.2f,
                },
            };
        }

        /// <summary>The row for a settlement type; null when it gets no dock (camps, thieves).</summary>
        public static DockDesign For(IReadOnlyList<DockDesign> designs, SettlementType type)
        {
            if (designs == null || type == null || type.Hostile)
            {
                return null;
            }
            foreach (DockDesign design in designs)
            {
                if (Array.IndexOf(design.Types, type.Id) >= 0)
                {
                    return design;
                }
            }
            return null;
        }
    }

    /// <summary>A place a vessel lies alongside a dock (GL726): a point on the deck's edge, the way off it over the water, and the way along it.</summary>
    public readonly struct DockBerth
    {
        public DockBerth(Vector2 edge, Vector2 outward, Vector2 along)
        {
            Edge = edge;
            Outward = outward;
            Along = along;
        }

        public Vector2 Edge { get; }

        public Vector2 Outward { get; }

        public Vector2 Along { get; }

        /// <summary>Where a vessel of <paramref name="beam"/> lies: its middle off the edge by half its beam and a fender's width.</summary>
        public Vector2 Mooring(float beam)
        {
            return Edge + Outward * (beam * 0.5f + 0.4f);
        }
    }

    /// <summary>
    /// A settlement's dock laid on its shore (GL726): a pier from the shore nearest its buildings ("docks need to be as
    /// close to town as the buildings in town") out to water deep enough, a head across its end for the larger ones,
    /// berths alongside, and the way up from the nearest building to the pier's root.
    /// </summary>
    public sealed class DockLayout
    {
        public DockDesign Design;

        /// <summary>The pier's root on land (x, z), where the shore is, and its end over deep water.</summary>
        public Vector2 Root, Shore, Tip;

        /// <summary>The way the pier runs out (length 1), and across it (to its right).</summary>
        public Vector2 Out, Across;

        /// <summary>The building front the way up starts at.</summary>
        public Vector2 PathFrom;

        public List<DockBerth> Berths = new List<DockBerth>();

        public float Length => Vector2.Distance(Root, Tip);

        /// <summary>A spot on the deck a step in from a berth's edge: where goods are set down and taken up.</summary>
        public Vector2 SetDown(int berth)
        {
            DockBerth at = Berths[Math.Abs(berth) % Berths.Count];
            return at.Edge - at.Outward * Mathf.Min(1.5f, Design.Width * 0.35f) + at.Along * 0.8f;
        }

        /// <summary>The berth a vessel of a run takes.</summary>
        public DockBerth Berth(int run)
        {
            return Berths[Math.Abs(run) % Berths.Count];
        }
    }

    /// <summary>Lays <see cref="DockLayout"/>s.</summary>
    public static class Docks
    {
        /// <summary>The farthest from a building, in metres, the shore may be for a dock.</summary>
        public const float ShoreReach = 140f;

        /// <summary>Metres of deck edge a berth takes: a sea trader's length and room to warp in.</summary>
        public const float BerthLength = 15f;

        /// <summary>Metres of the pier's root that stand on land before the shore.</summary>
        public const float OnLand = 2.5f;

        /// <summary>
        /// Lays a dock for <paramref name="design"/>: tries ways from each of <paramref name="fronts"/> (the settlement's
        /// building fronts) round the way to <paramref name="harbour"/> (the planned harbour) and takes the one that
        /// meets the water nearest a building; the pier runs on from the shore until the water is
        /// <paramref name="needDepth"/> deep (<paramref name="depthAt"/>: metres of water, 0 or less on land) or it is as
        /// long as the row allows. Null when no water is within reach.
        /// </summary>
        public static DockLayout Lay(DockDesign design, IReadOnlyList<Vector2> fronts, Vector2 harbour, Func<Vector2, float> depthAt, float needDepth)
        {
            if (design == null || fronts == null || fronts.Count == 0)
            {
                return null;
            }
            float bestShore = float.MaxValue;
            Vector2 bestFrom = default, bestDirection = default, bestAt = default;
            foreach (Vector2 front in fronts)
            {
                Vector2 toHarbour = harbour - front;
                if (toHarbour.sqrMagnitude < 1e-4f || toHarbour.magnitude > ShoreReach * 3f)
                {
                    continue;
                }
                float baseAngle = Mathf.Atan2(toHarbour.y, toHarbour.x);
                for (int k = -6; k <= 6; k++)
                {
                    float angle = baseAngle + k * 12f * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    for (float d = 1f; d < Mathf.Min(bestShore, ShoreReach); d += 1f)
                    {
                        Vector2 at = front + direction * d;
                        if (depthAt(at) > 0f)
                        {
                            // The shore must open onto water, not a puddle: some way on is wet too.
                            if (depthAt(at + direction * 4f) > 0f && d < bestShore)
                            {
                                bestShore = d;
                                bestFrom = front;
                                bestDirection = direction;
                                bestAt = at;
                            }
                            break;
                        }
                    }
                }
            }
            if (bestShore == float.MaxValue)
            {
                return null;
            }
            var dock = new DockLayout
            {
                Design = design, Shore = bestAt, Root = bestAt - bestDirection * OnLand, Out = bestDirection, Across = new Vector2(bestDirection.y, -bestDirection.x),
                PathFrom = bestFrom,
            };
            float length = OnLand;
            while (length < design.MaxLength + OnLand)
            {
                length += 1f;
                if (depthAt(dock.Root + bestDirection * length) >= needDepth && length >= OnLand + 4f)
                {
                    break;
                }
            }
            dock.Tip = dock.Root + bestDirection * length;
            LayBerths(dock);
            return dock;
        }

        // Berths along the head's outer face, or along the pier's sides near its end when it has no head.
        private static void LayBerths(DockLayout dock)
        {
            DockDesign design = dock.Design;
            int count = Math.Max(1, design.Berths);
            // The head's outer face takes vessels lying across the pier's line, one to every BerthLength of it.
            int onFace = design.HeadLength > 0f ? Math.Min(count, Math.Max(1, (int)(design.HeadLength / BerthLength))) : 0;
            Vector2 face = dock.Tip + dock.Out * (design.HeadWidth * 0.5f);
            for (int i = 0; i < onFace; i++)
            {
                float across = (i - (onFace - 1) * 0.5f) * BerthLength;
                dock.Berths.Add(new DockBerth(face + dock.Across * across, dock.Out, dock.Across));
            }
            // The rest lie along the pier's sides, back from its end (or from the head), a pair to every BerthLength.
            float back = design.HeadLength > 0f ? design.HeadWidth * 0.5f + BerthLength * 0.5f + 1f : BerthLength * 0.5f;
            for (int i = 0; i < count - onFace; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float along = Mathf.Min(back + BerthLength * (i / 2), Mathf.Max(0f, dock.Length - OnLand - 2f));
                Vector2 edge = dock.Tip - dock.Out * along + dock.Across * (side * design.Width * 0.5f);
                dock.Berths.Add(new DockBerth(edge, dock.Across * side, -dock.Out));
            }
        }
    }
}
