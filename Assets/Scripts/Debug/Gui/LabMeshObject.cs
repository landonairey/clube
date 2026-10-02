using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Clube.Debug
{
    /// <summary>
    /// A child object that renders one procedural mesh, for lab visuals that must
    /// be real geometry rather than gizmos (see <see cref="ChunkDebugView"/> for
    /// why). Never saved with the scene, and works in edit mode as well as Play mode.
    /// </summary>
    public sealed class LabMeshObject : IDisposable
    {
        private const string ColorProperty = "_BaseColor";

        private readonly GameObject gameObject;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public LabMeshObject(Transform parent, string name, Material material)
        {
            Mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();

            gameObject = new GameObject(name) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<MeshFilter>().sharedMesh = Mesh;

            Renderer = gameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        public Mesh Mesh { get; }

        public MeshRenderer Renderer { get; }

        public Transform Transform => gameObject.transform;

        public bool Visible
        {
            get => Renderer.enabled;
            set => Renderer.enabled = value;
        }

        /// <summary>Tints the whole mesh, for materials with a base colour (URP Unlit).</summary>
        public void SetColor(Color color)
        {
            properties.SetColor(ColorProperty, color);
            Renderer.SetPropertyBlock(properties);
        }

        public void Dispose()
        {
            DestroyNow(Mesh);
            DestroyNow(gameObject);
        }

        /// <summary>Destroy in Play mode, DestroyImmediate in edit mode (where Destroy isn't allowed).</summary>
        public static void DestroyNow(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
