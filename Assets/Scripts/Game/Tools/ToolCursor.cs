using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// The tool cursor (GL4, first pass of TL3): a small cross lying on the terrain mesh at
    /// every triangle corner the next hit can move. The held tool's reach gives the solid
    /// corners (samples) it will hit; each one controls the mesh vertices on its edges to air
    /// (<see cref="SurfacePoints"/>), and those are where the crosses go, flat on the surface.
    /// Each cross warms from <see cref="idleColor"/> towards <see cref="damagedColor"/> as
    /// its sample takes damage, showing how close it is to breaking.
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

        [Tooltip("Length of each arm of a cross, as a fraction of the voxel size.")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float armLength = 0.2f;

        private readonly List<SurfacePoint> surfacePoints = new List<SurfacePoint>();
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

        // After the tool user's Update has found this frame's targets.
        private void LateUpdate()
        {
            WorldView view = tools.WorldView;
            bool show = material != null && tools.Current != null && tools.Targets.Count > 0 && view != null && view.IsReady;
            markers.SetActive(show);
            if (!show)
            {
                return;
            }

            World world = view.World;
            ChunkMeshSettings settings = view.Config.MeshSettings;
            float arm = world.Grid.VoxelSize * armLength;
            points.Clear();
            colors.Clear();
            foreach (Vector3Int sample in tools.Targets)
            {
                surfacePoints.Clear();
                SurfacePoints.ForSample(world, sample, settings, surfacePoints);
                if (surfacePoints.Count == 0)
                {
                    continue;
                }
                Color color = Color.Lerp(idleColor, damagedColor, DamageFraction(view, sample));
                foreach (SurfacePoint point in surfacePoints)
                {
                    AddCross(point, arm, color);
                }
            }

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

        // Two short lines crossing at the point, in the surface's plane.
        private void AddCross(SurfacePoint point, float arm, Color color)
        {
            Vector3 n = point.Normal;
            Vector3 other = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(n, other).normalized * arm;
            Vector3 v = Vector3.Cross(n, u.normalized) * arm;
            points.Add(point.Position - u);
            points.Add(point.Position + u);
            points.Add(point.Position - v);
            points.Add(point.Position + v);
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
            }
        }

        // How far a sample is towards breaking, 0-1.
        private float DamageFraction(WorldView view, Vector3Int sample)
        {
            float damage = tools.Damage.Get(sample);
            if (damage <= 0f)
            {
                return 0f;
            }
            byte? id = view.World.GetMaterial(sample);
            VoxelMaterial voxelMaterial = id.HasValue && view.Config.Materials != null ? view.Config.Materials.Get(id.Value) : null;
            float hardness = voxelMaterial != null ? voxelMaterial.Hardness : 1f;
            return hardness > 0f ? Mathf.Clamp01(damage / hardness) : 1f;
        }
    }
}
