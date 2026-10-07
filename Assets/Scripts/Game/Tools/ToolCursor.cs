using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// The tool cursor (GL18, second pass of TL3): four small right-angle brackets lying on
    /// the terrain at the corners of the held tool's area of impact, so the hand and the
    /// pickaxe both show just the outline of what they reach. The reach box's face towards
    /// the player (across the axis they look along most) gives the four corners; each corner,
    /// and the ends of its two arms, are dropped onto the surface along that axis, so the
    /// brackets follow the ground's slope. They warm from <see cref="idleColor"/> towards
    /// <see cref="damagedColor"/> with the most damaged sample in reach, showing how close the
    /// next hits are to breaking it.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class ToolCursor : MonoBehaviour
    {
        [Tooltip("An unlit vertex-colour line material drawn over the surface (ToolCursorLines).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color idleColor = new Color(1f, 0.95f, 0.7f, 0.95f);

        [SerializeField]
        private Color damagedColor = new Color(1f, 0.35f, 0.05f, 1f);

        [Tooltip("Length of each arm of a bracket, as a fraction of the area's side.")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float armLength = 0.25f;

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private PlayerToolUser tools;
        private GameObject markers;
        private UnityEngine.Mesh mesh;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            mesh = new UnityEngine.Mesh { name = "Tool Cursor" };
            mesh.MarkDynamic();

            markers = new GameObject("Tool Cursor");
            markers.AddComponent<MeshFilter>().sharedMesh = mesh;
            var markerRenderer = markers.AddComponent<MeshRenderer>();
            markerRenderer.sharedMaterial = material;
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

            points.Clear();
            colors.Clear();
            AddBrackets(view.World, view.Config.MeshSettings, tools.Reach, tools.AimDirection, Color.Lerp(idleColor, damagedColor, MostDamaged(view)));

            // The points are in the world view's space, so the markers sit where it does.
            markers.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            markers.transform.localScale = view.transform.lossyScale;
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

        // A bracket at each corner of the reach box's face across the axis looked along most.
        private void AddBrackets(World world, ChunkMeshSettings settings, VoxelBox reach, Vector3 aim, Color color)
        {
            int axis = Mathf.Abs(aim.x) >= Mathf.Abs(aim.y) && Mathf.Abs(aim.x) >= Mathf.Abs(aim.z) ? 0
                : Mathf.Abs(aim.y) >= Mathf.Abs(aim.z) ? 1 : 2;
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;
            float sign = aim[axis] >= 0f ? 1f : -1f;

            Vector3 min = world.Grid.SampleToWorld(reach.MinSample);
            Vector3 max = world.Grid.SampleToWorld(reach.MaxSample);
            Vector3 size = max - min;
            float arm = Mathf.Min(size[u], size[v]) * armLength;

            // Rays run along the axis, from the player's side of the box, through it and a voxel past.
            float voxel = world.Grid.VoxelSize;
            float start = sign > 0f ? min[axis] - voxel : max[axis] + voxel;
            float length = size[axis] + voxel * 2f;
            Vector3 direction = Vector3.zero;
            direction[axis] = sign;

            for (int corner = 0; corner < 4; corner++)
            {
                bool highU = (corner & 1) != 0;
                bool highV = (corner & 2) != 0;
                Vector3 at = Vector3.zero;
                at[u] = highU ? max[u] : min[u];
                at[v] = highV ? max[v] : min[v];
                at[axis] = start;

                // Arms run inward along the face's two axes.
                Vector3 alongU = Vector3.zero;
                alongU[u] = highU ? -arm : arm;
                Vector3 alongV = Vector3.zero;
                alongV[v] = highV ? -arm : arm;

                if (!Drop(world, settings, at, direction, length, out Vector3 tip))
                {
                    continue;
                }
                AddArm(world, settings, tip, at + alongU, direction, length, color);
                AddArm(world, settings, tip, at + alongV, direction, length, color);
            }
        }

        // One arm from the corner to its end, the end dropped onto the surface too (or left in
        // the corner's plane when the ground falls away there).
        private void AddArm(World world, ChunkMeshSettings settings, Vector3 tip, Vector3 from, Vector3 direction, float length, Color color)
        {
            if (!Drop(world, settings, from, direction, length, out Vector3 end))
            {
                end = from;
                int axis = direction.x != 0f ? 0 : direction.y != 0f ? 1 : 2;
                end[axis] = tip[axis];
            }
            points.Add(tip);
            points.Add(end);
            colors.Add(color);
            colors.Add(color);
        }

        private static bool Drop(World world, ChunkMeshSettings settings, Vector3 from, Vector3 direction, float length, out Vector3 point)
        {
            if (world.Raycast(new Ray(from, direction), settings, length, out WorldHit hit)
                && Vector3.Dot(hit.Point - from, direction) <= length)
            {
                point = hit.Point;
                return true;
            }
            point = default;
            return false;
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
