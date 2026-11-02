using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What grass looks like from afar at a place (GL257): the average colour of the tufts that grow there, as
    /// <see cref="GrassGrowth"/> grows them. Short grass under trees is green; tall meadow grass is partly straw, as the
    /// grass shader dries tall tufts; wheat fields are the colour of wheat. The terrain shows this colour on its grass
    /// beyond the 3D grass, so the tufts give way to ground of the same colour, not to the bare grass texture.
    /// </summary>
    /// <remarks>Plain math; <c>GrassLawnMap</c> fills a texture round the focus with it.</remarks>
    public sealed class GrassLawn
    {
        // Rolls of a tuft's base height, averaged over: tall tufts dry to straw, short ones don't.
        private static readonly float[] Rolls = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

        /// <summary>Seen from afar: green tufts, dried tufts and wheat, linear colours, lit like the ground.</summary>
        public Color Green = new Color(0.2f, 0.33f, 0.08f);
        public Color Straw = new Color(0.44f, 0.3f, 0.08f);
        public Color Wheat = new Color(0.55f, 0.46f, 0.21f);

        /// <summary>A tuft's base height range, metres (before meadows grow it taller).</summary>
        public Vector2 BaseHeight = new Vector2(0.18f, 0.38f);

        /// <summary>Tufts are green up to <see cref="StrawFrom"/> metres tall and straw from <see cref="StrawAt"/>.</summary>
        public float StrawFrom = 0.3f;
        public float StrawAt = 0.8f;
        public float StrawStrength = 1f;

        /// <summary>The share of a place's tufts that are dried to straw (0-1), at a height scale.</summary>
        public float StrawShare(float heightScale)
        {
            float sum = 0f;
            foreach (float roll in Rolls)
            {
                float tall = Mathf.Lerp(BaseHeight.x, BaseHeight.y, roll) * heightScale;
                sum += Mathf.Clamp01((tall - StrawFrom) / Mathf.Max(StrawAt - StrawFrom, 0.01f));
            }
            return sum / Rolls.Length * StrawStrength;
        }

        /// <summary>The colour of grass seen from afar with this much forest, at this altitude, this share of it wheat.</summary>
        public Color ColourOf(GrassGrowth growth, float forest, float altitude, float wheat)
        {
            Color grass = Color.Lerp(Green, Straw, StrawShare(growth.HeightScale(forest, altitude)));
            return Color.Lerp(grass, Wheat, Mathf.Clamp01(wheat));
        }

        /// <summary>The colour of grass seen from afar at (x, z) on ground at <paramref name="altitude"/>.</summary>
        public Color ColourAt(GrassGrowth growth, TreeCover cover, int seed, float x, float z, float altitude)
        {
            float forest = cover != null ? cover.At(x, z) : 0f;
            return ColourOf(growth, forest, altitude, growth.WheatAt(seed, x, z, forest));
        }
    }
}
