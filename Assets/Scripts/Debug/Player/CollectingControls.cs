using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The collecting test section of the lab panel (GL95, GL96, GL99, GL102): the volume inventory and
    /// the least share of the mix a recipe takes, whether terrain materials meet in hard edges, which cursor style shows what a hit
    /// reaches, and which collect mode takes the ground, with the sphere modes' radii (hand and tool)
    /// and the gradual mode's amounts, so they can be swapped and compared while playing.
    /// </summary>
    public static class CollectingControls
    {
        private static readonly string[] StyleNames = { "Dot", "Outline", "Faces", "Glow", "Sphere" };
        private static readonly string[] ModeNames = { "Voxels", "Gradual", "Whole" };

        public static void Draw(LabPanelFrame frame, ToolCursor cursor, PlayerToolUser tools, PlayerInventory inventory)
        {
            if (inventory != null)
            {
                // The volume inventory test (GL99, GL100); off brings back the classic hotbar and backpack.
                inventory.UsesVolume = frame.ToggleField("Volume inventory (tool, mix, 3 items)", inventory.UsesVolume);
                if (inventory.UsesVolume)
                {
                    inventory.MinimumShare = frame.Slider("Least share of the mix a recipe takes", inventory.MinimumShare, 0f, 1f);
                }
            }
            if (cursor != null)
            {
                cursor.HardMaterialEdges = frame.ToggleField("Hard material edges (no blending)", cursor.HardMaterialEdges);
                cursor.Style = (ToolCursorStyle)frame.Toolbar("Cursor", (int)cursor.Style, StyleNames);
                GUILayout.Label(StyleHint(cursor.Style), frame.Hint);
            }
            if (tools == null)
            {
                return;
            }
            CollectingSettings settings = tools.Collecting;
            settings.Mode = (CollectMode)frame.Toolbar("Collect", (int)settings.Mode, ModeNames);
            GUILayout.Label(ModeHint(settings.Mode), frame.Hint);
            if (!settings.UsesSphere)
            {
                return;
            }
            settings.HandTakesAimedVoxel = frame.ToggleField("Hand takes only the voxel under the dot", settings.HandTakesAimedVoxel);
            if (!settings.HandTakesAimedVoxel)
            {
                settings.HandRadius = frame.Slider("Hand sphere radius (m)", settings.HandRadius, 0.05f, CollectingSettings.MaxRadius);
            }
            settings.ToolRadius = frame.Slider("Tool sphere radius (m)", settings.ToolRadius, 0.05f, CollectingSettings.MaxRadius);
            if (settings.Mode == CollectMode.SphereGradual)
            {
                settings.HandAmount = frame.Slider("Hand takes per hit", settings.HandAmount, 0.01f, 1f);
                settings.ToolAmount = frame.Slider("Tool takes per hit, its own kind", settings.ToolAmount, 0.01f, 1f);
                settings.ToolOffAmount = frame.Slider("Tool takes per hit, other kinds", settings.ToolOffAmount, 0f, 1f);
                settings.OtherMaterialFactor = frame.Slider("Off the dot's material, times", settings.OtherMaterialFactor, 0f, 1f);
            }
        }

        private static string StyleHint(ToolCursorStyle style)
        {
            switch (style)
            {
                case ToolCursorStyle.Dot:
                    return "A small dot only.";
                case ToolCursorStyle.DotOutline:
                    return "A dot and a black outline of the reached voxels.";
                case ToolCursorStyle.GlowCorners:
                    return "Soft glows on the solid corners a hit reaches, seen through the ground.";
                case ToolCursorStyle.ImpactSphere:
                    return "A translucent sphere over the area of impact.";
                default:
                    return "The ground's faces inside the reached voxels, shaded (today's).";
            }
        }

        private static string ModeHint(CollectMode mode)
        {
            switch (mode)
            {
                case CollectMode.SphereGradual:
                    return "Each hit lowers every sample in a sphere by the amount; an item per whole sample taken.";
                case CollectMode.SphereWhole:
                    return "Each hit takes every solid sample in a sphere at once; an item each.";
                default:
                    return "Today's: the reached voxels' corners break over several hits.";
            }
        }
    }
}
