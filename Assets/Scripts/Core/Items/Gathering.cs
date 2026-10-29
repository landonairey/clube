using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What a player's hits gather, sorted by what they were aimed at (GL120): everything one hit takes
    /// goes to the stack of the material under the dot, mixed (grass with the little dirt that came with
    /// it), and comes out as whole items of that material once a whole sample's worth has built up. Each
    /// item carries the mix as its contents and the aimed material's share of that mix as its quality (GL118,
    /// GL132: for ore, its share of the ore itself), so
    /// purity still matters for crafting while stacks stay sorted by what the player meant to take.
    /// Plain data.
    /// </summary>
    public sealed class Gathering
    {
        private readonly Dictionary<VoxelMaterial, VolumeMix> pending = new Dictionary<VoxelMaterial, VolumeMix>();
        private readonly List<VoxelMaterial> ready = new List<VoxelMaterial>();

        /// <summary>Gathers <paramref name="volume"/> samples of a material, holding <paramref name="contents"/>, into the stack of <paramref name="target"/>.</summary>
        public void Add(VoxelMaterial target, VoxelMaterial material, float volume, Composition contents = null)
        {
            if (target == null || material == null || volume <= 0f)
            {
                return;
            }
            if (!pending.TryGetValue(target, out VolumeMix mix))
            {
                mix = new VolumeMix();
                pending.Add(target, mix);
            }
            mix.Add(material, volume, contents);
        }

        /// <summary>What's gathered toward a target and not yet a whole item, in samples.</summary>
        public float PendingFor(VoxelMaterial target)
        {
            return target != null && pending.TryGetValue(target, out VolumeMix mix) ? mix.Total : 0f;
        }

        /// <summary>
        /// Turns every whole sample's worth gathered into items of its target (what the target gives when
        /// mined), adding them to <paramref name="into"/>; what's short of a whole one waits for the next hit.
        /// </summary>
        public void TakeWhole(MaterialRegistry materials, List<GatheredStack> into)
        {
            ready.Clear();
            foreach (KeyValuePair<VoxelMaterial, VolumeMix> entry in pending)
            {
                if (entry.Value.Total >= 1f - 1e-4f)
                {
                    ready.Add(entry.Key);
                }
            }
            foreach (VoxelMaterial target in ready)
            {
                VolumeMix mix = pending[target];
                int count = (int)(mix.Total + 1e-4f);
                VolumeMix taken = mix.Take(count);
                ItemDefinition item = target.FinalDrop;
                if (item == null)
                {
                    continue;
                }
                // How much of it is what was aimed at (GL132): for ore, its share of the ore itself, grade and all
                // (18% cassiterite), not of the samples mined as ore.
                Composition contents = taken.Contents();
                float quality = materials != null ? materials.Quality(contents, item) : 1f;
                into.Add(new GatheredStack(target, item, count, contents, quality));
            }
        }

        /// <summary>
        /// Hands over everything gathered (GL169): whole items and the part of one as a part item, so every hit
        /// shows in the inventory at once instead of after a whole sample's worth (ten hand hits).
        /// </summary>
        public void TakeAll(MaterialRegistry materials, List<GatheredStack> into)
        {
            ready.Clear();
            foreach (KeyValuePair<VoxelMaterial, VolumeMix> entry in pending)
            {
                if (entry.Value.Total > ItemStack.PartEpsilon)
                {
                    ready.Add(entry.Key);
                }
            }
            foreach (VoxelMaterial target in ready)
            {
                VolumeMix mix = pending[target];
                float total = mix.Total;
                VolumeMix taken = mix.Take(total);
                ItemDefinition item = target.FinalDrop;
                if (item == null)
                {
                    continue;
                }
                int count = (int)(total + 1e-4f);
                float part = Mathf.Max(0f, total - count);
                Composition contents = taken.Contents();
                float quality = materials != null ? materials.Quality(contents, item) : 1f;
                into.Add(new GatheredStack(target, item, count, contents, quality, part));
            }
        }
    }

    /// <summary>Whole items a <see cref="Gathering"/> has ready: of what, how many, made of what, how pure.</summary>
    public readonly struct GatheredStack
    {
        public GatheredStack(VoxelMaterial target, ItemDefinition item, int count, Composition contents, float quality, float part = 0f)
        {
            Target = target;
            Item = item;
            Count = count;
            Part = part;
            Contents = contents;
            Quality = quality;
        }

        /// <summary>The material the hits were aimed at.</summary>
        public VoxelMaterial Target { get; }

        public ItemDefinition Item { get; }

        public int Count { get; }

        /// <summary>A part of one more item (0-1), when everything was handed over (<see cref="Gathering.TakeAll"/>).</summary>
        public float Part { get; }

        public Composition Contents { get; }

        public float Quality { get; }
    }
}
