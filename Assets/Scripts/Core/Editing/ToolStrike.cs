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
    /// A tool reaches a cube of voxels around the one it's aimed at, or a ball inside one
    /// (<see cref="ToolDefinition.ImpactSize"/>: 1 for the hand, a 4-voxel ball for the pickaxe), and a hit
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

        /// <summary>The samples a tool reaching <paramref name="reach"/> hits: every loaded solid corner of its voxels.</summary>
        public static void FindTargets(World world, VoxelReach reach, float isoLevel, List<Vector3Int> targets)
        {
            targets.Clear();
            Vector3Int from = reach.Box.MinSample;
            Vector3Int to = reach.Box.MaxSample;
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (reach.Rounded && !reach.TouchesSample(sample))
                        {
                            continue;
                        }
                        float? density = world.GetDensity(sample);
                        if (density.HasValue && density.Value >= isoLevel)
                        {
                            targets.Add(sample);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// One hit of a tool on the given samples (from <see cref="FindTargets"/>). When it reaches
        /// a small loose piece of a material that's picked up whole (a stone on the grass, GL25),
        /// the hit picks that piece up instead and leaves the rest alone.
        /// </summary>
        /// <param name="materials">Looks up each sample's hardness and what it breaks into; null treats everything as hardness 1 with no drop.</param>
        /// <param name="damage">Damage carried between hits.</param>
        /// <param name="collected">Gets one drop item per removed sample; may be null.</param>
        /// <param name="isoLevel">Density from which a sample is solid, for telling a loose piece apart.</param>
        public static StrikeResult Hit(
            World world, IReadOnlyList<Vector3Int> targets, ToolDefinition tool, MaterialRegistry materials,
            StrikeDamage damage, List<ItemDefinition> collected, float isoLevel = 0.5f)
        {
            int picked = PickUpPiece(world, targets, materials, collected, isoLevel);
            if (picked > 0)
            {
                foreach (Vector3Int sample in Piece)
                {
                    damage.Forget(sample);
                }
                return new StrikeResult(targets.Count, picked, picked);
            }

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

        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        // The piece being looked at: solid samples of one material joined face to face.
        private static readonly List<Vector3Int> Piece = new List<Vector3Int>();
        private static readonly HashSet<Vector3Int> Seen = new HashSet<Vector3Int>();
        private static readonly Queue<Vector3Int> Frontier = new Queue<Vector3Int>();

        // Picks up the first reached piece of a pick-up material small enough to lift whole
        // (VoxelMaterial.PickUpPieceSize): removes it and collects a drop per sample. Returns
        // how many samples it took, 0 when no reached piece qualifies.
        private static int PickUpPiece(
            World world, IReadOnlyList<Vector3Int> targets, MaterialRegistry materials, List<ItemDefinition> collected, float isoLevel)
        {
            if (materials == null)
            {
                return 0;
            }
            foreach (Vector3Int sample in targets)
            {
                byte? id = world.GetMaterial(sample);
                VoxelMaterial material = id.HasValue ? materials.Get(id.Value) : null;
                if (material == null || material.PickUpPieceSize <= 0 || !FindPiece(world, sample, id.Value, material.PickUpPieceSize, isoLevel))
                {
                    continue;
                }
                foreach (Vector3Int part in Piece)
                {
                    world.SetDensity(part, 0f);
                    if (material.Drop != null)
                    {
                        collected?.Add(material.Drop);
                    }
                }
                return Piece.Count;
            }
            return 0;
        }

        // Fills Piece with the solid samples of one material joined to a start sample; false
        // once it grows past the limit (it's part of something bigger) or touches unloaded ground.
        private static bool FindPiece(World world, Vector3Int start, byte material, int limit, float isoLevel)
        {
            Piece.Clear();
            Seen.Clear();
            Frontier.Clear();
            Seen.Add(start);
            Frontier.Enqueue(start);
            while (Frontier.Count > 0)
            {
                Vector3Int sample = Frontier.Dequeue();
                float? density = world.GetDensity(sample);
                if (!density.HasValue)
                {
                    return false;
                }
                if (density.Value < isoLevel || world.GetMaterial(sample) != material)
                {
                    continue;
                }
                Piece.Add(sample);
                if (Piece.Count > limit)
                {
                    return false;
                }
                foreach (Vector3Int face in Faces)
                {
                    if (Seen.Add(sample + face))
                    {
                        Frontier.Enqueue(sample + face);
                    }
                }
            }
            return Piece.Count > 0;
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
