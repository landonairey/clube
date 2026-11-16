using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The timbering of a road tunnel (GL642, GL650): at each mouth a frame of wood planks (two doubled posts, a lintel
    /// of boards across them, struts up to a collar under the crown and a plank facing above), and along the bore a
    /// lighter frame of the same shape every few metres. Low-poly meshes in vertex colours; the portal's posts have box
    /// colliders, so it is walked through and not into, and the frames inside stand against the walls out of the way.
    /// </summary>
    public static class TunnelPortal
    {
        /// <summary>Metres between the frames inside a bore.</summary>
        public const float SupportSpacing = 6f;

        private static readonly Color Board = new Color(0.66f, 0.42f, 0.26f);
        private static readonly Color Beam = new Color(0.45f, 0.28f, 0.17f);

        /// <summary>Builds a portal under a parent, at the origin of its own frame: the mouth looks out along +z.</summary>
        /// <param name="width">The passage's width and <paramref name="height"/> its height at the middle, in metres.</param>
        public static GameObject Build(float width, float height, Material material, Transform parent, int seed)
        {
            var root = new GameObject("Tunnel portal");
            root.transform.SetParent(parent, false);
            var random = new System.Random(seed);
            var mesh = new LowPolyMeshBuilder();
            const float Post = 0.3f;
            float half = width * 0.5f;
            float spring = height * 0.67f;
            // Two frames a little apart, tied by the lintel boards, so the mouth has depth.
            Frame(mesh, Vector3.forward * 0.25f, Quaternion.identity, width, height, Post, random);
            Frame(mesh, Vector3.back * 0.35f, Quaternion.identity, width, height, Post, random);
            // The facing: planks standing on the lintel up past the crown, and a board along each side of the mouth.
            int planks = Mathf.Max(3, Mathf.RoundToInt((width + Post * 2f) / 0.34f));
            float plankWidth = (width + Post * 2f) / planks;
            for (int i = 0; i < planks; i++)
            {
                float x = -half - Post + (i + 0.5f) * plankWidth;
                // The planks follow the arch: tall over the posts, short over the crown.
                float under = spring + (height - spring) * Mathf.Clamp01(1f - Mathf.Abs(x) / half);
                float top = height + 0.55f + (float)random.NextDouble() * 0.12f;
                mesh.Box(new Vector3(x, (under + top) * 0.5f + 0.12f, 0.42f), new Vector3(plankWidth - 0.03f, top - under, 0.06f),
                    Quaternion.Euler(0f, 0f, ((float)random.NextDouble() - 0.5f) * 2f), Board * (0.85f + 0.2f * (float)random.NextDouble()));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(root.transform, new Vector3(side * (half + Post * 0.5f), spring * 0.5f, 0f), new Vector3(Post, spring, 0.9f));
            }
            AddBox(root.transform, new Vector3(0f, height + 0.45f, 0f), new Vector3(width + Post * 2f, 0.5f, 0.9f));
            root.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Tunnel portal");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            return root;
        }

        /// <summary>
        /// Builds the frames inside a bore as one mesh (GL650): one every <see cref="SupportSpacing"/> metres between
        /// <paramref name="from"/> and <paramref name="to"/> metres along it, each across the passage. Null when the bore
        /// is too short for any.
        /// </summary>
        /// <param name="at">The floor's middle a distance along the bore, in the parent's frame.</param>
        /// <param name="direction">The way the bore runs (x, z).</param>
        public static GameObject BuildSupports(Func<float, Vector3> at, float from, float to, Vector2 direction, float width, float height,
            Material material, Transform parent, int seed)
        {
            int count = Mathf.FloorToInt((to - from) / SupportSpacing);
            if (count < 1)
            {
                return null;
            }
            var root = new GameObject("Tunnel supports");
            root.transform.SetParent(parent, false);
            Vector3 origin = at(from);
            root.transform.localPosition = origin;
            var random = new System.Random(seed);
            var mesh = new LowPolyMeshBuilder();
            Quaternion facing = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);
            float first = from + (to - from - (count - 1) * SupportSpacing) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Frame(mesh, at(first + i * SupportSpacing) - origin, facing, width, height, 0.22f, random);
            }
            root.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Tunnel supports");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            return root;
        }

        // One frame across the passage, standing on a point of its floor: a post against each wall up to where the arch
        // springs, a strut from each post's head up to a collar beam under the crown.
        private static void Frame(LowPolyMeshBuilder mesh, Vector3 foot, Quaternion facing, float width, float height, float post, System.Random random)
        {
            float F() => (float)random.NextDouble();
            float half = width * 0.5f - post * 0.5f - 0.05f;
            float spring = height * 0.67f;
            float crown = height - post * 0.5f - 0.05f;
            float collarHalf = half * 0.4f;
            Vector3 P(float x, float y) => foot + facing * new Vector3(x, y, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(P(side * half, spring * 0.5f), new Vector3(post, spring, post), facing * Quaternion.Euler(0f, (F() - 0.5f) * 5f, 0f), Beam * (0.9f + 0.2f * F()));
                // The strut, from the post's head to the collar's end.
                Vector2 a = new Vector2(side * half, spring), b = new Vector2(side * collarHalf, crown);
                Vector2 mid = (a + b) * 0.5f;
                float length = Vector2.Distance(a, b) + post * 0.6f;
                float tilt = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                mesh.Box(P(mid.x, mid.y), new Vector3(length, post * 0.85f, post * 0.85f), facing * Quaternion.Euler(0f, 0f, tilt), Board * (0.8f + 0.2f * F()));
            }
            mesh.Box(P(0f, crown), new Vector3(collarHalf * 2f + post, post * 0.9f, post), facing, Beam * (0.9f + 0.2f * F()));
        }

        private static void AddBox(Transform parent, Vector3 centre, Vector3 size)
        {
            var child = new GameObject("Collider");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = centre;
            child.AddComponent<BoxCollider>().size = size;
        }
    }
}
