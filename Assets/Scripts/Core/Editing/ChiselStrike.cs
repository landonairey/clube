using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What a chisel does to the ground (GL87, first pass of TL2's chisel): a hit on solid samples
    /// that aren't all square squares them off (marks them square and fills them solid, so they
    /// mesh as sharp cubes, GL86); a hit on samples that are all square already cuts them away,
    /// collecting a drop for each like any tool. Two hits to carve a voxel out cleanly, one to
    /// shape it. Works on any material, through the world's edit paths (A7).
    /// </summary>
    public static class ChiselStrike
    {
        /// <summary>
        /// One chisel hit on the given samples (from <see cref="ToolStrike.FindTargets"/>). Returns
        /// how many samples it squared and how many it cut away.
        /// </summary>
        /// <param name="collected">Gets one drop per sample cut away (what its last break stage gives); may be null.</param>
        /// <param name="minedFrom">Gets, for each drop, the sample and material it came from; may be null.</param>
        public static ChiselResult Hit(
            World world, IReadOnlyList<Vector3Int> targets, MaterialRegistry materials, List<ItemDefinition> collected,
            float isoLevel = 0.5f, List<MinedSample> minedFrom = null)
        {
            bool allSquare = true;
            foreach (Vector3Int sample in targets)
            {
                if (Solid(world, sample, isoLevel) && !world.IsSquare(sample))
                {
                    allSquare = false;
                    break;
                }
            }

            int squared = 0;
            int cut = 0;
            foreach (Vector3Int sample in targets)
            {
                if (!Solid(world, sample, isoLevel))
                {
                    continue;
                }
                if (!allSquare)
                {
                    if (!world.IsSquare(sample))
                    {
                        world.SetDensity(sample, 1f);
                        world.SetSquare(sample, true);
                        squared++;
                    }
                    continue;
                }
                byte? id = world.GetMaterial(sample);
                VoxelMaterial material = id.HasValue && materials != null ? materials.Get(id.Value) : null;
                world.SetDensity(sample, 0f);
                cut++;
                ItemDefinition drop = ToolStrike.FinalDrop(material);
                if (drop != null)
                {
                    collected?.Add(drop);
                    minedFrom?.Add(new MinedSample(sample, material));
                }
            }
            return new ChiselResult(squared, cut);
        }

        private static bool Solid(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value >= isoLevel;
        }
    }

    /// <summary>What one <see cref="ChiselStrike.Hit"/> did.</summary>
    public readonly struct ChiselResult
    {
        public ChiselResult(int squared, int cut)
        {
            Squared = squared;
            Cut = cut;
        }

        /// <summary>Samples squared off by the hit.</summary>
        public int Squared { get; }

        /// <summary>Samples cut away (each one's drop collected).</summary>
        public int Cut { get; }
    }
}
