using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Picking up a loose stone by hand (GL109, GL133, GL149): a bare-hand hit on a small piece of a material
    /// picked up whole (<see cref="VoxelMaterial.PickUpPieceSize"/>: a surface rock, or a stone in the dirt) with
    /// only air and soft ground around it, never the rock face or ore it might be part of; or on the top of a
    /// pile of loose stone or ore (<see cref="VoxelMaterial.PickedFromPiles"/>, dropped cobbles),
    /// takes that sample whole, and up to <see cref="Neighbours"/> touching samples of the same stone
    /// with it, nearest the hit first. Through the world's edit paths (A7).
    /// </summary>
    public static class HandPickup
    {
        /// <summary>How many touching samples of the same stone come away with the one picked up.</summary>
        public const int Neighbours = 2;

        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private static readonly List<Vector3Int> Taken = new List<Vector3Int>();
        private static readonly List<Vector3Int> Candidates = new List<Vector3Int>();

        /// <summary>
        /// Picks up the loose stone among <paramref name="targets"/> nearest <paramref name="hitPoint"/>
        /// (world units, relative to the world origin), with up to <see cref="Neighbours"/> touching samples
        /// of it. Collects a drop per sample and returns how many it took; 0 when no target is a loose stone.
        /// </summary>
        public static int TakeStone(
            World world, IReadOnlyList<Vector3Int> targets, Vector3 hitPoint, MaterialRegistry materials,
            List<ItemDefinition> collected, float isoLevel = 0.5f, List<MinedSample> minedFrom = null)
        {
            if (materials == null)
            {
                return 0;
            }
            float voxel = world.Grid.VoxelSize;
            Vector3Int? first = null;
            float nearest = float.MaxValue;
            foreach (Vector3Int sample in targets)
            {
                if (!IsLooseStone(world, sample, materials, isoLevel))
                {
                    continue;
                }
                float distance = ((Vector3)sample * voxel - hitPoint).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    first = sample;
                }
            }
            if (!first.HasValue)
            {
                return 0;
            }

            byte id = world.GetMaterial(first.Value) ?? 0;
            Taken.Clear();
            Taken.Add(first.Value);
            Candidates.Clear();
            foreach (Vector3Int face in Faces)
            {
                Vector3Int next = first.Value + face;
                float? density = world.GetDensity(next);
                if (density.HasValue && density.Value >= isoLevel && world.GetMaterial(next) == id)
                {
                    Candidates.Add(next);
                }
            }
            Candidates.Sort((a, b) => ((Vector3)a * voxel - hitPoint).sqrMagnitude.CompareTo(((Vector3)b * voxel - hitPoint).sqrMagnitude));
            for (int i = 0; i < Candidates.Count && i < Neighbours; i++)
            {
                Taken.Add(Candidates[i]);
            }

            VoxelMaterial material = materials.Get(id);
            ItemDefinition drop = material != null ? material.FinalDrop : null;
            foreach (Vector3Int sample in Taken)
            {
                world.SetDensity(sample, 0f);
                if (drop != null)
                {
                    collected?.Add(drop);
                    minedFrom?.Add(new MinedSample(sample, material));
                }
            }
            return Taken.Count;
        }

        // A solid sample a bare hand lifts whole: a loose stone, or the top of a pile of them.
        private static bool IsLooseStone(World world, Vector3Int sample, MaterialRegistry materials, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            float? above = world.GetDensity(sample + Vector3Int.up);
            byte? id = world.GetMaterial(sample);
            VoxelMaterial material = id.HasValue ? materials.Get(id.Value) : null;
            if (!density.HasValue || density.Value < isoLevel || material == null)
            {
                return false;
            }
            // A pile of dropped stone at any size, from its top (GL133).
            if (material.PickedFromPiles)
            {
                return above.HasValue && above.Value < isoLevel;
            }
            // Otherwise a piece small enough to be loose, on the surface or in the dirt (GL149), with nothing but air
            // and soft ground around it: a stone against ore or rock is part of them.
            return material.PickUpPieceSize > 0 && ToolStrike.IsLoosePiece(world, sample, id.Value, material.PickUpPieceSize, isoLevel,
                next => IsBedding(world, next, materials, isoLevel));
        }

        // What a loose stone may lie in or against: air, or ground bare hands can dig (dirt, grass, clay, leaves; GL145).
        private static bool IsBedding(World world, Vector3Int sample, MaterialRegistry materials, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            if (!density.HasValue)
            {
                return false;
            }
            if (density.Value < isoLevel)
            {
                return true;
            }
            byte? id = world.GetMaterial(sample);
            VoxelMaterial material = id.HasValue ? materials.Get(id.Value) : null;
            return material != null && material.ToolTier == 0;
        }
    }
}
