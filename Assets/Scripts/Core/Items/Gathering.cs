using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// What a player's hits gather, sorted by what they were aimed at (GL120): everything one hit takes
    /// goes to the stack of the material under the dot, mixed (grass with the little dirt that came with
    /// it), and comes out as whole items of that material once a whole sample's worth has built up. Each
    /// item carries the mix as its contents and the aimed material's share as its quality (GL118), so
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
                float quality = materials != null ? materials.ShareOf(taken, item) : 1f;
                into.Add(new GatheredStack(target, item, count, taken.Contents(), quality));
            }
        }
    }

    /// <summary>Whole items a <see cref="Gathering"/> has ready: of what, how many, made of what, how pure.</summary>
    public readonly struct GatheredStack
    {
        public GatheredStack(VoxelMaterial target, ItemDefinition item, int count, Composition contents, float quality)
        {
            Target = target;
            Item = item;
            Count = count;
            Contents = contents;
            Quality = quality;
        }

        /// <summary>The material the hits were aimed at.</summary>
        public VoxelMaterial Target { get; }

        public ItemDefinition Item { get; }

        public int Count { get; }

        public Composition Contents { get; }

        public float Quality { get; }
    }
}
