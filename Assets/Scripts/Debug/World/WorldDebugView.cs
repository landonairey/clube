using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// WorldLab's view of the chunk grid: optionally outlines every loaded chunk, so you
    /// can see where the borders run while checking they're seamless (M2) and that edits
    /// cross them (M5). The focused chunk's edges (<see cref="ChunkFocus"/>, M4) are drawn
    /// in the highlight colour as part of the same grid. Reads the world view's public
    /// data only (A4).
    /// </summary>
    /// <remarks>
    /// Neighbouring chunks share their edges, so each grid edge is drawn once, in one
    /// colour: two coincident lines would fight over which one shows, which is how a
    /// highlight used to vanish depending on the camera angle. One line mesh with vertex
    /// colours, rebuilt when chunks load or unload or the focus changes.
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class WorldDebugView : MonoBehaviour
    {
        private const string VertexColorShader = "Universal Render Pipeline/Particles/Unlit";

        // Seconds between rebuilds of the grid while chunks keep loading.
        private const float RebuildInterval = 0.25f;

        [Tooltip("Outline every loaded chunk.")]
        [SerializeField]
        private bool showChunkBorders = true;

        [SerializeField]
        private Color borderColor = new Color(0.45f, 0.47f, 0.52f);

        [Tooltip("Colour of the focused chunk's edges.")]
        [SerializeField]
        private Color focusColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("Material that shows vertex colours (LabVertexColor). Falls back to URP Particles/Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        private readonly HashSet<(Vector3Int Start, int Axis)> edges = new HashSet<(Vector3Int, int)>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();

        private WorldView worldView;
        private ChunkFocus chunkFocus;
        private LabMeshObject borders;
        private Material ownedMaterial;
        private bool rebuildRequested;
        private float nextRebuildTime;

        public bool ShowChunkBorders
        {
            get => showChunkBorders;
            set
            {
                if (value == showChunkBorders)
                {
                    return;
                }
                showChunkBorders = value;
                rebuildRequested = true;
            }
        }

        /// <summary>True while the grid is drawn, so the focus outline is part of it.</summary>
        public bool IsDrawingBorders => enabled && showChunkBorders;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            chunkFocus = GetComponent<ChunkFocus>();
        }

        private void OnEnable()
        {
            worldView.ChunkLoaded += OnChunkLoaded;
            worldView.ChunkUnloaded += OnChunkUnloaded;
            if (chunkFocus != null)
            {
                chunkFocus.FocusChanged += OnFocusChanged;
                chunkFocus.HighlightChanged += OnHighlightChanged;
            }
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            worldView.ChunkLoaded -= OnChunkLoaded;
            worldView.ChunkUnloaded -= OnChunkUnloaded;
            if (chunkFocus != null)
            {
                chunkFocus.FocusChanged -= OnFocusChanged;
                chunkFocus.HighlightChanged -= OnHighlightChanged;
            }
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

        private void OnHighlightChanged()
        {
            rebuildRequested = true;
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            // At most a few times a second: while a large world streams in, chunks load every frame.
            if (rebuildRequested && worldView.World != null && Time.unscaledTime >= nextRebuildTime)
            {
                rebuildRequested = false;
                nextRebuildTime = Time.unscaledTime + RebuildInterval;
                RebuildBorders();
            }
        }

        private void OnChunkLoaded(Vector3Int coord)
        {
            rebuildRequested = true;
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            rebuildRequested = true;
        }

        private void OnFocusChanged(Vector3Int? coord)
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
                    ownedMaterial = new Material(Shader.Find(VertexColorShader)) { name = "Chunk Borders" };
                    material = ownedMaterial;
                }
                borders = new LabMeshObject(transform, "Chunk Borders", material);
            }

            // Each edge of the chunk grid once, keyed by its start corner (in chunk
            // coordinates) and axis; the focused chunk's twelve are highlighted.
            edges.Clear();
            foreach (Vector3Int coord in worldView.World.Chunks.Keys)
            {
                AddChunkEdges(coord, edges);
            }
            var focused = new HashSet<(Vector3Int, int)>();
            if (chunkFocus != null && chunkFocus.Focused.HasValue && chunkFocus.HighlightChunk)
            {
                AddChunkEdges(chunkFocus.Focused.Value, focused);
            }

            Vector3 size = worldView.World.Grid.ChunkWorldSize;
            Color32 normal = borderColor;
            Color32 highlight = focusColor;
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            foreach ((Vector3Int start, int axis) in edges)
            {
                Vector3Int end = start;
                end[axis] += 1;
                Color32 color = focused.Contains((start, axis)) ? highlight : normal;
                indices.Add(vertices.Count);
                vertices.Add(Vector3.Scale(start, size));
                colors.Add(color);
                indices.Add(vertices.Count);
                vertices.Add(Vector3.Scale(end, size));
                colors.Add(color);
            }

            Mesh mesh = borders.Mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            borders.Visible = true;
        }

        // A chunk's twelve edges: four along each axis, from the corners where that axis is 0.
        private static void AddChunkEdges(Vector3Int coord, HashSet<(Vector3Int, int)> into)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int a = (axis + 1) % 3;
                int b = (axis + 2) % 3;
                for (int i = 0; i <= 1; i++)
                {
                    for (int j = 0; j <= 1; j++)
                    {
                        Vector3Int start = coord;
                        start[a] += i;
                        start[b] += j;
                        into.Add((start, axis));
                    }
                }
            }
        }
    }
}
