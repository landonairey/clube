using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What one settlement's people work in the world, and how far they have got (GL730), whether the settlement is
    /// loaded or not: the trees it claims, nearest its middle first, and each mining shop's <see cref="DigSite"/>. All
    /// of it follows from the ground and the trees as generated, so it is the same every time it is worked out. How
    /// much is done by a time on the clock is the hours its people spent on each work (<see cref="DutyHours"/>) times
    /// the work's <see cref="WorkRate"/>: the first so many trees are down and the first so many voxels of each site
    /// dug out. The people on screen work the same trees and digs in the same order.
    /// </summary>
    public sealed class SettlementWorks
    {
        private readonly SettlementSite site;
        private readonly IReadOnlyList<Resident> residents;
        private readonly IReadOnlyList<Duty> duties;
        private readonly WorkLand land;
        private readonly TreeField trees;
        private readonly WorldGrid grid;
        private readonly Func<float, float, float> ground;
        private readonly Func<Vector2, bool> isDry;
        private readonly Dictionary<(int, bool), DigSite> sites = new Dictionary<(int, bool), DigSite>();
        private readonly List<DigSite> siteList = new List<DigSite>();
        private List<Vector2> claimed;

        /// <param name="site">The settlement as laid out.</param>
        /// <param name="residents">Its people (<see cref="SettlementRoster.Build"/>).</param>
        /// <param name="duties">The duty rows their shifts name: a work's range comes from them.</param>
        /// <param name="land">Where they may work.</param>
        /// <param name="trees">Where the world's trees stand; null for a world without trees.</param>
        /// <param name="ground">The ground's height as generated at (x, z).</param>
        /// <param name="isDry">False on sea and river.</param>
        public SettlementWorks(SettlementSite site, IReadOnlyList<Resident> residents, IReadOnlyList<Duty> duties, WorkLand land, TreeField trees,
            WorldGrid grid, Func<float, float, float> ground, Func<Vector2, bool> isDry)
        {
            this.site = site;
            this.residents = residents;
            this.duties = duties;
            this.land = land;
            this.trees = trees;
            this.grid = grid;
            this.ground = ground;
            this.isDry = isDry;
        }

        public SettlementSite Site => site;

        public IReadOnlyList<Resident> Residents => residents;

        /// <summary>The settlement's bounds (x, z).</summary>
        public Rect Town => site.Bounds;

        /// <summary>True when its people may work at a point (x, z), more than a margin (metres) outside the settlement (<see cref="WorkLand.MayWork"/>).</summary>
        public bool MayWork(Vector2 point, float margin)
        {
            return land.MayWork(site.Bounds, point, margin);
        }

        /// <summary>
        /// The trees the settlement's woodcutters fell, in order: every spot the tree field grows a tree at within the
        /// felling duty's range of the settlement's edge, on ground they may work, nearest the middle first.
        /// </summary>
        public IReadOnlyList<Vector2> Trees
        {
            get
            {
                if (claimed == null)
                {
                    claimed = new List<Vector2>();
                    Duty fell = WorkDuty(WorkRate.Fell);
                    if (trees != null && fell != null)
                    {
                        Rect town = site.Bounds;
                        float edge = Mathf.Max(town.width, town.height) * 0.5f;
                        var spots = new List<Vector2>();
                        TreeFinder.SpotsNear(trees, town.center, 0f, edge + fell.Range, spots);
                        foreach (Vector2 spot in spots)
                        {
                            if (land.MayWork(town, spot, 3f) && Grows(spot))
                            {
                                claimed.Add(spot);
                            }
                        }
                    }
                }
                return claimed;
            }
        }

        /// <summary>
        /// The site a shop's digging duty works: its tunnels, or for <paramref name="tunnel"/> false its pits. A shop
        /// with no slope near enough for a tunnel works its pits on the tunnel's shifts too. Null for a work with no duty row.
        /// </summary>
        public DigSite SiteOf(int shop, bool tunnel)
        {
            if (sites.TryGetValue((shop, tunnel), out DigSite found))
            {
                return found;
            }
            Duty duty = WorkDuty(tunnel ? WorkRate.DigTunnel : WorkRate.DigPit);
            if (duty == null)
            {
                return null;
            }
            var made = new DigSite(DigSite.SeedOf(site.Settlement.Index, shop), tunnel, site.Bounds, duty.Range, grid, ground, isDry, MayWork);
            if (tunnel && made.Count == 0)
            {
                DigSite pits = SiteOf(shop, false);
                sites[(shop, true)] = pits;
                return pits;
            }
            sites[(shop, tunnel)] = made;
            siteList.Add(made);
            return made;
        }

        /// <summary>Every dig site its people work, each once.</summary>
        public IReadOnlyList<DigSite> Sites
        {
            get
            {
                foreach (Resident resident in residents)
                {
                    foreach (DutyShift shift in resident.Occupation.Week)
                    {
                        Duty duty = Duty.Find(duties, shift.Duty);
                        if (duty != null && (duty.Work == WorkRate.DigPit || duty.Work == WorkRate.DigTunnel))
                        {
                            SiteOf(resident.Shop, duty.Work == WorkRate.DigTunnel);
                        }
                    }
                }
                return siteList;
            }
        }

        /// <summary>Trees felled and cleared by a time on the clock (hours since the game began).</summary>
        public int TreesBy(double time, DutyHours hours, IReadOnlyList<WorkRate> rates)
        {
            double worked = 0d;
            foreach (Resident resident in residents)
            {
                worked += hours.Before(resident.Occupation, WorkRate.Fell, time);
            }
            return Mathf.Min(Trees.Count, (int)Math.Floor(worked * WorkRate.Of(rates, WorkRate.Fell)));
        }

        /// <summary>Voxels of ground dug out of a site by a time on the clock (hours since the game began).</summary>
        public long VoxelsBy(DigSite target, double time, DutyHours hours, IReadOnlyList<WorkRate> rates)
        {
            double voxels = 0d;
            foreach (Resident resident in residents)
            {
                foreach (string work in DigWorks)
                {
                    // Only a resident who does the work has a site for it: asking for one would lay one.
                    if (hours.Weekly(resident.Occupation, work) > 0d && SiteOf(resident.Shop, work == WorkRate.DigTunnel) == target)
                    {
                        voxels += hours.Before(resident.Occupation, work, time) * WorkRate.Of(rates, work);
                    }
                }
            }
            return (long)Math.Floor(voxels);
        }

        private static readonly string[] DigWorks = { WorkRate.DigPit, WorkRate.DigTunnel };

        // True when the tree field's tree at a spot grows: the ground round it is flat enough (the field's own rule).
        private bool Grows(Vector2 spot)
        {
            float probe = TreeField.FlatnessProbe;
            return trees.IsFlatEnough(ground(spot.x, spot.y), ground(spot.x + probe, spot.y), ground(spot.x - probe, spot.y),
                ground(spot.x, spot.y + probe), ground(spot.x, spot.y - probe));
        }

        // The first duty row doing a work.
        private Duty WorkDuty(string work)
        {
            foreach (Duty duty in duties)
            {
                if (duty.Work == work)
                {
                    return duty;
                }
            }
            return null;
        }
    }
}
