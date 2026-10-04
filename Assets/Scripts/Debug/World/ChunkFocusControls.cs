using UnityEngine;

namespace Clube.Debug
{
    /// <summary>The focused chunk's section of the lab panel (M4, M20): its stats, and framing or clearing it.</summary>
    public static class ChunkFocusControls
    {
        public static void Draw(LabPanelFrame frame, ChunkFocus chunkFocus)
        {
            string description = chunkFocus.DescribeFocused();
            if (description == null)
            {
                GUILayout.Label("Click the terrain (Select tool) to focus a chunk and select a voxel in it.", frame.Hint);
                return;
            }

            GUILayout.Label(description, frame.Label);
            switch (frame.ButtonRow("Frame it", "Clear"))
            {
                case 0:
                    chunkFocus.FrameFocused();
                    break;
                case 1:
                    chunkFocus.Focus(null);
                    break;
            }
        }
    }
}
