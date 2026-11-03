using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A torch's flame (GL314, replacing GL304's cone): a cluster of small faceted tongues, red and orange outside, yellow
    /// in, a white core, each rising from the torch head, swelling and shrinking as it climbs, swaying and turning on its
    /// own beat, so together they dance like fire while staying flat-shaded and low-poly. Drawn in a material that glows
    /// in its vertex colours (<c>Clube/Held Item</c>: vertex alpha 0 glows). Shared by the torch in the hand and on the
    /// ground; <see cref="Animate"/> moves it each frame.
    /// </summary>
    public sealed class FlameCluster
    {
        private struct Tongue
        {
            public Transform Transform;
            public Vector3 Base;
            public float Size;
            public float Rise;
            public float Rate;
            public float Phase;
            public float Spin;
        }

        // From the outside in: colour, how many, ring radius, size, rise.
        private static readonly (Color color, int count, float radius, float size, float rise)[] Layers =
        {
            (new Color(0.86f, 0.18f, 0.08f, 0f), 4, 0.42f, 0.55f, 0.9f),
            (new Color(1f, 0.45f, 0.1f, 0f), 4, 0.3f, 0.62f, 1.1f),
            (new Color(1f, 0.78f, 0.22f, 0f), 3, 0.17f, 0.55f, 1.25f),
            (new Color(1f, 0.97f, 0.82f, 0f), 2, 0.05f, 0.42f, 0.8f),
        };

        private static readonly Dictionary<Color, Mesh> Meshes = new Dictionary<Color, Mesh>();

        private readonly GameObject holder;
        private readonly List<Tongue> tongues = new List<Tongue>();

        /// <param name="parent">What the flame stands on.</param>
        /// <param name="at">The foot of the flame, in the parent's space.</param>
        /// <param name="scale">The flame's height in metres (a placed torch's is about 0.3).</param>
        public FlameCluster(Transform parent, Vector3 at, float scale, Material material)
        {
            holder = new GameObject("Flame");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            holder.transform.localScale = Vector3.one * scale;
            var random = new System.Random(parent.GetInstanceID());
            foreach ((Color color, int count, float radius, float size, float rise) in Layers)
            {
                Mesh mesh = MeshFor(color);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.6f) * Mathf.PI * 2f / count;
                    var go = new GameObject("Tongue");
                    go.transform.SetParent(holder.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    tongues.Add(new Tongue
                    {
                        Transform = go.transform,
                        Base = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius) * 0.35f,
                        Size = size * (0.8f + 0.4f * (float)random.NextDouble()),
                        Rise = rise * 0.5f,
                        Rate = 1.6f + 1.4f * (float)random.NextDouble(),
                        Phase = (float)random.NextDouble(),
                        Spin = (float)random.NextDouble() * 360f,
                    });
                }
            }
            Animate(1f);
        }

        public bool Shown
        {
            get => holder != null && holder.activeSelf;
            set
            {
                if (holder != null && holder.activeSelf != value)
                {
                    holder.SetActive(value);
                }
            }
        }

        /// <summary>Moves the tongues; <paramref name="strength"/> (0-1) shrinks the flame as the torch burns low.</summary>
        public void Animate(float strength)
        {
            if (holder == null)
            {
                return;
            }
            float t = Time.time;
            float size = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength));
            foreach (Tongue tongue in tongues)
            {
                // Each tongue climbs from the head and fades out at the top of its climb, then starts again: swelling
                // quickly, thinning as it goes, leaning a little with a slow sway.
                float p = Mathf.Repeat(t * tongue.Rate + tongue.Phase, 1f);
                float swell = Mathf.Sin(Mathf.Sqrt(p) * Mathf.PI);
                float sway = Mathf.PerlinNoise(t * 1.3f, tongue.Phase * 17f) - 0.5f;
                float lean = Mathf.PerlinNoise(tongue.Phase * 9f, t * 1.1f) - 0.5f;
                Vector3 drift = new Vector3(sway, 0f, lean) * 0.18f * p;
                tongue.Transform.localPosition = (tongue.Base * (1f - 0.6f * p) + Vector3.up * (tongue.Rise * p) + drift) * size;
                float s = tongue.Size * size * Mathf.Max(0.05f, swell);
                tongue.Transform.localScale = new Vector3(s * (1f - 0.3f * p), s * (1f + 0.4f * p), s * (1f - 0.3f * p));
                tongue.Transform.localRotation = Quaternion.Euler(sway * 25f, tongue.Spin + t * 60f * tongue.Rate, lean * 25f);
            }
        }

        public void Destroy()
        {
            if (holder != null)
            {
                Object.Destroy(holder);
            }
        }

        // One tongue: a five-sided bipyramid, short below and pointed above, a metre tall (the cluster scales it), every
        // face its own vertices so it shades flat.
        private static Mesh MeshFor(Color color)
        {
            if (Meshes.TryGetValue(color, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            const int sides = 5;
            var top = new Vector3(0f, 0.75f, 0f);
            var bottom = new Vector3(0f, -0.2f, 0f);
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.22f;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.22f;
                // Faces alternate a touch lighter and darker, so the facets read even when glowing.
                Color face = Color.Lerp(color, Color.white, i % 2 == 0 ? 0.12f : 0f);
                face.a = color.a;
                foreach (Vector3[] tri in new[] { new[] { p0, top, p1 }, new[] { p1, bottom, p0 } })
                {
                    foreach (Vector3 p in tri)
                    {
                        triangles.Add(vertices.Count);
                        vertices.Add(p);
                        colors.Add(face);
                    }
                }
            }
            mesh = new Mesh { name = "Flame tongue" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Meshes[color] = mesh;
            return mesh;
        }
    }
}
