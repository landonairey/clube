using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Low-poly models of the town (GL400, GL405), made in code as vertex-coloured meshes for <c>Clube/Low Poly</c>: the
    /// cobblestone wall, the gate's leaves, the shops and the props the keepers' chores use. The wall is in the town's frame
    /// (<see cref="TownLayout"/>), everything else in its own: origin on the ground at its centre, front facing +z.
    /// </summary>
    public static class TownMeshes
    {
        private static readonly Color Timber = new Color(0.55f, 0.4f, 0.25f);
        private static readonly Color DarkTimber = new Color(0.36f, 0.25f, 0.15f);
        private static readonly Color Plank = new Color(0.62f, 0.47f, 0.3f);
        private static readonly Color Thatch = new Color(0.58f, 0.48f, 0.27f);
        private static readonly Color Stone = new Color(0.52f, 0.51f, 0.5f);
        private static readonly Color Iron = new Color(0.27f, 0.28f, 0.3f);
        private static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
        private static readonly Color Bark = new Color(0.4f, 0.3f, 0.2f);
        private static readonly Color Leaf = new Color(0.3f, 0.48f, 0.22f);
        private static readonly Color Cloth = new Color(0.55f, 0.35f, 0.3f);
        private static readonly Color Blanket = new Color(0.42f, 0.36f, 0.55f);
        private static readonly Color Paper = new Color(0.86f, 0.8f, 0.66f);

        // Courses of cobbles up the wall, and the range of a cobble's length along it.
        private const float CourseHeight = 0.4f;
        private const float MinCobble = 0.45f;
        private const float MaxCobble = 0.85f;

        /// <summary>
        /// The town wall (GL400) in the town's frame: courses of uneven cobbles round the square, a darker capping course,
        /// and two tall pillars either side of the gate's gap in the -z wall. Seeded, so a town's wall always looks the same.
        /// </summary>
        public static Mesh Wall(int seed)
        {
            var model = new LowPolyMeshBuilder();
            var random = new System.Random(seed);
            float outer = TownLayout.OuterHalf;
            float inner = TownLayout.InnerHalf;
            float middle = (outer + inner) * 0.5f;
            float thickness = TownLayout.WallThickness;
            float gate = TownLayout.GateWidth * 0.5f;
            const float Pillar = 1.5f;

            // The far and near walls run the full width; the sides fit between them.
            WallRun(model, random, new Vector2(-outer, middle), new Vector2(outer, middle), thickness);
            WallRun(model, random, new Vector2(-outer, -middle), new Vector2(-gate - Pillar, -middle), thickness);
            WallRun(model, random, new Vector2(gate + Pillar, -middle), new Vector2(outer, -middle), thickness);
            WallRun(model, random, new Vector2(-middle, -inner), new Vector2(-middle, inner), thickness);
            WallRun(model, random, new Vector2(middle, -inner), new Vector2(middle, inner), thickness);

            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (gate + Pillar * 0.5f);
                for (float y = 0f; y < TownLayout.WallHeight + 0.6f; y += CourseHeight)
                {
                    Color tint = Stone * (0.82f + (float)random.NextDouble() * 0.22f);
                    model.Block(new Vector3(x, y, -middle), new Vector3(Pillar - 0.04f, CourseHeight - 0.03f, thickness + 0.3f), tint);
                }
                model.Block(new Vector3(x, TownLayout.WallHeight + 0.6f, -middle), new Vector3(Pillar + 0.15f, 0.18f, thickness + 0.45f), Stone * 0.7f);
            }
            return model.Make("Town wall");
        }

        // One straight run of wall from a to b (along x or z), course by course; joints are staggered by the random lengths.
        private static void WallRun(LowPolyMeshBuilder model, System.Random random, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 along = b - a;
            float length = along.magnitude;
            if (length < 0.01f)
            {
                return;
            }
            Vector2 dir = along / length;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            int courses = Mathf.CeilToInt(TownLayout.WallHeight / CourseHeight);
            for (int course = 0; course < courses; course++)
            {
                float y = course * CourseHeight;
                float height = Mathf.Min(CourseHeight, TownLayout.WallHeight - y);
                float t = 0f;
                while (t < length - 0.01f)
                {
                    float piece = Mathf.Min(length - t, Mathf.Lerp(MinCobble, MaxCobble, (float)random.NextDouble()));
                    Vector2 centre = a + dir * (t + piece * 0.5f);
                    // Each cobble sits a touch in or out of the face and leaves a thin mortar joint round it.
                    float bulge = ((float)random.NextDouble() - 0.5f) * 0.08f;
                    Color tint = Stone * (0.75f + (float)random.NextDouble() * 0.3f);
                    tint.r *= 1f + ((float)random.NextDouble() - 0.5f) * 0.08f;
                    Vector3 size = alongX
                        ? new Vector3(piece - 0.05f, height - 0.04f, thickness + bulge)
                        : new Vector3(thickness + bulge, height - 0.04f, piece - 0.05f);
                    model.Block(new Vector3(centre.x, y, centre.y), size, tint);
                    t += piece;
                }
            }
            // The mortar behind the cobbles, so the joints never show light through.
            Vector2 mid = (a + b) * 0.5f;
            Vector3 core = alongX ? new Vector3(length, TownLayout.WallHeight, thickness - 0.12f) : new Vector3(thickness - 0.12f, TownLayout.WallHeight, length);
            model.Block(new Vector3(mid.x, 0f, mid.y), core, Stone * 0.45f);
            // The capping course: flat, darker slabs a little wider than the wall.
            Vector3 cap = alongX ? new Vector3(length, 0.16f, thickness + 0.2f) : new Vector3(thickness + 0.2f, 0.16f, length);
            model.Block(new Vector3(mid.x, TownLayout.WallHeight, mid.y), cap, Stone * 0.68f);
        }

        /// <summary>
        /// One leaf of the wooden gate (GL400): vertical planks with two cross rails and a brace, hinged at the origin and
        /// reaching along +x for <paramref name="width"/>.
        /// </summary>
        public static Mesh GateLeaf(float width, float height)
        {
            var model = new LowPolyMeshBuilder();
            int planks = Mathf.Max(2, Mathf.RoundToInt(width / 0.25f));
            float plank = width / planks;
            for (int i = 0; i < planks; i++)
            {
                float shade = i % 2 == 0 ? 1f : 0.9f;
                model.Block(new Vector3((i + 0.5f) * plank, 0.05f, 0f), new Vector3(plank - 0.02f, height - (i % 3) * 0.04f, 0.08f), Timber * shade);
            }
            model.Block(new Vector3(width * 0.5f, 0.45f, -0.07f), new Vector3(width, 0.18f, 0.06f), DarkTimber);
            model.Block(new Vector3(width * 0.5f, height - 0.65f, -0.07f), new Vector3(width, 0.18f, 0.06f), DarkTimber);
            float brace = Mathf.Atan2(height - 1.1f, width) * Mathf.Rad2Deg;
            model.Box(new Vector3(width * 0.5f, height * 0.5f - 0.05f, -0.07f),
                new Vector3(Mathf.Sqrt(width * width + (height - 1.1f) * (height - 1.1f)), 0.16f, 0.05f), Quaternion.Euler(0f, 0f, brace), DarkTimber);
            // Iron straps on the hinges.
            model.Block(new Vector3(0.25f, 0.4f, 0.05f), new Vector3(0.5f, 0.07f, 0.03f), Iron);
            model.Block(new Vector3(0.25f, height - 0.7f, 0.05f), new Vector3(0.5f, 0.07f, 0.03f), Iron);
            return model.Make("Town gate leaf");
        }

        /// <summary>
        /// A shop (GL400) in its own frame: a plank floor, timber walls on three sides, a thatched gable roof, an open front
        /// with a counter across it, and an awning and sign board in the shop's colour.
        /// </summary>
        public static Mesh Shop(Color colour)
        {
            var model = new LowPolyMeshBuilder();
            float w = TownLayout.ShopWidth * 0.5f;
            float d = TownLayout.ShopDepth * 0.5f;
            float h = TownLayout.ShopHeight;
            model.Block(new Vector3(0f, 0f, 0f), new Vector3(w * 2f, 0.1f, d * 2f), Plank * 0.9f);
            // Back and sides: plank walls between posts.
            model.Block(new Vector3(0f, 0f, -d + 0.1f), new Vector3(w * 2f, h, 0.2f), Timber);
            model.Block(new Vector3(-w + 0.1f, 0f, 0f), new Vector3(0.2f, h, d * 2f), Timber * 0.95f);
            model.Block(new Vector3(w - 0.1f, 0f, 0.35f), new Vector3(0.2f, h, d * 2f - 0.7f), Timber * 0.95f);
            foreach (float x in new[] { -w + 0.12f, w - 0.12f })
            {
                foreach (float z in new[] { -d + 0.12f, d - 0.12f })
                {
                    model.Block(new Vector3(x, 0f, z), new Vector3(0.26f, h + 0.05f, 0.26f), DarkTimber);
                }
            }
            model.Block(new Vector3(0f, h - 0.2f, d - 0.12f), new Vector3(w * 2f, 0.24f, 0.24f), DarkTimber);
            // The roof: a gable along x, its ends filled in.
            const float Rise = 1.2f;
            model.GableRoof(new Vector3(0f, h, 0f), w * 2f + 0.5f, d * 2f + 0.8f, Rise, 0.14f, Thatch);
            foreach (float x in new[] { -w + 0.1f, w - 0.1f })
            {
                var centre = new Vector3(x, h + Rise * 0.4f, 0f);
                model.Triangle(new Vector3(x, h, -d), new Vector3(x, h, d), new Vector3(x, h + Rise, 0f), centre - Vector3.right * Mathf.Sign(x), Timber * 0.88f);
                model.Triangle(new Vector3(x, h, d), new Vector3(x, h, -d), new Vector3(x, h + Rise, 0f), centre + Vector3.right * Mathf.Sign(x), Timber * 0.88f);
            }
            // The counter: a plank front and a lighter top.
            float counter = TownLayout.CounterHeight;
            model.Block(new Vector3(0f, 0f, TownLayout.CounterZ), new Vector3(TownLayout.CounterLength, counter - 0.08f, 0.55f), Timber * 0.85f);
            model.Block(new Vector3(0f, counter - 0.08f, TownLayout.CounterZ), new Vector3(TownLayout.CounterLength + 0.2f, 0.08f, 0.7f), Plank * 1.1f);
            // The awning: a sloping strip of cloth over the front, in the shop's colour, with a pale stripe.
            model.Box(new Vector3(0f, h - 0.15f, d + 0.45f), new Vector3(w * 2f + 0.3f, 0.06f, 1f), Quaternion.Euler(18f, 0f, 0f), colour);
            model.Box(new Vector3(0f, h - 0.33f, d + 0.93f), new Vector3(w * 2f + 0.3f, 0.2f, 0.04f), Quaternion.identity, Color.Lerp(colour, Color.white, 0.5f));
            // The sign board over the counter.
            model.Block(new Vector3(0f, h + 0.05f, d + 0.02f), new Vector3(2.6f, 0.62f, 0.08f), colour * 0.75f);
            model.Block(new Vector3(0f, h + 0.1f, d + 0.06f), new Vector3(2.4f, 0.5f, 0.04f), Color.Lerp(Plank, Color.white, 0.25f));
            // A shelf on the back wall.
            model.Block(new Vector3(-1.4f, 1.4f, -d + 0.4f), new Vector3(2.6f, 0.06f, 0.45f), Plank);
            return model.Make("Shop");
        }

        /// <summary>A sleeping roll (GL406): a flat blanket along x with a rolled pillow at the +x end.</summary>
        public static Mesh SleepingRoll()
        {
            var model = new LowPolyMeshBuilder();
            model.Block(new Vector3(0f, 0f, 0f), new Vector3(2f, 0.1f, 0.85f), Blanket);
            model.Block(new Vector3(-0.25f, 0.1f, 0f), new Vector3(1.4f, 0.06f, 0.8f), Blanket * 0.85f);
            model.Prism(new Vector3(0.82f, 0.16f, -0.38f), new Vector3(0.82f, 0.16f, 0.38f), 0.12f, 0.12f, 6, Paper);
            return model.Make("Sleeping roll");
        }

        /// <summary>A stone furnace (GL405): a squat kiln with a chimney and a glowing mouth facing +z.</summary>
        public static Mesh Furnace()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, 1.1f, 0f), 0.75f, 0.6f, 8, Stone * 0.9f);
            model.Prism(new Vector3(0f, 1.1f, 0f), new Vector3(0f, 1.5f, 0f), 0.6f, 0.35f, 8, Stone * 0.8f);
            model.Prism(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 2.3f, 0f), 0.22f, 0.2f, 6, Stone * 0.7f);
            model.Box(new Vector3(0f, 0.38f, 0.6f), new Vector3(0.45f, 0.38f, 0.25f), Quaternion.identity, Ember);
            model.Box(new Vector3(0f, 0.62f, 0.66f), new Vector3(0.62f, 0.1f, 0.2f), Quaternion.identity, Stone * 0.6f);
            return model.Make("Town furnace");
        }

        /// <summary>An anvil on a stump (GL405), its horn along +x.</summary>
        public static Mesh Anvil()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.5f, 0f), 0.3f, 0.27f, 7, Bark);
            model.Block(new Vector3(0f, 0.5f, 0f), new Vector3(0.28f, 0.16f, 0.22f), Iron);
            model.Block(new Vector3(0f, 0.66f, 0f), new Vector3(0.18f, 0.12f, 0.14f), Iron * 0.9f);
            model.Block(new Vector3(-0.04f, 0.78f, 0f), new Vector3(0.5f, 0.14f, 0.22f), Iron);
            model.Prism(new Vector3(0.2f, 0.85f, 0f), new Vector3(0.48f, 0.84f, 0f), 0.08f, 0.012f, 5, Iron * 1.1f);
            return model.Make("Town anvil");
        }

        /// <summary>A woodpile (GL405): logs stacked lying along z, ends showing pale on ±z.</summary>
        public static Mesh Woodpile(int seed)
        {
            var model = new LowPolyMeshBuilder();
            var random = new System.Random(seed);
            const float Radius = 0.14f;
            for (int row = 0; row < 4; row++)
            {
                int logs = 5 - row;
                for (int i = 0; i < logs; i++)
                {
                    float x = (i - (logs - 1) * 0.5f) * Radius * 2.05f;
                    float y = Radius + row * Radius * 1.75f;
                    float jut = ((float)random.NextDouble() - 0.5f) * 0.25f;
                    model.Prism(new Vector3(x, y, -1.1f + jut), new Vector3(x, y, 1.1f + jut), Radius, Radius * 0.95f, 6, Bark * (0.9f + (float)random.NextDouble() * 0.2f));
                }
            }
            // A chopping block with an axe in it beside the pile.
            model.Prism(new Vector3(0.95f, 0f, 0.7f), new Vector3(0.95f, 0.45f, 0.7f), 0.25f, 0.24f, 7, Bark * 1.1f);
            model.Prism(new Vector3(0.95f, 0.45f, 0.7f), new Vector3(0.95f, 0.95f, 0.95f), 0.025f, 0.022f, 5, DarkTimber);
            model.Box(new Vector3(0.95f, 0.47f, 0.66f), new Vector3(0.03f, 0.12f, 0.16f), Quaternion.identity, Iron);
            return model.Make("Woodpile");
        }

        /// <summary>A stump to sit on, 0.45 m tall.</summary>
        public static Mesh Stump()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.45f, 0f), 0.24f, 0.21f, 7, Bark);
            model.Prism(new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.46f, 0f), 0.2f, 0.2f, 7, Plank * 1.15f);
            return model.Make("Stump");
        }

        /// <summary>A low-poly tree (GL405): a tapered trunk and a crown of a few faceted lumps, sized by the seed.</summary>
        public static Mesh Tree(int seed)
        {
            var model = new LowPolyMeshBuilder();
            var random = new System.Random(seed);
            float height = 5f + (float)random.NextDouble() * 2.5f;
            model.Prism(new Vector3(0f, -0.2f, 0f), new Vector3(0f, height * 0.65f, 0f), 0.24f, 0.13f, 6, Bark);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad + (float)random.NextDouble();
                float r = i == 0 ? 0f : 0.9f + (float)random.NextDouble() * 0.5f;
                var centre = new Vector3(Mathf.Cos(a) * r, height * 0.72f + (float)random.NextDouble() * 0.9f, Mathf.Sin(a) * r);
                float size = 1.2f + (float)random.NextDouble() * 0.6f;
                model.Diamond(centre, new Vector3(size, size * 0.9f, size), Leaf * (0.85f + (float)random.NextDouble() * 0.3f));
            }
            return model.Make("Town tree");
        }

        /// <summary>A plank bench, 0.45 m high, along z.</summary>
        public static Mesh Bench()
        {
            var model = new LowPolyMeshBuilder();
            model.Block(new Vector3(0f, 0.4f, 0f), new Vector3(0.4f, 0.07f, 1.6f), Plank);
            foreach (float z in new[] { -0.65f, 0.65f })
            {
                model.Block(new Vector3(0f, 0f, z), new Vector3(0.34f, 0.4f, 0.08f), Timber);
            }
            return model.Make("Bench");
        }

        /// <summary>A plank table of a size (x, z), 0.8 m high unless given.</summary>
        public static Mesh Table(Vector2 size, float height = 0.8f)
        {
            var model = new LowPolyMeshBuilder();
            model.Block(new Vector3(0f, height - 0.06f, 0f), new Vector3(size.x, 0.06f, size.y), Plank);
            foreach (float x in new[] { -size.x * 0.5f + 0.07f, size.x * 0.5f - 0.07f })
            {
                foreach (float z in new[] { -size.y * 0.5f + 0.07f, size.y * 0.5f - 0.07f })
                {
                    model.Block(new Vector3(x, 0f, z), new Vector3(0.07f, height - 0.06f, 0.07f), Timber);
                }
            }
            return model.Make("Table");
        }

        /// <summary>The prospectors' display (GL405): a long table along z with ore samples in rows and an open book at -z.</summary>
        public static Mesh DisplayTable(Color[] ores, int seed)
        {
            var model = new LowPolyMeshBuilder();
            var random = new System.Random(seed);
            var size = new Vector2(0.8f, 2.2f);
            const float Height = 0.85f;
            model.Block(new Vector3(0f, Height - 0.06f, 0f), new Vector3(size.x, 0.06f, size.y), Plank);
            model.Block(new Vector3(0f, Height, 0f), new Vector3(size.x, 0.02f, size.y - 0.2f), Cloth);
            foreach (float x in new[] { -size.x * 0.5f + 0.07f, size.x * 0.5f - 0.07f })
            {
                foreach (float z in new[] { -size.y * 0.5f + 0.07f, size.y * 0.5f - 0.07f })
                {
                    model.Block(new Vector3(x, 0f, z), new Vector3(0.07f, Height - 0.06f, 0.07f), Timber);
                }
            }
            for (int i = 0; i < ores.Length * 2; i++)
            {
                float z = -0.35f + (i / 2) * 0.32f;
                float x = i % 2 == 0 ? -0.18f : 0.16f;
                float s = 0.05f + (float)random.NextDouble() * 0.04f;
                Color c = Color.Lerp(Stone, ores[i % ores.Length], 0.65f);
                model.Diamond(new Vector3(x, Height + 0.02f + s, z), new Vector3(s * 1.3f, s, s * 1.1f), c);
            }
            // The book lying open.
            model.Box(new Vector3(-0.1f, Height + 0.04f, -0.8f), new Vector3(0.22f, 0.03f, 0.3f), Quaternion.Euler(0f, 0f, -6f), Paper);
            model.Box(new Vector3(0.12f, Height + 0.04f, -0.8f), new Vector3(0.22f, 0.03f, 0.3f), Quaternion.Euler(0f, 0f, 6f), Paper * 0.95f);
            model.Box(new Vector3(0.01f, Height + 0.02f, -0.8f), new Vector3(0.48f, 0.02f, 0.33f), Quaternion.identity, new Color(0.4f, 0.15f, 0.12f));
            return model.Make("Display table");
        }
    }
}
