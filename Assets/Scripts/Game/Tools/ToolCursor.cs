using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// The tool cursor (GL24, third pass of TL3): shades the surface mesh's triangles inside
    /// every voxel the held tool reaches, and traces their edges, so the hand shows the one
    /// voxel's patch of ground and the pickaxe its ball of 32. Each voxel is polygonised from
    /// the world's densities exactly as the mesher does it (<see cref="MarchingCubes"/>, with
    /// the world's edge placement), so the shading lies on the surface. It warms from
    /// <see cref="idleColor"/> towards <see cref="damagedColor"/> with the most damaged sample
    /// in reach, showing how close the next hits are to breaking it.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class ToolCursor : MonoBehaviour
    {
        [Tooltip("An unlit vertex-colour material drawn over the surface (ToolCursorLines); used for the faces and their edges.")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color idleColor = new Color(1f, 0.95f, 0.7f, 0.95f);

        [SerializeField]
        private Color damagedColor = new Color(1f, 0.35f, 0.05f, 1f);

        [Tooltip("How opaque the shaded faces are; the edges use the full colour.")]
        [SerializeField, Range(0f, 1f)]
        private float faceOpacity = 0.3f;

        private readonly List<Vector3> voxelVertices = new List<Vector3>();
        private readonly List<int> voxelTriangles = new List<int>();
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> faces = new List<int>();
        private readonly List<int> edges = new List<int>();
        private readonly float[] corners = new float[MarchingCubes.CornerCount];
        private PlayerToolUser tools;
        private GameObject markers;
        private UnityEngine.Mesh mesh;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            mesh = new UnityEngine.Mesh { name = "Tool Cursor", subMeshCount = 2 };
            mesh.MarkDynamic();

            markers = new GameObject("Tool Cursor");
            markers.AddComponent<MeshFilter>().sharedMesh = mesh;
            var markerRenderer = markers.AddComponent<MeshRenderer>();
            markerRenderer.sharedMaterials = new[] { material, material };
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            markers.SetActive(false);
        }

        private void OnDisable()
        {
            if (markers != null)
            {
                markers.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Destroy(markers);
            Destroy(mesh);
        }

        // After the tool user's Update has found this frame's reach.
        private void LateUpdate()
        {
            WorldView view = tools.WorldView;
            bool show = material != null && tools.Current != null && tools.HasReach && tools.Targets.Count > 0 && view != null && view.IsReady;
            markers.SetActive(show);
            if (!show)
            {
                return;
            }

            Color edge = Color.Lerp(idleColor, damagedColor, MostDamaged(view));
            Color face = new Color(edge.r, edge.g, edge.b, edge.a * faceOpacity);
            points.Clear();
            colors.Clear();
            faces.Clear();
            edges.Clear();
            AddReach(view.World, view.Config.MeshSettings, tools.Reach, face, edge);

            // The points are in the world view's space, so the markers sit where it does.
            markers.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            markers.transform.localScale = view.transform.lossyScale;
            mesh.Clear();
            mesh.subMeshCount = 2;
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetIndices(faces, MeshTopology.Triangles, 0);
            mesh.SetIndices(edges, MeshTopology.Lines, 1);
        }

        // Every reached voxel's surface triangles: each corner twice, a face colour for the
        // shading and an edge colour for the outline.
        private void AddReach(World world, ChunkMeshSettings settings, VoxelReach reach, Color face, Color edge)
        {
            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            var writer = new FlatVertexWriter(voxelVertices);
            float voxelSize = world.Grid.VoxelSize;
            VoxelBox box = reach.Box;
            for (int z = box.Min.z; z <= box.Max.z; z++)
            {
                for (int y = box.Min.y; y <= box.Max.y; y++)
                {
                    for (int x = box.Min.x; x <= box.Max.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        if (!reach.Contains(voxel) || !ReadCorners(world, voxel))
                        {
                            continue;
                        }
                        voxelVertices.Clear();
                        voxelTriangles.Clear();
                        MarchingCubes.Polygonise(
                            corners, settings.IsoLevel, world.Grid.SampleToWorld(voxel), voxelSize, placer, writer, voxelTriangles);
                        for (int i = 0; i + 2 < voxelTriangles.Count; i += 3)
                        {
                            AddTriangle(voxelVertices[voxelTriangles[i]], voxelVertices[voxelTriangles[i + 1]], voxelVertices[voxelTriangles[i + 2]], face, edge);
                        }
                    }
                }
            }
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color face, Color edge)
        {
            int first = points.Count;
            points.Add(a);
            points.Add(b);
            points.Add(c);
            colors.Add(face);
            colors.Add(face);
            colors.Add(face);
            faces.Add(first);
            faces.Add(first + 1);
            faces.Add(first + 2);

            int outline = points.Count;
            points.Add(a);
            points.Add(b);
            points.Add(c);
            colors.Add(edge);
            colors.Add(edge);
            colors.Add(edge);
            edges.Add(outline);
            edges.Add(outline + 1);
            edges.Add(outline + 1);
            edges.Add(outline + 2);
            edges.Add(outline + 2);
            edges.Add(outline);
        }

        // A voxel's 8 corner densities; false when any is unloaded.
        private bool ReadCorners(World world, Vector3Int voxel)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float? density = world.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                if (!density.HasValue)
                {
                    return false;
                }
                corners[corner] = density.Value;
            }
            return true;
        }

        // How far the most damaged sample in reach is towards breaking, 0-1.
        private float MostDamaged(WorldView view)
        {
            float most = 0f;
            foreach (Vector3Int sample in tools.Targets)
            {
                float damage = tools.Damage.Get(sample);
                if (damage <= 0f)
                {
                    continue;
                }
                byte? id = view.World.GetMaterial(sample);
                VoxelMaterial voxelMaterial = id.HasValue && view.Config.Materials != null ? view.Config.Materials.Get(id.Value) : null;
                float hardness = voxelMaterial != null ? voxelMaterial.Hardness : 1f;
                most = Mathf.Max(most, hardness > 0f ? Mathf.Clamp01(damage / hardness) : 1f);
            }
            return most;
        }
    }
}
