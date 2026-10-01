using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the chunk mesh's normals in Play mode (V8): per vertex, as Unity
    /// will light them, and per face, from each triangle's winding. Flat shading
    /// shows vertex normals matching their faces; smooth shading shows them
    /// averaged (V4). <see cref="FlipFaces"/> turns both inwards (V5).
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class NormalGizmos : MonoBehaviour
    {
        [SerializeField]
        private bool showVertexNormals = true;

        [SerializeField]
        private bool showFaceNormals = true;

        [Tooltip("Normal length as a fraction of the voxel size.")]
        [SerializeField, Min(0f)]
        private float length = 0.2f;

        [SerializeField]
        private Color vertexNormalColor = new Color(0.3f, 0.8f, 1f);

        [SerializeField]
        private Color faceNormalColor = new Color(1f, 0.9f, 0.2f);

        private ChunkView chunkView;
        private Mesh mesh;
        private bool meshChanged;

        private Vector3[] vertices = new Vector3[0];
        private Vector3[] normals = new Vector3[0];
        private int[] triangles = new int[0];

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
        }

        // Only note the change here: other MeshRebuilt handlers (e.g. FlipFaces)
        // may still modify the mesh, so it is read when drawing instead.
        private void OnMeshRebuilt(Mesh rebuilt)
        {
            mesh = rebuilt;
            meshChanged = true;
        }

        private void OnDrawGizmos()
        {
            if (mesh == null || chunkView == null || chunkView.Config == null)
            {
                return;
            }

            if (meshChanged)
            {
                vertices = mesh.vertices;
                normals = mesh.normals;
                triangles = mesh.triangles;
                meshChanged = false;
            }

            float scaledLength = length * chunkView.Config.VoxelSize;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (showVertexNormals)
            {
                Gizmos.color = vertexNormalColor;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Gizmos.DrawLine(vertices[i], vertices[i] + normals[i] * scaledLength);
                }
            }

            if (showFaceNormals)
            {
                Gizmos.color = faceNormalColor;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];

                    // Same convention Unity uses: clockwise winding faces the viewer.
                    Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                    Vector3 centre = (a + b + c) / 3f;
                    Gizmos.DrawLine(centre, centre + faceNormal * scaledLength);
                }
            }
        }
    }
}
