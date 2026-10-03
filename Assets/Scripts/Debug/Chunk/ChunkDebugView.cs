using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// In Play mode, draws a sphere on every density sample shaded by its value,
    /// black = 0 to white = 1 (V6), and a wireframe: none, the chunk's outline, or
    /// the outline plus every voxel's edges. Reads the chunk's public data only (A4).
    /// </summary>
    /// <remarks>
    /// These are real meshes rather than gizmos. Unity's gizmo pass draws gizmo
    /// parts it thinks are hidden more faintly, and in this project (URP on
    /// Direct3D 12) it reads that from a vertically flipped depth buffer, so mirror
    /// images of opaque objects showed up inside gizmos. All spheres are one
    /// combined mesh with vertex colours (one draw call); a density edit only
    /// rewrites the colours. Outside Play mode, where there is no chunk yet, the
    /// outline is drawn as a gizmo from the config.
    /// </remarks>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkDebugView : MonoBehaviour
    {
        /// <summary>How much of the chunk's wireframe to draw.</summary>
        public enum Wireframe
        {
            None,

            /// <summary>The chunk's bounding box.</summary>
            Outline,

            /// <summary>The bounding box plus the grid lines between every voxel.</summary>
            VoxelGrid,
        }

        /// <summary>Above this many samples the spheres are suppressed, to keep the mesh and its rebuilds light.</summary>
        public const int MaxSampleSpheres = 40000;

        private const string VertexColorShader = "Universal Render Pipeline/Particles/Unlit";
        private const string LineShader = "Universal Render Pipeline/Unlit";

        [Tooltip("Draw a sphere on every density sample. Suppressed above 40,000 samples (about a 33³ chunk).")]
        [SerializeField]
        private bool showSamples = true;

        // Capped at half a voxel: any larger and neighbouring corner spheres overlap.
        [Tooltip("Sphere radius as a fraction of the voxel size (at most half an edge).")]
        [SerializeField, Range(0f, 0.5f)]
        private float cornerRadius = 0.05f;

        [Tooltip("How opaque the sample spheres are, so a full grid of them doesn't hide the surface. " +
                 "Needs a transparent vertex-colour material (LabVertexColorTransparent).")]
        [SerializeField, Range(0f, 1f)]
        private float sampleOpacity = 0.35f;

        [Tooltip("None, the chunk's bounding box, or the box plus every voxel's wireframe. " +
                 "In a 1×1×1 chunk (VoxelLab) the outline already is the voxel's wireframe.")]
        [SerializeField]
        private Wireframe wireframe = Wireframe.Outline;

        [Tooltip("Colour of the chunk's bounding box.")]
        [SerializeField]
        private Color outlineColor = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Colour of the inner voxel grid lines, dimmer so the outline still reads.")]
        [SerializeField]
        private Color voxelGridColor = new Color(0.32f, 0.33f, 0.36f);

        [Tooltip("Material that shows vertex colours and alpha, for the spheres (LabVertexColorTransparent). Falls back to URP Particles/Unlit if empty.")]
        [SerializeField]
        private Material sampleMaterial;

        [Tooltip("Unlit material for the outline and grid. Falls back to URP Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        private readonly List<Vector3> sphereVertices = new List<Vector3>();
        private readonly List<int> sphereTriangles = new List<int>();
        private readonly List<Color32> sampleColors = new List<Color32>();
        private readonly List<Vector3> scratchVertices = new List<Vector3>();
        private readonly List<int> scratchIndices = new List<int>();

        private ChunkView chunkView;
        private LabMeshObject samples;
        private LabMeshObject outline;
        private LabMeshObject grid;
        private Material ownedSampleMaterial;
        private Material ownedLineMaterial;

        // What the current meshes were built for, so they are only rebuilt when it changes.
        private Vector3Int builtSampleCount;
        private float builtVoxelSize;
        private float builtRadius;
        private Vector3Int builtGridCount;
        private float builtGridSize;

        private bool refreshRequested;

        /// <summary>True when sample spheres are wanted but the chunk has too many samples to draw them.</summary>
        public bool AreSamplesSuppressed
        {
            get
            {
                Chunk chunk = GetComponent<ChunkView>().Chunk;
                return showSamples && chunk != null && SampleTotal(chunk) > MaxSampleSpheres;
            }
        }

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;
            refreshRequested = true;
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
            SetVisible(samples, false);
            SetVisible(outline, false);
            SetVisible(grid, false);
        }

        private void OnDestroy()
        {
            samples?.Dispose();
            outline?.Dispose();
            grid?.Dispose();
            LabMeshObject.DestroyNow(ownedSampleMaterial);
            LabMeshObject.DestroyNow(ownedLineMaterial);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        // Objects can't be created during OnValidate, so the refresh waits for LateUpdate.
        private void OnValidate()
        {
            refreshRequested = true;
        }

        private void LateUpdate()
        {
            if (refreshRequested)
            {
                refreshRequested = false;
                Refresh();
            }

            // Step-through draws the samples itself, coloured by step; this check is
            // per frame because the mode can be switched at any time.
            if (samples != null)
            {
                samples.Visible = WantsSamples();
            }
        }

        // Before Play mode there is no chunk yet, so outline the config's chunk size.
        private void OnDrawGizmos()
        {
            var view = GetComponent<ChunkView>();
            if (!enabled || wireframe == Wireframe.None || view.Chunk != null || view.Config == null)
            {
                return;
            }

            Vector3 size = (Vector3)view.Config.ChunkSize * view.Config.VoxelSize;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(size * 0.5f, size);
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            Refresh();
        }

        private void Refresh()
        {
            Chunk chunk = chunkView.Chunk;
            if (chunk == null || !enabled)
            {
                return;
            }

            float voxelSize = chunkView.Config.VoxelSize;
            RefreshOutline(chunk.VoxelCount, voxelSize);
            RefreshGrid(chunk.VoxelCount, voxelSize);
            RefreshSamples(chunk, voxelSize);
        }

        private bool WantsSamples()
        {
            Chunk chunk = chunkView.Chunk;
            return enabled && showSamples && chunk != null && SampleTotal(chunk) <= MaxSampleSpheres
                   && !StepThroughMode.IsOn(this);
        }

        private void RefreshSamples(Chunk chunk, float voxelSize)
        {
            if (!showSamples || SampleTotal(chunk) > MaxSampleSpheres)
            {
                SetVisible(samples, false);
                return;
            }

            samples ??= new LabMeshObject(transform, "Density Samples", SampleMaterial());
            Vector3Int count = chunk.SampleCount;
            if (count != builtSampleCount || voxelSize != builtVoxelSize || cornerRadius != builtRadius)
            {
                BuildSampleGeometry(count, voxelSize);
            }

            // Densities are the only thing an ordinary edit changes: rewrite just the colours.
            int perSphere = sphereVertices.Count;
            byte alpha = (byte)(sampleOpacity * 255f);
            sampleColors.Clear();
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        byte grey = (byte)(Mathf.Clamp01(chunk.GetDensity(new Vector3Int(x, y, z))) * 255f);
                        var color = new Color32(grey, grey, grey, alpha);
                        for (int i = 0; i < perSphere; i++)
                        {
                            sampleColors.Add(color);
                        }
                    }
                }
            }
            samples.Mesh.SetColors(sampleColors);
            samples.Visible = WantsSamples();
        }

        private void BuildSampleGeometry(Vector3Int count, float voxelSize)
        {
            if (sphereVertices.Count == 0)
            {
                LabMeshes.Icosphere(sphereVertices, sphereTriangles);
            }

            float radius = cornerRadius * voxelSize;
            scratchVertices.Clear();
            scratchIndices.Clear();
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        Vector3 centre = new Vector3(x, y, z) * voxelSize;
                        int first = scratchVertices.Count;
                        foreach (Vector3 vertex in sphereVertices)
                        {
                            scratchVertices.Add(centre + vertex * radius);
                        }
                        foreach (int index in sphereTriangles)
                        {
                            scratchIndices.Add(first + index);
                        }
                    }
                }
            }

            Mesh mesh = samples.Mesh;
            mesh.Clear();
            mesh.indexFormat = scratchVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(scratchVertices);
            mesh.SetTriangles(scratchIndices, 0);
            mesh.RecalculateBounds();

            builtSampleCount = count;
            builtVoxelSize = voxelSize;
            builtRadius = cornerRadius;
        }

        private void RefreshOutline(Vector3Int voxelCount, float voxelSize)
        {
            if (wireframe == Wireframe.None)
            {
                SetVisible(outline, false);
                return;
            }

            outline ??= new LabMeshObject(transform, "Chunk Outline", LineMaterial());
            scratchVertices.Clear();
            scratchIndices.Clear();
            Vector3 size = (Vector3)voxelCount * voxelSize;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                scratchVertices.Add(Vector3.Scale(MarchingCubes.CornerOffset(corner), size));
            }
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                scratchIndices.Add(MarchingCubesTables.EdgeCorners[edge, 0]);
                scratchIndices.Add(MarchingCubesTables.EdgeCorners[edge, 1]);
            }

            outline.Mesh.Clear();
            outline.Mesh.SetVertices(scratchVertices);
            outline.Mesh.SetIndices(scratchIndices, MeshTopology.Lines, 0);
            outline.SetColor(outlineColor);
            outline.Visible = true;
        }

        // Lines through every sample row along each axis, which together outline every voxel.
        private void RefreshGrid(Vector3Int voxelCount, float voxelSize)
        {
            if (wireframe != Wireframe.VoxelGrid)
            {
                SetVisible(grid, false);
                return;
            }

            grid ??= new LabMeshObject(transform, "Voxel Grid", LineMaterial());
            grid.SetColor(voxelGridColor);
            grid.Visible = true;
            if (voxelCount == builtGridCount && voxelSize == builtGridSize)
            {
                return;
            }

            scratchVertices.Clear();
            scratchIndices.Clear();
            Vector3 size = (Vector3)voxelCount * voxelSize;
            for (int axis = 0; axis < 3; axis++)
            {
                int a = (axis + 1) % 3;
                int b = (axis + 2) % 3;
                for (int i = 0; i <= voxelCount[a]; i++)
                {
                    for (int j = 0; j <= voxelCount[b]; j++)
                    {
                        Vector3 start = Vector3.zero;
                        start[a] = i * voxelSize;
                        start[b] = j * voxelSize;
                        Vector3 end = start;
                        end[axis] = size[axis];

                        scratchIndices.Add(scratchVertices.Count);
                        scratchVertices.Add(start);
                        scratchIndices.Add(scratchVertices.Count);
                        scratchVertices.Add(end);
                    }
                }
            }

            Mesh mesh = grid.Mesh;
            mesh.Clear();
            mesh.indexFormat = scratchVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(scratchVertices);
            mesh.SetIndices(scratchIndices, MeshTopology.Lines, 0);
            builtGridCount = voxelCount;
            builtGridSize = voxelSize;
        }

        private Material SampleMaterial()
        {
            if (sampleMaterial != null)
            {
                return sampleMaterial;
            }
            ownedSampleMaterial ??= new Material(Shader.Find(VertexColorShader)) { name = "Density Samples" };
            return ownedSampleMaterial;
        }

        // The tint is applied per object, so one material serves every line mesh.
        private Material LineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }
            ownedLineMaterial ??= new Material(Shader.Find(LineShader)) { name = "Chunk Lines" };
            return ownedLineMaterial;
        }

        private static void SetVisible(LabMeshObject meshObject, bool visible)
        {
            if (meshObject != null)
            {
                meshObject.Visible = visible;
            }
        }

        private static int SampleTotal(Chunk chunk)
        {
            Vector3Int count = chunk.SampleCount;
            return count.x * count.y * count.z;
        }
    }
}
