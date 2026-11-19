using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A see-through wall standing on a line of points (GL783: the family parcel's hologram walls): one quad per step of the
    /// line, rising <c>height</c> metres from each point, in the given colour at the bottom and fading to nothing at the
    /// top. Each quad is wound both ways, so a back-face culled material shows it from either side.
    /// </summary>
    public static class FadingWall
    {
        /// <summary>
        /// Adds the wall over <paramref name="bottom"/> (the points it stands on, in order; a closed loop repeats its first
        /// point at the end) to the lists: four vertices and twelve indices per step.
        /// </summary>
        public static void Add(IReadOnlyList<Vector3> bottom, float height, Color color, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            Color top = color;
            top.a = 0f;
            for (int i = 1; i < bottom.Count; i++)
            {
                Vector3 a = bottom[i - 1];
                Vector3 b = bottom[i];
                int first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(b + Vector3.up * height);
                vertices.Add(a + Vector3.up * height);
                colors.Add(color);
                colors.Add(color);
                colors.Add(top);
                colors.Add(top);
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 1);
                triangles.Add(first);
                triangles.Add(first + 3);
                triangles.Add(first + 2);
            }
        }
    }
}
