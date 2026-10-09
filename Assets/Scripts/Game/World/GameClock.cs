using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// In-game time, in days since the scene started (GL34): what clay drying waits on. A
    /// first pass of the day count a day/night cycle (PK4) and calendar (PK18) will own; there's
    /// no sun or hour of day yet, only how long a day lasts in real seconds.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        [Tooltip("Real seconds in one in-game day.")]
        [SerializeField, Min(1f)]
        private float dayLengthSeconds = 1200f;

        [Tooltip("How fast in-game time runs: 1 is normal, higher to test things that take days.")]
        [SerializeField, Range(0f, 1000f)]
        private float timeScale = 1f;

        /// <summary>In-game days since the clock started, fractions included.</summary>
        public double Days { get; private set; }

        public float DayLengthSeconds
        {
            get => dayLengthSeconds;
            set => dayLengthSeconds = Mathf.Max(1f, value);
        }

        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        private void Update()
        {
            Days += Time.deltaTime * timeScale / dayLengthSeconds;
        }
    }
}
