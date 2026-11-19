using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A tool's durability (GL794, GL795): what tools call the stack's quality, the share of the tool's full hits it can
    /// hold. A tool knapped from a material starts at the durability of that material's grade, read from a row per grade
    /// (Poor, Medium, Good, High Grade, Pure); a slot shows the durability bar only while the tool holds fewer hits than a
    /// full new one.
    /// </summary>
    public static class ToolDurability
    {
        /// <summary>What tools call their quality (GL795).</summary>
        public const string Word = "Durability";

        /// <summary>The default rows, one per <see cref="Grade"/> in order: Poor 60%, Medium 70%, Good 80%, High Grade 90%, Pure 100% (GL794).</summary>
        public static readonly float[] DefaultByGrade = { 0.6f, 0.7f, 0.8f, 0.9f, 1f };

        /// <summary>
        /// The durability a tool made from a material of <paramref name="materialQuality"/> starts at: the row for that
        /// quality's grade. Full when the rows don't cover the grade.
        /// </summary>
        public static float FromGrade(float materialQuality, IReadOnlyList<float> byGrade)
        {
            int grade = (int)ProspectReading.GradeOf(materialQuality);
            return byGrade != null && grade < byGrade.Count ? Mathf.Max(0.01f, byGrade[grade]) : 1f;
        }

        /// <summary>
        /// True when a slot shows the stack's durability bar (GL795): a tool with hits that holds fewer than a full new one,
        /// worn or made below 100%. A full tool is only its icon.
        /// </summary>
        public static bool ShowsBar(ItemStack stack)
        {
            return !stack.IsEmpty && stack.Item is ToolDefinition tool && tool.MaxHits > 0 && stack.MaxHits - stack.Wear < tool.MaxHits;
        }
    }
}
