namespace Clube.Debug
{
    /// <summary>
    /// A voxel whose corners lab panels can drive by case index: the single voxel
    /// in <c>VoxelLab</c> (<see cref="VoxelCornerEditor"/>) or the selected voxel in
    /// <c>ChunkLab</c> (<see cref="SelectedVoxelEditor"/>). Lets both share one
    /// case and preset panel (V7, V10, V20).
    /// </summary>
    public interface IVoxelCaseTarget
    {
        /// <summary>The case index (0-255) the current corner values produce.</summary>
        int CaseIndex { get; }

        /// <summary>Sets every corner to 1 (solid) or 0 (empty) to match a case index.</summary>
        void ApplyCase(int caseIndex);

        /// <summary>Moves to the next (+1) or previous (-1) case index, wrapping 255 ↔ 0.</summary>
        void StepCase(int delta);
    }
}
