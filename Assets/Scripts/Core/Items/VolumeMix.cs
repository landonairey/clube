using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// A volume slot's contents (GL99): every mined voxel mixed together, kept as how much of each
    /// material went in (in samples, 1 per mined sample) and, for each, what that material held (ore's
    /// host rock and grade, GL32), blended by volume. Material can be taken back out as a share of the
    /// whole (<see cref="Take"/>: crafting, GL100) or one sample at a time at random, weighted by the
    /// mix (<see cref="TakeOne"/>: dropping, GL101), and low-grade ore stays low-grade either way (GL104).
    /// Plain data.
    /// </summary>
    public sealed class VolumeMix
    {
        /// <summary>One material in the mix.</summary>
        public sealed class Entry
        {
            internal Entry(VoxelMaterial material)
            {
                Material = material;
            }

            /// <summary>The material as it was mined (Stone, Cassiterite, Clay).</summary>
            public VoxelMaterial Material { get; }

            /// <summary>How much of it, in samples.</summary>
            public float Volume { get; internal set; }

            /// <summary>What it holds (ore: host rock and ore by share); null for plain material, all itself.</summary>
            public Composition Contents { get; internal set; }

            /// <summary>Its share of a content material: from <see cref="Contents"/>, or all or nothing for plain material.</summary>
            public float FractionOf(VoxelMaterial content)
            {
                return Contents != null ? Contents.FractionOf(content) : Material == content ? 1f : 0f;
            }
        }

        // Volumes this small are rounding left over from taking a share; they're dropped.
        private const float Crumb = 1e-4f;

        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>Raised after anything goes in or comes out.</summary>
        public event Action Changed;

        /// <summary>The materials in the mix, largest first.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>All of it, in samples.</summary>
        public float Total { get; private set; }

        public bool IsEmpty => Total <= Crumb;

        /// <summary>Puts mined material in: <paramref name="volume"/> samples of it, holding <paramref name="contents"/> (null: all itself).</summary>
        public void Add(VoxelMaterial material, float volume, Composition contents = null)
        {
            if (material == null || volume <= 0f)
            {
                return;
            }
            Entry entry = entries.Find(e => e.Material == material);
            if (entry == null)
            {
                entry = new Entry(material);
                entries.Add(entry);
            }
            entry.Contents = Blend(entry.Material, entry.Contents, entry.Volume, contents, volume);
            entry.Volume += volume;
            Total += volume;
            entries.Sort((a, b) => b.Volume.CompareTo(a.Volume));
            Changed?.Invoke();
        }

        /// <summary>The share of the mix that was mined as this material, 0-1 (76% Clay).</summary>
        public float ShareOf(VoxelMaterial material)
        {
            if (IsEmpty)
            {
                return 0f;
            }
            Entry entry = entries.Find(e => e.Material == material);
            return entry != null ? entry.Volume / Total : 0f;
        }

        /// <summary>What the whole mix holds, by content (stone, and the ore inside the ore), as shares; null when empty.</summary>
        public Composition Contents()
        {
            var shares = new List<Content>();
            foreach (Entry entry in entries)
            {
                if (entry.Contents == null)
                {
                    shares.Add(new Content(entry.Material, entry.Volume));
                    continue;
                }
                foreach (Content part in entry.Contents.Parts)
                {
                    shares.Add(new Content(part.Material, part.Fraction * entry.Volume));
                }
            }
            return Composition.From(shares);
        }

        /// <summary>
        /// Takes <paramref name="volume"/> samples out as a cut of the whole mix (each material in its
        /// share) and returns them as a mix of their own; less when there isn't that much.
        /// </summary>
        public VolumeMix Take(float volume)
        {
            var taken = new VolumeMix();
            if (IsEmpty || volume <= 0f)
            {
                return taken;
            }
            float part = Math.Min(1f, volume / Total);
            float removed = 0f;
            foreach (Entry entry in entries)
            {
                float amount = entry.Volume * part;
                taken.Add(entry.Material, amount, entry.Contents);
                entry.Volume -= amount;
                removed += amount;
            }
            Total = Math.Max(0f, Total - removed);
            Tidy();
            Changed?.Invoke();
            return taken;
        }

        /// <summary>
        /// Takes one sample out (or what's left, under one), picking its material at random weighted by
        /// the mix: <paramref name="roll"/> is a number from 0 to 1. Returns false when empty.
        /// </summary>
        public bool TakeOne(float roll, out VoxelMaterial material, out Composition contents)
        {
            material = null;
            contents = null;
            if (IsEmpty)
            {
                return false;
            }
            float at = Math.Max(0f, Math.Min(roll, 0.99999f)) * Total;
            Entry picked = entries[entries.Count - 1];
            foreach (Entry entry in entries)
            {
                if (at < entry.Volume)
                {
                    picked = entry;
                    break;
                }
                at -= entry.Volume;
            }
            material = picked.Material;
            contents = picked.Contents;
            float amount = Math.Min(1f, picked.Volume);
            picked.Volume -= amount;
            Total = Math.Max(0f, Total - amount);
            Tidy();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Empties it.</summary>
        public void Clear()
        {
            entries.Clear();
            Total = 0f;
            Changed?.Invoke();
        }

        // "76% Clay, 15% Dirt, 9% Stone".
        public override string ToString()
        {
            var shares = new List<Content>();
            foreach (Entry entry in entries)
            {
                shares.Add(new Content(entry.Material, entry.Volume));
            }
            return Composition.From(shares)?.ToString() ?? "empty";
        }

        private void Tidy()
        {
            entries.RemoveAll(e => e.Volume <= Crumb);
            if (entries.Count == 0)
            {
                Total = 0f;
            }
        }

        // Contents blended by volume, plain material counting as all itself.
        private static Composition Blend(VoxelMaterial material, Composition a, float volumeA, Composition b, float volumeB)
        {
            if (a == null && b == null)
            {
                return null;
            }
            Composition plain = Composition.From(new Content(material, 1f));
            return Composition.Blend(a ?? (volumeA > 0f ? plain : null), volumeA, b ?? plain, volumeB);
        }
    }
}
