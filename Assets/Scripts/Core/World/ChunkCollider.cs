using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Gives a <see cref="ChunkRenderer"/>'s chunk a collider (M6): the chunk's own mesh,
    /// handed to a <see cref="MeshCollider"/> again after every rebuild so the physics
    /// shape follows digging and placing. An empty chunk has no collider shape.
    /// </summary>
    [RequireComponent(typeof(ChunkRenderer), typeof(MeshCollider))]
    public class ChunkCollider : MonoBehaviour
    {
        private ChunkRenderer chunkRenderer;
        private MeshCollider meshCollider;

        private void Awake()
        {
            chunkRenderer = GetComponent<ChunkRenderer>();
            meshCollider = GetComponent<MeshCollider>();
        }

        private void OnEnable()
        {
            chunkRenderer.MeshRebuilt += Apply;
        }

        private void OnDisable()
        {
            chunkRenderer.MeshRebuilt -= Apply;

            // A pooled renderer comes back for another chunk; drop the old shape now.
            meshCollider.sharedMesh = null;
        }

        private void Apply(Mesh mesh)
        {
            // The mesh object is reused, so clear first: assigning the same mesh again doesn't re-bake it.
            meshCollider.sharedMesh = null;
            if (mesh.vertexCount > 0)
            {
                meshCollider.sharedMesh = mesh;
            }
        }
    }
}
