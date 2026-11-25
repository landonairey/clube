using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A knapped hand axe (GL875), low-poly: an almond outline with a rounded, thick butt for the hand, ending in a broad, blunt
    /// rounded arc (GL884); both faces covered in shallow scooped flake scars (each a small dished fan of facets) and a wavy
    /// edge where the flakes met. Built in its own frame: the butt at the origin, the working end up +Y, width along X and
    /// thickness along Z; callers place it with <see cref="LowPolyMeshBuilder.Placement"/>. One colour in, every
    /// shade derived from it, so another material (bone, GL877) is the same call with another colour.
    /// </summary>
    public static class HandAxeMesh
    {
        // Heights up the outline, as fractions of the length, from the butt (0) to the point (1); one ring point per
        // height on each side, plus the butt's bottom and the point.
        private static readonly float[] Heights = { 0.05f, 0.14f, 0.27f, 0.42f, 0.57f, 0.72f, 0.86f };

        // Small fixed irregularities (a knapped stone is never true): widths per height, right side then left.
        private static readonly float[] Wobble = { 1.02f, 0.97f, 1.04f, 0.95f, 1.03f, 0.92f, 1.06f, 0.98f, 1.03f, 0.99f, 0.94f, 1.05f, 0.96f, 1.08f };

        // Facet shades, picked round the faces so neighbouring scars differ.
        private static readonly float[] Shades = { 1f, 0.78f, 0.93f, 1.1f, 0.84f, 1.04f, 0.72f, 0.97f, 1.14f, 0.8f, 0.9f, 1.06f };

        /// <summary>Ring points round the outline: the butt's bottom, up the right, the point, down the left.</summary>
        public const int RingCount = 16;

        /// <summary>Adds the hand axe, <paramref name="length"/> from butt to point, in <paramref name="stone"/>.</summary>
        public static void Add(LowPolyMeshBuilder model, float length, Color stone)
        {
            float halfWidth = length * 0.31f, halfThickness = length * 0.14f;
            var edge = new Vector3[RingCount];
            var heights = new float[RingCount];
            for (int i = 0; i < RingCount; i++)
            {
                heights[i] = HeightAt(i);
                float s = heights[i];
                float side = i == 0 || i == RingCount / 2 ? 0f : i < RingCount / 2 ? 1f : -1f;
                float x = side * HalfWidth(s) * halfWidth * Wobble[i % Wobble.Length];
                // A wavy edge where the flakes from the two faces met; the butt's edge is blunt instead.
                float wave = (i % 2 == 0 ? 1f : -1f) * 0.12f * halfThickness * (1f - Blunt(s));
                edge[i] = new Vector3(x + side * 0.01f * length * Mathf.Sin(s * 9f), s * length, wave);
            }
            AddFace(model, edge, heights, length, halfWidth, halfThickness, stone, 1f, 0);
            AddFace(model, edge, heights, length, halfWidth, halfThickness, stone, -1f, 5);
            // The rim: a band round the edge, wide and rounded at the butt (cortex, the pebble's own skin), nothing at
            // the sharp sides where the two faces meet.
            Color cortex = Cortex(stone);
            var inside = new Vector3(0f, length * 0.3f, 0f);
            for (int i = 0; i < RingCount; i++)
            {
                int j = (i + 1) % RingCount;
                Vector3 a = edge[i], b = edge[j];
                float ta = halfThickness * EdgeThickness(heights[i]), tb = halfThickness * EdgeThickness(heights[j]);
                model.Quad(a + Vector3.forward * ta, b + Vector3.forward * tb, b - Vector3.forward * tb, a - Vector3.forward * ta, inside,
                    Shade(cortex, Shades[i % Shades.Length] * 0.95f));
            }
        }

        /// <summary>The butt's pebble skin for a stone colour: a little warmer and paler.</summary>
        public static Color Cortex(Color stone)
        {
            return new Color(Mathf.Min(1f, stone.r * 1.22f + 0.04f), Mathf.Min(1f, stone.g * 1.15f + 0.03f), stone.b * 1.02f, stone.a);
        }

        // One face (front, facing = 1; back, -1): a ring of flake scars along the edge, a ring of larger ones inside,
        // meeting at a ridge down the middle. Each scar is a fan round a dented centre, so it reads as scooped out.
        private static void AddFace(LowPolyMeshBuilder model, Vector3[] edge, float[] heights, float length, float halfWidth, float halfThickness,
            Color stone, float facing, int shadeOffset)
        {
            Color cortex = Cortex(stone);
            var inner = new Vector3[RingCount];
            // The ridge down the middle: a low point over the butt and a high one towards the point, so the scars from
            // the lower and upper edges meet it without long slivers.
            Vector3 low = OnFace(new Vector3(0.02f * facing * length, length * 0.3f, 0f), facing, length, halfWidth, halfThickness);
            Vector3 high = OnFace(new Vector3(-0.012f * facing * length, length * 0.64f, 0f), facing, length, halfWidth, halfThickness);
            var behind = new Vector3(0f, length * 0.4f, -facing * halfThickness * 2f);
            Vector3 RidgeFor(int ring) => heights[ring] <= 0.5f ? low : high;
            for (int i = 0; i < RingCount; i++)
            {
                Vector3 at = Vector3.Lerp(new Vector3(0f, edge[i].y, 0f), edge[i], 0.5f);
                // The ring sits nearer the middle at the butt and the point, so those scars aren't slivers.
                at = Vector3.Lerp(at, RidgeFor(i), i == 0 || i == RingCount / 2 ? 0.45f : 0.12f);
                inner[i] = OnFace(at, facing, length, halfWidth, halfThickness);
            }
            for (int i = 0; i < RingCount; i++)
            {
                int j = (i + 1) % RingCount;
                float s = (heights[i] + heights[j]) * 0.5f;
                Vector3 ea = edge[i] + Vector3.forward * facing * halfThickness * EdgeThickness(heights[i]);
                Vector3 eb = edge[j] + Vector3.forward * facing * halfThickness * EdgeThickness(heights[j]);
                Color shade = Shade(Color.Lerp(stone, cortex, Blunt(s)), Shades[(i + shadeOffset) % Shades.Length]);
                Scar(model, new[] { ea, eb, inner[j], inner[i] }, facing, length, halfWidth, halfThickness, 0.34f * (1f - Blunt(s)), shade, behind);
                Color deeper = Shade(Color.Lerp(stone, cortex, Blunt(s) * 0.6f), Shades[(i + shadeOffset + 4) % Shades.Length]);
                Vector3 ridgeI = RidgeFor(i), ridgeJ = RidgeFor(j);
                Vector3[] inside = ridgeI == ridgeJ ? new[] { inner[i], inner[j], ridgeI } : new[] { inner[i], inner[j], ridgeJ, ridgeI };
                Scar(model, inside, facing, length, halfWidth, halfThickness, 0.26f, deeper, behind);
            }
        }

        // A point moved onto the face's surface (front, facing = 1; back, -1).
        private static Vector3 OnFace(Vector3 at, float facing, float length, float halfWidth, float halfThickness)
        {
            at.z = facing * Surface(at, length, halfWidth, halfThickness);
            return at;
        }

        // A flake scar over a polygon: a fan of triangles round its middle, pushed in by a share of the depth there.
        private static void Scar(LowPolyMeshBuilder model, Vector3[] corners, float facing, float length, float halfWidth, float halfThickness, float dent,
            Color shade, Vector3 behind)
        {
            Vector3 middle = Vector3.zero;
            foreach (Vector3 corner in corners)
            {
                middle += corner;
            }
            middle /= corners.Length;
            float surface = Surface(middle, length, halfWidth, halfThickness);
            middle.z = facing * surface * (1f - dent);
            for (int k = 0; k < corners.Length; k++)
            {
                model.Triangle(corners[k], corners[(k + 1) % corners.Length], middle, behind, Shade(shade, k % 2 == 0 ? 1f : 0.88f));
            }
        }

        private static float HeightAt(int ring)
        {
            int half = RingCount / 2;
            if (ring == 0)
            {
                return 0f;
            }
            if (ring == half)
            {
                return 1f;
            }
            return ring < half ? Heights[ring - 1] : Heights[RingCount - ring - 1];
        }

        /// <summary>The outline's half width at a fraction <paramref name="s"/> up the length, the widest being 1.</summary>
        public static float HalfWidth(float s)
        {
            const float widest = 0.36f;
            if (s <= widest)
            {
                // The butt: a round end.
                float t = (widest - s) / widest;
                return Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            }
            // The working end (GL884): a broad rounded arc, not a spike; still nearly full width a long way up, closing over a blunt tip.
            float u = (s - widest) / (1f - widest);
            return Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, 2.4f)), 0.5f);
        }

        /// <summary>The middle's half thickness at a fraction up the length, the thickest being 1: thick in the hand,
        /// thin at the point.</summary>
        public static float Thickness(float s)
        {
            return s < 0.22f ? Mathf.Lerp(0.82f, 1f, s / 0.22f) : Mathf.Lerp(1f, 0.42f, Mathf.Pow((s - 0.22f) / 0.78f, 1.4f));
        }

        // How blunt (rounded cortex) the edge is at a height: all of it at the butt, none from a third of the way up.
        private static float Blunt(float s)
        {
            return Mathf.Clamp01(1f - s / 0.3f);
        }

        // The rim's half thickness, as a share of the half thickness: the butt's rounded edge, sharp everywhere else.
        private static float EdgeThickness(float s)
        {
            return Blunt(s) * Thickness(s) * 0.62f;
        }

        // The face's height over the middle plane at a point: an ellipse across the width, over the rim's thickness.
        private static float Surface(Vector3 at, float length, float halfWidth, float halfThickness)
        {
            float s = Mathf.Clamp01(at.y / length);
            float width = Mathf.Max(1e-4f, HalfWidth(s) * halfWidth);
            float across = Mathf.Clamp01(Mathf.Abs(at.x) / width);
            float rim = EdgeThickness(s);
            return halfThickness * (rim + (Thickness(s) - rim) * Mathf.Sqrt(1f - across * across));
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(Mathf.Min(1f, color.r * by), Mathf.Min(1f, color.g * by), Mathf.Min(1f, color.b * by), color.a);
        }
    }
}
