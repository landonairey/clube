using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The meshing section of the in-game lab panels (K31): iso level (V1), edge placement
    /// (V3), shading (V4), the mesher (managed or Burst, K12) and, with a material registry,
    /// how materials show (M15). Edits the view's runtime
    /// config copy and calls <see cref="WorldConfig.NotifyChanged"/>, the same as an Inspector edit.
    /// </summary>
    public static class MeshingControls
    {
        private static readonly string[] EdgePlacementNames = { "Interpolated", "Midpoint" };
        private static readonly string[] ShadingNames = { "Flat", "Smooth" };
        private static readonly string[] MesherNames = { "Managed", "Burst" };
        private static readonly string[] MaterialDisplayNames = { "None", "Hard", "Blended", "Debug" };

        public static void Draw(LabPanelFrame frame, WorldConfig config)
        {
            float iso = frame.Slider("Iso level", config.IsoLevel, 0f, 1f);
            var placement = (EdgePlacement)frame.Toolbar("Edges", (int)config.EdgePlacement, EdgePlacementNames);
            var shading = (Shading)frame.Toolbar("Shading", (int)config.Shading, ShadingNames);
            var mesher = (MesherBackend)frame.Toolbar("Mesher", (int)config.Mesher, MesherNames);
            MaterialDisplay display = config.Materials != null
                ? (MaterialDisplay)frame.Toolbar("Materials", (int)config.MaterialDisplay, MaterialDisplayNames)
                : config.MaterialDisplay;

            // Only real changes notify: each one rebuilds the mesh.
            if (!Mathf.Approximately(iso, config.IsoLevel) || placement != config.EdgePlacement || shading != config.Shading
                || mesher != config.Mesher || display != config.MaterialDisplay)
            {
                config.IsoLevel = iso;
                config.EdgePlacement = placement;
                config.Shading = shading;
                config.Mesher = mesher;
                config.MaterialDisplay = display;
                config.NotifyChanged();
            }
        }
    }
}
