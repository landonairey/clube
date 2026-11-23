using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// "&lt;AMT&gt; XP" floating up by the cursor whenever experience is earned (GL846): on every tool hit, strike or finished
    /// recipe. The cursor is the crosshair in the middle of the screen while walking, and the mouse while a screen (a
    /// station's panel) is open. Experience earned in one frame shows as one number.
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class XpPopups : MonoBehaviour, IGuiDrawer
    {
        private const float Seconds = 1.2f;
        private const float Rise = 60f;
        private const int MaxShown = 12;

        private struct Popup
        {
            public string Text;
            public Vector2 From;
            public float Born;
        }

        private readonly List<Popup> popups = new List<Popup>();
        private PlayerSkills skills;
        private PlayerController player;
        private GUIStyle style;
        private float pending;
        private int pendingFrame = -1;
        private Vector2 lastMouse;

        private void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            skills.Skills.Gained += OnGained;
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            skills.Skills.Gained -= OnGained;
            GuiHost.Remove(this);
            popups.Clear();
        }

        private void OnGained(SkillType skill, float amount)
        {
            // Several skills trained by one hit add up into one number.
            if (pendingFrame != Time.frameCount)
            {
                pending = 0f;
                pendingFrame = Time.frameCount;
                if (popups.Count >= MaxShown)
                {
                    popups.RemoveAt(0);
                }
                popups.Add(new Popup { From = CursorPoint(), Born = Time.unscaledTime });
            }
            pending += amount;
            Popup last = popups[popups.Count - 1];
            last.Text = $"{Format(pending)} XP";
            popups[popups.Count - 1] = last;
        }

        /// <summary>An amount of experience as the popup shows it: "12", "0.5", never "0" for a little.</summary>
        public static string Format(float amount)
        {
            return amount >= 10f ? amount.ToString("0") : amount >= 0.05f ? amount.ToString("0.#") : amount.ToString("0.##");
        }

        // The crosshair while the cursor is locked, else the mouse as IMGUI last saw it.
        private Vector2 CursorPoint()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked || (player != null && !player.IsInMenu);
            return locked ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : lastMouse;
        }

        int IGuiDrawer.GuiDepth => -60;

        void IGuiDrawer.DrawGui()
        {
            Event current = Event.current;
            lastMouse = current.mousePosition;
            if (popups.Count == 0 || current.type != EventType.Repaint)
            {
                return;
            }
            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = GameUi.Font(16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.55f, 0.95f, 0.45f) },
            };
            Color previous = GUI.color;
            float now = Time.unscaledTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                float age = (now - popups[i].Born) / Seconds;
                if (age >= 1f)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                Vector2 at = popups[i].From + new Vector2(18f, -18f - Rise * age);
                GUI.color = new Color(1f, 1f, 1f, 1f - age * age);
                GameUi.OutlinedLabel(new Rect(at.x, at.y - 14f, 160f, 28f), popups[i].Text, style);
            }
            GUI.color = previous;
        }
    }
}
