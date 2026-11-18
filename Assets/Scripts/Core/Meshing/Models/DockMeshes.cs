using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly model of a dock (GL726) from its <see cref="DockLayout"/>: a deck of planks on piles, or a stone quay
    /// on stone piers for the grand docks, a head across its end, bollards at the berths, and a ramp from the ground at
    /// its root down (or up) to the deck. Built in the dock's own frame: the root at the origin, the deck's top at 0,
    /// +z out along the pier and +x across it.
    /// </summary>
    public static class DockMeshes
    {
        private static readonly Color Plank = new Color(0.52f, 0.38f, 0.24f);
        private static readonly Color DarkPlank = new Color(0.36f, 0.26f, 0.17f);
        private static readonly Color Stone = new Color(0.58f, 0.57f, 0.54f);
        private static readonly Color DarkStone = new Color(0.42f, 0.41f, 0.4f);
        private static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f);

        /// <summary>Metres of the deck at the root that slope to meet the ground.</summary>
        public const float RampLength = 4f;

        /// <summary>The deck's thickness, in metres.</summary>
        public const float Thickness = 0.25f;

        /// <summary>
        /// The dock's mesh. <paramref name="rootRise"/> is the ground at the root over the deck (negative below it);
        /// <paramref name="pileDepth"/> how far the piles go down under the deck.
        /// </summary>
        public static Mesh Build(DockLayout dock, float rootRise, float pileDepth)
        {
            DockDesign design = dock.Design;
            var model = new LowPolyMeshBuilder();
            Color deck = design.Stone ? Stone : Plank, dark = design.Stone ? DarkStone : DarkPlank;
            float length = dock.Length, width = design.Width;
            float ramp = Mathf.Min(RampLength, length * 0.4f);
            rootRise = Mathf.Clamp(rootRise, -Docks.MostRampRise, Docks.MostRampRise);

            // The ramp from the ground down to the deck, then the deck in boards (or flags) across it.
            Vector3 rampLow = new Vector3(0f, 0f, ramp), rampHigh = new Vector3(0f, rootRise, 0f);
            Vector3 rampMid = (rampLow + rampHigh) * 0.5f - Vector3.up * Thickness * 0.5f;
            float rampLength = Vector3.Distance(rampLow, rampHigh);
            model.Box(rampMid, new Vector3(width, Thickness, rampLength), Quaternion.LookRotation(rampLow - rampHigh), deck);
            float board = design.Stone ? 1.2f : 0.5f;
            int k = 0;
            for (float z = ramp; z < length - 1e-3f; z += board, k++)
            {
                float step = Mathf.Min(board, length - z);
                model.Box(new Vector3(0f, -Thickness * 0.5f, z + step * 0.5f), new Vector3(width, Thickness, step * 0.96f), Quaternion.identity, k % 3 == 1 ? dark : deck);
            }
            // Edge beams along both sides.
            foreach (float side in new[] { -1f, 1f })
            {
                model.Box(new Vector3(side * (width * 0.5f - 0.08f), -Thickness - 0.12f, (ramp + length) * 0.5f), new Vector3(0.16f, 0.24f, length - ramp), Quaternion.identity, dark);
            }
            // The head across the end.
            if (design.HeadLength > 0f)
            {
                model.Box(new Vector3(0f, -Thickness * 0.5f, length), new Vector3(design.HeadLength, Thickness, design.HeadWidth), Quaternion.identity, deck);
                model.Box(new Vector3(0f, -Thickness - 0.12f, length + design.HeadWidth * 0.5f - 0.08f), new Vector3(design.HeadLength, 0.24f, 0.16f), Quaternion.identity, dark);
            }
            // Piles (stone piers for a quay) in pairs down the pier and along the head.
            float spacing = design.Stone ? 6f : 3f;
            float pile = design.Stone ? 0.7f : 0.14f;
            for (float z = ramp; z <= length + 1e-3f; z += spacing)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Pile(model, new Vector3(side * (width * 0.5f - pile), 0f, z), pile, pileDepth, design.Stone, dark);
                }
            }
            if (design.HeadLength > 0f)
            {
                for (float x = -design.HeadLength * 0.5f + pile; x <= design.HeadLength * 0.5f - pile + 1e-3f; x += spacing)
                {
                    Pile(model, new Vector3(x, 0f, length + design.HeadWidth * 0.5f - pile), pile, pileDepth, design.Stone, dark);
                    Pile(model, new Vector3(x, 0f, length - design.HeadWidth * 0.5f + pile), pile, pileDepth, design.Stone, dark);
                }
            }
            // Bollards at each berth, a pace either side of its middle.
            foreach (DockBerth berth in dock.Berths)
            {
                foreach (float off in new[] { -3f, 3f })
                {
                    Vector2 at = berth.Edge - berth.Outward * 0.35f + berth.Along * off;
                    Vector3 local = Local(dock, at);
                    model.Prism(local, local + Vector3.up * 0.45f, 0.13f, 0.11f, 6, Iron);
                    model.Prism(local + Vector3.up * 0.4f, local + Vector3.up * 0.5f, 0.17f, 0.17f, 6, Iron);
                }
            }
            return model.Make(design.Name);
        }

        /// <summary>A point on the map (x, z) in the dock's own frame, at the deck's height.</summary>
        public static Vector3 Local(DockLayout dock, Vector2 at)
        {
            Vector2 d = at - dock.Root;
            return new Vector3(Vector2.Dot(d, dock.Across), 0f, Vector2.Dot(d, dock.Out));
        }

        private static void Pile(LowPolyMeshBuilder model, Vector3 top, float radius, float depth, bool stone, Color colour)
        {
            if (stone)
            {
                model.Box(top + Vector3.down * (depth * 0.5f + Thickness), new Vector3(radius * 2f, depth, radius * 2f), Quaternion.identity, colour);
                return;
            }
            model.Prism(top + Vector3.up * 0.15f, top + Vector3.down * (depth + Thickness), radius, radius, 6, colour);
        }
    }
}
