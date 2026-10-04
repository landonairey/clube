using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The test fill for the lab panel (K2, K31): on or off, shape and radius. While it's on
    /// it overrides the terrain generator; turning it off gives the chunk back to the terrain.
    /// </summary>
    public static class TestFillControls
    {
        private static readonly string[] ShapeNames = { "Ball", "Block + shaft" };

        public static void Draw(LabPanelFrame frame, ChunkTestFill testFill)
        {
            bool on = frame.ToggleField("Test fill (overrides the terrain)", testFill.enabled);
            if (on != testFill.enabled)
            {
                testFill.enabled = on;
            }
            if (!on)
            {
                return;
            }

            var shape = (ChunkTestFill.Shape)frame.Toolbar("Shape", (int)testFill.FillShape, ShapeNames);
            if (shape != testFill.FillShape)
            {
                testFill.FillShape = shape;
            }

            float radius = frame.Slider("Radius (of the side)", testFill.Radius, 0f, 1f);
            if (!Mathf.Approximately(radius, testFill.Radius))
            {
                testFill.Radius = radius;
            }
        }
    }
}
