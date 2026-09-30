using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Owns one <see cref="Chunk"/> and keeps its mesh in sync: whenever the chunk
    /// is dirty (an edit, or a config change), the mesh is rebuilt once at the end
    /// of the frame (G1). The chunk's sample (0,0,0) sits at this transform's origin.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkView : MonoBehaviour
    {
        [SerializeField]
        private WorldConfig config;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        private Mesh mesh;

        /// <summary>The chunk this view renders. Created in Awake; null outside Play mode.</summary>
        public Chunk Chunk { get; private set; }

        public WorldConfig Config => config;

        private void Awake()
        {
            if (config == null)
            {
                UnityEngine.Debug.LogError($"{nameof(ChunkView)} on '{name}' has no {nameof(WorldConfig)} assigned.", this);
                enabled = false;
                return;
            }

            Chunk = new Chunk(config.ChunkSize);

            mesh = new Mesh { name = "Chunk" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void OnEnable()
        {
            if (config != null)
            {
                config.Changed += OnConfigChanged;
            }
        }

        private void OnDisable()
        {
            if (config != null)
            {
                config.Changed -= OnConfigChanged;
            }
        }

        private void LateUpdate()
        {
            if (Chunk.IsDirty)
            {
                RebuildMesh();
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void OnConfigChanged()
        {
            Chunk?.MarkDirty();
        }

        private void RebuildMesh()
        {
            ChunkMesher.Build(Chunk, config.MeshSettings, vertices, triangles);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Chunk.MarkClean();
        }
    }
}
