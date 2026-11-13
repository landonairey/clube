using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Small low-poly models of items lying on a shop counter (GL403), made in code by what the item is: ingots, buns, ore
    /// lumps, logs and tools lying flat, anything else a little crate. Coloured by the metal or material the name names.
    /// Origin on the counter at the item's centre.
    /// </summary>
    public static class DisplayItemMeshes
    {
        private static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f);
        private static readonly Color Stone = new Color(0.5f, 0.5f, 0.52f);
        private static readonly Dictionary<ItemDefinition, Mesh> Cache = new Dictionary<ItemDefinition, Mesh>();

        /// <summary>The model of an item, made once and shared.</summary>
        public static Mesh For(ItemDefinition item)
        {
            if (item == null)
            {
                return null;
            }
            if (!Cache.TryGetValue(item, out Mesh mesh) || mesh == null)
            {
                mesh = Build(item);
                Cache[item] = mesh;
            }
            return mesh;
        }

        /// <summary>The colour of the metal or material an item's name names.</summary>
        public static Color ColourOf(string name)
        {
            name = name.ToLowerInvariant();
            if (name.Contains("gold")) return new Color(0.95f, 0.75f, 0.25f);
            if (name.Contains("silver")) return new Color(0.82f, 0.84f, 0.88f);
            if (name.Contains("bronze")) return new Color(0.72f, 0.46f, 0.2f);
            if (name.Contains("copper") || name.Contains("chalcopyrite")) return new Color(0.8f, 0.45f, 0.25f);
            if (name.Contains("malachite")) return new Color(0.25f, 0.6f, 0.4f);
            if (name.Contains("tin") || name.Contains("cassiterite")) return new Color(0.55f, 0.52f, 0.48f);
            if (name.Contains("charcoal")) return new Color(0.12f, 0.11f, 0.1f);
            // Iron and steel, their two ores and coal (GL621-GL624); charcoal is caught above, before "coal".
            if (name.Contains("steel")) return new Color(0.7f, 0.74f, 0.8f);
            if (name.Contains("magnetite")) return new Color(0.15f, 0.21f, 0.33f);
            if (name.Contains("hematite")) return new Color(0.55f, 0.2f, 0.16f);
            if (name.Contains("iron")) return new Color(0.45f, 0.46f, 0.5f);
            if (name.Contains("coal")) return new Color(0.1f, 0.1f, 0.11f);
            if (name.Contains("maple")) return new Color(0.6f, 0.35f, 0.2f);
            if (name.Contains("oak")) return new Color(0.7f, 0.53f, 0.33f);
            if (name.Contains("wood") || name.Contains("stick")) return Wood;
            if (name.Contains("clay") || name.Contains("brick")) return new Color(0.68f, 0.38f, 0.26f);
            return Stone;
        }

        private static Mesh Build(ItemDefinition item)
        {
            var model = new LowPolyMeshBuilder();
            string name = item.DisplayName.ToLowerInvariant();
            Color colour = ColourOf(name);
            if (item is ToolDefinition tool)
            {
                Tool(model, tool, name);
            }
            else if (name.Contains("ingot"))
            {
                // A trapezoid bar, wider at the base.
                model.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.22f, 0.07f, 0.09f), Quaternion.identity, colour);
                model.Box(new Vector3(0f, 0.075f, 0f), new Vector3(0.18f, 0.02f, 0.07f), Quaternion.identity, colour * 1.1f);
            }
            else if (name.Contains("bun") || name.Contains("button"))
            {
                model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.06f, 0f), 0.08f, 0.05f, 8, colour);
            }
            else if (name.Contains("ore") || name.Contains("cassiterite") || name.Contains("chalcopyrite") || name.Contains("malachite")
                || name.Contains("magnetite") || name.Contains("hematite") || name == "coal")
            {
                Color rock = Color.Lerp(Stone, colour, name.Contains("crushed") ? 0.5f : 0.7f);
                model.Diamond(new Vector3(0f, 0.06f, 0f), new Vector3(0.08f, 0.06f, 0.07f), rock);
                model.Diamond(new Vector3(0.09f, 0.04f, 0.04f), new Vector3(0.05f, 0.04f, 0.05f), rock * 0.9f);
            }
            else if (name.Contains("boards"))
            {
                // A short stack of sawn planks (GL411).
                for (int i = 0; i < 3; i++)
                {
                    model.Box(new Vector3(0f, 0.025f + i * 0.05f, (i - 1) * 0.01f), new Vector3(0.4f, 0.045f, 0.11f), Quaternion.Euler(0f, i * 4f, 0f), colour * (0.95f + i * 0.04f));
                }
            }
            else if (name.Contains("wood") || name.Contains("charcoal"))
            {
                model.Prism(new Vector3(-0.15f, 0.06f, 0f), new Vector3(0.15f, 0.06f, 0f), 0.06f, 0.055f, 6, colour);
            }
            else if (name.Contains("stick"))
            {
                model.Prism(new Vector3(-0.2f, 0.02f, -0.02f), new Vector3(0.2f, 0.02f, 0.02f), 0.015f, 0.012f, 5, colour);
                model.Prism(new Vector3(-0.2f, 0.02f, 0.04f), new Vector3(0.18f, 0.02f, 0.06f), 0.014f, 0.012f, 5, colour * 0.9f);
            }
            else if (name.Contains("torch"))
            {
                model.Prism(new Vector3(-0.17f, 0.02f, 0f), new Vector3(0.12f, 0.02f, 0f), 0.016f, 0.016f, 5, Wood);
                model.Prism(new Vector3(0.1f, 0.025f, 0f), new Vector3(0.19f, 0.025f, 0f), 0.03f, 0.025f, 6, new Color(0.12f, 0.11f, 0.1f));
            }
            else
            {
                model.Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.identity, Wood);
                model.Box(new Vector3(0f, 0.161f, 0f), new Vector3(0.1f, 0.002f, 0.1f), Quaternion.identity, colour);
            }
            return model.Make($"Display {item.DisplayName}");
        }

        // A tool lying on its side: the handle along x, its head at +x.
        private static void Tool(LowPolyMeshBuilder model, ToolDefinition tool, string name)
        {
            Color head = HeldToolMeshes.HeadColourOf(tool);
            float y = 0.025f;
            switch (HeldToolMeshes.ShapeOf(tool))
            {
                case HeldShape.HandAxe:
                    model.Diamond(new Vector3(0f, 0.035f, 0f), new Vector3(0.09f, 0.035f, 0.06f), head);
                    return;
                case HeldShape.Shovel:
                    model.Prism(new Vector3(-0.35f, y, 0f), new Vector3(0.15f, y, 0f), 0.017f, 0.016f, 5, Wood);
                    model.Box(new Vector3(0.24f, 0.012f, 0f), new Vector3(0.2f, 0.012f, 0.16f), Quaternion.identity, head);
                    return;
                case HeldShape.Chisel:
                    model.Prism(new Vector3(-0.12f, 0.02f, 0f), new Vector3(0.02f, 0.02f, 0f), 0.02f, 0.02f, 6, Wood);
                    model.Box(new Vector3(0.08f, 0.012f, 0f), new Vector3(0.12f, 0.012f, 0.035f), Quaternion.identity, head);
                    return;
                case HeldShape.Spear:
                    model.Prism(new Vector3(-0.45f, y, 0f), new Vector3(0.4f, y, 0f), 0.015f, 0.014f, 5, Wood);
                    model.Diamond(new Vector3(0.48f, y, 0f), new Vector3(0.1f, 0.02f, 0.03f), head);
                    return;
                default:
                    // Picks, hatchets and hammers: a handle with a head across its end.
                    model.Prism(new Vector3(-0.25f, y, 0f), new Vector3(0.2f, y, 0f), 0.017f, 0.015f, 5, Wood);
                    bool pick = name.Contains("pick");
                    model.Box(new Vector3(0.21f, 0.03f, pick ? 0f : 0.04f), new Vector3(0.05f, 0.05f, pick ? 0.34f : 0.14f), Quaternion.identity, head);
                    return;
            }
        }
    }
}
