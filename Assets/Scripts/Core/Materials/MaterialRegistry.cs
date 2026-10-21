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
        private static readonly int LayerRemapId = Shader.PropertyToID("_LayerRemap");

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

        // The texture layer each id is drawn with (GL54): its own, or another material's while it's
        // hidden. Runtime state only; never saved.
        private float[] layerRemap;

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

        /// <summary>
        /// The material an item becomes when put back into the ground (dropping, I4): one
        /// that drops it and doesn't break any further, so the end of its break chain (stone
        /// goes back as loose stone, copper ore as loose copper). Null for items no material
        /// drops, like tools.
        /// </summary>
        public VoxelMaterial PlacedFrom(ItemDefinition item)
        {
            VoxelMaterial any = null;
            foreach (VoxelMaterial material in materials)
            {
                if (material == null || item == null || material.Drop != item)
                {
                    continue;
                }
                if (material.BreaksInto == null)
                {
                    return material;
                }
                any ??= material;
            }
            return any;
        }

        /// <summary>
        /// Whether a material is where an item comes from (GL100): mining it, or any stage of its break
        /// chain, gives the item (stone, cracked stone and loose stone all give stone).
        /// </summary>
        public bool IsSourceOf(VoxelMaterial material, ItemDefinition item)
        {
            return material != null && item != null && material.FinalDrop == item;
        }

        /// <summary>The share of a mix mined as materials that give the item, 0-1 (GL100: how much of it is clay).</summary>
        public float ShareOf(VolumeMix mix, ItemDefinition item)
        {
            float share = 0f;
            if (mix != null)
            {
                foreach (VolumeMix.Entry entry in mix.Entries)
                {
                    if (IsSourceOf(entry.Material, item))
                    {
                        share += mix.ShareOf(entry.Material);
                    }
                }
            }
            return share;
        }

        /// <summary>
        /// How good something made of these contents is as the item (GL100): the share of materials that
        /// give it, so a clay furnace of 76% clay works at 76%. 1 with no contents (made from pure items).
        /// </summary>
        public float Quality(Composition contents, ItemDefinition item)
        {
            if (contents == null)
            {
                return 1f;
            }
            float share = 0f;
            foreach (Content part in contents.Parts)
            {
                if (IsSourceOf(part.Material, item))
                {
                    share += part.Fraction;
                }
            }
            return share;
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

        /// <summary>
        /// Draws one material with another's texture and colour (GL54), e.g. copper ore as stone until
        /// the player can tell them apart, or as itself again with <paramref name="shownAs"/> null.
        /// Takes effect at once on every chunk; the meshes are unchanged.
        /// </summary>
        public void ShowAs(VoxelMaterial material, VoxelMaterial shownAs)
        {
            if (material == null)
            {
                return;
            }
            EnsureLookup();
            layerRemap[material.Id] = shownAs != null ? shownAs.Id : material.Id;
            Prepare(terrainMaterial);
            Prepare(debugMaterial);
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
            material.SetFloatArray(LayerRemapId, layerRemap);
        }

        private void EnsureLookup()
        {
            if (byId != null)
            {
                return;
            }
            byId = new VoxelMaterial[MaxMaterials];
            colors = new Vector4[MaxMaterials];
            layerRemap = new float[MaxMaterials];
            for (int i = 0; i < MaxMaterials; i++)
            {
                layerRemap[i] = i;
            }
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
