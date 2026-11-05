using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Low-poly models of the town (GL400, GL405, GL414), made in code as vertex-coloured meshes for <c>Clube/Low Poly</c>:
    /// the gate's leaves, signs, lanterns and the props the keepers' chores use. The wall, the shops and the shafts are
    /// terrain (<see cref="TownStructures"/>). Each model is in its own frame: origin on the ground at its centre, front facing +z.
    /// </summary>
    public static class TownMeshes
    {
        private static readonly Color Timber = new Color(0.55f, 0.4f, 0.25f);
        private static readonly Color DarkTimber = new Color(0.36f, 0.25f, 0.15f);
        private static readonly Color Plank = new Color(0.62f, 0.47f, 0.3f);
        private static readonly Color Stone = new Color(0.52f, 0.51f, 0.5f);
        private static readonly Color Iron = new Color(0.27f, 0.28f, 0.3f);
        private static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
        private static readonly Color Bark = new Color(0.4f, 0.3f, 0.2f);
        private static readonly Color Leaf = new Color(0.3f, 0.48f, 0.22f);
        private static readonly Color Cloth = new Color(0.55f, 0.35f, 0.3f);
        private static readonly Color Blanket = new Color(0.42f, 0.36f, 0.55f);
        private static readonly Color Paper = new Color(0.86f, 0.8f, 0.66f);

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

        /// <summary>A shop's sign board (GL400): a plank in a frame of the shop's colour, its face towards +z, centred on the origin.</summary>
        public static Mesh SignBoard(Color colour)
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 0.5f, 0.06f), Quaternion.identity, colour * 0.8f);
            model.Box(new Vector3(0f, 0.2f, 0.02f), new Vector3(2.24f, 0.38f, 0.04f), Quaternion.identity, Color.Lerp(Plank, Color.white, 0.3f));
            // Two short chains up to the lintel.
            model.Box(new Vector3(-0.95f, 0.5f, 0f), new Vector3(0.03f, 0.12f, 0.03f), Quaternion.identity, Iron);
            model.Box(new Vector3(0.95f, 0.5f, 0f), new Vector3(0.03f, 0.12f, 0.03f), Quaternion.identity, Iron);
            return model.Make("Sign board");
        }

        /// <summary>A signpost (GL414): a post with a board at head height, its face towards +z.</summary>
        public static Mesh SignPost()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 1.9f, 0f), 0.06f, 0.05f, 6, DarkTimber);
            model.Box(new Vector3(0f, 1.62f, 0f), new Vector3(1.05f, 0.34f, 0.05f), Quaternion.identity, Plank * 1.1f);
            return model.Make("Sign post");
        }

        /// <summary>A lantern (GL414): an iron hood and base round a glowing core, hung from a short chain; origin at the top.</summary>
        public static Mesh Lantern()
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.025f, 0.2f, 0.025f), Quaternion.identity, Iron);
            model.Prism(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.04f, 0f), 0.11f, 0.03f, 6, Iron);
            model.Prism(new Vector3(0f, -0.2f, 0f), new Vector3(0f, -0.02f, 0f), 0.07f, 0.07f, 6, Ember * 1.2f);
            model.Prism(new Vector3(0f, -0.24f, 0f), new Vector3(0f, -0.2f, 0f), 0.1f, 0.1f, 6, Iron);
            return model.Make("Lantern");
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
