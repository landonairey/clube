using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A copy of a prefab's look without its workings (GL419, GL424): the meshes it draws (and, if wanted, its colliders),
    /// none of its scripts, so the town shows the game's own anvil, furnace, crate or sleeping roll as decoration that can't
    /// be used, picked up or saved. A prefab with a "Placement preview" child (a terrain structure such as the furnace,
    /// whose real body is ground) is copied from that child: its meshed surface is the structure's exact look.
    /// </summary>
    public static class ModelCopy
    {
        private const string PreviewName = "Placement preview";
        private const string OutlineName = "Outline shape";

        /// <summary>
        /// Copies <paramref name="prefab"/>'s visible meshes under <paramref name="parent"/>. With <paramref name="fitInto"/>
        /// above 0 the copy is scaled down so its largest side is no more than that many metres. Returns the copy's root.
        /// </summary>
        public static GameObject Copy(GameObject prefab, Transform parent, string name, bool colliders = false, float fitInto = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            if (prefab == null)
            {
                return root;
            }
            Transform source = prefab.transform.Find(PreviewName);
            if (source != null)
            {
                CopyTree(source, root.transform, colliders, source);
            }
            else
            {
                CopyTree(prefab.transform, root.transform, colliders, prefab.transform);
            }
            if (colliders && root.GetComponentInChildren<Collider>() == null)
            {
                // A terrain structure's preview has none: a box round the model, so it stands solid.
                Bounds model = LocalBounds(root.transform);
                var box = root.AddComponent<BoxCollider>();
                box.center = model.center;
                box.size = model.size;
            }
            if (fitInto > 0f)
            {
                Bounds bounds = LocalBounds(root.transform);
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (largest > fitInto)
                {
                    root.transform.localScale = Vector3.one * (fitInto / largest);
                }
            }
            return root;
        }

        private static void CopyTree(Transform from, Transform to, bool colliders, Transform top)
        {
            if (from != top)
            {
                var child = new GameObject(from.name);
                child.transform.SetParent(to, false);
                child.transform.localPosition = from.localPosition;
                child.transform.localRotation = from.localRotation;
                child.transform.localScale = from.localScale;
                to = child.transform;
            }
            var filter = from.GetComponent<MeshFilter>();
            var renderer = from.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && renderer.enabled && from.name != OutlineName)
            {
                to.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                to.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            if (colliders && from.TryGetComponent(out BoxCollider box))
            {
                var copy = to.gameObject.AddComponent<BoxCollider>();
                copy.center = box.center;
                copy.size = box.size;
            }
            foreach (Transform child in from)
            {
                if (child.name != OutlineName)
                {
                    CopyTree(child, to, colliders, top);
                }
            }
        }

        // The copy's meshes' bounds in its root's frame (before any scaling).
        private static Bounds LocalBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                    Vector3 local = toRoot.MultiplyPoint3x4(mesh.min + Vector3.Scale(mesh.size, point));
                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            return bounds;
        }
    }
}
