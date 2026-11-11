using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>The shape a held tool is drawn as (GL269), and how it swings.</summary>
    public enum HeldShape
    {
        Hand,
        HandAxe,
        Pick,
        Hatchet,
        Shovel,
        Hammer,
        Chisel,
        Spear,
    }

    /// <summary>
    /// Low-poly models of the hand and the tools as the player holds them (GL269), made in code: a sleeve and fist with the
    /// tool gripped in it, flat shaded, coloured by vertex (wood handles, stone, bronze or iron heads). The fist is at the
    /// origin, the handle runs up +Y and the tool's working edge faces +Z. Read by <see cref="HeldToolView"/>.
    /// </summary>
    public static class HeldToolMeshes
    {
        private static readonly Color Skin = new Color(0.78f, 0.56f, 0.42f);
        private static readonly Color Sleeve = new Color(0.24f, 0.28f, 0.36f);
        private static readonly Color Wood = new Color(0.42f, 0.28f, 0.15f);
        private static readonly Color Stone = new Color(0.5f, 0.5f, 0.52f);
        private static readonly Color Flint = new Color(0.36f, 0.35f, 0.38f);
        private static readonly Color Bronze = new Color(0.72f, 0.46f, 0.2f);
        private static readonly Color Iron = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color Cord = new Color(0.62f, 0.52f, 0.34f);
        private static readonly Color Copper = new Color(0.8f, 0.43f, 0.25f);
        private static readonly Color Patina = new Color(0.33f, 0.62f, 0.5f);
        private static readonly Color Charcoal = new Color(0.12f, 0.11f, 0.1f);
        // Alpha 0: Clube/Held Item draws it glowing, unlit (GL294).

        // A tool made of stone (a stone pick, hatchet or hammer), as against bronze or iron.
        private static bool IsStone(ToolDefinition tool)
        {
            return tool != null && tool.DisplayName.ToLowerInvariant().Contains("stone");
        }

        /// <summary>The shape a tool is drawn as, by its name; the hand when there's none.</summary>
        public static HeldShape ShapeOf(ToolDefinition tool)
        {
            string name = tool != null ? tool.DisplayName.ToLowerInvariant() : "hand";
            if (name.Contains("hand axe"))
            {
                return HeldShape.HandAxe;
            }
            if (name.Contains("pick"))
            {
                return HeldShape.Pick;
            }
            if (name.Contains("hatchet") || name.Contains("axe"))
            {
                return HeldShape.Hatchet;
            }
            if (name.Contains("shovel"))
            {
                return HeldShape.Shovel;
            }
            if (name.Contains("hammer"))
            {
                return HeldShape.Hammer;
            }
            if (name.Contains("chisel") || (tool != null && tool.Chisels))
            {
                return HeldShape.Chisel;
            }
            if (name.Contains("spear"))
            {
                return HeldShape.Spear;
            }
            return HeldShape.Hand;
        }

        /// <summary>The colour of a tool's head, by what it's made of.</summary>
        public static Color HeadColourOf(ToolDefinition tool)
        {
            string name = tool != null ? tool.DisplayName.ToLowerInvariant() : string.Empty;
            if (name.Contains("bronze"))
            {
                return Bronze;
            }
            if (name.Contains("copper"))
            {
                return Copper; // GL523: the copper pick's head is copper (with its patina), not iron grey
            }
            if (name.Contains("stone") || name.Contains("hand axe"))
            {
                return name.Contains("hand axe") ? Flint : Stone;
            }
            return Iron;
        }

        /// <summary>The model of a tool held in the fist (the bare fist for the hand).</summary>
        public static Mesh Build(ToolDefinition tool, bool withHand = true)
        {
            HeldShape shape = ShapeOf(tool);
            Color head = HeadColourOf(tool);
            var model = new LowPolyMeshBuilder();
            // The forearm, back and down to the bottom right of the view, and the fist round the grip.
            if (withHand)
            {
                model.Prism(new Vector3(0.06f, -0.2f, -0.5f), new Vector3(0.01f, -0.03f, -0.06f), 0.05f, 0.042f, 6, Sleeve);
                model.Box(new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.075f, 0.09f), Quaternion.identity, Skin);
            }
            switch (shape)
            {
                case HeldShape.Hand:
                    // Knuckles a little forward, a thumb over the side.
                    model.Box(new Vector3(0f, 0.01f, 0.05f), new Vector3(0.08f, 0.045f, 0.03f), Quaternion.identity, Skin * 0.95f);
                    model.Box(new Vector3(-0.045f, 0.02f, 0.02f), new Vector3(0.025f, 0.025f, 0.06f), Quaternion.Euler(0f, 20f, 0f), Skin * 0.92f);
                    break;
                case HeldShape.HandAxe:
                    // A knapped stone, pointed end forward, held in the fist.
                    model.Diamond(new Vector3(0f, 0.04f, 0.09f), new Vector3(0.05f, 0.085f, 0.11f), head);
                    break;
                case HeldShape.Pick when IsStone(tool):
                    // A primitive pick (as its icon): a rough stick, and a pointed stone lashed across its top,
                    // sticking out to one side only, bound on with cord.
                    model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0.01f, 0.44f, -0.01f), 0.019f, 0.016f, 5, Wood);
                    model.Prism(new Vector3(0f, 0.43f, -0.035f), new Vector3(0f, 0.38f, 0.23f), 0.034f, 0.007f, 5, head);
                    model.Box(new Vector3(0f, 0.44f, -0.045f), new Vector3(0.05f, 0.055f, 0.04f), Quaternion.Euler(8f, 0f, 0f), head * 0.9f);
                    model.Box(new Vector3(0.002f, 0.415f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.45f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(-30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.39f, 0.006f), new Vector3(0.04f, 0.014f, 0.04f), Quaternion.identity, Cord * 0.9f);
                    break;
                case HeldShape.Pick:
                    model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0f, 0.46f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Prism(new Vector3(0f, 0.45f, -0.02f), new Vector3(0f, 0.39f, 0.24f), 0.028f, 0.006f, 4, head);
                    model.Prism(new Vector3(0f, 0.45f, 0.02f), new Vector3(0f, 0.41f, -0.17f), 0.026f, 0.008f, 4, head);
                    break;
                case HeldShape.Hatchet:
                    model.Prism(new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.42f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Box(new Vector3(0f, 0.4f, 0.04f), new Vector3(0.032f, 0.07f, 0.06f), Quaternion.identity, head);
                    model.Wedge(new Vector3(0f, 0.4f, 0.07f), new Vector3(0f, 0.4f, 0.17f), 0.016f, 0.07f, 0.13f, head);
                    break;
                case HeldShape.Shovel:
                    model.Prism(new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.55f, 0f), 0.017f, 0.016f, 5, Wood);
                    // A spade (GL561, as its icon): a D-handle at the shaft's foot (a grip bar closed by two short uprights
                    // to the shaft), a socket, and a rounded blade narrowing to a point.
                    model.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.11f, 0.025f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(-0.055f, -0.17f, 0f), new Vector3(0.022f, 0.07f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(0.055f, -0.17f, 0f), new Vector3(0.022f, 0.07f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(0f, -0.135f, 0f), new Vector3(0.12f, 0.022f, 0.03f), Quaternion.identity, Wood);
                    model.Prism(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.6f, 0.01f), 0.022f, 0.028f, 5, head * 0.85f);
                    model.Blade(new Vector3(0f, 0.6f, 0.01f), new Vector3(0f, 0.7f, 0.025f), 0.12f, 0.19f, 0.012f, 0.01f, head);
                    model.Blade(new Vector3(0f, 0.7f, 0.025f), new Vector3(0f, 0.78f, 0.035f), 0.19f, 0.14f, 0.01f, 0.008f, head);
                    model.Blade(new Vector3(0f, 0.78f, 0.035f), new Vector3(0f, 0.84f, 0.04f), 0.14f, 0.03f, 0.008f, 0.006f, head * 1.15f);
                    break;
                case HeldShape.Hammer:
                    model.Prism(new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.36f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Box(new Vector3(0f, 0.38f, 0.02f), new Vector3(0.07f, 0.07f, 0.15f), Quaternion.identity, head);
                    break;
                case HeldShape.Chisel:
                    // As its icon (GL398): a rounded wooden handle, a dark ferrule, and a bronze blade flaring from a narrow
                    // neck to a wide, bright bevelled edge.
                    model.Prism(new Vector3(0f, -0.115f, 0.011f), new Vector3(0f, -0.09f, 0.015f), 0.014f, 0.026f, 6, Wood * 0.72f);
                    model.Prism(new Vector3(0f, -0.09f, 0.015f), new Vector3(0f, 0.1f, 0.04f), 0.026f, 0.024f, 6, Wood * 0.8f);
                    model.Prism(new Vector3(0f, 0.098f, 0.04f), new Vector3(0f, 0.125f, 0.044f), 0.028f, 0.028f, 6, head * 0.6f);
                    model.Blade(new Vector3(0f, 0.125f, 0.044f), new Vector3(0f, 0.235f, 0.06f), 0.016f, 0.044f, 0.012f, 0.007f, head);
                    model.Blade(new Vector3(0f, 0.235f, 0.06f), new Vector3(0f, 0.255f, 0.063f), 0.044f, 0.046f, 0.007f, 0.0015f, head * 1.3f);
                    break;
                case HeldShape.Spear:
                    model.Prism(new Vector3(0f, -0.35f, -0.1f), new Vector3(0f, 0.85f, 0.25f), 0.015f, 0.014f, 5, Wood);
                    model.Diamond(new Vector3(0f, 0.93f, 0.28f), new Vector3(0.025f, 0.13f, 0.04f), head);
                    break;
            }
            if (head == Copper)
            {
                AddPatina(model, shape);
            }
            return model.Make($"Held {shape}");
        }

        // Copper weathers green at its edges (GL286): patina over the tips and the eye round the handle.
        private static void AddPatina(LowPolyMeshBuilder model, HeldShape shape)
        {
            switch (shape)
            {
                case HeldShape.Pick:
                    model.Prism(new Vector3(0f, 0.394f, 0.2f), new Vector3(0f, 0.39f, 0.245f), 0.0085f, 0.0035f, 4, Patina);
                    model.Prism(new Vector3(0f, 0.415f, -0.14f), new Vector3(0f, 0.41f, -0.175f), 0.0095f, 0.004f, 4, Patina);
                    model.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.064f, 0.034f, 0.062f), Quaternion.identity, Patina * 0.9f);
                    break;
                case HeldShape.Hatchet:
                    model.Box(new Vector3(0f, 0.4f, 0.165f), new Vector3(0.02f, 0.13f, 0.012f), Quaternion.identity, Patina);
                    model.Box(new Vector3(0f, 0.4f, 0.012f), new Vector3(0.036f, 0.072f, 0.012f), Quaternion.identity, Patina * 0.9f);
                    break;
            }
        }

        /// <summary>
        /// A torch held in the fist (GL287): a stick with a charred head bound on; when lit, HeldToolView stands a dancing
        /// flame cluster (FlameCluster, GL314) on its head.
        /// </summary>
        public static Mesh BuildTorch(bool lit, bool withHand = true)
        {
            var model = new LowPolyMeshBuilder();
            if (withHand)
            {
                model.Prism(new Vector3(0.06f, -0.2f, -0.5f), new Vector3(0.01f, -0.03f, -0.06f), 0.05f, 0.042f, 6, Sleeve);
                model.Box(new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.075f, 0.09f), Quaternion.identity, Skin);
            }
            model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0.005f, 0.34f, 0.03f), 0.018f, 0.016f, 5, Wood);
            model.Prism(new Vector3(0.005f, 0.3f, 0.027f), new Vector3(0.006f, 0.4f, 0.035f), 0.032f, 0.026f, 6, Charcoal);
            model.Box(new Vector3(0.005f, 0.3f, 0.027f), new Vector3(0.068f, 0.014f, 0.068f), Quaternion.identity, Cord);
            return model.Make(lit ? "Held lit torch" : "Held torch");
        }

        /// <summary>
        /// A wooden bucket held by its handle (GL319): the fist on a bent rope handle over a tapered, eight-sided body of
        /// staves with two dark iron-coloured hoops.
        /// </summary>
        public static Mesh BuildBucket(bool withHand = true)
        {
            var model = new LowPolyMeshBuilder();
            if (withHand)
            {
                model.Prism(new Vector3(0.06f, -0.2f, -0.5f), new Vector3(0.01f, -0.03f, -0.06f), 0.05f, 0.042f, 6, Sleeve);
                model.Box(new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.075f, 0.09f), Quaternion.identity, Skin);
            }
            // The handle: a rope from the fist down to each side of the rim.
            model.Prism(new Vector3(-0.01f, -0.01f, 0f), new Vector3(-0.1f, -0.13f, 0.02f), 0.007f, 0.007f, 4, Cord);
            model.Prism(new Vector3(0.01f, -0.01f, 0f), new Vector3(0.1f, -0.13f, 0.02f), 0.007f, 0.007f, 4, Cord);
            // The body: wider at the rim than the base.
            model.Prism(new Vector3(0f, -0.36f, 0.02f), new Vector3(0f, -0.13f, 0.02f), 0.085f, 0.105f, 8, Wood);
            model.Prism(new Vector3(0f, -0.33f, 0.02f), new Vector3(0f, -0.31f, 0.02f), 0.092f, 0.093f, 8, Charcoal);
            model.Prism(new Vector3(0f, -0.18f, 0.02f), new Vector3(0f, -0.16f, 0.02f), 0.102f, 0.103f, 8, Charcoal);
            return model.Make("Held bucket");
        }
    }
}
