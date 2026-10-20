using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws the volume slot (GL99) with IMGUI, beside the item slots and sized like them: a bar of the
    /// mix's materials, each in its colour and as tall as its share, and how much there is in all. Ore the
    /// player can't tell apart yet (<see cref="OreKnowledge"/>) is drawn and named as "? ore".
    /// </summary>
    public static class MixSlotGui
    {
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedBorder = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color Unknown = new Color(0.5f, 0.5f, 0.52f);

        private static GUIStyle countStyle;

        /// <summary>The player's ore knowledge, for naming hidden ore; set by whoever has it, null names everything.</summary>
        public static OreKnowledge Knowledge { get; set; }

        public static void Draw(Rect rect, VolumeMix mix, bool selected, string key = null)
        {
            countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
            ItemSlotGui.Fill(rect, Background);
            Rect bar = ItemSlotGui.Inset(rect, 8f);
            if (mix != null && !mix.IsEmpty)
            {
                float y = bar.yMax;
                foreach (VolumeMix.Entry entry in mix.Entries)
                {
                    float height = bar.height * entry.Volume / mix.Total;
                    y -= height;
                    ItemSlotGui.Fill(new Rect(bar.x, y, bar.width, height), ColourOf(entry.Material));
                }
                GUI.Label(ItemSlotGui.Inset(rect, 3f), Mathf.FloorToInt(mix.Total).ToString(), countStyle);
            }
            ItemSlotGui.Outline(rect, selected ? SelectedBorder : Border, selected ? 2f : 1f);
            if (key != null)
            {
                GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, 20f, 16f), key);
            }
        }

        /// <summary>"Mix: 76% Clay, 15% Dirt, 9% Stone (42)", or that it's empty.</summary>
        public static string Describe(VolumeMix mix)
        {
            if (mix == null || mix.IsEmpty)
            {
                return "Mix: empty (what you mine goes here)";
            }
            var text = new StringBuilder("Mix: ");
            var shares = new List<(string Name, float Share)>();
            foreach (VolumeMix.Entry entry in mix.Entries)
            {
                string name = NameOf(entry.Material);
                int at = shares.FindIndex(s => s.Name == name);
                float share = entry.Volume / mix.Total;
                if (at >= 0)
                {
                    shares[at] = (name, shares[at].Share + share);
                }
                else
                {
                    shares.Add((name, share));
                }
            }
            for (int i = 0; i < shares.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }
                float percent = shares[i].Share * 100f;
                text.Append(percent >= 1f ? $"{percent:0}%" : $"{percent:0.#}%").Append(' ').Append(shares[i].Name);
            }
            return text.Append($"  ({mix.Total:0.#})").ToString();
        }

        private static bool Hidden(VoxelMaterial material)
        {
            return Knowledge != null && !Knowledge.Knows(material);
        }

        private static string NameOf(VoxelMaterial material)
        {
            return Hidden(material) ? "? ore" : material.DisplayName;
        }

        private static Color ColourOf(VoxelMaterial material)
        {
            Color colour = Hidden(material) ? Unknown : material.DebugColor;
            colour.a = 1f;
            return colour;
        }
    }
}
