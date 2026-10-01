using UnityEngine;

namespace Clube.Core
{
    /// <summary>Which closing surface a volume tetrahedron was built from.</summary>
    public enum TetrahedronSource
    {
        /// <summary>A Marching Cubes surface triangle.</summary>
        SurfaceTriangle,

        /// <summary>Part of a cube face lying inside the solid.</summary>
        CubeFace,
    }

    /// <summary>
    /// One tetrahedron of the exact volume decomposition (V12): an apex (a solid
    /// corner of its piece) plus one triangle of the closed solid's boundary.
    /// </summary>
    public readonly struct Tetrahedron
    {
        public Tetrahedron(Vector3 apex, Vector3 a, Vector3 b, Vector3 c, TetrahedronSource source)
        {
            Apex = apex;
            A = a;
            B = b;
            C = c;
            Source = source;
            SignedVolume = Vector3.Dot(a - apex, Vector3.Cross(b - apex, c - apex)) / 6f;
        }

        public Vector3 Apex { get; }

        public Vector3 A { get; }

        public Vector3 B { get; }

        public Vector3 C { get; }

        public TetrahedronSource Source { get; }

        /// <summary>
        /// Positive when the base triangle faces away from the apex, negative when it
        /// faces towards it (the tetrahedron then lies outside the solid). The volume
        /// decomposition picks apexes that avoid negative tetrahedra.
        /// </summary>
        public float SignedVolume { get; }

        public Vector3 Centroid => (Apex + A + B + C) * 0.25f;
    }
}
