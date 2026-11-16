namespace Clube.Core
{
    /// <summary>
    /// Decides what an item's inventory icon shows (GL612), from data alone: the shape set on its asset, or for
    /// <see cref="ItemIconShape.Auto"/> its kind, and which material's texture the model wears. The models themselves
    /// are <see cref="ItemChunkMeshes"/> and, for tools and placeables, the game's own.
    /// </summary>
    public static class ItemIconShapes
    {
        /// <summary>
        /// The shape an item's icon is drawn as, never <see cref="ItemIconShape.Auto"/>: the asset's choice; else its
        /// model when its kind has one (a tool, torch, bucket or placeable); else by the material it is a piece of,
        /// a mound for soil and a chunk for the rest. An item of no material is a chunk in its own tint.
        /// </summary>
        public static ItemIconShape Resolve(ItemDefinition item, MaterialRegistry materials)
        {
            if (item == null)
            {
                return ItemIconShape.Painted;
            }
            if (item.IconShape != ItemIconShape.Auto)
            {
                return item.IconShape;
            }
            if (item is ToolDefinition || item is TorchDefinition || item is BucketDefinition || item is PlaceableDefinition)
            {
                return ItemIconShape.Model;
            }
            VoxelMaterial material = MaterialOf(item, materials);
            return material != null && material.Kind == MaterialKind.Soil ? ItemIconShape.Mound : ItemIconShape.Chunk;
        }

        /// <summary>
        /// The material an item is a piece of, whose texture its icon wears: the one it goes back into the ground as,
        /// or for a crushed ore the ore's (<see cref="ItemDefinition.PurityAs"/>). Null for an item of no material
        /// (an ingot, a hide), which is drawn in its <see cref="ItemDefinition.IconTint"/>.
        /// </summary>
        public static VoxelMaterial MaterialOf(ItemDefinition item, MaterialRegistry materials)
        {
            if (item == null || materials == null)
            {
                return null;
            }
            return materials.PlacedFrom(item) ?? (item.PurityAs != null ? materials.PlacedFrom(item.PurityAs) : null);
        }
    }
}
