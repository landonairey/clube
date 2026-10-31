using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How tall the grass grows where (GL234, GL235): up to twice its base height in open meadows, its base
    /// height under dense trees, and back towards the base height higher up. In large patches of open land the
    /// grass is wheat instead, about half a player tall. From the seed and the place alone, so the same
    /// ground always grows the same grass.
    /// </summary>
    [Serializable]
    public class GrassGrowth
    {
        // Keeps the wheat fields' noise apart from the forests' (TreeForests) and everything else's.
        private const int WheatSalt = 0x57484541;

        // Half the width of a wheat field's ragged edge, in noise units (the noise runs 0-1).
        private const float WheatEdge = 0.05f;

        [Tooltip("How much taller grass grows in open meadows than under dense trees: 1 doubles it.")]
        [SerializeField, Range(0f, 2f)]
        private float meadowGrowth = 1f;

        [Tooltip("Height above y = 0 (m) where grass starts growing shorter, and where it's back to its base height.")]
        [SerializeField]
        private Vector2 shorterWithAltitude = new Vector2(30f, 90f);

        [Tooltip("Rough share of the open land that is wheat (0: none).")]
        [SerializeField, Range(0f, 1f)]
        private float wheatShare = 0.18f;

        [Tooltip("Width of a wheat field's patch of noise (m); fields are smaller than that.")]
        [SerializeField, Min(4f)]
        private float wheatScale = 48f;

        [Tooltip("Wheat grows only where the forest is thinner than this (0 open field, 1 deep forest).")]
        [SerializeField, Range(0f, 1f)]
        private float wheatMaxForest = 0.2f;

        [Tooltip("Wheat's height in metres, smallest and largest (the player is 1.8 m).")]
        [SerializeField]
        private Vector2 wheatHeight = new Vector2(0.78f, 0.98f);

        public float MeadowGrowth
        {
            get => meadowGrowth;
            set => meadowGrowth = Mathf.Clamp(value, 0f, 2f);
        }

        public Vector2 ShorterWithAltitude
        {
            get => shorterWithAltitude;
            set => shorterWithAltitude = value;
        }

        public float WheatShare
        {
            get => wheatShare;
            set => wheatShare = Mathf.Clamp01(value);
        }

        public float WheatScale
        {
            get => wheatScale;
            set => wheatScale = Mathf.Max(4f, value);
        }

        public float WheatMaxForest
        {
            get => wheatMaxForest;
            set => wheatMaxForest = Mathf.Clamp01(value);
        }

        public Vector2 WheatHeight
        {
            get => wheatHeight;
            set => wheatHeight = value;
        }

        /// <summary>
        /// What the base grass height is multiplied by at a place with <paramref name="forest"/> (0 open field,
        /// 1 deep forest) at <paramref name="altitude"/> metres: 1 under dense trees or high up, up to
        /// 1 + <see cref="MeadowGrowth"/> in a low meadow.
        /// </summary>
        public float HeightScale(float forest, float altitude)
        {
            float low = 1f - Mathf.InverseLerp(shorterWithAltitude.x, shorterWithAltitude.y, altitude);
            return 1f + meadowGrowth * (1f - Mathf.Clamp01(forest)) * low;
        }

        /// <summary>
        /// How much of the place is a wheat field, 0-1 (the edges fray: a tuft there is wheat by that chance).
        /// None where the forest is <see cref="WheatMaxForest"/> or denser.
        /// </summary>
        public float WheatAt(int seed, float x, float z, float forest)
        {
            if (wheatShare <= 0f || forest >= wheatMaxForest)
            {
                return 0f;
            }
            float noise = 0.7f * ValueNoise.At(seed, x / wheatScale, z / wheatScale, 0, WheatSalt)
                          + 0.3f * ValueNoise.At(seed, x / (wheatScale * 0.35f), z / (wheatScale * 0.35f), 1, WheatSalt);
            // Like the forests: the sum gathers round 0.5, so the share maps onto a threshold that narrow way.
            float threshold = 0.5f + (0.5f - wheatShare) * 0.6f;
            float field = Mathf.InverseLerp(threshold - WheatEdge, threshold + WheatEdge, noise);
            // Thinning towards the forest's edge too, so a field doesn't stop in a straight line at the trees.
            float open = 1f - Mathf.InverseLerp(wheatMaxForest * 0.5f, wheatMaxForest, forest);
            return Mathf.SmoothStep(0f, 1f, field) * open;
        }

        /// <summary>A wheat stalk's height, from a roll 0-1.</summary>
        public float WheatHeightFor(float roll)
        {
            return Mathf.Lerp(wheatHeight.x, wheatHeight.y, roll);
        }
    }
}
