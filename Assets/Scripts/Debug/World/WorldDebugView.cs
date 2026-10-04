using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// WorldLab's view of the chunk grid: optionally outlines every loaded chunk, so you
    /// can see where the borders run while checking they're seamless (M2) and that edits
    /// cross them (M5). One line mesh, rebuilt when chunks load or unload. Reads the
    /// world view's public data only (A4).
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class WorldDebugView : MonoBehaviour
    {
        private const string LineShader = "Universal Render Pipeline/Unlit";

        [Tooltip("Outline every loaded chunk.")]
        [SerializeField]
        private bool showChunkBorders = true;

        [SerializeField]
        private Color borderColor = new Color(0.45f, 0.47f, 0.52f);

        [Tooltip("Unlit material for the lines. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();

        private WorldView worldView;
        private LabMeshObject borders;
        private Material ownedMaterial;
        private bool rebuildRequested;

        public bool ShowChunkBorders
        {
            get => showChunkBorders;
            set
            {
                showChunkBorders = value;
                rebuildRequested = true;
            }
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void OnEnable()
        {
            worldView.ChunkLoaded += OnChunkLoaded;
            worldView.ChunkUnloaded += OnChunkUnloaded;
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            worldView.ChunkLoaded -= OnChunkLoaded;
            worldView.ChunkUnloaded -= OnChunkUnloaded;
            if (borders != null)
            {
                borders.Visible = false;
            }
        }

        private void OnDestroy()
        {
            borders?.Dispose();
            LabMeshObject.DestroyNow(ownedMaterial);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (rebuildRequested && worldView.World != null)
            {
                rebuildRequested = false;
                RebuildBorders();
            }
        }

        private void OnChunkLoaded(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            rebuildRequested = true;
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            rebuildRequested = true;
        }

        private void RebuildBorders()
        {
            if (!showChunkBorders)
            {
                if (borders != null)
                {
                    borders.Visible = false;
                }
                return;
            }

            if (borders == null)
            {
                Material material = lineMaterial;
                if (material == null)
                {
                    ownedMaterial = new Material(Shader.Find(LineShader)) { name = "Chunk Borders" };
                    material = ownedMaterial;
                }
                borders = new LabMeshObject(transform, "Chunk Borders", material);
            }

            WorldGrid grid = worldView.World.Grid;
            Vector3 size = grid.ChunkWorldSize;
            vertices.Clear();
            indices.Clear();
            foreach (Vector3Int coord in worldView.Renderers.Keys)
            {
                Vector3 origin = grid.ChunkOrigin(coord);
                int first = vertices.Count;
                for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                {
                    vertices.Add(origin + Vector3.Scale(MarchingCubes.CornerOffset(corner), size));
                }
                for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
                {
                    indices.Add(first + MarchingCubesTables.EdgeCorners[edge, 0]);
                    indices.Add(first + MarchingCubesTables.EdgeCorners[edge, 1]);
                }
            }

            Mesh mesh = borders.Mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            borders.SetColor(borderColor);
            borders.Visible = true;
        }
    }
}
