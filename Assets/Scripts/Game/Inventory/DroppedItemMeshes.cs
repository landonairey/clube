using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The models of items lying dropped in the world (GL610, GL611), made in code and callable outside Play mode (an
    /// inventory icon can be drawn from them): a tool, torch or bucket is its held model without the hand
    /// (<see cref="HeldToolMeshes"/>); everything else a brown canvas bag. Read by <see cref="DroppedItems"/>.
    /// </summary>
    public static class DroppedItemMeshes
    {
        private static readonly Color Canvas = new Color(0.4f, 0.27f, 0.15f);
        private static readonly Color Cord = new Color(0.72f, 0.62f, 0.42f);

        /// <summary>True when the item has a model of its own to lie on the ground as; a bag otherwise.</summary>
        public static bool HasModel(ItemDefinition item)
        {
            return item is ToolDefinition || item is TorchDefinition || item is BucketDefinition;
        }

        /// <summary>The model an item lies on the ground as: its own when it has one and isn't bagged, else the bag.</summary>
        public static Mesh For(ItemDefinition item, bool bagged)
        {
            if (bagged || !HasModel(item))
            {
                return Bag();
            }
            return item is TorchDefinition ? HeldToolMeshes.BuildTorch(false, withHand: false)
                : item is BucketDefinition ? HeldToolMeshes.BuildBucket(withHand: false)
                : HeldToolMeshes.Build(item as ToolDefinition, withHand: false);
        }

        /// <summary>
        /// A brown canvas bag (GL611, as Rust's): a slumped, faceted sack sitting on its base at the origin, 0.34 m across
        /// and 0.4 m to the top of its tied neck, with a pale cord round the neck and a patch on its side.
        /// </summary>
        public static Mesh Bag()
        {
            var model = new LowPolyMeshBuilder();
            // The body: wide and sagging low, narrowing to the neck, a little off true so it doesn't look turned on a lathe.
            model.Loft(
                new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0.005f, 0.05f, 0f), new Vector3(0.012f, 0.15f, -0.005f),
                    new Vector3(0.006f, 0.24f, 0.004f), new Vector3(0f, 0.29f, 0.008f),
                },
                new[]
                {
                    new Vector2(0.12f, 0.11f), new Vector2(0.17f, 0.155f), new Vector2(0.16f, 0.15f),
                    new Vector2(0.1f, 0.095f), new Vector2(0.045f, 0.045f),
                }, 7, Canvas);
            // The neck's tie, and the gathered canvas flaring above it.
            model.Prism(new Vector3(0f, 0.285f, 0.008f), new Vector3(0f, 0.305f, 0.008f), 0.05f, 0.05f, 7, Cord);
            model.Prism(new Vector3(0f, 0.305f, 0.008f), new Vector3(0.012f, 0.4f, 0.018f), 0.04f, 0.075f, 6, Shade(Canvas, 1.12f));
            model.Prism(new Vector3(0.045f, 0.29f, 0.03f), new Vector3(0.085f, 0.2f, 0.07f), 0.008f, 0.006f, 4, Cord);
            // A darker patch sewn on the front.
            model.Box(new Vector3(-0.03f, 0.12f, 0.148f), new Vector3(0.09f, 0.08f, 0.02f), Quaternion.Euler(-6f, -8f, 5f), Shade(Canvas, 0.72f));
            return model.Make("Dropped bag");
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(color.r * by, color.g * by, color.b * by, color.a);
        }
    }
}
