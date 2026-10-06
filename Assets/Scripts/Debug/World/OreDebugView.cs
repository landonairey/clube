using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Debug
{
    /// <summary>
    /// WorldLab's ore view (O7, O8): marks every ore node near the loaded chunks (a cross at
    /// the centroid, and rings at 1σ, 2σ and 3σ in each axis plane, in the ore's debug colour),
    /// and an X-ray mode that hides the terrain and shows each solid ore sample as a small
    /// cube. Also counts solid samples per material in every loaded chunk for the panel's ore
    /// readout. Reads the world view's public data only (A4).
    /// </summary>
    /// <remarks>
    /// Chunks are scanned once when they load or rebuild and the results kept, so an edit
    /// rescans one chunk, not the world.
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class OreDebugView : MonoBehaviour
    {
        private const int RingSegments = 32;
        private const float CrossSize = 0.5f;

        // Frames the counts keep updating after the panel last read them.
        private const int ScanWhileReadFrames = 30;

        [Tooltip("Mark each ore node: a cross at its centroid.")]
        [SerializeField]
        private bool showNodes;

        [Tooltip("Rings at 1σ, 2σ and 3σ around each node.")]
        [SerializeField]
        private bool showSpread = true;

        [Tooltip("Hide the terrain and show solid ore samples only.")]
        [SerializeField]
        private bool xRay;

        [Tooltip("Size of an X-ray cube, as a share of the voxel size.")]
        [SerializeField, Range(0.1f, 1f)]
        private float cubeSize = 0.5f;

        [Tooltip("Material that shows vertex colours (LabVertexColor).")]
        [SerializeField]
        private Material vertexColorMaterial;

        private readonly Dictionary<Vector3Int, ChunkScan> scans = new Dictionary<Vector3Int, ChunkScan>();
        private readonly HashSet<Vector3Int> dirty = new HashSet<Vector3Int>();
        private readonly List<OreNode> nodes = new List<OreNode>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();
        private readonly int[] loadedCounts = new int[MaterialRegistry.MaxMaterials];

        private WorldView worldView;
        private LabMeshObject nodeLines;
        private LabMeshObject oreCubes;
        private bool nodesChanged = true;
        private bool cubesChanged = true;
        private bool terrainHidden;

        // The last frame something read the counts; scanning stops a while after nobody does.
        private int countsReadFrame = -ScanWhileReadFrames;

        /// <summary>Solid samples per material id over every loaded chunk (border samples count once per chunk).</summary>
        public IReadOnlyList<int> LoadedCounts
        {
            get
            {
                countsReadFrame = Time.frameCount;
                return loadedCounts;
            }
        }

        public bool ShowNodes
        {
            get => showNodes;
            set => Set(ref showNodes, value);
        }

        public bool ShowSpread
        {
            get => showSpread;
            set => Set(ref showSpread, value);
        }

        public bool XRay
        {
            get => xRay;
            set => Set(ref xRay, value);
        }

        /// <summary>The ore nodes reaching the loaded area, as last drawn.</summary>
        public int NodeCount => nodes.Count;

        /// <summary>Solid samples per material id in one loaded chunk, or null if it hasn't been scanned.</summary>
        public IReadOnlyList<int> CountsFor(Vector3Int coord)
        {
            countsReadFrame = Time.frameCount;
            return scans.TryGetValue(coord, out ChunkScan scan) ? scan.Counts : null;
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void OnEnable()
        {
            worldView.ChunkLoaded += OnChunkLoaded;
            worldView.ChunkUnloaded += OnChunkUnloaded;
            worldView.ChunkMeshed += OnChunkMeshed;
            if (worldView.World != null)
            {
                foreach (Vector3Int coord in worldView.World.Chunks.Keys)
                {
                    OnChunkLoaded(coord);
                }
            }
        }

        private void OnDisable()
        {
            worldView.ChunkLoaded -= OnChunkLoaded;
            worldView.ChunkUnloaded -= OnChunkUnloaded;
            worldView.ChunkMeshed -= OnChunkMeshed;
            scans.Clear();
            dirty.Clear();
            SetTerrainHidden(false);
            if (nodeLines != null)
            {
                nodeLines.Visible = false;
            }
            if (oreCubes != null)
            {
                oreCubes.Visible = false;
            }
        }

        private void OnDestroy()
        {
            nodeLines?.Dispose();
            oreCubes?.Dispose();
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            nodesChanged = true;
            cubesChanged = true;
        }

        private void LateUpdate()
        {
            if (worldView.World == null)
            {
                return;
            }

            // Scanning reads every sample of a chunk, so only while the counts or the X-ray are in use.
            bool scanning = xRay || Time.frameCount - countsReadFrame < ScanWhileReadFrames;
            if (dirty.Count > 0 && scanning)
            {
                ScanDirtyChunks();
                cubesChanged = true;
            }
            // Collecting nodes walks the cells under the whole loaded area: only while they show or are counted.
            if (nodesChanged && (showNodes || scanning))
            {
                nodesChanged = false;
                RebuildNodes();
            }
            if (cubesChanged)
            {
                cubesChanged = false;
                RebuildCubes();
            }

            SetTerrainHidden(xRay);
            // New chunks load visible; keep them hidden while in X-ray.
            if (terrainHidden)
            {
                foreach (ChunkRenderer chunkRenderer in worldView.Renderers.Values)
                {
                    chunkRenderer.Renderer.enabled = false;
                }
            }
        }

        private void OnChunkLoaded(Vector3Int coord)
        {
            dirty.Add(coord);
            nodesChanged = true;
        }

        // A rebuilt mesh means the chunk may have been edited: count it again.
        private void OnChunkMeshed(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            dirty.Add(coord);
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            scans.Remove(coord);
            dirty.Remove(coord);
            nodesChanged = true;
            cubesChanged = true;
        }

        private void ScanDirtyChunks()
        {
            MaterialRegistry registry = worldView.Config.Materials;
            var wanted = new bool[MaterialRegistry.MaxMaterials];
            if (registry != null)
            {
                foreach (VoxelMaterial material in registry.Materials)
                {
                    if (material != null && material.Category == VoxelMaterialCategory.Ore)
                    {
                        wanted[material.Id] = true;
                    }
                }
            }

            float iso = worldView.Config.IsoLevel;
            foreach (Vector3Int coord in dirty)
            {
                if (!worldView.World.TryGetChunk(coord, out Chunk chunk))
                {
                    continue;
                }
                if (!scans.TryGetValue(coord, out ChunkScan scan))
                {
                    scan = new ChunkScan();
                    scans.Add(coord, scan);
                }
                MaterialCensus.Count(chunk, iso, scan.Counts, wanted, scan.OreSamples);
                scan.OreMaterials.Clear();
                foreach (Vector3Int sample in scan.OreSamples)
                {
                    scan.OreMaterials.Add(chunk.GetMaterial(sample));
                }
            }
            dirty.Clear();

            System.Array.Clear(loadedCounts, 0, loadedCounts.Length);
            foreach (ChunkScan scan in scans.Values)
            {
                for (int i = 0; i < loadedCounts.Length; i++)
                {
                    loadedCounts[i] += scan.Counts[i];
                }
            }
        }

        // Crosses at the centroids and rings at 1σ, 2σ, 3σ, as one line mesh.
        private void RebuildNodes()
        {
            OreField ores = worldView.Ores;
            nodes.Clear();
            if (ores != null && worldView.World.LoadedCount > 0)
            {
                ores.CollectNodes(LoadedBounds(), nodes);
            }

            if (!showNodes || nodes.Count == 0)
            {
                if (nodeLines != null)
                {
                    nodeLines.Visible = false;
                }
                return;
            }

            nodeLines ??= new LabMeshObject(transform, "Ore Nodes", vertexColorMaterial);
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            MaterialRegistry registry = worldView.Config.Materials;
            foreach (OreNode node in nodes)
            {
                Color32 color = ColorOf(registry, node.Material);
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 direction = Vector3.zero;
                    direction[axis] = CrossSize;
                    AddLine(node.Centre - direction, node.Centre + direction, color);
                }
                if (showSpread)
                {
                    for (int sigmas = 1; sigmas <= 3; sigmas++)
                    {
                        Color32 ringColor = color;
                        ringColor.a = (byte)(255 / sigmas);
                        AddRings(node.Centre, node.Spread * sigmas, ringColor);
                    }
                }
            }

            Mesh mesh = nodeLines.Mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            nodeLines.Visible = true;
        }

        // A cube at every solid ore sample, as one triangle mesh.
        private void RebuildCubes()
        {
            if (!xRay)
            {
                if (oreCubes != null)
                {
                    oreCubes.Visible = false;
                }
                return;
            }

            oreCubes ??= new LabMeshObject(transform, "Ore Samples", vertexColorMaterial);
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            MaterialRegistry registry = worldView.Config.Materials;
            float voxelSize = worldView.Config.VoxelSize;
            float half = voxelSize * cubeSize * 0.5f;
            foreach (KeyValuePair<Vector3Int, ChunkScan> entry in scans)
            {
                Vector3 origin = worldView.World.Grid.ChunkOrigin(entry.Key);
                ChunkScan scan = entry.Value;
                for (int i = 0; i < scan.OreSamples.Count; i++)
                {
                    AddCube(origin + (Vector3)scan.OreSamples[i] * voxelSize, half, ColorOf(registry, scan.OreMaterials[i]));
                }
            }

            Mesh mesh = oreCubes.Mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            oreCubes.Visible = true;
        }

        private Bounds LoadedBounds()
        {
            var bounds = new Bounds();
            bool first = true;
            Vector3 size = worldView.World.Grid.ChunkWorldSize;
            foreach (Vector3Int coord in worldView.World.Chunks.Keys)
            {
                var chunkBounds = new Bounds(worldView.World.Grid.ChunkOrigin(coord) + size * 0.5f, size);
                if (first)
                {
                    bounds = chunkBounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(chunkBounds);
                }
            }
            return bounds;
        }

        private void AddLine(Vector3 from, Vector3 to, Color32 color)
        {
            indices.Add(vertices.Count);
            vertices.Add(from);
            colors.Add(color);
            indices.Add(vertices.Count);
            vertices.Add(to);
            colors.Add(color);
        }

        // An ellipse in each axis plane, with the node's σ per axis as radii.
        private void AddRings(Vector3 centre, Vector3 radii, Color32 color)
        {
            for (int plane = 0; plane < 3; plane++)
            {
                int a = plane;
                int b = (plane + 1) % 3;
                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= RingSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / RingSegments;
                    Vector3 point = centre;
                    point[a] += Mathf.Cos(angle) * radii[a];
                    point[b] += Mathf.Sin(angle) * radii[b];
                    if (i > 0)
                    {
                        AddLine(previous, point, color);
                    }
                    previous = point;
                }
            }
        }

        private void AddCube(Vector3 centre, float half, Color32 color)
        {
            int start = vertices.Count;
            for (int corner = 0; corner < 8; corner++)
            {
                vertices.Add(centre + new Vector3((corner & 1) == 0 ? -half : half, (corner & 2) == 0 ? -half : half, (corner & 4) == 0 ? -half : half));
                colors.Add(color);
            }
            foreach (int index in CubeTriangles)
            {
                indices.Add(start + index);
            }
        }

        // Corner bits: x = 1, y = 2, z = 4; clockwise from outside, as Unity's front faces are.
        private static readonly int[] CubeTriangles =
        {
            0, 2, 3, 0, 3, 1, // -z
            4, 5, 7, 4, 7, 6, // +z
            0, 4, 6, 0, 6, 2, // -x
            1, 3, 7, 1, 7, 5, // +x
            0, 1, 5, 0, 5, 4, // -y
            2, 6, 7, 2, 7, 3, // +y
        };

        private static Color32 ColorOf(MaterialRegistry registry, byte id)
        {
            VoxelMaterial material = registry != null ? registry.Get(id) : null;
            return material != null ? material.DebugColor : Color.magenta;
        }

        private void SetTerrainHidden(bool hidden)
        {
            if (hidden == terrainHidden)
            {
                return;
            }
            terrainHidden = hidden;
            foreach (ChunkRenderer chunkRenderer in GetComponentsInChildren<ChunkRenderer>(true))
            {
                chunkRenderer.Renderer.enabled = !hidden;
            }
        }

        private void Set(ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }
            field = value;
            nodesChanged = true;
            cubesChanged = true;
        }

        /// <summary>What one chunk's scan found.</summary>
        private sealed class ChunkScan
        {
            public readonly int[] Counts = new int[MaterialRegistry.MaxMaterials];
            public readonly List<Vector3Int> OreSamples = new List<Vector3Int>();
            public readonly List<byte> OreMaterials = new List<byte>();
        }
    }
}
