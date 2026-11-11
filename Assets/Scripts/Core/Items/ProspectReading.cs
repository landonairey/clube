using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A quality read as a word (GL457): PURE, High Grade, Good, Medium, Poor.</summary>
    public enum Grade
    {
        Poor,
        Medium,
        Good,
        HighGrade,
        Pure,
    }

    /// <summary>
    /// What the player can read of a gathered stack's contents at a Prospecting level (GL457). Below
    /// <see cref="ExactLevel"/> shares read as grades, not numbers: the stack's own material first (for ore, the ore, even
    /// when the host rock is the bigger share), then one more component per level, the rest a question mark. At
    /// <see cref="ExactLevel"/> every share reads exactly.
    /// </summary>
    public static class ProspectReading
    {
        /// <summary>The Prospecting level that reads exact shares.</summary>
        public const int ExactLevel = 10;

        /// <summary>The grade of a quality or share: 100% PURE, 90-99% High Grade, 75-89% Good, 50-74% Medium, under 50% Poor.</summary>
        public static Grade GradeOf(float quality)
        {
            int percent = QualityText.WholePercent(quality);
            return percent >= 100 ? Grade.Pure : percent >= 90 ? Grade.HighGrade : percent >= 75 ? Grade.Good : percent >= 50 ? Grade.Medium : Grade.Poor;
        }

        /// <summary>The grade's word: "PURE", "High Grade", "Good", "Medium", "Poor".</summary>
        public static string Name(Grade grade)
        {
            switch (grade)
            {
                case Grade.Pure: return "PURE";
                case Grade.HighGrade: return "High Grade";
                case Grade.Good: return "Good";
                case Grade.Medium: return "Medium";
                default: return "Poor";
            }
        }

        /// <summary>A short form for a slot's corner: "PURE", "HIGH", "GOOD", "MED", "POOR".</summary>
        public static string ShortName(Grade grade)
        {
            switch (grade)
            {
                case Grade.Pure: return "PURE";
                case Grade.HighGrade: return "HIGH";
                case Grade.Good: return "GOOD";
                case Grade.Medium: return "MED";
                default: return "POOR";
            }
        }

        /// <summary>The grade's colour: white, gold, green, yellow, red.</summary>
        public static Color ColourOf(Grade grade)
        {
            switch (grade)
            {
                case Grade.Pure: return Color.white;
                case Grade.HighGrade: return new Color(1f, 0.78f, 0.2f);
                case Grade.Good: return new Color(0.4f, 0.9f, 0.4f);
                case Grade.Medium: return new Color(1f, 0.95f, 0.35f);
                default: return new Color(1f, 0.35f, 0.3f);
            }
        }

        /// <summary>A quality as the player may read it (GL562): "87%" from <see cref="ExactLevel"/>, the grade's word below.</summary>
        public static string QualityAt(float quality, int level)
        {
            return level >= ExactLevel ? QualityText.Percent(quality) : Name(GradeOf(quality));
        }

        /// <summary>
        /// How big a part a non-primary component of a mix is (GL574): Dominant 50% and up, Abundant 25-49%, Moderate 10-24%,
        /// Minor 1-9%, Trace under 1%. Read from the share's whole percent, like the grades.
        /// </summary>
        public static string ShareWord(float share)
        {
            float percent = share * 100f + 0.01f;
            return percent >= 50f ? "Dominant" : percent >= 25f ? "Abundant" : percent >= 10f ? "Moderate" : percent >= 1f ? "Minor" : "Trace";
        }

        private static readonly string[] Ordinals = { "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth" };

        /// <summary>
        /// What reaching a Prospecting level lets the player read of a material mix (GL572): the next item in it, and exact
        /// shares from <see cref="ExactLevel"/>. Null for level 0 or beyond.
        /// </summary>
        public static string UnlockAt(int level)
        {
            if (level == ExactLevel)
            {
                return "you can now read exact shares in a material mix";
            }
            return level >= 1 && level < ExactLevel ? $"you can now see the {Ordinals[level]} item in a material mix" : null;
        }

        /// <summary>How many components a level reads: the stack's own material, plus one a level; all from <see cref="ExactLevel"/>.</summary>
        public static int ComponentsRead(int level)
        {
            return level >= ExactLevel ? int.MaxValue : 1 + Mathf.Max(0, level);
        }

        /// <summary>
        /// The contents in reading order: the parts that are the item's own material first (<paramref name="isOwn"/>), then the
        /// rest by share, biggest first.
        /// </summary>
        public static List<Content> InReadingOrder(Composition contents, System.Func<VoxelMaterial, bool> isOwn)
        {
            var own = new List<Content>();
            var rest = new List<Content>();
            if (contents == null)
            {
                return own;
            }
            foreach (Content part in contents.Parts)
            {
                (isOwn != null && isOwn(part.Material) ? own : rest).Add(part);
            }
            own.Sort((a, b) => b.Fraction.CompareTo(a.Fraction));
            rest.Sort((a, b) => b.Fraction.CompareTo(a.Fraction));
            own.AddRange(rest);
            return own;
        }
    }
}
