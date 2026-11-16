using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The portal at an end of a road tunnel (GL642): two stone piers and an arch of blocks over the mouth, with a
    /// keystone, standing against the hillside where the bore begins. One low-poly mesh with box colliders on its
    /// piers, so it is walked through and not into.
    /// </summary>
    public static class TunnelPortal
    {
        private static readonly Color Stone = new Color(0.52f, 0.5f, 0.47f);

        /// <summary>Builds a portal under a parent, at the origin of its own frame: the mouth looks out along +z.</summary>
        /// <param name="width">The passage's width and <paramref name="height"/> its height at the middle, in metres.</param>
        public static GameObject Build(float width, float height, Material material, Transform parent, int seed)
        {
            var root = new GameObject("Tunnel portal");
            root.transform.SetParent(parent, false);
            var random = new System.Random(seed);
            float F() => (float)random.NextDouble();
            var mesh = new LowPolyMeshBuilder();
            const float Pier = 0.8f;
            float half = width * 0.5f;
            float spring = height * 0.67f;
            // The piers, in three courses each a little off true.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int course = 0; course < 3; course++)
                {
                    float y = spring * (course + 0.5f) / 3f;
                    mesh.Box(new Vector3(side * (half + Pier * 0.5f), y, 0f), new Vector3(Pier + F() * 0.12f, spring / 3f - 0.03f, Pier + F() * 0.15f),
                        Quaternion.Euler(0f, (F() - 0.5f) * 6f, 0f), Stone * (0.85f + 0.2f * F()));
                }
                AddBox(root.transform, new Vector3(side * (half + Pier * 0.5f), spring * 0.5f, 0f), new Vector3(Pier, spring, Pier));
            }
            // The arch: blocks round a half circle from pier to pier, the keystone proud at its top.
            const int Blocks = 9;
            float rise = height - spring + 0.2f;
            for (int i = 0; i < Blocks; i++)
            {
                float angle = Mathf.PI * (i + 0.5f) / Blocks;
                var at = new Vector3(-Mathf.Cos(angle) * (half + Pier * 0.5f), spring + Mathf.Sin(angle) * (rise + Pier * 0.35f), 0f);
                bool key = i == Blocks / 2;
                float length = Mathf.PI * (half + Pier * 0.5f) / Blocks * 1.25f;
                mesh.Box(at + (key ? new Vector3(0f, 0.08f, 0.06f) : Vector3.zero), new Vector3(length, Pier * (key ? 1.05f : 0.85f), Pier * (key ? 1.1f : 0.95f)),
                    Quaternion.Euler(0f, 0f, 90f - angle * Mathf.Rad2Deg), Stone * (key ? 1.08f : 0.82f + 0.22f * F()));
            }
            AddBox(root.transform, new Vector3(0f, spring + rise + Pier * 0.35f, 0f), new Vector3(width + Pier * 2f, Pier * 0.8f, Pier));
            root.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Tunnel portal");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            return root;
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
