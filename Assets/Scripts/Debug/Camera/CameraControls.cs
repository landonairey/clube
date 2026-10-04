namespace Clube.Debug
{
    /// <summary>
    /// The camera section of the in-game lab panels: the free-fly camera's base speed and
    /// how much Shift multiplies it, for crossing a big world quickly.
    /// </summary>
    public static class CameraControls
    {
        public static void Draw(LabPanelFrame frame, FreeFlyCamera flyCamera)
        {
            flyCamera.MoveSpeed = frame.Slider("Move speed (u/s)", flyCamera.MoveSpeed, FreeFlyCamera.MinMoveSpeed, FreeFlyCamera.MaxMoveSpeed, "0.0");
            flyCamera.FastMultiplier = frame.Slider("Shift boost ×", flyCamera.FastMultiplier, 1f, FreeFlyCamera.MaxFastMultiplier, "0.0");
        }
    }
}
