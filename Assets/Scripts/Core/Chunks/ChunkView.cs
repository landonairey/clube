using System;
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

        // Play mode works on a copy, so Inspector tweaks during Play revert on
        // exit like scene values do, instead of being saved into the asset.
        // (A chunk manager will own one shared copy once there are many chunks.)
        private WorldConfig runtimeConfig;

        /// <summary>
        /// Raised after the mesh is rebuilt, with normals and bounds already set.
        /// Lab tools hook in here (A4) rather than the core calling into them.
        /// </summary>
        public event Action<Mesh> MeshRebuilt;

        /// <summary>The chunk this view renders. Created in Awake; null outside Play mode.</summary>
        public Chunk Chunk { get; private set; }

        /// <summary>The config in use: the Play mode copy while playing, otherwise the assigned asset.</summary>
        public WorldConfig Config => runtimeConfig != null ? runtimeConfig : config;

        /// <summary>True while <see cref="Config"/> is a Play mode copy whose edits are discarded on exit.</summary>
        public bool IsUsingRuntimeConfig => runtimeConfig != null;

        private void Awake()
        {
            if (config == null)
            {
                UnityEngine.Debug.LogError($"{nameof(ChunkView)} on '{name}' has no {nameof(WorldConfig)} assigned.", this);
                enabled = false;
                return;
            }

            runtimeConfig = Instantiate(config);
            runtimeConfig.name = $"{config.name} (Play mode copy)";

            Chunk = new Chunk(Config.ChunkSize);

            mesh = new Mesh { name = "Chunk" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void OnEnable()
        {
            if (Config != null)
            {
                Config.Changed += OnConfigChanged;
            }
        }

        private void OnDisable()
        {
            if (Config != null)
            {
                Config.Changed -= OnConfigChanged;
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

            if (runtimeConfig != null)
            {
                Destroy(runtimeConfig);
            }
        }

        private void OnConfigChanged()
        {
            Chunk?.MarkDirty();
        }

        private void RebuildMesh()
        {
            ChunkMesher.Build(Chunk, Config.MeshSettings, vertices, triangles);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Chunk.MarkClean();
            MeshRebuilt?.Invoke(mesh);
        }
    }
}
