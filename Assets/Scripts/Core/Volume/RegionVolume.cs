using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The solid volume of the world's surface around some samples, in voxels (one voxel's cube is 1, as one mined
    /// sample is 1 unit): the exact Marching Cubes solid (V12, by tetrahedra) of every voxel that has one of the samples
    /// as a corner. Measured before and after an edit, the difference is how much the mesh in the world changed, which
    /// is what a hit gathers (GL288), however far each sample's density moved.
    /// </summary>
    public sealed class RegionVolume
    {
        private readonly HashSet<Vector3Int> voxels = new HashSet<Vector3Int>();
        private readonly float[] corners = new float[MarchingCubes.CornerCount];

        /// <summary>The solid in every voxel touching one of <paramref name="samples"/>, in voxels.</summary>
        public float SolidAround(World world, IEnumerable<Vector3Int> samples, float isoLevel)
        {
            voxels.Clear();
            foreach (Vector3Int sample in samples)
            {
                for (int z = -1; z <= 0; z++)
                {
                    for (int y = -1; y <= 0; y++)
                    {
                        for (int x = -1; x <= 0; x++)
                        {
                            voxels.Add(sample + new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            float total = 0f;
            foreach (Vector3Int voxel in voxels)
            {
                total += SolidIn(world, voxel, isoLevel);
            }
            return total;
        }

        // One voxel's solid share; a voxel with a corner not loaded counts as empty, the same before and after.
        private float SolidIn(World world, Vector3Int voxel, float isoLevel)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float? density = world.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                if (!density.HasValue)
                {
                    return 0f;
                }
                corners[corner] = density.Value;
            }
            int caseIndex = MarchingCubes.GetCaseIndex(corners, isoLevel);
            if (caseIndex == 0)
            {
                return 0f;
            }
            if (caseIndex == MarchingCubes.CaseCount - 1)
            {
                return 1f;
            }
            return VoxelVolume.Exact(corners, isoLevel, InterpolatedEdgePlacer.Instance);
        }
    }
}
