using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which dropped sacks lie in one pile to be merged into a stack (GL837): sacks holding the same thing, close together,
    /// when there are more than <see cref="Threshold"/> of them.
    /// </summary>
    public static class SackPiles
    {
        /// <summary>More than this many identical sacks in one area merge into one.</summary>
        public const int Threshold = 10;

        /// <summary>How far apart, in metres, neighbouring sacks of one pile lie (GL857: from any sack of it, not just the first).</summary>
        public const float Radius = 3f;

        /// <summary>True when two stacks are the same sack contents: item, count, part, wear, quality and what it's made of.</summary>
        public static bool SameContents(ItemStack a, ItemStack b)
        {
            return a.Item == b.Item && a.Count == b.Count && a.Wear == b.Wear
                && Mathf.Abs(a.Part - b.Part) < ItemStack.PartEpsilon && Mathf.Abs(a.Quality - b.Quality) < 0.001f
                && Composition.Same(a.Contents, b.Contents);
        }

        /// <summary>
        /// Groups of indices into the given sacks to merge: each more than <see cref="Threshold"/> sacks of one content, each within
        /// <see cref="Radius"/> of another in the group; the first index is the lowest. A sack in a group counts as the <paramref name="sacks"/> it already
        /// stands for.
        /// </summary>
        public static List<List<int>> Plan(IReadOnlyList<ItemStack> stacks, IReadOnlyList<Vector3> positions, IReadOnlyList<int> sacks)
        {
            var groups = new List<List<int>>();
            var taken = new bool[stacks.Count];
            for (int i = 0; i < stacks.Count; i++)
            {
                if (taken[i])
                {
                    continue;
                }
                // A pile is a chain (GL857): a sack joins when it lies within Radius of any sack already in it, so a heap
                // that spread out as it grew (fifty sacks tumbling over each other) is still one pile.
                var group = new List<int> { i };
                var inGroup = new bool[stacks.Count];
                inGroup[i] = true;
                int total = sacks[i];
                for (int k = 0; k < group.Count; k++)
                {
                    Vector3 from = positions[group[k]];
                    for (int j = i + 1; j < stacks.Count; j++)
                    {
                        if (!taken[j] && !inGroup[j] && (positions[j] - from).sqrMagnitude <= Radius * Radius && SameContents(stacks[i], stacks[j]))
                        {
                            inGroup[j] = true;
                            group.Add(j);
                            total += sacks[j];
                        }
                    }
                }
                group.Sort();
                if (total > Threshold && group.Count > 1)
                {
                    foreach (int index in group)
                    {
                        taken[index] = true;
                    }
                    groups.Add(group);
                }
            }
            return groups;
        }
    }
}
