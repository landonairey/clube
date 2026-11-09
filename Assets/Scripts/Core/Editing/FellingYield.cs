using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How much wood a felled piece keeps (GL468). A woodcutter fells at most <see cref="WoodCap"/> volume of wood in one
    /// piece (one sample of wood mines one volume): 10 at Woodcutting 0, 10% more a level, compounding. A piece with more
    /// wood than that lands with only the wood nearest the cut; the rest shatters as it falls and is lost. So a beginner
    /// who fells a whole tree gets little of it, and does better climbing it (GL467) and taking the upper limbs off one
    /// at a time first.
    /// </summary>
    /// <remarks>
    /// The cap is on the piece that comes off, before it lands, so the wood on the ground is what can be gathered: there's
    /// no hidden limit on gathering, and wood that already lay there (re-cut, GL349) only ever falls under it again.
    /// </remarks>
    public static class FellingYield
    {
        /// <summary>Most wood, in volume, one piece keeps at Woodcutting 0.</summary>
        public const float BaseCap = 10f;

        /// <summary>The cap's growth per Woodcutting level, compounding.</summary>
        public const float GrowthPerLevel = 1.1f;

        /// <summary>Most wood, in volume, one felled piece keeps at a Woodcutting level.</summary>
        public static float WoodCap(int woodcuttingLevel)
        {
            return BaseCap * Mathf.Pow(GrowthPerLevel, Mathf.Max(0, woodcuttingLevel));
        }

        /// <summary>
        /// Takes the wood of <paramref name="limb"/> past <paramref name="cap"/> volume out of it, farthest from the cut first
        /// (ties: highest first), so the piece that lands is the end it was cut from. Leaves are all kept. Returns the wood
        /// lost, in volume.
        /// </summary>
        public static int CapWood(FelledLimb limb, float cap)
        {
            int keep = Mathf.Max(0, Mathf.FloorToInt(cap + 0.0001f));
            var wood = new List<FelledSample>();
            foreach (FelledSample sample in limb.Solid)
            {
                if (sample.IsWood)
                {
                    wood.Add(sample);
                }
            }
            if (wood.Count <= keep)
            {
                return 0;
            }
            Vector3Int pivot = limb.Pivot;
            wood.Sort((a, b) =>
            {
                int order = (a.Sample - pivot).sqrMagnitude.CompareTo((b.Sample - pivot).sqrMagnitude);
                return order != 0 ? order : a.Sample.y.CompareTo(b.Sample.y);
            });
            var lost = new HashSet<Vector3Int>();
            for (int i = keep; i < wood.Count; i++)
            {
                lost.Add(wood[i].Sample);
            }
            limb.Solid.RemoveAll(sample => sample.IsWood && lost.Contains(sample.Sample));
            limb.WoodCount = keep;
            limb.WoodLost += lost.Count;
            return lost.Count;
        }
    }
}
