using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the chunk mesh's normals in Play mode (V8): per vertex, as Unity
    /// will light them, and per face, from each triangle's winding. Flat shading
    /// shows vertex normals matching their faces; smooth shading shows them
    /// averaged (V4). <see cref="FlipFaces"/> turns both inwards (V5). Optional
    /// triangle outlines show where each face normal's triangle is.
    /// </summary>
    /// <remarks>
    /// One line mesh with vertex colours rather than gizmos, which dim wrongly in
    /// this project (see <see cref="ChunkDebugView"/>).
    /// </remarks>
    [RequireComponent(typeof(ChunkView))]
    public class NormalLines : MonoBehaviour
    {
        private const string VertexColorShader = "Universal Render Pipeline/Particles/Unlit";

        [SerializeField]
        private bool showVertexNormals = true;

        [SerializeField]
        private bool showFaceNormals = true;

        [Tooltip("Outline every triangle, so it is clear each face normal starts at its triangle's centre.")]
        [SerializeField]
        private bool showTriangleEdges = true;

        [Tooltip("Normal length as a fraction of the voxel size.")]
        [SerializeField, Min(0f)]
        private float length = 0.2f;

        [SerializeField]
        private Color vertexNormalColor = new Color(0.3f, 0.8f, 1f);

        [SerializeField]
        private Color faceNormalColor = new Color(1f, 0.9f, 0.2f);

        [SerializeField]
        private Color triangleEdgeColor = new Color(0.75f, 0.75f, 0.75f);

        [Tooltip("Material that shows vertex colours. Falls back to URP Particles/Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        private readonly List<Vector3> lineVertices = new List<Vector3>();
        private readonly List<Color32> lineColors = new List<Color32>();
        private readonly List<int> lineIndices = new List<int>();

        private ChunkView chunkView;
        private LabMeshObject lines;
        private Material ownedMaterial;
        private bool rebuildRequested;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;

            // Turned on after the chunk was built: no rebuild is coming, so draw now.
            rebuildRequested = chunkView.Chunk != null;
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
            if (lines != null)
            {
                lines.Visible = false;
            }
        }

        private void OnDestroy()
        {
            lines?.Dispose();
            LabMeshObject.DestroyNow(ownedMaterial);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            rebuildRequested = true;
        }

        // Other MeshRebuilt handlers (e.g. FlipFaces) may still modify the mesh, so
        // the lines are built later in the frame rather than here.
        private void OnMeshRebuilt(Mesh rebuilt)
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (rebuildRequested && chunkView.Chunk != null)
            {
                rebuildRequested = false;
                Rebuild(GetComponent<MeshFilter>().sharedMesh);
            }

            // Step-through hides the finished mesh, so its normals would give the answer away.
            if (lines != null)
            {
                lines.Visible = !StepThroughMode.IsOn(this);
            }
        }

        private void Rebuild(Mesh source)
        {
            if (lines == null)
            {
                Material material = lineMaterial;
                if (material == null)
                {
                    ownedMaterial = new Material(Shader.Find(VertexColorShader)) { name = "Normal Lines" };
                    material = ownedMaterial;
                }
                lines = new LabMeshObject(transform, "Normal Lines", material);
            }

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            int[] triangles = source.triangles;
            float scaledLength = length * chunkView.Config.VoxelSize;

            lineVertices.Clear();
            lineColors.Clear();
            lineIndices.Clear();

            if (showTriangleEdges)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    AddLine(a, b, triangleEdgeColor);
                    AddLine(b, c, triangleEdgeColor);
                    AddLine(c, a, triangleEdgeColor);
                }
            }

            if (showVertexNormals)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    AddLine(vertices[i], vertices[i] + normals[i] * scaledLength, vertexNormalColor);
                }
            }

            if (showFaceNormals)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];

                    // Same convention Unity uses: clockwise winding faces the viewer.
                    Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                    Vector3 centre = (a + b + c) / 3f;
                    AddLine(centre, centre + faceNormal * scaledLength, faceNormalColor);
                }
            }

            Mesh mesh = lines.Mesh;
            mesh.Clear();
            mesh.indexFormat = lineVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(lineVertices);
            mesh.SetColors(lineColors);
            mesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
            lines.Visible = true;
        }

        private void AddLine(Vector3 from, Vector3 to, Color color)
        {
            lineIndices.Add(lineVertices.Count);
            lineVertices.Add(from);
            lineColors.Add(color);
            lineIndices.Add(lineVertices.Count);
            lineVertices.Add(to);
            lineColors.Add(color);
        }
    }
}
