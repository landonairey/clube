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
    [RequireComponent(typeof(LabChunkTarget))]
    public class FlipFaces : MonoBehaviour
    {
        [Tooltip("Reverse each triangle's winding order after every rebuild, flipping which side is the front face.")]
        [SerializeField]
        private bool flipFaces;

        private LabChunkTarget target;

        // The chunk last asked to rebuild, so a focus change can restore its normal winding.
        private Chunk flippedChunk;

        /// <summary>Reverse every triangle's winding; changing it rebuilds the mesh.</summary>
        public bool Flip
        {
            get => flipFaces;
            set
            {
                flipFaces = value;
                RequestRebuild();
            }
        }

        private void Awake()
        {
            target = GetComponent<LabChunkTarget>();
        }

        private void OnEnable()
        {
            target.MeshRebuilt += OnMeshRebuilt;
            target.Changed += RequestRebuild;
            RequestRebuild();
        }

        private void OnDisable()
        {
            target.MeshRebuilt -= OnMeshRebuilt;
            target.Changed -= RequestRebuild;
            RequestRebuild();
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            RequestRebuild();
        }

        // Rebuilding from scratch keeps this simple: the mesher always starts from
        // the correct winding, and this component only ever flips a fresh mesh. A chunk
        // that stopped being the target (WorldLab's focus moved) rebuilds unflipped.
        private void RequestRebuild()
        {
            if (target == null)
            {
                return;
            }
            if (flippedChunk != null && flippedChunk != target.Chunk)
            {
                flippedChunk.MarkDirty();
            }
            flippedChunk = target.Chunk;
            target.RequestRebuild();
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
