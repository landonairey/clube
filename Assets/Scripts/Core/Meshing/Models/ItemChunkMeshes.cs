using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly models inventory icons are pictures of (GL612), for items with no model of their own: "a small low
    /// poly chunk of the stuff". A faceted lump like the loose stones on the ground for stone and ores, a mound for
    /// soil, and a shape apiece for the made things (log, boards, ingot, bun, brick, hide, tuft, stick). Each is about
    /// one unit across, standing on y = 0, coloured by vertex in the tint given; a material's drop is built white and
    /// wears the material's texture instead. Tools and placeables are drawn from the game's own models.
    /// </summary>
    public static class ItemChunkMeshes
    {
        private static readonly Vector2 Wood = new Vector2(LowPolyMeshBuilder.WoodSurface, 0f);
        private static readonly Color Sapwood = new Color(0.9f, 0.8f, 0.6f);

        /// <summary>True when <paramref name="shape"/> is one of the shapes built here.</summary>
        public static bool Has(ItemIconShape shape)
        {
            return shape != ItemIconShape.Auto && shape != ItemIconShape.Model && shape != ItemIconShape.Painted;
        }

        /// <summary>The model of a shape in a tint; null for the shapes not built here (<see cref="Has"/>).</summary>
        public static Mesh Build(ItemIconShape shape, Color tint)
        {
            var model = new LowPolyMeshBuilder();
            switch (shape)
            {
                case ItemIconShape.Chunk:
                    Lump(model, new Vector3(0f, 0.36f, 0f), new Vector3(0.5f, 0.4f, 0.44f), 0f, tint, 1);
                    break;
                case ItemIconShape.Mound:
                    Mound(model, tint);
                    break;
                case ItemIconShape.Gravel:
                    Gravel(model, tint);
                    break;
                case ItemIconShape.Log:
                    Log(model, tint);
                    break;
                case ItemIconShape.Leaves:
                    Leaves(model, tint);
                    break;
                case ItemIconShape.Boards:
                    Boards(model, tint);
                    break;
                case ItemIconShape.Ingot:
                    Ingot(model, tint);
                    break;
                case ItemIconShape.Bun:
                    Bun(model, tint);
                    break;
                case ItemIconShape.Brick:
                    Brick(model, tint);
                    break;
                case ItemIconShape.Hide:
                    Hide(model, tint);
                    break;
                case ItemIconShape.Tuft:
                    Tuft(model, tint);
                    break;
                case ItemIconShape.Stick:
                    Stick(model, tint);
                    break;
                default:
                    return null;
            }
            return model.Make("Item " + shape);
        }

        // The corners of an icosahedron, and its twenty faces: the fewest facets that still read as a rounded stone.
        private const float Phi = 1.618034f;

        private static readonly Vector3[] Corners =
        {
            new Vector3(-1f, Phi, 0f), new Vector3(1f, Phi, 0f), new Vector3(-1f, -Phi, 0f), new Vector3(1f, -Phi, 0f),
            new Vector3(0f, -1f, Phi), new Vector3(0f, 1f, Phi), new Vector3(0f, -1f, -Phi), new Vector3(0f, 1f, -Phi),
            new Vector3(Phi, 0f, -1f), new Vector3(Phi, 0f, 1f), new Vector3(-Phi, 0f, -1f), new Vector3(-Phi, 0f, 1f),
        };

        private static readonly int[] Faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>
        /// A faceted stone: an icosahedron with every corner pushed in or out by <paramref name="seed"/>, its underside
        /// cut flat where it sits, each facet a shade apart so the shape shows without a light.
        /// </summary>
        private static void Lump(LowPolyMeshBuilder model, Vector3 centre, Vector3 radii, float turn, Color color, int seed)
        {
            Quaternion spin = Quaternion.Euler(seed * 37f % 23f, turn, seed * 53f % 19f);
            var points = new Vector3[Corners.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = spin * (Corners[i].normalized * (0.8f + 0.36f * Hash(seed, i)));
                p.y = Mathf.Max(p.y, -0.62f);
                points[i] = centre + Vector3.Scale(p, radii);
            }
            for (int f = 0; f < Faces.Length; f += 3)
            {
                model.Triangle(points[Faces[f]], points[Faces[f + 1]], points[Faces[f + 2]], centre,
                    Shade(color, 0.93f + 0.14f * Hash(seed + 7, f)));
            }
        }

        // A low heap, wider than tall and a little lopsided.
        private static void Mound(LowPolyMeshBuilder model, Color color)
        {
            model.Loft(
                new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0.01f, 0.1f, 0.01f), new Vector3(0.04f, 0.25f, -0.01f),
                    new Vector3(0.06f, 0.36f, 0.01f), new Vector3(0.07f, 0.41f, 0.01f),
                },
                new[]
                {
                    new Vector2(0.5f, 0.46f), new Vector2(0.43f, 0.4f), new Vector2(0.27f, 0.25f),
                    new Vector2(0.12f, 0.11f), new Vector2(0.02f, 0.02f),
                }, 7, color);
            // A couple of clods fallen off the side.
            Lump(model, new Vector3(-0.4f, 0.05f, 0.34f), new Vector3(0.1f, 0.08f, 0.09f), 20f, Shade(color, 0.94f), 3);
            Lump(model, new Vector3(0.44f, 0.04f, 0.3f), new Vector3(0.07f, 0.06f, 0.07f), 70f, Shade(color, 1.04f), 4);
        }

        // Crushed rock: a handful of small stones, the biggest behind.
        private static void Gravel(LowPolyMeshBuilder model, Color color)
        {
            Lump(model, new Vector3(-0.08f, 0.2f, -0.14f), new Vector3(0.3f, 0.24f, 0.27f), 0f, color, 2);
            Lump(model, new Vector3(0.33f, 0.14f, 0.02f), new Vector3(0.2f, 0.17f, 0.19f), 50f, Shade(color, 0.95f), 5);
            Lump(model, new Vector3(-0.36f, 0.11f, 0.16f), new Vector3(0.16f, 0.13f, 0.15f), 110f, Shade(color, 1.05f), 6);
            Lump(model, new Vector3(0.04f, 0.1f, 0.3f), new Vector3(0.15f, 0.12f, 0.14f), 170f, color, 8);
            Lump(model, new Vector3(0.34f, 0.07f, 0.34f), new Vector3(0.1f, 0.08f, 0.09f), 230f, Shade(color, 0.92f), 9);
            Lump(model, new Vector3(-0.2f, 0.06f, 0.42f), new Vector3(0.08f, 0.07f, 0.08f), 290f, Shade(color, 1.03f), 10);
        }

        // A length of trunk standing on its end (the grain runs along Y): bark round it, pale wood and a heart on the cuts.
        private static void Log(LowPolyMeshBuilder model, Color bark)
        {
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.9f, 0f), 0.3f, 0.28f, 8, bark);
            // A stub where a branch was.
            model.Prism(new Vector3(0.22f, 0.5f, 0.08f), new Vector3(0.4f, 0.62f, 0.14f), 0.07f, 0.06f, 5, Shade(bark, 0.9f));
            Color wood = Color.Lerp(bark, Sapwood, 0.8f);
            model.Surface = Wood;
            foreach (float end in new[] { 0.9f, 0f })
            {
                float outward = end > 0f ? 1f : -1f;
                model.Prism(new Vector3(0f, end, 0f), new Vector3(0f, end + 0.012f * outward, 0f), 0.25f, 0.24f, 8, wood);
                model.Prism(new Vector3(0.02f, end + 0.012f * outward, 0.01f), new Vector3(0.02f, end + 0.02f * outward, 0.01f), 0.13f, 0.12f, 7, Shade(wood, 0.82f));
                model.Prism(new Vector3(0.03f, end + 0.02f * outward, 0.01f), new Vector3(0.03f, end + 0.026f * outward, 0.01f), 0.045f, 0.04f, 6, Shade(wood, 0.62f));
            }
            model.Surface = Vector2.zero;
        }

        // A sprig: a twig with leaves off both sides and one at its tip.
        private static void Leaves(LowPolyMeshBuilder model, Color color)
        {
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.75f, 0f), 0.022f, 0.012f, 4, Shade(color, 0.45f));
            float[] heights = { 0.2f, 0.34f, 0.48f, 0.6f, 0.76f };
            for (int i = 0; i < heights.Length; i++)
            {
                bool tip = i == heights.Length - 1;
                float side = tip ? 0f : i % 2 == 0 ? 1f : -1f;
                float lean = side * (58f - i * 5f);
                var turn = Quaternion.Euler(tip ? 0f : 14f * side, 0f, -lean);
                float size = tip ? 0.2f : 0.25f - i * 0.02f;
                var root = new Vector3(0.03f * heights[i], heights[i], 0f);
                model.Placement = Matrix4x4.TRS(root, turn, Vector3.one);
                model.Diamond(new Vector3(0f, size, 0f), new Vector3(size * 0.48f, size, 0.02f), Shade(color, 0.9f + 0.07f * i));
                model.Placement = Matrix4x4.identity;
            }
        }

        // Three sawn planks standing together (the grain runs along Y), one slipped out of line.
        private static void Boards(LowPolyMeshBuilder model, Color wood)
        {
            model.Surface = Wood;
            model.Box(new Vector3(0f, 0.5f, -0.1f), new Vector3(0.34f, 1f, 0.09f), Quaternion.identity, Shade(wood, 0.92f));
            model.Box(new Vector3(0.02f, 0.53f, 0f), new Vector3(0.34f, 1f, 0.09f), Quaternion.Euler(0f, 0f, 3f), wood);
            model.Box(new Vector3(-0.03f, 0.47f, 0.1f), new Vector3(0.34f, 0.94f, 0.09f), Quaternion.Euler(0f, 0f, -5f), Shade(wood, 1.08f));
            model.Surface = Vector2.zero;
        }

        // A cast bar: wider at the foot than the top, as it came out of the mould.
        private static void Ingot(LowPolyMeshBuilder model, Color metal)
        {
            model.Surface = new Vector2(LowPolyMeshBuilder.MetalSurface, 0.6f);
            Slab(model, new Vector2(0.5f, 0.24f), new Vector2(0.4f, 0.15f), 0.2f, metal);
            model.Surface = Vector2.zero;
        }

        // A rough dome of metal, as a smelt leaves it.
        private static void Bun(LowPolyMeshBuilder model, Color metal)
        {
            model.Surface = new Vector2(LowPolyMeshBuilder.MetalSurface, 0.35f);
            model.Loft(
                new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0.1f, 0f), new Vector3(0.01f, 0.22f, 0.01f),
                    new Vector3(0.02f, 0.3f, 0.01f), new Vector3(0.03f, 0.31f, 0.01f),
                },
                new[]
                {
                    new Vector2(0.5f, 0.48f), new Vector2(0.47f, 0.45f), new Vector2(0.36f, 0.34f),
                    new Vector2(0.18f, 0.17f), new Vector2(0.03f, 0.03f),
                }, 8, metal);
            model.Surface = Vector2.zero;
        }

        // A fired brick.
        private static void Brick(LowPolyMeshBuilder model, Color clay)
        {
            Slab(model, new Vector2(0.5f, 0.24f), new Vector2(0.485f, 0.225f), 0.3f, clay);
        }

        // A hide laid flat: a body with a neck, four legs and a tail, seen from above.
        private static readonly Vector2[] HideOutline =
        {
            new Vector2(-0.12f, 0.5f), new Vector2(0.12f, 0.5f), new Vector2(0.2f, 0.34f), new Vector2(0.48f, 0.42f),
            new Vector2(0.5f, 0.27f), new Vector2(0.27f, 0.16f), new Vector2(0.25f, -0.2f), new Vector2(0.5f, -0.36f),
            new Vector2(0.43f, -0.5f), new Vector2(0.16f, -0.4f), new Vector2(0f, -0.53f), new Vector2(-0.16f, -0.4f),
            new Vector2(-0.43f, -0.5f), new Vector2(-0.5f, -0.36f), new Vector2(-0.25f, -0.2f), new Vector2(-0.27f, 0.16f),
            new Vector2(-0.5f, 0.27f), new Vector2(-0.48f, 0.42f), new Vector2(-0.2f, 0.34f),
        };

        private static void Hide(LowPolyMeshBuilder model, Color hide)
        {
            var below = new Vector3(0f, -1f, 0f);
            var middle = new Vector3(0f, 0.05f, 0f);
            for (int i = 0; i < HideOutline.Length; i++)
            {
                Vector2 a = HideOutline[i], b = HideOutline[(i + 1) % HideOutline.Length];
                // Darker along the spine, paler out at the flanks and legs.
                float shade = 0.84f + 0.2f * Mathf.Min(1f, Mathf.Abs(a.x + b.x));
                model.Triangle(middle, new Vector3(a.x, 0.01f, a.y), new Vector3(b.x, 0.01f, b.y), below, Shade(hide, shade));
            }
        }

        // A soft tuft: a few round locks pressed together.
        private static void Tuft(LowPolyMeshBuilder model, Color wool)
        {
            // A fleece is many small curls, not a few lumps (which read as stones): a ring of them round the foot, a
            // smaller ring above and one on top, every other curl a shade darker so each shows.
            int curl = 0;
            for (int ring = 0; ring < 3; ring++)
            {
                int count = ring == 0 ? 7 : ring == 1 ? 5 : 1;
                float out_ = ring == 0 ? 0.33f : ring == 1 ? 0.19f : 0f;
                float height = 0.14f + ring * 0.17f;
                for (int i = 0; i < count; i++, curl++)
                {
                    float around = Mathf.PI * 2f * (i + ring * 0.5f) / count;
                    float size = 0.17f - ring * 0.012f + 0.03f * Hash(11, curl);
                    var centre = new Vector3(Mathf.Cos(around) * out_, height + 0.03f * Hash(12, curl), Mathf.Sin(around) * out_ * 0.9f);
                    model.Blob(centre, new Vector3(size, size * 0.9f, size), Quaternion.Euler(90f * Hash(13, curl), around * Mathf.Rad2Deg, 0f),
                        Shade(wool, curl % 2 == 0 ? 1.07f : 0.84f), 7, 4);
                }
            }
        }

        // A stick with a kink in it and a twig stub.
        private static void Stick(LowPolyMeshBuilder model, Color wood)
        {
            model.Surface = Wood;
            model.Prism(new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.55f, 0f), 0.04f, 0.035f, 5, wood);
            model.Prism(new Vector3(0.03f, 0.55f, 0f), new Vector3(-0.02f, 1f, 0.01f), 0.035f, 0.025f, 5, Shade(wood, 1.06f));
            model.Prism(new Vector3(0.03f, 0.55f, 0f), new Vector3(0.14f, 0.68f, 0.02f), 0.02f, 0.012f, 4, Shade(wood, 0.9f));
            model.Surface = Vector2.zero;
        }

        // A block standing on y = 0: its foot and top given as half-sizes across x and z.
        private static void Slab(LowPolyMeshBuilder model, Vector2 foot, Vector2 top, float height, Color color)
        {
            var middle = new Vector3(0f, height * 0.5f, 0f);
            Vector3 F(float x, float z) => new Vector3(foot.x * x, 0f, foot.y * z);
            Vector3 T(float x, float z) => new Vector3(top.x * x, height, top.y * z);
            model.Quad(T(-1, -1), T(1, -1), T(1, 1), T(-1, 1), middle, Shade(color, 1.08f));
            model.Quad(F(-1, -1), F(1, -1), F(1, 1), F(-1, 1), middle, Shade(color, 0.8f));
            model.Quad(F(-1, 1), F(1, 1), T(1, 1), T(-1, 1), middle, color);
            model.Quad(F(-1, -1), F(1, -1), T(1, -1), T(-1, -1), middle, Shade(color, 0.9f));
            model.Quad(F(1, -1), F(1, 1), T(1, 1), T(1, -1), middle, Shade(color, 0.95f));
            model.Quad(F(-1, -1), F(-1, 1), T(-1, 1), T(-1, -1), middle, Shade(color, 0.88f));
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(Mathf.Clamp01(color.r * by), Mathf.Clamp01(color.g * by), Mathf.Clamp01(color.b * by), color.a);
        }

        // 0-1, the same every time for the same pair.
        private static float Hash(int seed, int index)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)(index * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }
    }
}
