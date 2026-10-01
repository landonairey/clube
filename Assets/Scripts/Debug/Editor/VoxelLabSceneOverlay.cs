using System;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Scene-view teaching overlay for <see cref="VoxelCornerEditor"/>:
    /// <list type="bullet">
    /// <item>V21: each corner labelled with its index and the bit it sets in the
    /// case index ("c3 → bit 3 = 8"), green when solid, plus the case index in
    /// binary and decimal above the cube.</item>
    /// <item>V9: the edges the surface crosses, highlighted and numbered.</item>
    /// </list>
    /// Reads the corner sliders, not the chunk, so it works outside Play mode too.
    /// </summary>
    internal static class VoxelLabSceneOverlay
    {
        private const float CrossedEdgeThickness = 4f;

        private static readonly Color SolidColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color EmptyColor = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color CrossedEdgeColor = new Color(1f, 0.55f, 0.1f);

        private static GUIStyle solidStyle;
        private static GUIStyle emptyStyle;
        private static GUIStyle edgeStyle;
        private static GUIStyle caseStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(VoxelCornerEditor corners, GizmoType gizmoType)
        {
            if (!VoxelLabViewSettings.ShowCornerLabels && !VoxelLabViewSettings.ShowCrossedEdges)
            {
                return;
            }

            EnsureStyles();

            int caseIndex = corners.CaseIndex;
            Func<Vector3, Vector3> toWorld = local => corners.transform.TransformPoint(local * corners.VoxelSize);

            if (VoxelLabViewSettings.ShowCrossedEdges)
            {
                DrawCrossedEdges(caseIndex, toWorld);
            }

            if (VoxelLabViewSettings.ShowCornerLabels)
            {
                DrawCornerLabels(corners, caseIndex, toWorld);
            }
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

        private static void DrawCornerLabels(VoxelCornerEditor corners, int caseIndex, Func<Vector3, Vector3> toWorld)
        {
            var centre = new Vector3(0.5f, 0.5f, 0.5f);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                bool solid = MarchingCubes.IsCornerSolid(caseIndex, corner);
                Vector3 local = MarchingCubes.CornerPosition(corner);

                // Nudge labels outwards so they sit off the corner spheres.
                Vector3 position = toWorld(local + (local - centre) * 0.25f);
                string text = $"c{corner} → bit {corner} = {1 << corner}\n" +
                              $"{(solid ? "solid" : "empty")} ({corners.CornerValues[corner]:0.00})";
                Handles.Label(position, text, solid ? solidStyle : emptyStyle);
            }

            string binary = Convert.ToString(caseIndex, 2).PadLeft(8, '0');
            Handles.Label(toWorld(new Vector3(0.5f, 1.6f, 0.5f)), $"case {caseIndex} = 0b{binary}", caseStyle);
        }

        private static void EnsureStyles()
        {
            if (solidStyle != null)
            {
                return;
            }

            solidStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = SolidColor } };
            emptyStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = EmptyColor } };
            edgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = CrossedEdgeColor } };
            caseStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = 14 };
        }
    }
}
