using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A source of solid pieces stamped into the terrain after it's generated, beside the trees (<see cref="TreeStamp"/>): large
    /// boulders (<see cref="BoulderField"/>), rocks and logs in rivers (<see cref="RiverDebris"/>). Main thread only.
    /// </summary>
    public interface IStampField
    {
        /// <summary>Adds every part within <paramref name="margin"/> of the box to <paramref name="results"/>.</summary>
        void CollectParts(Bounds box, List<TreePart> results, float margin);
    }

    /// <summary>Several <see cref="IStampField"/>s gathered as one.</summary>
    public sealed class StampFields : IStampField
    {
        private readonly IStampField[] fields;

        private StampFields(IStampField[] fields)
        {
            this.fields = fields;
        }

        /// <summary>The fields that exist, as one; null when none do.</summary>
        public static IStampField Combine(params IStampField[] all)
        {
            var present = new List<IStampField>();
            foreach (IStampField field in all)
            {
                if (field != null)
                {
                    present.Add(field);
                }
            }
            return present.Count == 0 ? null : present.Count == 1 ? present[0] : new StampFields(present.ToArray());
        }

        public void CollectParts(Bounds box, List<TreePart> results, float margin)
        {
            foreach (IStampField field in fields)
            {
                field.CollectParts(box, results, margin);
            }
        }
    }
}
