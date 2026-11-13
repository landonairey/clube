using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What to put in front of the camera for an item's inventory icon (GL612): a mesh or a prefab, the texture it
    /// wears, and how it is turned and viewed.
    /// </summary>
    public readonly struct ItemIconModel
    {
        public ItemIconModel(Mesh mesh, GameObject prefab, Texture albedo, Color tint, Quaternion turn, Quaternion view)
        {
            Mesh = mesh;
            Prefab = prefab;
            Albedo = albedo;
            Tint = tint;
            Turn = turn;
            View = view;
        }

        /// <summary>The model, coloured by vertex; null when the item is a <see cref="Prefab"/> or has no model.</summary>
        public Mesh Mesh { get; }

        /// <summary>A placeable's prefab, drawn with its own materials.</summary>
        public GameObject Prefab { get; }

        /// <summary>The terrain texture a chunk of a material wears; null for a model drawn in its vertex colours.</summary>
        public Texture Albedo { get; }

        /// <summary>The tint over <see cref="Albedo"/>.</summary>
        public Color Tint { get; }

        /// <summary>How the model is turned before it is looked at (a log laid over, a pick set on the diagonal).</summary>
        public Quaternion Turn { get; }

        /// <summary>The camera's rotation.</summary>
        public Quaternion View { get; }

        /// <summary>True when there is nothing to draw: the item keeps the icon it has.</summary>
        public bool IsEmpty => Mesh == null && Prefab == null;
    }

    /// <summary>
    /// Finds the model of every item's inventory icon (GL612), so icons match the 3D world: a tool, torch or bucket is
    /// its held model without the hand (<see cref="HeldToolMeshes"/>), a placeable its prefab, a material's drop a
    /// low-poly chunk of the material in its terrain texture, and the made things (ingots, boards, hides) their shape
    /// in the item's tint (<see cref="ItemChunkMeshes"/>). Which is which is data: <see cref="ItemIconShapes.Resolve"/>.
    /// Callable outside Play mode; the editor's icon baker renders what this returns.
    /// </summary>
    public static class ItemIconModels
    {
        // Tools are seen side-on, set on the diagonal as a tool lies in a slot.
        private static readonly Quaternion ToolView = Quaternion.Euler(6f, 0f, 0f);
        private static readonly Quaternion ToolTurn = Quaternion.Euler(0f, 0f, -42f);

        /// <summary>The model of an item's icon; empty (<see cref="ItemIconModel.IsEmpty"/>) when it has none.</summary>
        public static ItemIconModel For(ItemDefinition item, MaterialRegistry materials)
        {
            ItemIconShape shape = ItemIconShapes.Resolve(item, materials);
            if (shape == ItemIconShape.Painted)
            {
                return default;
            }
            if (shape == ItemIconShape.Model)
            {
                return OwnModel(item);
            }
            VoxelMaterial material = ItemIconShapes.MaterialOf(item, materials);
            if (material != null && WearsTexture(shape))
            {
                // Built white: the texture gives the colour (or the material's flat colour, when it has no texture).
                Color tint = material.Albedo != null ? item.IconTint : item.IconTint * material.DebugColor;
                return new ItemIconModel(ItemChunkMeshes.Build(shape, Color.white), null, material.Albedo, tint, TurnOf(shape), ViewOf(shape));
            }
            return new ItemIconModel(ItemChunkMeshes.Build(shape, item.IconTint), null, null, Color.white, TurnOf(shape), ViewOf(shape));
        }

        private static ItemIconModel OwnModel(ItemDefinition item)
        {
            // A torch is a placeable too, but is drawn as it is held.
            if (item is PlaceableDefinition placeable && !(item is TorchDefinition))
            {
                return new ItemIconModel(null, placeable.Prefab, null, Color.white, Quaternion.identity, IconFraming.ThreeQuarter);
            }
            Mesh mesh = null;
            if (item is TorchDefinition torch)
            {
                mesh = HeldToolMeshes.BuildTorch(torch.IsLit, withHand: false);
            }
            else if (item is BucketDefinition)
            {
                mesh = HeldToolMeshes.BuildBucket(withHand: false);
                return new ItemIconModel(mesh, null, null, Color.white, Quaternion.identity, IconFraming.ThreeQuarter);
            }
            else if (item is ToolDefinition tool && HeldToolMeshes.ShapeOf(tool) != HeldShape.Hand)
            {
                mesh = HeldToolMeshes.Build(tool, withHand: false);
            }
            // A head that reaches forward (a pick, a hatchet) is turned side-on, or only its edge would show.
            bool reachesForward = mesh != null && mesh.bounds.size.z > mesh.bounds.size.x * 1.2f;
            Quaternion turn = reachesForward ? ToolTurn * Quaternion.Euler(0f, 90f, 0f) : ToolTurn;
            return new ItemIconModel(mesh, null, null, Color.white, turn, ToolView);
        }

        // The loose stuff shows its material's texture; a log, boards or a brick are drawn in the item's tint.
        private static bool WearsTexture(ItemIconShape shape)
        {
            return shape == ItemIconShape.Chunk || shape == ItemIconShape.Mound || shape == ItemIconShape.Gravel || shape == ItemIconShape.Leaves;
        }

        private static Quaternion TurnOf(ItemIconShape shape)
        {
            switch (shape)
            {
                case ItemIconShape.Log:
                    // Laid over with a cut end toward the camera.
                    return Quaternion.Euler(-58f, 0f, -28f);
                case ItemIconShape.Boards:
                    return Quaternion.Euler(-20f, 0f, -52f);
                case ItemIconShape.Stick:
                    return Quaternion.Euler(0f, 0f, -45f);
                case ItemIconShape.Leaves:
                    return Quaternion.Euler(-10f, 0f, -24f);
                case ItemIconShape.Hide:
                    // Held up flat to the camera.
                    return Quaternion.Euler(-66f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
                case ItemIconShape.Ingot:
                case ItemIconShape.Brick:
                    return Quaternion.Euler(0f, 24f, 0f);
                default:
                    return Quaternion.identity;
            }
        }

        private static Quaternion ViewOf(ItemIconShape shape)
        {
            switch (shape)
            {
                case ItemIconShape.Hide:
                case ItemIconShape.Stick:
                case ItemIconShape.Leaves:
                case ItemIconShape.Boards:
                    return ToolView;
                default:
                    return IconFraming.ThreeQuarter;
            }
        }
    }
}
