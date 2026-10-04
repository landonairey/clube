using System;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Where playback is in a recorded step log: the current step and how far
    /// through it (0-1), and whether it is playing (V17). Knows nothing about
    /// what the steps mean or how they are drawn.
    /// </summary>
    public sealed class StepPlayback
    {
        public int StepCount { get; private set; }

        /// <summary>The step being shown, 0 to <see cref="StepCount"/> - 1.</summary>
        public int StepIndex { get; private set; }

        /// <summary>How far through the current step, 0 (just started) to 1 (complete).</summary>
        public float Progress { get; private set; }

        public bool IsPlaying { get; private set; }

        public bool IsAtEnd => StepCount == 0 || (StepIndex == StepCount - 1 && Progress >= 1f);

        /// <summary>Starts over on a new log, paused at the very beginning.</summary>
        public void Load(int stepCount)
        {
            StepCount = Math.Max(0, stepCount);
            StepIndex = 0;
            Progress = 0f;
            IsPlaying = false;
        }

        /// <summary>Plays from the current point, or from the start if already at the end.</summary>
        public void Play()
        {
            if (IsAtEnd)
            {
                Restart();
            }
            IsPlaying = StepCount > 0;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void TogglePlay()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        /// <summary>Back to the start of the first step, keeping play/pause as it was.</summary>
        public void Restart()
        {
            StepIndex = 0;
            Progress = 0f;
        }

        /// <summary>
        /// Pauses and moves whole steps forward (+) or back (-), showing the step
        /// complete. Stepping forward from a step part-way through first completes it.
        /// </summary>
        public void Step(int delta)
        {
            Pause();
            if (StepCount == 0)
            {
                return;
            }

            if (delta > 0 && Progress < 1f)
            {
                delta--;
            }

            int target = StepIndex + delta;
            if (target < 0)
            {
                Restart();
                return;
            }

            StepIndex = Mathf.Min(target, StepCount - 1);
            Progress = 1f;
        }

        /// <summary>Pauses and shows step <paramref name="index"/> complete, e.g. from a scrub slider.</summary>
        public void Seek(int index)
        {
            Pause();
            if (StepCount == 0)
            {
                return;
            }

            StepIndex = Mathf.Clamp(index, 0, StepCount - 1);
            Progress = 1f;
        }

        /// <summary>
        /// Pauses at <paramref name="progress"/> (0-1) of the way through step
        /// <paramref name="index"/>, e.g. at the start of a voxel jumped to (K21), so
        /// playing from there shows it being built.
        /// </summary>
        public void Seek(int index, float progress)
        {
            Seek(index);
            Progress = Mathf.Clamp01(progress);
        }

        /// <summary>Moves time forward while playing; a step of duration 0 completes at once.</summary>
        /// <param name="durationOf">Seconds the step at a given index takes at speed 1.</param>
        public void Advance(float deltaTime, Func<int, float> durationOf)
        {
            if (!IsPlaying)
            {
                return;
            }

            float remaining = deltaTime;
            while (remaining > 0f || Progress >= 1f)
            {
                if (Progress >= 1f)
                {
                    if (StepIndex >= StepCount - 1)
                    {
                        IsPlaying = false;
                        return;
                    }
                    StepIndex++;
                    Progress = 0f;
                }

                float duration = durationOf(StepIndex);
                if (duration <= 0f)
                {
                    Progress = 1f;
                    continue;
                }

                float needed = (1f - Progress) * duration;
                if (remaining < needed)
                {
                    Progress += remaining / duration;
                    return;
                }

                remaining -= needed;
                Progress = 1f;
            }
        }
    }
}
