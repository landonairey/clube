using System;

namespace Clube.Debug
{
    /// <summary>
    /// The display section of the lab panel (K31, M20): toggles for whichever lab visuals
    /// the scene has (any argument may be null). Only real changes are written, since some
    /// rebuild meshes.
    /// </summary>
    public static class DisplayControls
    {
        private static readonly string[] WireframeNames = { "None", "Outline", "Grid" };
        private static readonly string[] TriangleOutlineNames = { "None", "Voxel", "All" };

        public static void Draw(
            LabPanelFrame frame, VoxelLabels labels, NormalLines normals, FlipFaces flipFaces,
            ChunkDebugView debugView, WorldDebugView worldDebug, AxesHud axesHud)
        {
            if (labels != null)
            {
                labels.CornerLabelsOn = frame.ToggleField("Corner labels and bit table", labels.CornerLabelsOn);
                labels.CornerValuesOn = frame.ToggleField("Densities on corner labels", labels.CornerValuesOn);
                labels.CrossedEdgesOn = frame.ToggleField("Highlight crossed edges", labels.CrossedEdgesOn);
                labels.EdgeLabelsOn = frame.ToggleField("Edge numbers", labels.EdgeLabelsOn);
            }
            DrawTriangleOutlines(frame, labels, normals);
            if (normals != null)
            {
                SetIfChanged(frame.ToggleField("Vertex normals", normals.ShowVertexNormals), normals.ShowVertexNormals, v => normals.ShowVertexNormals = v);
                SetIfChanged(frame.ToggleField("Face normals", normals.ShowFaceNormals), normals.ShowFaceNormals, v => normals.ShowFaceNormals = v);
            }
            if (flipFaces != null)
            {
                SetIfChanged(frame.ToggleField("Flip faces (reverse winding)", flipFaces.Flip), flipFaces.Flip, v => flipFaces.Flip = v);
            }
            if (debugView != null)
            {
                SetIfChanged(frame.ToggleField("Density samples", debugView.ShowSamples), debugView.ShowSamples, v => debugView.ShowSamples = v);
                var wireframe = (ChunkDebugView.Wireframe)frame.Toolbar("Chunk wireframe", (int)debugView.WireframeMode, WireframeNames);
                if (wireframe != debugView.WireframeMode)
                {
                    debugView.WireframeMode = wireframe;
                }
            }
            if (worldDebug != null)
            {
                worldDebug.ShowChunkBorders = frame.ToggleField("Chunk borders", worldDebug.ShowChunkBorders);
            }
            if (axesHud != null)
            {
                axesHud.ShowAxes = frame.ToggleField("Axes", axesHud.ShowAxes);
            }
        }

        // One control for both triangle outlines (K31): the voxel's own triangles
        // (VoxelLabels), or every triangle in the chunk (NormalLines) as well.
        private static void DrawTriangleOutlines(LabPanelFrame frame, VoxelLabels labels, NormalLines normals)
        {
            if (labels == null && normals == null)
            {
                return;
            }

            bool all = normals != null && normals.ShowTriangleEdges;
            bool voxel = labels != null && labels.TriangleEdgesOn;
            int current = all ? 2 : voxel ? 1 : 0;
            int chosen = frame.Toolbar("Triangle outlines", current, TriangleOutlineNames);
            if (chosen == current)
            {
                return;
            }

            if (labels != null)
            {
                labels.TriangleEdgesOn = chosen >= 1;
            }
            if (normals != null)
            {
                normals.ShowTriangleEdges = chosen == 2;
            }
        }

        private static void SetIfChanged(bool chosen, bool current, Action<bool> set)
        {
            if (chosen != current)
            {
                set(chosen);
            }
        }
    }
}
