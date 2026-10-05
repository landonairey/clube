using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Shows a build grid on the terrain surface (M22): lines every <see cref="CellSize"/>
    /// metres (1 m by default, the usual voxel-game build cell), so the marching-cubes voxel
    /// size can be judged against the size things will be built at. The terrain renderers
    /// carry the <c>BuildGridOverlay</c> material (<c>Clube/Build Grid Overlay</c>); this sets
    /// its global shader values, and with the grid off it draws nothing.
    /// </summary>
    public class BuildGridOverlay : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_ClubeBuildGridOpacity");
        private static readonly int CellSizeId = Shader.PropertyToID("_ClubeBuildGridCellSize");
        private static readonly int LineWidthId = Shader.PropertyToID("_ClubeBuildGridLineWidth");
        private static readonly int HeightLinesId = Shader.PropertyToID("_ClubeBuildGridHeightLines");
        private static readonly int ColorId = Shader.PropertyToID("_ClubeBuildGridColor");

        public const float MinCellSize = 0.25f;
        public const float MaxCellSize = 4f;

        [Tooltip("Draw the build grid on the terrain.")]
        [SerializeField]
        private bool show;

        [Tooltip("Size of one build cell in metres (1 m is typical: a player is about two cells tall).")]
        [SerializeField, Range(MinCellSize, MaxCellSize)]
        private float cellSize = 1f;

        [Tooltip("Also draw where the surface crosses each cell's floor height (contour lines).")]
        [SerializeField]
        private bool heightLines = true;

        [Tooltip("Line width in pixels.")]
        [SerializeField, Range(0.5f, 4f)]
        private float lineWidth = 1.5f;

        [SerializeField, Range(0f, 1f)]
        private float opacity = 0.8f;

        [SerializeField]
        private Color color = new Color(0.2f, 0.9f, 1f);

        public bool Show
        {
            get => show;
            set
            {
                show = value;
                Apply();
            }
        }

        /// <summary>Build cell size in metres.</summary>
        public float CellSize
        {
            get => cellSize;
            set
            {
                cellSize = Mathf.Clamp(value, MinCellSize, MaxCellSize);
                Apply();
            }
        }

        public bool HeightLines
        {
            get => heightLines;
            set
            {
                heightLines = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(OpacityId, 0f);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            Shader.SetGlobalFloat(OpacityId, show && isActiveAndEnabled ? opacity : 0f);
            Shader.SetGlobalFloat(CellSizeId, cellSize);
            Shader.SetGlobalFloat(LineWidthId, lineWidth);
            Shader.SetGlobalFloat(HeightLinesId, heightLines ? 1f : 0f);
            Shader.SetGlobalColor(ColorId, color);
        }
    }
}
