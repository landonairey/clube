using System.Collections.Generic;
using Clube.Core;

namespace Clube.Debug
{
    /// <summary>
    /// Groups a recorded build's steps into the units playback moves through (K19):
    /// single steps, whole voxels, or whole Z slices, each a contiguous run of steps.
    /// Maps a point in a unit back to the recorded step and how far through it, so
    /// the visuals and labels keep working per step whatever the granularity.
    /// </summary>
    /// <remarks>
    /// A unit's sub-steps share its time in proportion to their own durations, so a
    /// voxel played as one unit still shows corners, case, edges and triangles in turn,
    /// just faster.
    /// </remarks>
    public sealed class StepUnits
    {
        // First step of each unit; a unit runs to the next unit's first step.
        private readonly List<int> starts = new List<int>();

        private MeshingRecorder recording;

        public StepGranularity Granularity { get; private set; }

        public int Count => starts.Count;

        /// <summary>Regroups the recording's steps.</summary>
        public void Build(MeshingRecorder newRecording, StepGranularity granularity)
        {
            recording = newRecording;
            Granularity = granularity;
            starts.Clear();

            IReadOnlyList<MeshingStep> steps = recording.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (i == 0 || StartsUnit(steps[i - 1], steps[i]))
                {
                    starts.Add(i);
                }
            }
        }

        public int FirstStep(int unit)
        {
            return starts[unit];
        }

        public int LastStep(int unit)
        {
            return (unit + 1 < starts.Count ? starts[unit + 1] : recording.Steps.Count) - 1;
        }

        /// <summary>The unit that contains a step.</summary>
        public int UnitOf(int step)
        {
            int index = starts.BinarySearch(step);
            return index >= 0 ? index : ~index - 1;
        }

        /// <summary>Seconds a unit takes at speed 1: a step's own duration, or the per-voxel or per-slice time.</summary>
        public float Duration(int unit, StepTimings timings)
        {
            int first = FirstStep(unit);
            if (Granularity == StepGranularity.SubStep || recording.Steps[first].VoxelIndex < 0)
            {
                return timings.For(recording.Steps[first].Type);
            }
            return Granularity == StepGranularity.Voxel ? timings.Voxel : timings.Slice;
        }

        /// <summary>
        /// The recorded step shown <paramref name="progress"/> (0-1) of the way through
        /// <paramref name="unit"/>, and how far through that step.
        /// </summary>
        public (int Step, float Progress) Locate(int unit, float progress, StepTimings timings)
        {
            int first = FirstStep(unit);
            int last = LastStep(unit);
            if (first == last)
            {
                return (first, progress);
            }
            if (progress >= 1f)
            {
                return (last, 1f);
            }

            float total = 0f;
            for (int i = first; i <= last; i++)
            {
                total += timings.For(recording.Steps[i].Type);
            }
            if (total <= 0f)
            {
                return (last, 1f);
            }

            float remaining = progress * total;
            for (int i = first; i <= last; i++)
            {
                float duration = timings.For(recording.Steps[i].Type);
                if (remaining < duration)
                {
                    return (i, remaining / duration);
                }
                remaining -= duration;
            }
            return (last, 1f);
        }

        /// <summary>e.g. "Voxel 37 / 512" or "Slice z = 3 (4 / 8)"; empty for single steps and the once-per-build steps.</summary>
        public string Describe(int unit)
        {
            MeshingStep step = recording.Steps[FirstStep(unit)];
            if (Granularity == StepGranularity.SubStep || step.VoxelIndex < 0)
            {
                return "";
            }

            if (Granularity == StepGranularity.Voxel)
            {
                return $"Voxel {step.VoxelIndex + 1} / {recording.Voxels.Count}";
            }

            int z = recording.Voxels[step.VoxelIndex].Voxel.z;
            int slices = recording.SampleCount.z - 1;
            return $"Slice z = {z} ({z + 1} / {slices})";
        }

        private bool StartsUnit(MeshingStep previous, MeshingStep step)
        {
            if (Granularity == StepGranularity.SubStep || previous.VoxelIndex < 0 || step.VoxelIndex < 0)
            {
                return true;
            }
            if (Granularity == StepGranularity.Voxel)
            {
                return step.VoxelIndex != previous.VoxelIndex;
            }
            return recording.Voxels[step.VoxelIndex].Voxel.z != recording.Voxels[previous.VoxelIndex].Voxel.z;
        }
    }
}
