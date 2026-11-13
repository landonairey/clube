using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Gives every settlement its shops from the supply chains of what lies nearby (GL595): "a trading post that's nearby
    /// lumber turns into a lumber mill". A bigger type holds more shops; one with room for every chain (the capital) holds
    /// each chain at least once; then a last pass makes sure every specialty exists somewhere in the world (GL594).
    /// </summary>
    internal sealed class SpecialtyAssigner
    {
        private readonly PlanContext context;
        private readonly List<EconomySpecialty> specialties;
        private readonly Dictionary<EconomySpecialty, int> inWorld = new Dictionary<EconomySpecialty, int>();
        private readonly List<string> chains = new List<string>();

        public SpecialtyAssigner(PlanContext context)
        {
            this.context = context;
            specialties = context.Rules.Specialties;
            foreach (EconomySpecialty specialty in specialties)
            {
                inWorld[specialty] = 0;
                if (!chains.Contains(specialty.Chain))
                {
                    chains.Add(specialty.Chain);
                }
            }
        }

        public void Assign(WorldPlan plan)
        {
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                int shops = settlement.Type.Shops;
                if (shops >= chains.Count)
                {
                    foreach (string chain in chains)
                    {
                        AddBest(settlement, chain);
                    }
                }
                else if (shops >= 3)
                {
                    // A place of some size has a general merchant among its shops.
                    AddBest(settlement, SupplyChains.Merchant);
                }
                while (settlement.Shops.Count < shops && AddBest(settlement, null))
                {
                }
            }
            GuaranteeEvery(plan);
        }

        /// <summary>How well a specialty suits a settlement, 0 to 1: the scarcest of the resources it needs nearby.</summary>
        public float Fit(PlannedSettlement settlement, EconomySpecialty specialty)
        {
            if (specialty.Needs.Length == 0)
            {
                return 0.5f;
            }
            float fit = 1f;
            int cell = settlement.Cell.x + settlement.Cell.y * context.Size;
            foreach (string need in specialty.Needs)
            {
                int resource = context.Reach.IndexOf(need);
                fit = Mathf.Min(fit, resource < 0 ? 0f : context.Reach.Richness(resource, context.ReachOf(settlement.Type))[cell]);
            }
            return fit;
        }

        // Adds the specialty that suits best (of one chain, or of any), preferring ones the world has few of.
        private bool AddBest(PlannedSettlement settlement, string chain)
        {
            EconomySpecialty best = null;
            float bestScore = float.NegativeInfinity;
            foreach (EconomySpecialty specialty in specialties)
            {
                if ((chain != null && specialty.Chain != chain) || specialty.CraftLevel > settlement.Type.CraftLevel
                    || settlement.Has(specialty.Id) || (chain == null && specialty.Chain == SupplyChains.Merchant))
                {
                    continue;
                }
                float fit = Fit(settlement, specialty);
                // Without its resource nearby a specialty is only taken when nothing else is left.
                float score = fit > 0.02f ? fit / (1f + inWorld[specialty]) : fit - 1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = specialty;
                }
            }
            if (best == null)
            {
                return false;
            }
            Add(settlement, best, false);
            return true;
        }

        private void Add(PlannedSettlement settlement, EconomySpecialty specialty, bool guaranteed)
        {
            inWorld[specialty]++;
            settlement.Shops.Add(new PlannedShop { Specialty = specialty, Guaranteed = guaranteed, Reason = Reason(settlement, specialty, guaranteed) });
        }

        private string Reason(PlannedSettlement settlement, EconomySpecialty specialty, bool guaranteed)
        {
            string why;
            if (specialty.Needs.Length == 0)
            {
                why = "trades in everything; needs no resource";
            }
            else
            {
                var parts = new List<string>();
                int cell = settlement.Cell.x + settlement.Cell.y * context.Size;
                foreach (string need in specialty.Needs)
                {
                    int resource = context.Reach.IndexOf(need);
                    float value = resource < 0 ? 0f : context.Reach.Richness(resource, context.ReachOf(settlement.Type))[cell];
                    string name = resource < 0 ? need : context.Resources[resource].Name;
                    parts.Add(resource >= 0 && context.Reach.Totals[resource] <= 0f
                        ? $"{name}: none on this map yet"
                        : $"{name} nearby at {value * 100f:F0}% of the map's richest spot");
                }
                why = string.Join("; ", parts);
            }
            return guaranteed
                ? $"{specialty.Name} ({specialty.Chain}): placed here so the world has one, at its best site ({why})."
                : $"{specialty.Name} ({specialty.Chain}): {why}.";
        }

        // Every specialty occurs at least once: a missing one takes the place of a shop the world has spares of, in the
        // settlement that suits it best.
        private void GuaranteeEvery(WorldPlan plan)
        {
            foreach (EconomySpecialty missing in specialties)
            {
                if (inWorld[missing] > 0)
                {
                    continue;
                }
                PlannedSettlement bestSettlement = null;
                int bestSlot = -1;
                float bestScore = float.NegativeInfinity;
                foreach (PlannedSettlement settlement in plan.Settlements)
                {
                    if (settlement.Type.Shops == 0 || settlement.Type.CraftLevel < missing.CraftLevel)
                    {
                        continue;
                    }
                    // The shop to give up: the one the world has most of (never the last of its kind).
                    int slot = -1;
                    for (int i = 0; i < settlement.Shops.Count; i++)
                    {
                        EconomySpecialty held = settlement.Shops[i].Specialty;
                        // A settlement holding every chain (the capital) keeps its only shop of a chain.
                        bool onlyOfChain = settlement.Type.Shops >= chains.Count && held.Chain != missing.Chain
                            && settlement.Shops.FindAll(shop => shop.Specialty.Chain == held.Chain).Count == 1;
                        if (inWorld[held] > 1 && !onlyOfChain && !settlement.Shops[i].Guaranteed && (slot < 0 || inWorld[held] > inWorld[settlement.Shops[slot].Specialty]))
                        {
                            slot = i;
                        }
                    }
                    if (slot < 0)
                    {
                        continue;
                    }
                    float score = Fit(settlement, missing) + 0.001f * inWorld[settlement.Shops[slot].Specialty];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSettlement = settlement;
                        bestSlot = slot;
                    }
                }
                if (bestSettlement == null)
                {
                    plan.Notes.Add($"No settlement could hold a {missing.Name}.");
                    continue;
                }
                inWorld[bestSettlement.Shops[bestSlot].Specialty]--;
                bestSettlement.Shops.RemoveAt(bestSlot);
                Add(bestSettlement, missing, true);
            }
        }

        /// <summary>Adds a specialty to a settlement beside its others (the ore port of a named vessel).</summary>
        public static void AddExtra(PlanContext context, PlannedSettlement settlement, string specialtyId, string reason)
        {
            EconomySpecialty specialty = context.Rules.Specialties.Find(s => s.Id == specialtyId);
            if (specialty != null && !settlement.Has(specialtyId))
            {
                settlement.Shops.Add(new PlannedShop { Specialty = specialty, Reason = reason });
            }
        }
    }
}
