using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// What the player has learned about the ores (GL73): which they have found, how deep they
    /// first found each, the range of grades they have mined of it, and which recipes they have
    /// made, so the journal can say what an ore is good for. Plain data, like
    /// <see cref="SkillSet"/>; the player's journal component fills it in.
    /// </summary>
    public sealed class MiningJournal
    {
        /// <summary>One ore as the player knows it.</summary>
        public sealed class Entry
        {
            public Entry(VoxelMaterial ore, float firstDepth, float grade)
            {
                Ore = ore;
                FirstDepth = firstDepth;
                LowestGrade = grade;
                HighestGrade = grade;
            }

            public VoxelMaterial Ore { get; }

            /// <summary>Metres below the surface where it was first found.</summary>
            public float FirstDepth { get; }

            /// <summary>The leanest and richest share of the ore in a sample mined so far, 0-1.</summary>
            public float LowestGrade { get; internal set; }

            public float HighestGrade { get; internal set; }

            /// <summary>Samples of it mined while known.</summary>
            public int Samples { get; internal set; } = 1;
        }

        private readonly Dictionary<VoxelMaterial, Entry> entries = new Dictionary<VoxelMaterial, Entry>();
        private readonly HashSet<Recipe> made = new HashSet<Recipe>();

        /// <summary>Raised the first time an ore is found.</summary>
        public event Action<Entry> Discovered;

        /// <summary>Raised after anything is recorded.</summary>
        public event Action Changed;

        /// <summary>The player's entry for an ore, or null while it's undiscovered.</summary>
        public Entry EntryFor(VoxelMaterial ore)
        {
            return ore != null && entries.TryGetValue(ore, out Entry entry) ? entry : null;
        }

        public bool HasFound(VoxelMaterial ore) => EntryFor(ore) != null;

        /// <summary>True once the player has finished the recipe at least once.</summary>
        public bool HasMade(Recipe recipe) => recipe != null && made.Contains(recipe);

        /// <summary>
        /// Records one mined sample of an ore at a depth with a grade (its share of the sample,
        /// 0-1). Returns true when this is the ore's first find.
        /// </summary>
        public bool Record(VoxelMaterial ore, float depth, float grade)
        {
            if (ore == null)
            {
                return false;
            }
            if (entries.TryGetValue(ore, out Entry entry))
            {
                entry.LowestGrade = Math.Min(entry.LowestGrade, grade);
                entry.HighestGrade = Math.Max(entry.HighestGrade, grade);
                entry.Samples++;
                Changed?.Invoke();
                return false;
            }
            entry = new Entry(ore, depth, grade);
            entries.Add(ore, entry);
            Discovered?.Invoke(entry);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Records a recipe the player has finished.</summary>
        public void RecordMade(Recipe recipe)
        {
            if (recipe != null && made.Add(recipe))
            {
                Changed?.Invoke();
            }
        }
    }
}
