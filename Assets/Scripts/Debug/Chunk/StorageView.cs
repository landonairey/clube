using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// Shows how the target chunk's densities are stored (K27). Run-length storage: each run as a
    /// bar along its axis, coloured by its value (air blue, solid brown, the surface's in-between
    /// values yellow), with a gap where one run ends and the next begins. Octree storage: every
    /// leaf as a wireframe box coloured by its depth, bricks (dense leaves) brighter. Flat
    /// storage has no structure to show. Redrawn after every rebuild of the chunk.
    /// </summary>
    [RequireComponent(typeof(LabChunkTarget))]
    public class StorageView : MonoBehaviour
    {
        private const string VertexColorShader = "Universal Render Pipeline/Particles/Unlit";

        // Gap left at each end of a run bar, as a fraction of a voxel, so neighbouring runs read as separate.
        private const float RunGap = 0.12f;

        private static readonly Color AirColor = new Color(0.3f, 0.55f, 1f, 1f);
        private static readonly Color SolidColor = new Color(0.75f, 0.5f, 0.3f, 1f);
        private static readonly Color SurfaceColor = new Color(1f, 0.9f, 0.3f, 1f);

        private static readonly Color[] DepthColors =
        {
            new Color(1f, 1f, 1f), new Color(0.6f, 0.8f, 1f), new Color(0.4f, 1f, 0.6f), new Color(1f, 0.9f, 0.3f),
            new Color(1f, 0.55f, 0.25f), new Color(1f, 0.3f, 0.5f), new Color(0.75f, 0.4f, 1f), new Color(0.5f, 0.5f, 0.5f),
        };

        [Tooltip("Draw the storage's structure: run-length runs, or octree leaves.")]
        [SerializeField]
        private bool show;

        [Tooltip("Run-length storage: leave out runs of air (density 0), usually most of them.")]
        [SerializeField]
        private bool hideAirRuns = true;

        [Tooltip("Octree storage: leave out uniform leaves of air, so the boxes around the surface stand out.")]
        [SerializeField]
        private bool hideAirLeaves = true;

        [Tooltip("Material that shows vertex colours (LabVertexColor). Falls back to URP Particles/Unlit if empty.")]
        [SerializeField]
        private Material lineMaterial;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();

        private LabChunkTarget target;
        private LabMeshObject lines;
        private Material ownedMaterial;
        private bool rebuildRequested;

        public bool Show
        {
            get => show;
            set
            {
                if (value != show)
                {
                    show = value;
                    rebuildRequested = true;
                }
            }
        }

        public bool HideAirRuns
        {
            get => hideAirRuns;
            set
            {
                if (value != hideAirRuns)
                {
                    hideAirRuns = value;
                    rebuildRequested = true;
                }
            }
        }

        public bool HideAirLeaves
        {
            get => hideAirLeaves;
            set
            {
                if (value != hideAirLeaves)
                {
                    hideAirLeaves = value;
                    rebuildRequested = true;
                }
            }
        }

        /// <summary>What's drawn, for the panel: run or node counts, or why there's nothing.</summary>
        public string Summary { get; private set; } = "";

        private void Awake()
        {
            target = GetComponent<LabChunkTarget>();
        }

        private void OnEnable()
        {
            target.MeshRebuilt += OnMeshRebuilt;
            target.Changed += RequestRebuild;
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            target.MeshRebuilt -= OnMeshRebuilt;
            target.Changed -= RequestRebuild;
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

        private void OnMeshRebuilt(Mesh mesh)
        {
            rebuildRequested = true;
        }

        private void RequestRebuild()
        {
            rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (rebuildRequested)
            {
                rebuildRequested = false;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();

            Chunk chunk = target.Chunk;
            if (chunk == null)
            {
                Summary = "";
                SetVisible(false);
                return;
            }

            float size = target.VoxelSize;
            switch (chunk.Storage)
            {
                case RunLengthVoxelStorage runs:
                    AddRuns(runs, size);
                    Summary = $"{runs.RunCount:N0} runs along {"XYZ"[runs.RunAxis]}" + (hideAirRuns ? " (air hidden)" : "");
                    break;
                case OctreeVoxelStorage octree:
                    AddOctree(octree, size);
                    Summary = $"{octree.NodeCount:N0} nodes, {octree.BrickCount:N0} bricks of {octree.BrickSize}³, max depth {octree.MaxDepth}";
                    break;
                default:
                    Summary = "Flat storage: one value per sample, nothing to show.";
                    break;
            }

            if (!show || indices.Count == 0)
            {
                SetVisible(false);
                return;
            }

            if (lines == null)
            {
                lines = new LabMeshObject(transform, "Storage View", LineMaterial());
            }
            Mesh mesh = lines.Mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            lines.Visible = true;
        }

        // A bar per run, from its first sample to its last, pulled in a little at each end.
        private void AddRuns(RunLengthVoxelStorage runs, float size)
        {
            Vector3 along = Vector3.zero;
            along[runs.RunAxis] = 1f;
            runs.VisitRuns((first, length, value) =>
            {
                if (hideAirRuns && value == 0f)
                {
                    return;
                }
                Vector3 start = (Vector3)first * size;
                Vector3 end = start + along * ((length - 1) * size);
                Vector3 inset = along * (RunGap * size);
                if (length == 1)
                {
                    // A one-sample run: a short tick so it still shows.
                    start -= inset;
                    end += inset;
                }
                else
                {
                    start += inset;
                    end -= inset;
                }
                AddLine(start, end, ValueColor(value));
            });
        }

        // Every leaf's box (and none of the parents, whose boxes the leaves fill).
        private void AddOctree(OctreeVoxelStorage octree, float size)
        {
            Vector3Int samples = octree.SampleCount;
            octree.VisitNodes((origin, edge, depth, kind) =>
            {
                if (kind == OctreeVoxelStorage.NodeKind.Parent)
                {
                    return;
                }
                // Leaves wholly in the padding outside the samples aren't part of the chunk.
                if (origin.x >= samples.x || origin.y >= samples.y || origin.z >= samples.z)
                {
                    return;
                }
                if (hideAirLeaves && kind == OctreeVoxelStorage.NodeKind.Uniform && octree.GetDensity(origin.x, origin.y, origin.z) == 0f)
                {
                    return;
                }

                Color color = DepthColors[Mathf.Min(depth, DepthColors.Length - 1)];
                if (kind == OctreeVoxelStorage.NodeKind.Uniform)
                {
                    color *= 0.6f;
                }
                // Clip to the sample grid, so boxes don't reach into the padding.
                Vector3 min = (Vector3)origin * size;
                Vector3 max = Vector3.Min((Vector3)(origin + Vector3Int.one * edge), (Vector3)(samples - Vector3Int.one)) * size;
                AddBox(min, max, color);
            });
        }

        private void AddBox(Vector3 min, Vector3 max, Color color)
        {
            for (int corner = 0; corner < MarchingCubes.EdgeCount; corner++)
            {
                Vector3 a = Vector3.Scale(MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[corner, 0]), max - min) + min;
                Vector3 b = Vector3.Scale(MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[corner, 1]), max - min) + min;
                AddLine(a, b, color);
            }
        }

        private void AddLine(Vector3 a, Vector3 b, Color color)
        {
            indices.Add(vertices.Count);
            vertices.Add(a);
            colors.Add(color);
            indices.Add(vertices.Count);
            vertices.Add(b);
            colors.Add(color);
        }

        private static Color ValueColor(float value)
        {
            if (value <= 0f)
            {
                return AirColor;
            }
            return value >= 1f ? SolidColor : SurfaceColor;
        }

        private void SetVisible(bool visible)
        {
            if (lines != null)
            {
                lines.Visible = visible;
            }
        }

        private Material LineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }
            ownedMaterial ??= new Material(Shader.Find(VertexColorShader)) { name = "Storage View" };
            return ownedMaterial;
        }
    }
}
