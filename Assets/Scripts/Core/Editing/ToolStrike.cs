using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Working the ground with a tool (GL1–GL5): where a tool is aimed, which samples it
    /// reaches, and what a hit does to them. Pure Core logic on a <see cref="World"/>, so every
    /// write goes through the world's edit paths and border copies stay equal (A7, M2).
    /// </summary>
    /// <remarks>
    /// A tool reaches a cube of voxels around the one it's aimed at
    /// (<see cref="ToolDefinition.ImpactSize"/>: 1 for the hand, 3 for the pickaxe), and a hit
    /// adds its power to the damage of every solid corner (sample) of those voxels, buried
    /// ones too, so it works in 3D on floors and walls alike. A sample whose damage reaches
    /// its material's hardness breaks: it turns into the material's
    /// <see cref="VoxelMaterial.BreaksInto"/> (solid → cracked → loose), or, at the end of
    /// that chain, it's removed and its <see cref="VoxelMaterial.Drop"/> collected.
    /// </remarks>
    public static class ToolStrike
    {
        /// <summary>The voxel a ray hit, by global voxel index: what a tool is aimed at.</summary>
        public static Vector3Int AimedVoxel(World world, WorldHit hit)
        {
            return world.Grid.ChunkFirstSample(hit.Chunk) + hit.Voxel;
        }

        /// <summary>The samples a tool reaching <paramref name="box"/> hits: every loaded solid corner of its voxels.</summary>
        public static void FindTargets(World world, VoxelBox box, float isoLevel, List<Vector3Int> targets)
        {
            targets.Clear();
            Vector3Int from = box.MinSample;
            Vector3Int to = box.MaxSample;
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float? density = world.GetDensity(sample);
                        if (density.HasValue && density.Value >= isoLevel)
                        {
                            targets.Add(sample);
                        }
                    }
                }
            }
        }

        /// <summary>One hit of a tool on the given samples (from <see cref="FindTargets"/>).</summary>
        /// <param name="materials">Looks up each sample's hardness and what it breaks into; null treats everything as hardness 1 with no drop.</param>
        /// <param name="damage">Damage carried between hits.</param>
        /// <param name="collected">Gets one drop item per removed sample; may be null.</param>
        public static StrikeResult Hit(
            World world, IReadOnlyList<Vector3Int> targets, ToolDefinition tool, MaterialRegistry materials,
            StrikeDamage damage, List<ItemDefinition> collected)
        {
            int broken = 0;
            int removed = 0;
            foreach (Vector3Int sample in targets)
            {
                byte? id = world.GetMaterial(sample);
                if (!id.HasValue)
                {
                    continue;
                }
                VoxelMaterial material = materials != null ? materials.Get(id.Value) : null;
                float hardness = material != null ? material.Hardness : 1f;
                if (damage.Add(sample, tool.Power) < hardness)
                {
                    continue;
                }

                damage.Forget(sample);
                broken++;
                if (material != null && material.BreaksInto != null)
                {
                    world.SetMaterial(sample, material.BreaksInto.Id);
                    continue;
                }

                world.SetDensity(sample, 0f);
                removed++;
                if (material != null && material.Drop != null)
                {
                    collected?.Add(material.Drop);
                }
            }
            return new StrikeResult(targets.Count, broken, removed);
        }
    }

    /// <summary>What one <see cref="ToolStrike.Hit"/> did.</summary>
    public readonly struct StrikeResult
    {
        public StrikeResult(int reached, int broken, int removed)
        {
            Reached = reached;
            Broken = broken;
            Removed = removed;
        }

        /// <summary>Samples the hit reached.</summary>
        public int Reached { get; }

        /// <summary>Samples that broke: moved on a stage (e.g. cracked) or were removed.</summary>
        public int Broken { get; }

        /// <summary>Samples removed from the terrain (each one's drop collected).</summary>
        public int Removed { get; }
    }
}
