namespace Clube.Core
{
    /// <summary>
    /// A watchdog for something that should keep making progress (GL586): feed it time while the thing is working, reset it
    /// whenever it gets somewhere, and it reports once when too long has passed with nothing to show, so the owner can re-plan
    /// instead of standing silently stuck.
    /// </summary>
    public struct StallTimer
    {
        private float idle;

        /// <summary>Seconds without progress so far.</summary>
        public float Idle => idle;

        /// <summary>Progress was made: start counting again.</summary>
        public void Reset() { idle = 0f; }

        /// <summary>Adds time without progress; true (once, and the count restarts) when it reaches the limit.</summary>
        public bool Tick(float dt, float limitSeconds)
        {
            idle += dt;
            if (idle < limitSeconds)
            {
                return false;
            }
            idle = 0f;
            return true;
        }
    }
}
