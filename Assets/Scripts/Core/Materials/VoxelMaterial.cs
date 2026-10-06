using UnityEngine;

namespace Clube.Core
{
    /// <summary>What kind of material a <see cref="VoxelMaterial"/> is (M9).</summary>
    public enum VoxelMaterialCategory
    {
        /// <summary>Bulk ground: grass, dirt, stone.</summary>
        Aggregate,

        /// <summary>Holds a metal worth extracting: gold, silver, copper (3D, Chapter 5).</summary>
        Ore,
    }

    /// <summary>
    /// One terrain material (M9): grass, dirt, stone, an ore. An asset, so new materials are
    /// added without code changes: make one, give it an unused <see cref="Id"/> and add it
    /// to the <see cref="MaterialRegistry"/>. Voxels store only the id (M10).
    /// </summary>
    /// <remarks>
    /// Where an ore generates is set per world (<see cref="OreSpec"/> in the terrain settings,
    /// O1), not here. Later chapters add to it rather than replace it, e.g. hidden mineral
    /// species (PR2).
    /// </remarks>
    [CreateAssetMenu(fileName = "VoxelMaterial", menuName = "Clube/Voxel Material")]
    public class VoxelMaterial : ScriptableObject
    {
        [Tooltip("Stored in every voxel of this material, and in saves, so never change it once used. Also its texture array layer.")]
        [SerializeField, Range(0, MaterialRegistry.MaxMaterials - 1)]
        private int id;

        [SerializeField]
        private string displayName = "Material";

        [SerializeField]
        private VoxelMaterialCategory category;

        [Tooltip("Surface texture, tiled over the terrain from every side (M11, M14). Square, the size of the other materials' textures.")]
        [SerializeField]
        private Texture2D albedo;

        [Tooltip("Flat colour in the Debug colours display mode (M15), to check where each material is at a glance.")]
        [SerializeField]
        private Color debugColor = Color.grey;

        [Tooltip("How hard it is to extract, relative to dirt = 1. Sets digging speed once tools exist (MF1).")]
        [SerializeField, Min(0f)]
        private float hardness = 1f;

        [Tooltip("The item extracting it gives (O6).")]
        [SerializeField]
        private ItemDefinition drop;

        /// <summary>Makes a material in code (tests, tools); the game's materials are assets.</summary>
        public static VoxelMaterial Create(int id, string displayName, VoxelMaterialCategory category = VoxelMaterialCategory.Aggregate)
        {
            var material = CreateInstance<VoxelMaterial>();
            material.id = Mathf.Clamp(id, 0, MaterialRegistry.MaxMaterials - 1);
            material.displayName = displayName;
            material.category = category;
            material.name = displayName;
            return material;
        }

        public byte Id => (byte)id;

        public string DisplayName => displayName;

        public VoxelMaterialCategory Category => category;

        public Texture2D Albedo => albedo;

        public Color DebugColor => debugColor;

        public float Hardness => hardness;

        /// <summary>The item extracting it gives (O6), or null.</summary>
        public ItemDefinition Drop => drop;
    }
}
