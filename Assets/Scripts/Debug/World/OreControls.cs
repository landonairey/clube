using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The ore section of WorldLab's panel (O7, O8): the ore view's toggles, live tuning of
    /// each ore's generation (nodes per cell, peak chance, spread, depth range), and how many
    /// solid samples of each ore the loaded world and the focused chunk hold. Tuning edits the
    /// runtime config copy and regenerates, which drops brush edits, like the terrain sliders.
    /// </summary>
    public static class OreControls
    {
        private const float MaxNodesPerCell = 8f;
        private const float MaxSpread = 8f;
        private const float MaxDepth = 64f;

        public static void Draw(LabPanelFrame frame, WorldConfig config, OreDebugView view, ChunkFocus focus)
        {
            if (view != null)
            {
                DrawView(frame, view);
            }

            OreGeneration ores = config.Terrain.Ores;
            float cellSize = Mathf.Round(frame.Slider("Ore cell (m)", ores.CellSize, 4f, 64f, "0"));
            bool changed = !Mathf.Approximately(cellSize, ores.CellSize);
            ores.CellSize = cellSize;
            foreach (OreSpec spec in ores.Ores)
            {
                if (spec?.Ore != null)
                {
                    changed |= DrawSpec(frame, spec);
                }
            }
            if (changed)
            {
                config.NotifyChanged();
            }

            if (view != null && config.Materials != null)
            {
                DrawCounts(frame, config.Materials, view, focus);
            }
        }

        private static void DrawView(LabPanelFrame frame, OreDebugView view)
        {
            view.ShowNodes = frame.ToggleField("Show ore nodes", view.ShowNodes);
            if (view.ShowNodes)
            {
                view.ShowSpread = frame.ToggleField("Rings at 1σ, 2σ, 3σ", view.ShowSpread);
            }
            view.XRay = frame.ToggleField("X-ray: ore only, terrain hidden", view.XRay);
            if (Application.isPlaying)
            {
                frame.Line($"{view.NodeCount} nodes reach the loaded area");
            }
        }

        // One ore's knobs; true when any changed.
        private static bool DrawSpec(LabPanelFrame frame, OreSpec spec)
        {
            GUILayout.Label(spec.Ore.DisplayName, frame.Header);
            float nodes = frame.Slider("Nodes per cell", spec.NodesPerCell, 0f, MaxNodesPerCell, "0.0#");
            float peak = frame.Slider("Peak chance", spec.PeakProbability, 0f, 1f, "0.00");
            float across = frame.Slider("σ across (x, z) m", spec.Spread.x, 0.25f, MaxSpread, "0.0#");
            float up = frame.Slider("σ up (y) m", spec.Spread.y, 0.25f, MaxSpread, "0.0#");
            float minDepth = frame.Slider("Min depth (m)", spec.MinDepth, 0f, MaxDepth, "0.0");
            float maxDepth = Mathf.Max(minDepth, frame.Slider("Max depth (m)", spec.MaxDepth, 0f, MaxDepth, "0.0"));

            var spread = new Vector3(across, up, across);
            if (Mathf.Approximately(nodes, spec.NodesPerCell) && Mathf.Approximately(peak, spec.PeakProbability)
                && spread == spec.Spread && Mathf.Approximately(minDepth, spec.MinDepth) && Mathf.Approximately(maxDepth, spec.MaxDepth))
            {
                return false;
            }
            spec.NodesPerCell = nodes;
            spec.PeakProbability = peak;
            spec.Spread = spread;
            spec.MinDepth = minDepth;
            spec.MaxDepth = maxDepth;
            return true;
        }

        private static void DrawCounts(LabPanelFrame frame, MaterialRegistry registry, OreDebugView view, ChunkFocus focus)
        {
            if (!Application.isPlaying)
            {
                return;
            }
            frame.Line("Solid ore samples, loaded: " + Describe(registry, view.LoadedCounts));
            if (focus != null && focus.Focused.HasValue)
            {
                IReadOnlyList<int> counts = view.CountsFor(focus.Focused.Value);
                frame.Line("Focused chunk: " + (counts != null ? Describe(registry, counts) : "not scanned yet"));
            }
        }

        private static string Describe(MaterialRegistry registry, IReadOnlyList<int> counts)
        {
            var text = new StringBuilder();
            foreach (VoxelMaterial material in registry.Materials)
            {
                if (material == null || material.Category != VoxelMaterialCategory.Ore)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(" · ");
                }
                text.Append(material.DisplayName.ToLowerInvariant()).Append(' ').Append(counts[material.Id].ToString("N0"));
            }
            return text.Length > 0 ? text.ToString() : "no ores in the registry";
        }
    }
}
