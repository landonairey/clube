using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shows a local patch of the build grid on the terrain while building (GL23): the cell
    /// being aimed at lit, the grid lines fading out in a circle around it, and nothing
    /// otherwise. Drives the <c>Clube/Build Grid Overlay</c> material the terrain chunks carry,
    /// through its global shader values (the lab's <c>BuildGridOverlay</c> uses the same ones
    /// to show the whole grid). On ground too steep to build on (GL84) the grid instead floats level
    /// at the placement height, as a hologram, so it doesn't run up the slope.
    /// </summary>
    public class BuildGridDisplay : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_ClubeBuildGridOpacity");
        private static readonly int CellSizeId = Shader.PropertyToID("_ClubeBuildGridCellSize");
        private static readonly int LineWidthId = Shader.PropertyToID("_ClubeBuildGridLineWidth");
        private static readonly int ColorId = Shader.PropertyToID("_ClubeBuildGridColor");
        private static readonly int FocusId = Shader.PropertyToID("_ClubeBuildGridFocus");
        private static readonly int HighlightId = Shader.PropertyToID("_ClubeBuildGridHighlight");

        [Tooltip("How far the grid shows around the aimed cell before it has faded out, in metres.")]
        [SerializeField, Min(0.5f)]
        private float radius = 2.5f;

        [Tooltip("Line width in pixels.")]
        [SerializeField, Range(0.5f, 4f)]
        private float lineWidth = 1.5f;

        [SerializeField, Range(0f, 1f)]
        private float opacity = 0.9f;

        [SerializeField]
        private Color color = new Color(0.55f, 0.95f, 1f);

        [Tooltip("The aimed cell's outline in the floating grid when it can't be built on.")]
        [SerializeField]
        private Color blockedColor = new Color(1f, 0.3f, 0.25f);

        [Tooltip("A transparent material that draws vertex colours (LabVertexColorTransparent), for the floating grid.")]
        [SerializeField]
        private Material hologramMaterial;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private GameObject hologram;
        private Mesh hologramMesh;

        public bool IsShown { get; private set; }

        /// <summary>
        /// Shows the grid around a cell: <paramref name="cellMin"/> is its (x, z) corner in world
        /// space and <paramref name="cellSize"/> its side.
        /// </summary>
        public void Show(Vector2 cellMin, float cellSize)
        {
            IsShown = true;
            SetHologramVisible(false);
            Vector2 centre = cellMin + Vector2.one * (cellSize * 0.5f);
            Shader.SetGlobalFloat(OpacityId, opacity);
            Shader.SetGlobalFloat(CellSizeId, cellSize);
            Shader.SetGlobalFloat(LineWidthId, lineWidth);
            Shader.SetGlobalColor(ColorId, color);
            Shader.SetGlobalVector(FocusId, new Vector4(centre.x, centre.y, radius, 0f));
            Shader.SetGlobalVector(HighlightId, new Vector4(cellMin.x, cellMin.y, cellSize, 1f));
        }

        public void Hide()
        {
            SetHologramVisible(false);
            if (!IsShown)
            {
                return;
            }
            IsShown = false;
            Shader.SetGlobalFloat(OpacityId, 0f);
            Shader.SetGlobalVector(FocusId, Vector4.zero);
            Shader.SetGlobalVector(HighlightId, Vector4.zero);
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Destroy(hologram);
            Destroy(hologramMesh);
        }

        /// <summary>
        /// Shows the grid around a cell as a level hologram at a height (world space), instead of on
        /// the terrain (GL84): for steep ground. The aimed cell is outlined, in the blocked colour
        /// when <paramref name="allowed"/> is false.
        /// </summary>
        public void ShowFloating(Vector2 cellMin, float cellSize, float height, bool allowed)
        {
            // The terrain patch is off; only the hologram shows.
            Hide();
            IsShown = true;
            if (hologramMaterial == null)
            {
                return;
            }
            if (hologram == null)
            {
                hologram = new GameObject("Build grid hologram");
                hologramMesh = new Mesh { name = "Build grid hologram" };
                hologram.AddComponent<MeshFilter>().sharedMesh = hologramMesh;
                var meshRenderer = hologram.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = hologramMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hologram.SetActive(true);

            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            Vector2 centre = cellMin + Vector2.one * (cellSize * 0.5f);
            float y = height + 0.03f;
            float half = 0.015f;
            int lines = Mathf.CeilToInt(radius / cellSize) + 1;
            for (int i = -lines; i <= lines + 1; i++)
            {
                // Lines along z at x = constant, and along x at z = constant, each faded by distance.
                float x = cellMin.x + i * cellSize;
                float z = cellMin.y + i * cellSize;
                AddLine(new Vector3(x, y, centre.y - radius), new Vector3(x, y, centre.y + radius), new Vector3(half, 0f, 0f), centre);
                AddLine(new Vector3(centre.x - radius, y, z), new Vector3(centre.x + radius, y, z), new Vector3(0f, 0f, half), centre);
            }
            // The aimed cell's outline, brighter and wider.
            Color outline = allowed ? color : blockedColor;
            var min = new Vector3(cellMin.x, y + 0.005f, cellMin.y);
            var max = new Vector3(cellMin.x + cellSize, y + 0.005f, cellMin.y + cellSize);
            AddQuadStrip(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(0f, 0f, 0.04f), outline, outline);
            AddQuadStrip(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(0f, 0f, 0.04f), outline, outline);
            AddQuadStrip(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(0.04f, 0f, 0f), outline, outline);
            AddQuadStrip(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(0.04f, 0f, 0f), outline, outline);

            hologramMesh.Clear();
            hologramMesh.SetVertices(vertices);
            hologramMesh.SetColors(colors);
            hologramMesh.SetTriangles(triangles, 0);
            hologramMesh.RecalculateBounds();
        }

        // A grid line in a few pieces, each as opaque as it is near the centre.
        private void AddLine(Vector3 from, Vector3 to, Vector3 halfWidth, Vector2 centre)
        {
            const int pieces = 8;
            for (int i = 0; i < pieces; i++)
            {
                Vector3 a = Vector3.Lerp(from, to, i / (float)pieces);
                Vector3 b = Vector3.Lerp(from, to, (i + 1) / (float)pieces);
                AddQuadStrip(a, b, halfWidth, Faded(a, centre), Faded(b, centre));
            }
        }

        private Color Faded(Vector3 point, Vector2 centre)
        {
            float distance = Vector2.Distance(new Vector2(point.x, point.z), centre);
            return new Color(color.r, color.g, color.b, opacity * Mathf.Clamp01(1f - distance / radius));
        }

        // A flat strip from one point to another, its width across; drawn from both sides.
        private void AddQuadStrip(Vector3 from, Vector3 to, Vector3 halfWidth, Color fromColor, Color toColor)
        {
            int first = vertices.Count;
            vertices.Add(from - halfWidth);
            vertices.Add(from + halfWidth);
            vertices.Add(to + halfWidth);
            vertices.Add(to - halfWidth);
            colors.Add(fromColor);
            colors.Add(fromColor);
            colors.Add(toColor);
            colors.Add(toColor);
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }

        private void SetHologramVisible(bool visible)
        {
            if (hologram != null)
            {
                hologram.SetActive(visible);
            }
        }
    }
}
