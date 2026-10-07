using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shows a local patch of the build grid on the terrain while building (GL23): the cell
    /// being aimed at lit, the grid lines fading out in a circle around it, and nothing
    /// otherwise. Drives the <c>Clube/Build Grid Overlay</c> material the terrain chunks carry,
    /// through its global shader values (the lab's <c>BuildGridOverlay</c> uses the same ones
    /// to show the whole grid).
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

        public bool IsShown { get; private set; }

        /// <summary>
        /// Shows the grid around a cell: <paramref name="cellMin"/> is its (x, z) corner in world
        /// space and <paramref name="cellSize"/> its side.
        /// </summary>
        public void Show(Vector2 cellMin, float cellSize)
        {
            IsShown = true;
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
    }
}
