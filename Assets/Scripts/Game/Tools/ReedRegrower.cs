using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts picked reeds back (GL879): a clump picked (or broken by digging) is due again after 1-3 game days
    /// (<see cref="ReedRegrowSchedule"/>), then <see cref="BushScatter.Release"/> lets its cell grow again. The schedule
    /// is kept in the save's named values. Added to the player by <see cref="PlayerBushes"/>.
    /// </summary>
    public class ReedRegrower : MonoBehaviour, ISavedValues
    {
        private const string Key = "reeds.regrow";

        [Tooltip("Real seconds between looks at the clock for clumps that are due.")]
        [SerializeField, Min(0.1f)]
        private float checkSeconds = 2f;

        private readonly ReedRegrowSchedule schedule = new ReedRegrowSchedule();
        private readonly List<Vector2Int> dueNow = new List<Vector2Int>();
        private PlayerToolUser tools;
        private BushScatter scatter;
        private GameClock clock;
        private float nextCheck;

        /// <summary>The clumps waiting to grow back.</summary>
        public ReedRegrowSchedule Schedule => schedule;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            if (tools != null)
            {
                tools.BushPicked += OnPicked;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.BushPicked -= OnPicked;
            }
            if (scatter != null)
            {
                scatter.Broken -= OnPicked;
                scatter = null;
            }
        }

        private void Update()
        {
            if (scatter == null && tools != null && tools.WorldView != null)
            {
                scatter = tools.WorldView.GetComponent<BushScatter>();
                if (scatter != null)
                {
                    scatter.Broken += OnPicked;
                }
            }
            if (clock == null)
            {
                clock = FindAnyObjectByType<GameClock>();
            }
            if (scatter == null || clock == null || schedule.Count == 0 || Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + checkSeconds;
            schedule.TakeDue(clock.Days, dueNow);
            if (dueNow.Count > 0)
            {
                scatter.Release(dueNow);
            }
        }

        private void OnPicked(Bush bush)
        {
            if (!bush.IsReeds)
            {
                return;
            }
            if (clock == null)
            {
                clock = FindAnyObjectByType<GameClock>();
            }
            if (clock != null)
            {
                schedule.Picked(bush.Cell, clock.Days, Random.value);
            }
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            values[Key] = schedule.Encode();
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            schedule.Decode(values.TryGetValue(Key, out string saved) ? saved : null);
        }
    }
}
