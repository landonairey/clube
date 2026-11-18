using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Keeps lumpy work to an average share of the frame (GL770). Work that can't be cut smaller than a frame's budget (one
    /// road piece, one dig laid out) runs whole, and what it took over the budget is paid back by the frames after it, which
    /// do none of that work.
    /// </summary>
    public sealed class FrameAllowance
    {
        private float owed;
        private int lastFrame = int.MinValue;

        /// <summary>True when the work may run this frame; each frame pays back <paramref name="millisecondsAFrame"/> of what is owed.</summary>
        public bool MayWork(float millisecondsAFrame, int frame)
        {
            if (lastFrame != int.MinValue && frame > lastFrame)
            {
                owed = Mathf.Max(0f, owed - millisecondsAFrame * (frame - lastFrame));
            }
            lastFrame = frame;
            return owed <= 0f;
        }

        /// <summary>Records what the work took.</summary>
        public void Spend(float milliseconds)
        {
            owed += Mathf.Max(0f, milliseconds);
        }
    }
}
