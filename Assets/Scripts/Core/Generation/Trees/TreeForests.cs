using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where the forests are and how old their trees are (GL164, GL165), from the seed alone, so the
    /// voxel trees (<see cref="TreeField"/>, <see cref="TreeShape"/>) and the trees drawn far off
    /// (<see cref="DistantTreeBuilder"/>) agree.
    /// </summary>
    /// <remarks>
    /// <para>The land is split into forest and open field by a smooth two-octave value noise about
    /// <see cref="TreeGeneration.ForestScale"/> wide: deep forest gives a cell
    /// <see cref="TreeGeneration.Chance"/> of a tree, open field <see cref="TreeGeneration.FieldChance"/>,
    /// with a soft edge between. <see cref="TreeGeneration.ForestCover"/> is roughly the share of
    /// forest (the noise isn't evenly spread, so it's approximate).</para>
    /// <para>Each tree has an age, hashed per cell, that scales its whole shape from
    /// <see cref="TreeGeneration.YoungestSize"/> up to full-grown. Deep forest is mostly old trees;
    /// at its edges and out in the fields, young ones are as common as old.</para>
    /// </remarks>
    public static class TreeForests
    {
        // Salts keeping these rolls apart from the cell rolls (TreeField) and the shapes' (TreeShape).
        private const int ForestSalt = 0x464F5245;
        private const int AgeSalt = 0x41474520;

        // Half the width of a forest's edge, in noise units (the noise runs 0-1).
        private const float Edge = 0.06f;

        // The second octave's share and its size against the first's.
        private const float DetailShare = 0.35f;
        private const float DetailSize = 0.4f;

        /// <summary>How much forest there is at (x, z): 0 in open field, 1 deep in a forest.</summary>
        public static float ForestAt(int seed, TreeGeneration settings, float x, float z)
        {
            float cover = settings.ForestCover;
            if (cover >= 1f)
            {
                return 1f;
            }
            if (cover <= 0f)
            {
                return 0f;
            }
            float scale = settings.ForestScale;
            float noise = (1f - DetailShare) * ValueNoise(seed, x / scale, z / scale, 0)
                          + DetailShare * ValueNoise(seed, x / (scale * DetailSize), z / (scale * DetailSize), 1);

            // The two octaves' sum gathers round 0.5 (spread about 0.2), so the cover maps onto a
            // threshold that narrow way.
            float threshold = 0.5f + (0.5f - cover) * 0.6f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - Edge, threshold + Edge, noise));
        }

        /// <summary>The chance that the cell has a tree: the forest's chance, the field's, or between at an edge.</summary>
        public static float ChanceIn(int seed, Vector2Int cell, TreeGeneration settings)
        {
            return Mathf.Lerp(settings.FieldChance, settings.Chance, ForestAtCell(seed, cell, settings));
        }

        /// <summary>The cell's tree's size against a full-grown one, from its age: <see cref="TreeGeneration.YoungestSize"/> to 1.</summary>
        public static float SizeOf(int seed, Vector2Int cell, TreeGeneration settings)
        {
            float youngest = settings.YoungestSize;
            if (youngest >= 1f)
            {
                return 1f;
            }
            float age = VoxelHash.Uniform(seed, cell.x, 3, cell.y, AgeSalt);
            // Deep forest leans old (the root pushes ages up); fields and edges keep them even.
            age = Mathf.Pow(age, Mathf.Lerp(1f, 0.5f, ForestAtCell(seed, cell, settings)));
            return Mathf.Lerp(youngest, 1f, age);
        }

        private static float ForestAtCell(int seed, Vector2Int cell, TreeGeneration settings)
        {
            float size = settings.CellSize;
            return ForestAt(seed, settings, (cell.x + 0.5f) * size, (cell.y + 0.5f) * size);
        }

        // Smoothly blended hashed values on a unit lattice, 0-1.
        private static float ValueNoise(int seed, float x, float z, int octave)
        {
            int ix = Mathf.FloorToInt(x);
            int iz = Mathf.FloorToInt(z);
            float fx = Fade(x - ix);
            float fz = Fade(z - iz);
            float a = VoxelHash.Uniform(seed, ix, octave, iz, ForestSalt);
            float b = VoxelHash.Uniform(seed, ix + 1, octave, iz, ForestSalt);
            float c = VoxelHash.Uniform(seed, ix, octave, iz + 1, ForestSalt);
            float d = VoxelHash.Uniform(seed, ix + 1, octave, iz + 1, ForestSalt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        private static float Fade(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
