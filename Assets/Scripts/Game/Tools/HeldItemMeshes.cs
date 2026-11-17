using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The model an item shows in the player's hand when it is selected on the hotbar and is not a tool, torch or bucket
    /// (GL693): the same low-poly model its inventory icon is drawn from (<see cref="ItemIconModels"/>), baked into vertex
    /// colours (the held shader has no texture), centred on its middle, turned as the icon is, and sized to sit in a hand.
    /// An item with no model (a hand-painted icon) is held as a small canvas bag.
    /// Callable outside Play mode.
    /// </summary>
    public static class HeldItemMeshes
    {
        // The longest side of a held thing, metres; a stick is longer than a lump.
        private const float Lump = 0.2f;
        private const float Long = 0.42f;

        /// <summary>True when the item is shown as itself in the hand: anything that is not a tool, torch, bucket or placeable.</summary>
        public static bool IsHeldAsModel(ItemDefinition item)
        {
            return item != null && !(item is ToolDefinition) && !(item is TorchDefinition) && !(item is BucketDefinition)
                && !(item is PlaceableDefinition);
        }

        /// <summary>A new mesh for the item (the caller owns it); never null.</summary>
        public static Mesh For(ItemDefinition item, MaterialRegistry materials)
        {
            ItemIconModel model = ItemIconModels.For(item, materials);
            if (model.Mesh == null)
            {
                return Shrunk(DroppedItemMeshes.Bag(), 0.6f, Quaternion.identity, Color.white, Lump);
            }
            ItemIconShape shape = ItemIconShapes.Resolve(item, materials);
            float longest = shape == ItemIconShape.Stick || shape == ItemIconShape.Boards || shape == ItemIconShape.Log ? Long : Lump;
            return Shrunk(model.Mesh, 1f, model.Turn, model.Tint, longest);
        }

        // Turns the mesh, tints its vertex colours, centres it and scales its longest side to the size given. The source
        // mesh is destroyed.
        private static Mesh Shrunk(Mesh source, float extra, Quaternion turn, Color tint, float longest)
        {
            Vector3[] vertices = source.vertices;
            Color[] colours = source.colors;
            var result = new Mesh { name = "Held item" };
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = turn * vertices[i];
            }
            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = size > 0f ? longest * extra / size : 1f;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = (vertices[i] - bounds.center) * scale;
                if (colours.Length == vertices.Length)
                {
                    Color c = colours[i];
                    colours[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
                }
            }
            result.indexFormat = source.indexFormat;
            result.vertices = vertices;
            if (colours.Length == vertices.Length)
            {
                result.colors = colours;
            }
            result.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                result.SetTriangles(source.GetTriangles(s), s);
            }
            // The turn would leave the stored normals pointing the old way; they are rebuilt from the faces.
            result.RecalculateNormals();
            result.RecalculateBounds();
            Object.DestroyImmediate(source);
            return result;
        }
    }
}
