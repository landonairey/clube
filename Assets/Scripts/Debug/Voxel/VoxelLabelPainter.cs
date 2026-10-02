using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the voxel lab's teaching labels with IMGUI, given a way to turn world
    /// points into GUI points:
    /// <list type="bullet">
    /// <item>V21: corners labelled c0-c7 (green when solid), and a table above the
    /// cube lining each corner up with the bit it sets in the case index.</item>
    /// <item>V9: the edges the surface crosses, highlighted.</item>
    /// <item>Edge labels: all 12 edges numbered, crossed ones in the highlight colour.</item>
    /// </list>
    /// Shared by the Game view (<see cref="VoxelLabels"/>, runtime OnGUI) and the
    /// Scene view (an editor overlay), so both show exactly the same thing. Drawn
    /// in screen space, so text stays sharp and gizmo culling doesn't apply.
    /// </summary>
    public static class VoxelLabelPainter
    {
        /// <summary>Projects a world point to GUI coordinates; false when it is behind the camera.</summary>
        public delegate bool WorldToGui(Vector3 world, out Vector2 gui);

        private const float CrossedEdgeThickness = 4f;

        // Case table layout, in GUI pixels.
        private const float TablePadding = 6f;
        private const float RowLabelWidth = 48f;
        private const float CellWidth = 30f;
        private const float RowHeight = 18f;
        private const int TableRows = 3;

        private static readonly Color SolidColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color EmptyColor = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color MutedColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color CrossedEdgeColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color TableBackground = new Color(0f, 0f, 0f, 0.7f);

        // The editor and the player use different GUI skins, so styles are rebuilt
        // whenever the active skin changes.
        private static GUISkin stylesSkin;
        private static GUIStyle solidCornerStyle;
        private static GUIStyle emptyCornerStyle;
        private static GUIStyle edgeStyle;
        private static GUIStyle mutedEdgeStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle rowLabelStyle;
        private static GUIStyle solidCellStyle;
        private static GUIStyle emptyCellStyle;

        /// <summary>Call from inside a GUI pass (OnGUI, or Handles.BeginGUI in the editor).</summary>
        /// <param name="showCrossedEdges">Highlight the edges the surface crosses (V9).</param>
        /// <param name="showEdgeLabels">Number all 12 edges, crossed ones in the highlight colour.</param>
        public static void Draw(
            VoxelCornerEditor corners, bool showCornerLabels, bool showCrossedEdges, bool showEdgeLabels, WorldToGui worldToGui)
        {
            EnsureStyles();

            int caseIndex = corners.CaseIndex;
            Transform transform = corners.transform;
            float voxelSize = corners.VoxelSize;

            bool ToGui(Vector3 local, out Vector2 gui) => worldToGui(transform.TransformPoint(local * voxelSize), out gui);

            if (showCrossedEdges)
            {
                DrawCrossedEdges(caseIndex, ToGui);
            }

            if (showEdgeLabels)
            {
                DrawEdgeLabels(caseIndex, ToGui);
            }

            if (showCornerLabels)
            {
                DrawCornerLabels(caseIndex, ToGui);
                if (ToGui(new Vector3(0.5f, 1.35f, 0.5f), out Vector2 anchor))
                {
                    DrawCaseTable(caseIndex, anchor);
                }
            }
        }

        private delegate bool LocalToGui(Vector3 local, out Vector2 gui);

        private static void DrawCrossedEdges(int caseIndex, LocalToGui toGui)
        {
            int mask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                if ((mask & (1 << edge)) == 0)
                {
                    continue;
                }

                Vector3 a = MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 0]);
                Vector3 b = MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 1]);
                if (toGui(a, out Vector2 guiA) && toGui(b, out Vector2 guiB))
                {
                    GuiDrawing.Line(guiA, guiB, CrossedEdgeColor, CrossedEdgeThickness);
                }
            }
        }

        // Labels sit beside each edge's midpoint, nudged away from the cube centre.
        private static void DrawEdgeLabels(int caseIndex, LocalToGui toGui)
        {
            var centre = new Vector3(0.5f, 0.5f, 0.5f);
            int mask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                Vector3 a = MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 0]);
                Vector3 b = MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 1]);
                Vector3 middle = (a + b) * 0.5f;
                if (toGui(middle + (middle - centre) * 0.15f, out Vector2 gui))
                {
                    bool crossed = (mask & (1 << edge)) != 0;
                    GuiDrawing.CentredLabel(gui, $"e{edge}", crossed ? edgeStyle : mutedEdgeStyle);
                }
            }
        }

        private static void DrawCornerLabels(int caseIndex, LocalToGui toGui)
        {
            var centre = new Vector3(0.5f, 0.5f, 0.5f);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3 local = MarchingCubes.CornerPosition(corner);

                // Nudge labels outwards so they sit off the corner spheres.
                if (toGui(local + (local - centre) * 0.25f, out Vector2 gui))
                {
                    bool solid = MarchingCubes.IsCornerSolid(caseIndex, corner);
                    GuiDrawing.CentredLabel(gui, $"c{corner}", solid ? solidCornerStyle : emptyCornerStyle);
                }
            }
        }

        /// <summary>
        /// Draws, centred above <paramref name="anchor"/>:
        /// <code>
        /// case 41
        /// corner  c7  c6  c5  c4  c3  c2  c1  c0
        /// bit      0   0   1   0   1   0   0   1
        /// </code>
        /// </summary>
        private static void DrawCaseTable(int caseIndex, Vector2 anchor)
        {
            float width = TablePadding * 2 + RowLabelWidth + CellWidth * MarchingCubes.CornerCount;
            float height = TablePadding * 2 + RowHeight * TableRows;
            var panel = new Rect(anchor.x - width * 0.5f, anchor.y - height, width, height);
            GuiDrawing.Rect(panel, TableBackground);

            float left = panel.x + TablePadding;
            float rowTitle = panel.y + TablePadding;
            float rowCorner = rowTitle + RowHeight;
            float rowBit = rowCorner + RowHeight;

            GUI.Label(new Rect(left, rowTitle, width, RowHeight), $"case {caseIndex}", titleStyle);
            GUI.Label(new Rect(left, rowCorner, RowLabelWidth, RowHeight), "corner", rowLabelStyle);
            GUI.Label(new Rect(left, rowBit, RowLabelWidth, RowHeight), "bit", rowLabelStyle);

            // Most significant bit (corner 7) first, matching how binary is written.
            for (int column = 0; column < MarchingCubes.CornerCount; column++)
            {
                int corner = MarchingCubes.CornerCount - 1 - column;
                bool solid = MarchingCubes.IsCornerSolid(caseIndex, corner);
                GUIStyle style = solid ? solidCellStyle : emptyCellStyle;
                float x = left + RowLabelWidth + column * CellWidth;

                GUI.Label(new Rect(x, rowCorner, CellWidth, RowHeight), $"c{corner}", style);
                GUI.Label(new Rect(x, rowBit, CellWidth, RowHeight), solid ? "1" : "0", style);
            }
        }

        private static void EnsureStyles()
        {
            if (stylesSkin == GUI.skin && solidCornerStyle != null)
            {
                return;
            }

            stylesSkin = GUI.skin;
            GUIStyle label = GUI.skin.label;

            solidCornerStyle = Style(label, SolidColor, FontStyle.Bold, 13);
            emptyCornerStyle = Style(label, EmptyColor, FontStyle.Bold, 13);
            edgeStyle = Style(label, CrossedEdgeColor, FontStyle.Bold, 11);
            mutedEdgeStyle = Style(label, MutedColor, FontStyle.Normal, 11);
            titleStyle = Style(label, Color.white, FontStyle.Bold, 12);
            rowLabelStyle = Style(label, MutedColor, FontStyle.Normal, 12);
            solidCellStyle = Style(label, SolidColor, FontStyle.Bold, 12, TextAnchor.MiddleCenter);
            emptyCellStyle = Style(label, EmptyColor, FontStyle.Normal, 12, TextAnchor.MiddleCenter);
        }

        private static GUIStyle Style(GUIStyle baseStyle, Color color, FontStyle fontStyle, int fontSize,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(baseStyle)
            {
                fontStyle = fontStyle,
                fontSize = fontSize,
                alignment = alignment,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = color },
            };
        }
    }
}
