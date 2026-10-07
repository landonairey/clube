using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Every terrain material in the game (M9), looked up by the id voxels store (M10), and
    /// the render materials that draw them (M11, M15): the textured terrain material, which
    /// samples the registry's texture array by id, and the debug colours material.
    /// </summary>
    /// <remarks>
    /// The texture array holds each material's <see cref="VoxelMaterial.Albedo"/> at the layer
    /// of its id. It is built in the Editor (<i>Clube → Materials → Build texture array</i>)
    /// whenever materials or textures change.
    /// </remarks>
    [CreateAssetMenu(fileName = "MaterialRegistry", menuName = "Clube/Material Registry")]
    public class MaterialRegistry : ScriptableObject
    {
        /// <summary>Most materials a world can have: ids fit in a byte, and the shader's colour table is this long.</summary>
        public const int MaxMaterials = 64;

        private static readonly int TexturesId = Shader.PropertyToID("_Textures");
        private static readonly int MaterialColorsId = Shader.PropertyToID("_MaterialColors");

        [SerializeField]
        private List<VoxelMaterial> materials = new List<VoxelMaterial>();

        [Tooltip("Every material's albedo, one layer per id. Built by Clube → Materials → Build texture array.")]
        [SerializeField]
        private Texture2DArray textures;

        [Tooltip("Draws terrain with each material's texture (Clube/Terrain Materials).")]
        [SerializeField]
        private Material terrainMaterial;

        [Tooltip("Draws terrain in each material's debug colour (Clube/Terrain Materials with Debug colours on).")]
        [SerializeField]
        private Material debugMaterial;

        private VoxelMaterial[] byId;
        private Vector4[] colors;

        /// <summary>Makes a registry in code (tests, tools); the game's registry is an asset.</summary>
        public static MaterialRegistry Create(params VoxelMaterial[] materials)
        {
            var registry = CreateInstance<MaterialRegistry>();
            registry.materials.AddRange(materials);
            return registry;
        }

        public IReadOnlyList<VoxelMaterial> Materials => materials;

        /// <summary>Set by the Editor's texture array builder.</summary>
        public Texture2DArray Textures
        {
            get => textures;
            set => textures = value;
        }

        /// <summary>The material with this id, or null if there is none.</summary>
        public VoxelMaterial Get(byte id)
        {
            EnsureLookup();
            return id < MaxMaterials ? byId[id] : null;
        }

        /// <summary>The material with this display name (ignoring case), or null.</summary>
        public VoxelMaterial Find(string displayName)
        {
            foreach (VoxelMaterial material in materials)
            {
                if (material != null && string.Equals(material.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return material;
                }
            }
            return null;
        }

        /// <summary>
        /// The render material for a display mode (M15), ready to draw: its texture array and
        /// colour table set. Null for <see cref="MaterialDisplay.None"/>: the view keeps its own.
        /// </summary>
        public Material RenderMaterial(MaterialDisplay display)
        {
            switch (display)
            {
                case MaterialDisplay.HardSeams:
                case MaterialDisplay.Blended:
                    Prepare(terrainMaterial);
                    return terrainMaterial;
                case MaterialDisplay.DebugColours:
                    Prepare(debugMaterial);
                    return debugMaterial;
                default:
                    return null;
            }
        }

        // Array properties aren't saved with a material asset, so they're set before each use.
        private void Prepare(Material material)
        {
            if (material == null)
            {
                return;
            }
            EnsureLookup();
            if (textures != null)
            {
                material.SetTexture(TexturesId, textures);
            }
            material.SetVectorArray(MaterialColorsId, colors);
        }

        private void EnsureLookup()
        {
            if (byId != null)
            {
                return;
            }
            byId = new VoxelMaterial[MaxMaterials];
            colors = new Vector4[MaxMaterials];
            foreach (VoxelMaterial material in materials)
            {
                if (material == null)
                {
                    continue;
                }
                if (byId[material.Id] != null)
                {
                    UnityEngine.Debug.LogWarning($"{name}: '{material.name}' and '{byId[material.Id].name}' share id {material.Id}.", this);
                    continue;
                }
                byId[material.Id] = material;
                colors[material.Id] = material.DebugColor;
            }
        }

        // Called by Unity whenever an Inspector value changes; ids or colours may have.
        private void OnValidate()
        {
            byId = null;
        }
    }
}
