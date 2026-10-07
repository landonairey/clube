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
    /// A hit adds the tool's power to the damage of every exposed solid sample in its impact
    /// shape. A sample whose damage reaches its material's hardness breaks: it turns into the
    /// material's <see cref="VoxelMaterial.BreaksInto"/> (solid → cracked → loose), or, at
    /// the end of that chain, it's removed and its <see cref="VoxelMaterial.Drop"/> collected.
    /// Only exposed samples (touching air) are reached, so a tool chips the surface away
    /// rather than cracking rock it can't see.
    /// </remarks>
    public static class ToolStrike
    {
        // The six face neighbours: a solid sample touching air through one of them is exposed.
        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>
        /// The sample a tool aimed at a surface point works on: the solid corner of the hit
        /// voxel nearest the point. False if none of its corners is solid.
        /// </summary>
        public static bool TryAim(World world, WorldHit hit, float isoLevel, out Vector3Int sample)
        {
            sample = default;
            Vector3Int first = world.Grid.ChunkFirstSample(hit.Chunk) + hit.Voxel;
            float best = float.PositiveInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3Int candidate = first + new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                float? density = world.GetDensity(candidate);
                if (!density.HasValue || density.Value < isoLevel)
                {
                    continue;
                }
                float distance = (world.Grid.SampleToWorld(candidate) - hit.Point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    sample = candidate;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        /// <summary>The samples a tool aimed at <paramref name="aimed"/> reaches: the exposed solid ones in its shape.</summary>
        public static void FindTargets(World world, Vector3Int aimed, ToolImpact impact, float isoLevel, List<Vector3Int> targets)
        {
            targets.Clear();
            foreach (Vector3Int offset in ToolImpacts.Offsets(impact))
            {
                Vector3Int sample = aimed + offset;
                if (IsExposedSolid(world, sample, isoLevel))
                {
                    targets.Add(sample);
                }
            }
        }

        /// <summary>True for a loaded solid sample with air on at least one face. Unloaded neighbours count as solid.</summary>
        public static bool IsExposedSolid(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            if (!density.HasValue || density.Value < isoLevel)
            {
                return false;
            }
            foreach (Vector3Int face in Faces)
            {
                float? neighbour = world.GetDensity(sample + face);
                if (neighbour.HasValue && neighbour.Value < isoLevel)
                {
                    return true;
                }
            }
            return false;
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
