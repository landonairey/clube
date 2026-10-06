using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A chunk renderer's collider (M6): its own mesh in a <see cref="MeshCollider"/>, so a
    /// player can walk on the terrain. The world's <see cref="WorldStreamer"/> cooks the shape
    /// in a job first (<see cref="ChunkPipeline.StartBake"/>), only for chunks near the focus,
    /// then <see cref="Assign"/> hands it over cheaply; this component remembers which version
    /// of the mesh the shape is for. An empty chunk has no collider shape.
    /// </summary>
    [RequireComponent(typeof(ChunkRenderer), typeof(MeshCollider))]
    public class ChunkCollider : MonoBehaviour
    {
        private MeshCollider meshCollider;

        /// <summary>The <see cref="ChunkRenderer.MeshVersion"/> the collider shape is for; -1 when it has none.</summary>
        public int Version { get; private set; } = -1;

        /// <summary>The options shapes are cooked with; bakes must use the same, or assigning cooks again.</summary>
        public MeshColliderCookingOptions CookingOptions => Collider.cookingOptions;

        private MeshCollider Collider => meshCollider != null ? meshCollider : meshCollider = GetComponent<MeshCollider>();

        /// <summary>Gives the collider a mesh already baked for <paramref name="version"/>.</summary>
        public void Assign(Mesh mesh, int version)
        {
            // The mesh object is reused, so clear first: assigning the same mesh again doesn't re-read it.
            Collider.sharedMesh = null;
            if (mesh.vertexCount > 0)
            {
                Collider.sharedMesh = mesh;
            }
            Version = version;
        }

        /// <summary>Drops the shape: the renderer is pooled, or left the area that needs collision.</summary>
        public void Clear()
        {
            Collider.sharedMesh = null;
            Version = -1;
        }
    }
}
