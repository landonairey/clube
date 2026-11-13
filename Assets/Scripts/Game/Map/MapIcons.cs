using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The world map's settlement icons (GL599) as data: each icon id (<see cref="Clube.Core.SettlementType.Icon"/>)
    /// is a list of triangles in a square from (-1, -1) to (1, 1), y up. A new icon is a new entry; an id with none
    /// draws as a diamond.
    /// </summary>
    public static class MapIcons
    {
        private static readonly Dictionary<string, Vector2[]> Shapes = new Dictionary<string, Vector2[]>
        {
            ["tent"] = Join(Triangle(-1f, -0.8f, 1f, -0.8f, 0f, 0.9f)),
            ["house"] = Join(Box(-0.7f, -0.8f, 0.7f, 0.1f), Triangle(-1f, 0.1f, 1f, 0.1f, 0f, 0.9f)),
            ["houses"] = Join(
                Box(-1f, -0.8f, -0.1f, -0.1f), Triangle(-1.1f, -0.1f, 0f, -0.1f, -0.55f, 0.5f),
                Box(0.1f, -0.8f, 1f, 0.2f), Triangle(0f, 0.2f, 1.1f, 0.2f, 0.55f, 0.9f)),
            ["town-hall"] = Join(
                Box(-1f, -0.8f, 1f, 0f), Triangle(-1.1f, 0f, 1.1f, 0f, 0f, 0.5f),
                Box(-0.18f, 0.2f, 0.18f, 0.8f), Triangle(-0.3f, 0.8f, 0.3f, 0.8f, 0f, 1.1f)),
            ["tower"] = Join(
                Box(-0.45f, -0.9f, 0.45f, 0.5f), Box(-0.7f, 0.5f, 0.7f, 0.75f),
                Box(-0.7f, 0.75f, -0.4f, 1f), Box(-0.15f, 0.75f, 0.15f, 1f), Box(0.4f, 0.75f, 0.7f, 1f)),
            ["castle"] = Join(
                Box(-0.6f, -0.8f, 0.6f, 0.2f), Box(-1.1f, -0.8f, -0.6f, 0.7f), Box(0.6f, -0.8f, 1.1f, 0.7f),
                Box(-1.1f, 0.7f, -0.95f, 0.95f), Box(-0.75f, 0.7f, -0.6f, 0.95f), Box(0.6f, 0.7f, 0.75f, 0.95f), Box(0.95f, 0.7f, 1.1f, 0.95f),
                Box(-0.2f, 0.2f, 0.2f, 0.75f), Triangle(-0.3f, 0.75f, 0.3f, 0.75f, 0f, 1.15f)),
        };

        private static readonly Vector2[] Diamond = Join(Triangle(-0.8f, 0f, 0f, -0.8f, 0.8f, 0f), Triangle(-0.8f, 0f, 0.8f, 0f, 0f, 0.8f));

        /// <summary>True when the id has a drawn icon of its own.</summary>
        public static bool Has(string icon)
        {
            return icon != null && Shapes.ContainsKey(icon);
        }

        /// <summary>The icon's triangles, three points each; the diamond for an unknown id.</summary>
        public static Vector2[] Triangles(string icon)
        {
            return icon != null && Shapes.TryGetValue(icon, out Vector2[] shape) ? shape : Diamond;
        }

        private static Vector2[] Triangle(float ax, float ay, float bx, float by, float cx, float cy)
        {
            return new[] { new Vector2(ax, ay), new Vector2(bx, by), new Vector2(cx, cy) };
        }

        private static Vector2[] Box(float left, float bottom, float right, float top)
        {
            return new[]
            {
                new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top),
                new Vector2(left, bottom), new Vector2(right, top), new Vector2(left, top),
            };
        }

        private static Vector2[] Join(params Vector2[][] parts)
        {
            var all = new List<Vector2>();
            foreach (Vector2[] part in parts)
            {
                all.AddRange(part);
            }
            return all.ToArray();
        }
    }
}
