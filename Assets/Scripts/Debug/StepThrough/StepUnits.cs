using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Groups a recorded build's steps into the units playback moves through (K19):
    /// single steps, whole voxels, or whole Z slices, each a contiguous run of steps.
    /// Maps a point in a unit back to the recorded step and how far through it, so
    /// the visuals and labels keep working per step whatever the granularity.
    /// Optionally skips empty voxels (K20).
    /// </summary>
    /// <remarks>
    /// A unit's sub-steps share its time in proportion to their own durations, so a
    /// voxel played as one unit still shows corners, case, edges and triangles in turn,
    /// just faster. An empty voxel (case 0 or 255: no edge crossed, no triangles) is
    /// classified and dropped. With skipping on, its steps get no units of their own
    /// and take no time inside a slice; a slice with nothing but empty voxels is skipped.
    /// </remarks>
    public sealed class StepUnits
    {
        // First and last step of each unit, in build order. With skipping on there
        // are gaps between units where the empty voxels are.
        private readonly List<int> starts = new List<int>();
        private readonly List<int> ends = new List<int>();

        private MeshingRecorder recording;

        public StepGranularity Granularity { get; private set; }

        public bool SkipEmpty { get; private set; }

        public int Count => starts.Count;

        /// <summary>How many voxels are empty (skipped when <see cref="SkipEmpty"/> is on).</summary>
        public int EmptyVoxelCount { get; private set; }

        /// <summary>Regroups the recording's steps.</summary>
        public void Build(MeshingRecorder newRecording, StepGranularity granularity, bool skipEmpty)
        {
            recording = newRecording;
            Granularity = granularity;
            SkipEmpty = skipEmpty;
            starts.Clear();
            ends.Clear();

            EmptyVoxelCount = 0;
            foreach (RecordedVoxel voxel in recording.Voxels)
            {
                if (voxel.CrossedEdgeMask == 0)
                {
                    EmptyVoxelCount++;
                }
            }

            IReadOnlyList<MeshingStep> steps = recording.Steps;
            int start = 0;
            for (int i = 1; i <= steps.Count; i++)
            {
                if (i == steps.Count || StartsUnit(steps[i - 1], steps[i]))
                {
                    AddUnit(start, i - 1);
                    start = i;
                }
            }
        }

        public int FirstStep(int unit)
        {
            return starts[unit];
        }

        public int LastStep(int unit)
        {
            return ends[unit];
        }

        /// <summary>The unit that holds a step, or the last unit before it if the step was skipped.</summary>
        public int UnitOf(int step)
        {
            int index = starts.BinarySearch(step);
            return index >= 0 ? index : System.Math.Max(0, ~index - 1);
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
                return (LastShown(first, last), 1f);
            }

            float total = 0f;
            for (int i = first; i <= last; i++)
            {
                total += Weight(i, timings);
            }
            if (total <= 0f)
            {
                return (LastShown(first, last), 1f);
            }

            float remaining = progress * total;
            for (int i = first; i <= last; i++)
            {
                float weight = Weight(i, timings);
                if (remaining < weight)
                {
                    return (i, remaining / weight);
                }
                remaining -= weight;
            }
            return (LastShown(first, last), 1f);
        }

        // The unit's last step that isn't skipped, so a slice ending in empty voxels
        // still finishes on a voxel that made something.
        private int LastShown(int first, int last)
        {
            for (int i = last; i > first; i--)
            {
                if (!IsSkipped(i))
                {
                    return i;
                }
            }
            return first;
        }

        /// <summary>
        /// The inverse of <see cref="Locate"/>: the unit holding <paramref name="step"/> and
        /// the progress through that unit at which the step begins.
        /// </summary>
        public (int Unit, float Progress) Find(int step, StepTimings timings)
        {
            int unit = UnitOf(step);
            int first = FirstStep(unit);
            int last = LastStep(unit);
            if (step <= first || step > last)
            {
                return (unit, 0f);
            }

            float before = 0f;
            float total = 0f;
            for (int i = first; i <= last; i++)
            {
                float weight = Weight(i, timings);
                if (i < step)
                {
                    before += weight;
                }
                total += weight;
            }

            // Aim a hair past the boundary (a thousandth of the step's own time): exactly on
            // it, float rounding in Locate can land on the end of the previous step instead.
            float nudge = Weight(step, timings) * 1e-3f;
            return (unit, total > 0f ? (before + nudge) / total : 0f);
        }

        /// <summary>The first recorded step of a voxel (its corner sampling), or -1 if it isn't in the recording.</summary>
        public int FirstStepOfVoxel(Vector3Int voxel)
        {
            IReadOnlyList<RecordedVoxel> voxels = recording.Voxels;
            IReadOnlyList<MeshingStep> steps = recording.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                int index = steps[i].VoxelIndex;
                if (index >= 0 && voxels[index].Voxel == voxel)
                {
                    return i;
                }
            }
            return -1;
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

        // Every unit is kept unless skipping is on and it holds only empty voxels' steps.
        private void AddUnit(int first, int last)
        {
            if (SkipEmpty)
            {
                bool anyKept = false;
                for (int i = first; i <= last && !anyKept; i++)
                {
                    anyKept = !IsSkipped(i);
                }
                if (!anyKept)
                {
                    return;
                }
            }

            starts.Add(first);
            ends.Add(last);
        }

        // Skipped steps take no time, so a slice's time goes to its non-empty voxels.
        private float Weight(int step, StepTimings timings)
        {
            return IsSkipped(step) ? 0f : timings.For(recording.Steps[step].Type);
        }

        private bool IsSkipped(int step)
        {
            int voxel = recording.Steps[step].VoxelIndex;
            return SkipEmpty && voxel >= 0 && recording.Voxels[voxel].CrossedEdgeMask == 0;
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
