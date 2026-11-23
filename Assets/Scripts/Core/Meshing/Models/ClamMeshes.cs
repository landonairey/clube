using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly clam (GL808, in place of GL735's squashed sphere): two domed, ribbed half shells with growth bands on
    /// the outside and pale nacre inside, hinged at the origin and reaching towards +z. Each half is its own mesh so the
    /// top one can swing open about the hinge (the x axis), and the pearl that sits inside is a third.
    /// </summary>
    public static class ClamMeshes
    {
        /// <summary>Across the shell (x), along it from the hinge (z), and the height of each half's dome, in metres.</summary>
        public const float Width = 0.28f, Length = 0.24f, Dome = 0.07f;

        private const int Sides = 14, Rings = 3, Ribs = 9;

        private static readonly Color Nacre = new Color(0.9f, 0.87f, 0.9f);

        /// <summary>Where the pearl rests on the lower half, in the clam's frame.</summary>
        public static Vector3 PearlSpot => new Vector3(0f, 0.012f, Length * 0.52f);

        /// <summary>The pearl's radius.</summary>
        public const float PearlRadius = 0.026f;

        /// <summary>One half shell: the top one domes up, the bottom one down; both meet on y = 0 round the rim.</summary>
        public static Mesh BuildShell(bool top, Color outside)
        {
            var model = new LowPolyMeshBuilder();
            float sign = top ? 1f : -1f;
            var centre = new Vector3(0f, 0f, Length * 0.5f);
            // Outer faces face away from a point just inside the shell; inner faces away from a point far outside it.
            Vector3 inner = centre + Vector3.up * (sign * Dome * 0.3f);
            Vector3 outer = centre + Vector3.up * (sign * Dome * 6f);
            for (int ring = 0; ring < Rings; ring++)
            {
                // Growth bands: every other ring a shade darker.
                Color band = ring % 2 == 0 ? outside : Shade(outside, 0.86f);
                for (int s = 0; s < Sides; s++)
                {
                    Vector3 a = Point(s, ring, sign, 1f), b = Point(s + 1, ring, sign, 1f);
                    Vector3 c = Point(s + 1, ring + 1, sign, 1f), d = Point(s, ring + 1, sign, 1f);
                    Color rib = s % 2 == 0 ? band : Shade(band, 0.93f);
                    model.Quad(a, b, c, d, inner, rib);
                    Vector3 ia = Point(s, ring, sign, 0.45f), ib = Point(s + 1, ring, sign, 0.45f);
                    Vector3 ic = Point(s + 1, ring + 1, sign, 0.45f), id = Point(s, ring + 1, sign, 0.45f);
                    model.Quad(ia, ib, ic, id, outer, Shade(Nacre, 0.95f + 0.05f * (ring % 2)));
                }
            }
            return model.Make(top ? "Clam top" : "Clam bottom");
        }

        /// <summary>The pearl inside an open clam, centred on its own origin.</summary>
        public static Mesh BuildPearl(Color tint)
        {
            var model = new LowPolyMeshBuilder();
            ItemChunkMeshes.Pearl(model, Vector3.zero, PearlRadius, tint);
            return model.Make("Clam pearl");
        }

        // A point of the shell: side s round the rim (the hinge at s = 0), ring 0 at the middle of the dome to ring Rings at
        // the rim. The rim is an oval through the hinge, scalloped by the ribs; the dome's height falls to 0 at the rim.
        private static Vector3 Point(int side, int ring, float sign, float height)
        {
            float around = Mathf.PI * 2f * side / Sides;
            float t = (float)ring / Rings;
            float scallop = 1f + 0.035f * Mathf.Cos(around * Ribs) * t;
            // The rim is drawn through the hinge: it stays a point there however the ribs go.
            float hinge = Mathf.Abs(Mathf.Sin(around * 0.5f));
            scallop = Mathf.Lerp(1f, scallop, hinge);
            float x = Mathf.Sin(around) * Width * 0.5f * t * scallop;
            float z = Length * 0.5f - Mathf.Cos(around) * Length * 0.5f * t * scallop;
            // A dome highest a little towards the hinge, as a real shell's umbo.
            float y = Dome * height * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * (1f + 0.15f * Mathf.Cos(around) * t);
            return new Vector3(x, sign * y, z);
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(Mathf.Clamp01(color.r * by), Mathf.Clamp01(color.g * by), Mathf.Clamp01(color.b * by), color.a);
        }
    }
}
