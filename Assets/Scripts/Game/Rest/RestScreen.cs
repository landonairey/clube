using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Resting on a sleeping roll (GL290), as a bed works in the old Elder Scrolls games: a popup with a dial of the hours to
    /// rest, 1 to 24, and the time the player will wake; Cancel closes it, Rest begins. While resting the view blurs
    /// (<see cref="RestBlur"/>), the popup counts the hours, one each second, and the game clock runs with them, so the sun,
    /// the clouds and the weather move on; then the popup closes and the view clears. Opened by <see cref="SleepingRoll"/>.
    /// </summary>
    public class RestScreen : MonoBehaviour
    {
        private const float Width = 380f;
        private const float Height = 210f;
        private const int MaxHours = 24;

        [Tooltip("The game's clock, which resting runs on. Found in the scene when empty.")]
        [SerializeField]
        private GameClock clock;

        [Tooltip("The sky, for the hour and day shown. Found in the scene when empty.")]
        [SerializeField]
        private DayNightCycle dayNight;

        [Tooltip("Closes the popup before resting starts (the Interact key, as other screens).")]
        [SerializeField]
        private InputActionReference closeAction;

        [Tooltip("Real seconds each hour of rest takes (GL370: half a second, twice the first pass's speed).")]
        [SerializeField, Min(0.05f)]
        private float secondsPerHour = 0.5f;

        [Tooltip("Seconds the blur takes to come and go.")]
        [SerializeField, Min(0.01f)]
        private float blurSeconds = 0.6f;

        private PlayerController player;
        private RestBlur blur;
        private SleepingRoll roll;
        private int hours = 8;
        private bool resting;
        private float restedHours;
        private int openedFrame;
        private GUIStyle titleStyle;
        private GUIStyle centredStyle;

        /// <summary>True while the popup is up, choosing or resting.</summary>
        public bool IsOpen => roll != null;

        /// <summary>True while hours are passing.</summary>
        public bool IsResting => resting;

        /// <summary>Raised when a rest ends, with the hours rested.</summary>
        public event System.Action<int> Rested;

        /// <summary>Opens the popup for a roll.</summary>
        public void Open(SleepingRoll by)
        {
            if (clock == null)
            {
                return;
            }
            roll = by;
            resting = false;
            restedHours = 0f;
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        /// <summary>Starts resting the chosen hours.</summary>
        public void BeginRest()
        {
            if (IsOpen && !resting)
            {
                resting = true;
                restedHours = 0f;
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            if (resting)
            {
                Rested?.Invoke(Mathf.FloorToInt(restedHours));
            }
            roll = null;
            resting = false;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<GameClock>();
            }
            if (dayNight == null)
            {
                dayNight = FindAnyObjectByType<DayNightCycle>();
            }
        }

        private void OnEnable()
        {
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
            Blur(0f);
        }

        private void OnDestroy()
        {
            blur?.Destroy();
        }

        private void Update()
        {
            float target = resting ? 1f : 0f;
            if (blur != null || target > 0f)
            {
                blur ??= new RestBlur();
                Blur(Mathf.MoveTowards(blur.Amount, target, Time.unscaledDeltaTime / blurSeconds));
            }
            if (!IsOpen)
            {
                return;
            }
            if (resting)
            {
                // The hours pass on the clock: the sun, the clouds and whatever grows or burns by game time move on.
                float step = Mathf.Min(Time.unscaledDeltaTime / secondsPerHour, hours - restedHours);
                restedHours += step;
                clock.Advance(step / 24d);
                if (restedHours >= hours - 1e-4f)
                {
                    Close();
                }
                return;
            }
            bool closePressed = Time.frameCount != openedFrame && closeAction != null && closeAction.action.WasPressedThisFrame();
            if (closePressed || (player != null && !player.enabled))
            {
                Close();
            }
        }

        private void Blur(float amount)
        {
            if (blur != null)
            {
                blur.Amount = amount;
            }
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (!IsOpen)
            {
                return;
            }
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(18), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            centredStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(14), alignment = TextAnchor.MiddleCenter };
            var panel = new Rect((Screen.width - Width) * 0.5f, (Screen.height - Height) * 0.5f, Width, Height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + 20f, y = panel.y + 16f, inner = Width - 40f;
            GUI.Label(new Rect(x, y, inner, 26f), resting ? "Resting" : "Rest", titleStyle);
            y += 36f;
            if (resting)
            {
                int done = Mathf.Min(hours, Mathf.FloorToInt(restedHours + 1e-4f));
                GUI.Label(new Rect(x, y, inner, 28f), $"{done} of {hours} hours", centredStyle);
                y += 30f;
                // The hours as a bar filling up.
                var bar = new Rect(x, y, inner, 14f);
                GUI.Box(bar, GUIContent.none);
                GUI.Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(restedHours / hours), bar.height), GUIContent.none);
                y += 28f;
                GUI.Label(new Rect(x, y, inner, 28f), $"It's {Clock(CurrentHour())}", centredStyle);
                y += 34f;
                if (GameUi.Button(new Rect(panel.center.x - 60f, y, 120f, 30f), "Wake up"))
                {
                    Close();
                }
                return;
            }

            GUI.Label(new Rect(x, y, inner, 28f), $"Rest for {hours} hour{(hours == 1 ? "" : "s")}", centredStyle);
            y += 28f;
            if (GameUi.Button(new Rect(x, y, 30f, 28f), "-"))
            {
                hours = Mathf.Max(1, hours - 1);
            }
            hours = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 40f, y + 5f, inner - 80f, 16f), hours, 1f, MaxHours));
            if (GameUi.Button(new Rect(x + inner - 30f, y, 30f, 28f), "+"))
            {
                hours = Mathf.Min(MaxHours, hours + 1);
            }
            y += 34f;
            float wakeHour = CurrentHour() + hours;
            int wakeDay = CurrentDay() + Mathf.FloorToInt(wakeHour / 24f);
            GUI.Label(new Rect(x, y, inner, 28f), $"You'll wake at {Clock(wakeHour)}, day {wakeDay}", centredStyle);
            y += 40f;
            if (GameUi.Button(new Rect(panel.center.x - 130f, y, 120f, 30f), "Cancel"))
            {
                Close();
            }
            if (GameUi.Button(new Rect(panel.center.x + 10f, y, 120f, 30f), "Rest"))
            {
                BeginRest();
            }
        }

        private float CurrentHour()
        {
            return dayNight != null ? dayNight.ClockHour : (float)(clock.Days % 1d) * 24f;
        }

        private int CurrentDay()
        {
            return dayNight != null ? dayNight.Day : (int)System.Math.Floor(clock.Days) + 1;
        }

        private static string Clock(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int minutes = Mathf.FloorToInt(hour * 60f + 1e-3f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }
}
