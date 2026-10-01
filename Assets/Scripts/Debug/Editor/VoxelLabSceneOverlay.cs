using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Scene-view teaching overlay for <see cref="VoxelCornerEditor"/>:
    /// <list type="bullet">
    /// <item>V21: corners labelled c0-c7 (green when solid), and a table above the
    /// cube showing which bit of the case index each corner sets, with its place
    /// value, so corner → bit → case index reads left to right.</item>
    /// <item>V9: the edges the surface crosses, highlighted and numbered.</item>
    /// <item>X, Y, Z axis arrows for the coordinate system corner positions use.</item>
    /// </list>
    /// Reads the corner sliders, not the chunk, so it works outside Play mode too.
    /// </summary>
    internal static class VoxelLabSceneOverlay
    {
        private const float CrossedEdgeThickness = 4f;
        private const float AxisLength = 0.6f;

        // Case table layout, in GUI pixels.
        private const float TablePadding = 6f;
        private const float RowLabelWidth = 48f;
        private const float CellWidth = 30f;
        private const float RowHeight = 16f;
        private const int TableRows = 5;

        private static readonly Color SolidColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color EmptyColor = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color MutedColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color CrossedEdgeColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color TableBackground = new Color(0f, 0f, 0f, 0.7f);

        private static GUIStyle solidCornerStyle;
        private static GUIStyle emptyCornerStyle;
        private static GUIStyle edgeStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle rowLabelStyle;
        private static GUIStyle solidCellStyle;
        private static GUIStyle emptyCellStyle;
        private static GUIStyle mutedCellStyle;
        private static GUIStyle xAxisStyle;
        private static GUIStyle yAxisStyle;
        private static GUIStyle zAxisStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(VoxelCornerEditor corners, GizmoType gizmoType)
        {
            EnsureStyles();

            int caseIndex = corners.CaseIndex;
            Transform transform = corners.transform;
            float voxelSize = corners.VoxelSize;
            Func<Vector3, Vector3> toWorld = local => transform.TransformPoint(local * voxelSize);

            if (VoxelLabViewSettings.ShowAxes)
            {
                DrawAxes(transform, voxelSize, toWorld);
            }

            if (VoxelLabViewSettings.ShowCrossedEdges)
            {
                DrawCrossedEdges(caseIndex, toWorld);
            }

            if (VoxelLabViewSettings.ShowCornerLabels)
            {
                DrawCornerLabels(caseIndex, toWorld);
                DrawCaseTable(caseIndex, toWorld(new Vector3(0.5f, 1.35f, 0.5f)));
            }
        }

        // Arrows start just off corner c0 so they don't hide the cube's own edges.
        private static void DrawAxes(Transform transform, float voxelSize, Func<Vector3, Vector3> toWorld)
        {
            Vector3 origin = toWorld(new Vector3(-0.35f, -0.35f, -0.35f));
            float length = AxisLength * voxelSize;

            DrawAxis(origin, transform.right, length, Handles.xAxisColor, xAxisStyle, "+X");
            DrawAxis(origin, transform.up, length, Handles.yAxisColor, yAxisStyle, "+Y");
            DrawAxis(origin, transform.forward, length, Handles.zAxisColor, zAxisStyle, "+Z");
        }

        private static void DrawAxis(Vector3 origin, Vector3 direction, float length, Color color, GUIStyle style, string label)
        {
            Handles.color = color;
            Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(direction), length, EventType.Repaint);

            Handles.Label(origin + direction * (length * 1.15f), label, style);
        }

        private static void DrawCrossedEdges(int caseIndex, Func<Vector3, Vector3> toWorld)
        {
            int mask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
            Handles.color = CrossedEdgeColor;

            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                if ((mask & (1 << edge)) == 0)
                {
                    continue;
                }

                Vector3 a = toWorld(MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 0]));
                Vector3 b = toWorld(MarchingCubes.CornerPosition(MarchingCubesTables.EdgeCorners[edge, 1]));
                Handles.DrawLine(a, b, CrossedEdgeThickness);
                Handles.Label((a + b) * 0.5f, $"e{edge}", edgeStyle);
            }
        }

        private static void DrawCornerLabels(int caseIndex, Func<Vector3, Vector3> toWorld)
        {
            var centre = new Vector3(0.5f, 0.5f, 0.5f);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3 local = MarchingCubes.CornerPosition(corner);

                // Nudge labels outwards so they sit off the corner spheres.
                Vector3 position = toWorld(local + (local - centre) * 0.2f);
                bool solid = MarchingCubes.IsCornerSolid(caseIndex, corner);
                Handles.Label(position, $"c{corner}", solid ? solidCornerStyle : emptyCornerStyle);
            }
        }

        /// <summary>
        /// Draws, centred above <paramref name="worldAnchor"/>:
        /// <code>
        /// case 41
        /// corner  c7  c6  c5  c4  c3  c2  c1  c0
        /// value  128  64  32  16   8   4   2   1
        /// bit      0   0   1   0   1   0   0   1
        ///         = 32 + 8 + 1 = 41
        /// </code>
        /// Drawn as screen-space GUI so the columns line up exactly.
        /// </summary>
        private static void DrawCaseTable(int caseIndex, Vector3 worldAnchor)
        {
            Camera camera = Camera.current;
            if (camera != null && camera.WorldToViewportPoint(worldAnchor).z < 0f)
            {
                return;
            }

            Vector2 anchor = HandleUtility.WorldToGUIPoint(worldAnchor);
            float width = TablePadding * 2 + RowLabelWidth + CellWidth * MarchingCubes.CornerCount;
            float height = TablePadding * 2 + RowHeight * TableRows;
            var panel = new Rect(anchor.x - width * 0.5f, anchor.y - height, width, height);

            Handles.BeginGUI();
            EditorGUI.DrawRect(panel, TableBackground);

            float left = panel.x + TablePadding;
            float top = panel.y + TablePadding;
            GUI.Label(new Rect(left, top, width, RowHeight), $"case {caseIndex}", titleStyle);

            float rowCorner = top + RowHeight;
            float rowValue = rowCorner + RowHeight;
            float rowBit = rowValue + RowHeight;
            GUI.Label(new Rect(left, rowCorner, RowLabelWidth, RowHeight), "corner", rowLabelStyle);
            GUI.Label(new Rect(left, rowValue, RowLabelWidth, RowHeight), "value", rowLabelStyle);
            GUI.Label(new Rect(left, rowBit, RowLabelWidth, RowHeight), "bit", rowLabelStyle);

            // Most significant bit (corner 7) first, matching how binary is written.
            for (int column = 0; column < MarchingCubes.CornerCount; column++)
            {
                int corner = MarchingCubes.CornerCount - 1 - column;
                bool solid = MarchingCubes.IsCornerSolid(caseIndex, corner);
                float x = left + RowLabelWidth + column * CellWidth;

                GUI.Label(new Rect(x, rowCorner, CellWidth, RowHeight), $"c{corner}", solid ? solidCellStyle : emptyCellStyle);
                GUI.Label(new Rect(x, rowValue, CellWidth, RowHeight), (1 << corner).ToString(), mutedCellStyle);
                GUI.Label(new Rect(x, rowBit, CellWidth, RowHeight), solid ? "1" : "0", solid ? solidCellStyle : emptyCellStyle);
            }

            GUI.Label(
                new Rect(left + RowLabelWidth, rowBit + RowHeight, width, RowHeight),
                $"= {FormatSum(caseIndex)}",
                rowLabelStyle);

            Handles.EndGUI();
        }

        /// <summary>e.g. 41 → "32 + 8 + 1 = 41"; 0 → "0".</summary>
        private static string FormatSum(int caseIndex)
        {
            var terms = new List<string>();
            for (int corner = MarchingCubes.CornerCount - 1; corner >= 0; corner--)
            {
                if (MarchingCubes.IsCornerSolid(caseIndex, corner))
                {
                    terms.Add((1 << corner).ToString());
                }
            }

            return terms.Count > 1 ? $"{string.Join(" + ", terms)} = {caseIndex}" : caseIndex.ToString();
        }

        private static void EnsureStyles()
        {
            if (solidCornerStyle != null)
            {
                return;
            }

            solidCornerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = SolidColor } };
            emptyCornerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = EmptyColor } };
            edgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = CrossedEdgeColor } };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            rowLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = MutedColor } };
            solidCellStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = SolidColor } };
            emptyCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = EmptyColor } };
            mutedCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = MutedColor } };
            xAxisStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Handles.xAxisColor } };
            yAxisStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Handles.yAxisColor } };
            zAxisStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Handles.zAxisColor } };
        }
    }
}
