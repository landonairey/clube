using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// The tool cursor (GL4, first pass of TL3): outlines the surface mesh inside the voxels
    /// the held tool reaches (a single voxel for the hand, 3×3×3 for the pickaxe), so the
    /// player sees exactly which part of the ground a hit will work on. The outline is the
    /// mesh's own triangle edges (<see cref="SurfaceOutline"/>), drawn just in front of the
    /// surface. Each voxel's lines warm from <see cref="idleColor"/> towards
    /// <see cref="damagedColor"/> as its corners take damage, showing how close it is to
    /// breaking.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class ToolCursor : MonoBehaviour
    {
        [Tooltip("An unlit vertex-colour line material drawn over the surface (ToolCursorLines).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color idleColor = new Color(1f, 0.95f, 0.7f, 0.9f);

        [SerializeField]
        private Color damagedColor = new Color(1f, 0.4f, 0.05f, 1f);

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private PlayerToolUser tools;
        private GameObject outline;
        private UnityEngine.Mesh mesh;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            mesh = new UnityEngine.Mesh { name = "Tool Cursor" };
            mesh.MarkDynamic();

            outline = new GameObject("Tool Cursor");
            outline.AddComponent<MeshFilter>().sharedMesh = mesh;
            var outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = material;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outline.SetActive(false);
        }

        private void OnDisable()
        {
            if (outline != null)
            {
                outline.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Destroy(outline);
            Destroy(mesh);
        }

        // After the tool user's Update has found this frame's reach.
        private void LateUpdate()
        {
            WorldView view = tools.WorldView;
            bool show = material != null && tools.Current != null && tools.HasReach && view != null && view.IsReady;
            outline.SetActive(show);
            if (!show)
            {
                return;
            }

            World world = view.World;
            ChunkMeshSettings settings = view.Config.MeshSettings;
            VoxelBox box = tools.Reach;
            points.Clear();
            colors.Clear();
            for (int z = box.Min.z; z <= box.Max.z; z++)
            {
                for (int y = box.Min.y; y <= box.Max.y; y++)
                {
                    for (int x = box.Min.x; x <= box.Max.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        int before = points.Count;
                        SurfaceOutline.AddEdges(world, voxel, settings, points);
                        if (points.Count == before)
                        {
                            continue;
                        }
                        Color color = Color.Lerp(idleColor, damagedColor, DamageFraction(view, voxel));
                        for (int i = before; i < points.Count; i++)
                        {
                            colors.Add(color);
                        }
                    }
                }
            }

            // The points are in the world view's space, so the outline sits where it does.
            outline.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            outline.transform.localScale = view.transform.lossyScale;
            indices.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                indices.Add(i);
            }
            mesh.Clear();
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        // How far the voxel's most damaged corner is towards breaking, 0-1.
        private float DamageFraction(WorldView view, Vector3Int voxel)
        {
            float most = 0f;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3Int sample = voxel + MarchingCubes.CornerOffset(corner);
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
