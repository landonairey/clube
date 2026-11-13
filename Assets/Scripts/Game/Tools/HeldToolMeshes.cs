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
        private static readonly Color Skin = FigureLooks.Of(FigurePreset.Player).Skin;
        private static readonly Color Sleeve = FigureLooks.Of(FigurePreset.Player).Shirt;
        private static readonly Color Wood = new Color(0.42f, 0.28f, 0.15f);
        private static readonly Color Stone = new Color(0.5f, 0.5f, 0.52f);
        private static readonly Color Flint = new Color(0.36f, 0.35f, 0.38f);
        private static readonly Color Bronze = new Color(0.72f, 0.46f, 0.2f);
        private static readonly Color Iron = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color Steel = new Color(0.7f, 0.74f, 0.8f);
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

        /// <summary>
        /// What a tool's head is made of (GL609), found by a word in the tool's name: its colour, how much it shines (0 for
        /// stone; the nicer the metal the more) and how worn it looks (cracks and notches: 3 for knapped stone, 1 for steel).
        /// </summary>
        public readonly struct HeadMaterial
        {
            public readonly string Word;
            public readonly Color Colour;
            public readonly float Shine;
            public readonly int Flaws;

            public HeadMaterial(string word, Color colour, float shine, int flaws)
            {
                Word = word;
                Colour = colour;
                Shine = shine;
                Flaws = flaws;
            }

            public bool IsMetal => Shine > 0.2f;
        }

        // The first row whose word is in the tool's name; iron for a tool that names none (the shovel, the spear).
        private static readonly HeadMaterial IronHead = new HeadMaterial("iron", Iron, 0.55f, 2);
        private static readonly HeadMaterial[] Heads =
        {
            new HeadMaterial("bronze", Bronze, 0.7f, 2),
            new HeadMaterial("steel", Steel, 1f, 1), // GL624: paler and bluer than iron
            new HeadMaterial("copper", Copper, 0.45f, 2), // GL523: the copper pick's head is copper (with its patina)
            IronHead,
            new HeadMaterial("hand axe", Flint, 0.12f, 3),
            new HeadMaterial("stone", Stone, 0f, 3),
        };

        /// <summary>What a tool's head is made of, by its name.</summary>
        public static HeadMaterial HeadOf(ToolDefinition tool)
        {
            string name = tool != null ? tool.DisplayName.ToLowerInvariant() : string.Empty;
            foreach (HeadMaterial head in Heads)
            {
                if (name.Contains(head.Word))
                {
                    return head;
                }
            }
            return IronHead;
        }

        /// <summary>The colour of a tool's head, by what it's made of.</summary>
        public static Color HeadColourOf(ToolDefinition tool)
        {
            return HeadOf(tool).Colour;
        }

        // A wooden handle (GL609): carved a little out of true, swelling at the middle, with the shader's grain down it
        // and dark knots standing just proud of it. Along Y (a Loft); from is the foot.
        private static void Handle(LowPolyMeshBuilder model, Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, Color wood, int knots = 2)
        {
            Vector3 bend = new Vector3(fromRadius * 0.22f, 0f, -fromRadius * 0.16f);
            Vector3 low = Vector3.Lerp(from, to, 0.35f) + bend, high = Vector3.Lerp(from, to, 0.72f) - bend * 0.7f;
            float lowRadius = Mathf.Lerp(fromRadius, toRadius, 0.35f) * 1.07f, highRadius = Mathf.Lerp(fromRadius, toRadius, 0.72f) * 0.96f;
            model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
            model.Loft(new[] { from, low, high, to },
                new[] { Vector2.one * fromRadius, Vector2.one * lowRadius, Vector2.one * highRadius, Vector2.one * toRadius }, sides, wood);
            model.Surface = Vector2.zero;
            Vector3 along = (to - from).normalized;
            for (int i = 0; i < knots; i++)
            {
                // Above the fist (the origin), on the sides the player sees: left, then front right.
                float t = Mathf.InverseLerp(from.y, to.y, Mathf.Min(0.1f + 0.14f * i, to.y - 0.07f));
                float around = (200f - 130f * i) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(around), 0f, Mathf.Sin(around));
                float radius = Mathf.Lerp(fromRadius, toRadius, t);
                Vector3 at = Vector3.Lerp(from, to, t) + outward * radius * 0.82f;
                Quaternion turn = Quaternion.LookRotation(outward, along);
                model.Blob(at, new Vector3(radius * 0.5f, radius * 0.85f, radius * 0.3f), turn, Shade(wood, 0.6f), 5, 2);
                model.Blob(at + outward * radius * 0.2f, new Vector3(radius * 0.22f, radius * 0.4f, radius * 0.16f), turn, Shade(wood, 0.36f), 4, 2);
            }
        }

        // A crack (GL609): a dark sliver through the head across its length, standing just out of one side of it.
        private static void Crack(LowPolyMeshBuilder model, Vector3 at, Vector3 size, Vector3 turn, Color head)
        {
            Vector2 surface = model.Surface;
            model.Surface = Vector2.zero;
            model.Box(at, size, Quaternion.Euler(turn), Shade(head, 0.3f));
            model.Surface = surface;
        }

        // A notch (GL609): a dark chip out of an edge.
        private static void Notch(LowPolyMeshBuilder model, Vector3 at, Vector3 half, Color head)
        {
            Vector2 surface = model.Surface;
            model.Surface = Vector2.zero;
            model.Diamond(at, half, Shade(head, 0.34f));
            model.Surface = surface;
        }

        /// <summary>The model of a tool held in the fist (the bare fist for the hand); without the hand, the tool alone
        /// (dropped on the ground, GL610; an inventory icon).</summary>
        public static Mesh Build(ToolDefinition tool, bool withHand = true)
        {
            HeldShape shape = ShapeOf(tool);
            HeadMaterial material = HeadOf(tool);
            Color head = material.Colour;
            int flaws = material.Flaws;
            var model = new LowPolyMeshBuilder();
            // The forearm, back and down to the bottom right of the view, and the fist round the grip.
            if (withHand)
            {
                Arm(model);
            }
            Vector2 headSurface = material.Shine > 0f ? new Vector2(LowPolyMeshBuilder.MetalSurface, material.Shine) : Vector2.zero;
            switch (shape)
            {
                case HeldShape.Hand:
                    // Knuckles a little forward, a thumb over the side.
                    model.Blob(new Vector3(0f, 0.012f, 0.04f), new Vector3(0.042f, 0.026f, 0.026f), Quaternion.identity, Shade(Skin, 0.95f), 5, 3);
                    model.Prism(new Vector3(-0.042f, 0.012f, -0.015f), new Vector3(-0.03f, 0.03f, 0.055f), 0.017f, 0.011f, 4, Shade(Skin, 0.92f));
                    break;
                case HeldShape.HandAxe:
                    // A knapped stone, pointed end forward, held in the fist.
                    model.Surface = headSurface;
                    model.Diamond(new Vector3(0f, 0.04f, 0.09f), new Vector3(0.05f, 0.085f, 0.11f), head);
                    // Knapped: flake scars along the edge, and a crack across the point.
                    Notch(model, new Vector3(0.026f, 0.085f, 0.09f), new Vector3(0.012f, 0.02f, 0.016f), head);
                    Notch(model, new Vector3(-0.03f, 0.005f, 0.075f), new Vector3(0.012f, 0.016f, 0.02f), head);
                    Crack(model, new Vector3(0.004f, 0.045f, 0.15f), new Vector3(0.05f, 0.06f, 0.003f), new Vector3(0f, 18f, 12f), head);
                    break;
                case HeldShape.Pick when IsStone(tool):
                    // A primitive pick (as its icon): a rough stick, and a pointed stone lashed across its top,
                    // sticking out to one side only, bound on with cord.
                    Handle(model, new Vector3(0f, -0.14f, 0f), new Vector3(0.01f, 0.44f, -0.01f), 0.019f, 0.016f, 5, Wood);
                    model.Prism(new Vector3(0f, 0.43f, -0.035f), new Vector3(0f, 0.38f, 0.23f), 0.034f, 0.007f, 5, head);
                    model.Box(new Vector3(0f, 0.44f, -0.045f), new Vector3(0.05f, 0.055f, 0.04f), Quaternion.Euler(8f, 0f, 0f), head * 0.9f);
                    // Cracks across the stone, and a chip off its point.
                    Crack(model, new Vector3(0.004f, 0.418f, 0.075f), new Vector3(0.05f, 0.044f, 0.003f), new Vector3(-10f, 20f, 0f), head);
                    Crack(model, new Vector3(-0.003f, 0.4f, 0.15f), new Vector3(0.028f, 0.024f, 0.0025f), new Vector3(-12f, -25f, 0f), head);
                    Notch(model, new Vector3(0.004f, 0.386f, 0.2f), new Vector3(0.009f, 0.007f, 0.014f), head);
                    model.Box(new Vector3(0.002f, 0.415f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.45f, -0.004f), new Vector3(0.046f, 0.016f, 0.05f), Quaternion.Euler(-30f, 0f, 0f), Cord);
                    model.Box(new Vector3(0.002f, 0.39f, 0.006f), new Vector3(0.04f, 0.014f, 0.04f), Quaternion.identity, Cord * 0.9f);
                    break;
                case HeldShape.Pick:
                    Handle(model, new Vector3(0f, -0.14f, 0f), new Vector3(0f, 0.46f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Surface = headSurface;
                    // The eye round the handle, darker, and the two arms.
                    model.Box(new Vector3(0f, 0.447f, 0f), new Vector3(0.046f, 0.05f, 0.05f), Quaternion.identity, Shade(head, 0.82f));
                    model.Prism(new Vector3(0f, 0.45f, -0.02f), new Vector3(0f, 0.39f, 0.24f), 0.028f, 0.006f, 4, head);
                    model.Prism(new Vector3(0f, 0.45f, 0.02f), new Vector3(0f, 0.41f, -0.17f), 0.026f, 0.008f, 4, head);
                    // Worn by the rock: a crack across the long arm, a notch near its point, and more on a softer metal.
                    Crack(model, new Vector3(0.003f, 0.427f, 0.1f), new Vector3(0.036f, 0.032f, 0.0025f), new Vector3(-12f, 22f, 0f), head);
                    if (flaws > 1)
                    {
                        Notch(model, new Vector3(0.003f, 0.394f, 0.19f), new Vector3(0.008f, 0.007f, 0.013f), head);
                        Crack(model, new Vector3(-0.003f, 0.437f, -0.09f), new Vector3(0.026f, 0.024f, 0.0025f), new Vector3(8f, -20f, 0f), head);
                    }
                    break;
                case HeldShape.Hatchet:
                    Handle(model, new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.42f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Surface = headSurface;
                    model.Box(new Vector3(0f, 0.4f, 0.04f), new Vector3(0.032f, 0.07f, 0.06f), Quaternion.identity, Shade(head, 0.88f));
                    model.Wedge(new Vector3(0f, 0.4f, 0.07f), new Vector3(0f, 0.4f, 0.17f), 0.016f, 0.07f, 0.13f, head);
                    // Nicks out of the cutting edge, and on stone or soft metal a crack back from it.
                    Notch(model, new Vector3(0f, 0.43f, 0.168f), new Vector3(0.006f, 0.009f, 0.008f), head);
                    if (flaws > 1)
                    {
                        Notch(model, new Vector3(0f, 0.368f, 0.168f), new Vector3(0.006f, 0.012f, 0.01f), head);
                    }
                    if (flaws > 2)
                    {
                        Crack(model, new Vector3(0f, 0.405f, 0.11f), new Vector3(0.02f, 0.003f, 0.07f), new Vector3(14f, 0f, 0f), head);
                    }
                    break;
                case HeldShape.Shovel:
                    Handle(model, new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.55f, 0f), 0.017f, 0.016f, 5, Wood);
                    model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
                    // A spade (GL561, as its icon): a D-handle at the shaft's foot (a grip bar closed by two short uprights
                    // to the shaft), a socket, and a rounded blade narrowing to a point.
                    model.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.11f, 0.025f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(-0.055f, -0.17f, 0f), new Vector3(0.022f, 0.07f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(0.055f, -0.17f, 0f), new Vector3(0.022f, 0.07f, 0.03f), Quaternion.identity, Wood);
                    model.Box(new Vector3(0f, -0.135f, 0f), new Vector3(0.12f, 0.022f, 0.03f), Quaternion.identity, Wood);
                    model.Surface = headSurface;
                    model.Prism(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.6f, 0.01f), 0.022f, 0.028f, 5, head * 0.85f);
                    model.Blade(new Vector3(0f, 0.6f, 0.01f), new Vector3(0f, 0.7f, 0.025f), 0.12f, 0.19f, 0.012f, 0.01f, head);
                    model.Blade(new Vector3(0f, 0.7f, 0.025f), new Vector3(0f, 0.78f, 0.035f), 0.19f, 0.14f, 0.01f, 0.008f, head);
                    model.Blade(new Vector3(0f, 0.78f, 0.035f), new Vector3(0f, 0.84f, 0.04f), 0.14f, 0.03f, 0.008f, 0.006f, head * 1.15f);
                    // A dent in the blade's edge from the stones it has met.
                    Notch(model, new Vector3(0.052f, 0.8f, 0.037f), new Vector3(0.012f, 0.014f, 0.008f), head);
                    if (flaws > 1)
                    {
                        Notch(model, new Vector3(-0.088f, 0.715f, 0.027f), new Vector3(0.009f, 0.016f, 0.008f), head);
                    }
                    break;
                case HeldShape.Hammer:
                    Handle(model, new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.36f, 0f), 0.017f, 0.015f, 5, Wood);
                    model.Surface = headSurface;
                    model.Box(new Vector3(0f, 0.38f, 0.02f), new Vector3(0.07f, 0.07f, 0.15f), Quaternion.identity, head);
                    // A battered head: a crack round it, and its striking corners chipped.
                    Crack(model, new Vector3(0.004f, 0.384f, 0.05f), new Vector3(0.073f, 0.072f, 0.003f), new Vector3(0f, 14f, 0f), head);
                    Notch(model, new Vector3(0.035f, 0.415f, 0.095f), new Vector3(0.012f, 0.012f, 0.014f), head);
                    if (flaws > 1)
                    {
                        Notch(model, new Vector3(-0.035f, 0.345f, 0.095f), new Vector3(0.01f, 0.012f, 0.012f), head);
                    }
                    break;
                case HeldShape.Chisel:
                    // As its icon (GL398): a rounded wooden handle, a dark ferrule, and a bronze blade flaring from a narrow
                    // neck to a wide, bright bevelled edge.
                    model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
                    model.Prism(new Vector3(0f, -0.115f, 0.011f), new Vector3(0f, -0.09f, 0.015f), 0.014f, 0.026f, 6, Wood * 0.72f);
                    model.Prism(new Vector3(0f, -0.09f, 0.015f), new Vector3(0f, 0.1f, 0.04f), 0.026f, 0.024f, 6, Wood * 0.8f);
                    model.Surface = headSurface;
                    model.Prism(new Vector3(0f, 0.098f, 0.04f), new Vector3(0f, 0.125f, 0.044f), 0.028f, 0.028f, 6, head * 0.6f);
                    model.Blade(new Vector3(0f, 0.125f, 0.044f), new Vector3(0f, 0.235f, 0.06f), 0.016f, 0.044f, 0.012f, 0.007f, head);
                    model.Blade(new Vector3(0f, 0.235f, 0.06f), new Vector3(0f, 0.255f, 0.063f), 0.044f, 0.046f, 0.007f, 0.0015f, head * 1.3f);
                    Notch(model, new Vector3(0.012f, 0.253f, 0.063f), new Vector3(0.005f, 0.006f, 0.004f), head);
                    break;
                case HeldShape.Spear:
                    model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
                    model.Prism(new Vector3(0f, -0.35f, -0.1f), new Vector3(0f, 0.85f, 0.25f), 0.015f, 0.014f, 5, Wood);
                    model.Surface = Vector2.zero;
                    // The head lashed on with cord.
                    model.Prism(new Vector3(0f, 0.8f, 0.235f), new Vector3(0f, 0.85f, 0.25f), 0.02f, 0.019f, 5, Cord);
                    model.Surface = headSurface;
                    model.Diamond(new Vector3(0f, 0.93f, 0.28f), new Vector3(0.025f, 0.13f, 0.04f), head);
                    Notch(model, new Vector3(0.017f, 0.96f, 0.29f), new Vector3(0.007f, 0.012f, 0.008f), head);
                    break;
            }
            model.Surface = Vector2.zero;
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
                Arm(model);
            }
            model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
            model.Prism(new Vector3(0f, -0.14f, 0f), new Vector3(0.005f, 0.34f, 0.03f), 0.018f, 0.016f, 5, Wood);
            model.Surface = Vector2.zero;
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
                Arm(model);
            }
            // The handle: a rope from the fist down to each side of the rim.
            model.Prism(new Vector3(-0.01f, -0.01f, 0f), new Vector3(-0.1f, -0.13f, 0.02f), 0.007f, 0.007f, 4, Cord);
            model.Prism(new Vector3(0.01f, -0.01f, 0f), new Vector3(0.1f, -0.13f, 0.02f), 0.007f, 0.007f, 4, Cord);
            // The body: wider at the rim than the base.
            model.Surface = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
            model.Prism(new Vector3(0f, -0.36f, 0.02f), new Vector3(0f, -0.13f, 0.02f), 0.085f, 0.105f, 8, Wood);
            model.Surface = Vector2.zero;
            model.Prism(new Vector3(0f, -0.33f, 0.02f), new Vector3(0f, -0.31f, 0.02f), 0.092f, 0.093f, 8, Charcoal);
            model.Prism(new Vector3(0f, -0.18f, 0.02f), new Vector3(0f, -0.16f, 0.02f), 0.102f, 0.103f, 8, Charcoal);
            return model.Make("Held bucket");
        }

        // The player's forearm and fist (GL608), in the figures' look: a tapered six-sided sleeve from the bottom right of
        // the view, a cuff, a bare wrist and a faceted fist round the grip at the origin.
        private static void Arm(LowPolyMeshBuilder model)
        {
            Vector3 elbow = new Vector3(0.06f, -0.2f, -0.5f), wrist = new Vector3(0.01f, -0.03f, -0.06f);
            float length = (wrist - elbow).magnitude;
            Matrix4x4 before = model.Placement;
            model.Placement = before * Matrix4x4.TRS(elbow, Quaternion.FromToRotation(Vector3.up, wrist - elbow), Vector3.one);
            model.Loft(new[] { Vector3.zero, Vector3.up * (length - 0.1f) }, new[] { new Vector2(0.06f, 0.06f), new Vector2(0.046f, 0.046f) }, 6, Sleeve);
            model.Loft(new[] { Vector3.up * (length - 0.13f), Vector3.up * (length - 0.085f) }, new[] { new Vector2(0.052f, 0.052f), new Vector2(0.05f, 0.05f) }, 6,
                Shade(Sleeve, 0.8f));
            model.Loft(new[] { Vector3.up * (length - 0.1f), Vector3.up * (length - 0.02f) }, new[] { new Vector2(0.037f, 0.037f), new Vector2(0.034f, 0.034f) }, 6,
                Shade(Skin, 0.95f));
            model.Placement = before;
            model.Blob(Vector3.zero, new Vector3(0.047f, 0.042f, 0.052f), Quaternion.identity, Skin, 6, 3);
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(color.r * by, color.g * by, color.b * by, color.a);
        }
    }
}
