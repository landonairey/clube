using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Flips every triangle after each chunk build by reversing its winding order
    /// (V5). Unity culls back faces, so flipped faces are only visible from the
    /// solid side, and recalculated normals point inwards. Lab-only: the core
    /// mesher always produces the correct winding.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class FlipFaces : MonoBehaviour
    {
        [Tooltip("Reverse each triangle's winding order after every rebuild, flipping which side is the front face.")]
        [SerializeField]
        private bool flipFaces;

        private ChunkView chunkView;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;
            RequestRebuild();
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
            RequestRebuild();
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            RequestRebuild();
        }

        // Rebuilding from scratch keeps this simple: the mesher always starts from
        // the correct winding, and this component only ever flips a fresh mesh.
        private void RequestRebuild()
        {
            if (chunkView != null && chunkView.Chunk != null)
            {
                chunkView.Chunk.MarkDirty();
            }
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            if (!flipFaces)
            {
                return;
            }

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }
    }
}
