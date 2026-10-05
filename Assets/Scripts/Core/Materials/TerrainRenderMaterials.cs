using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The materials a chunk renderer draws with (M15): the view's own, with the first (the
    /// surface) swapped for the registry's render material when the config shows materials.
    /// The rest (a depth prepass, the lab's build grid overlay) stay as they are.
    /// </summary>
    public static class TerrainRenderMaterials
    {
        public static Material[] For(Material[] own, WorldConfig config)
        {
            MaterialDisplay display = config.MeshSettings.MaterialDisplay;
            Material surface = config.Materials != null ? config.Materials.RenderMaterial(display) : null;
            if (surface == null || own == null || own.Length == 0)
            {
                return own;
            }

            var materials = (Material[])own.Clone();
            materials[0] = surface;
            return materials;
        }
    }
}
