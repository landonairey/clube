using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The active quest on the HUD (GL216): under "Active Quest", the first quest of the checklist not done
    /// (<see cref="PlayerQuests"/>), in the top-left corner under the lab HUD box (<see cref="HudLayout"/>). The
    /// field log (Q) has the whole list. Shown while the player is in control; gone once every quest is done.
    /// </summary>
    [RequireComponent(typeof(PlayerQuests))]
    public class QuestHud : MonoBehaviour
    {
        private const float Width = 400f;

        private PlayerQuests quests;
        private PlayerController player;
        private GUIStyle headingStyle;
        private GUIStyle questStyle;

        private void Awake()
        {
            quests = GetComponent<PlayerQuests>();
            player = GetComponent<PlayerController>();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || (player != null && (!player.enabled || player.IsInMenu)))
            {
                return;
            }
            QuestLog.Quest active = quests.Log.Active;
            if (active == null)
            {
                return;
            }
            headingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 6, 0), normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            questStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, padding = new RectOffset(10, 10, 2, 8), normal = { textColor = Color.white } };

            string heading = active.Section == PlayerQuests.MainSection ? "Active Quest" : "Active Quest (side)";
            var text = new GUIContent(active.Title);
            float top = HudLayout.TopLeftFree;
            float headingHeight = 22f;
            float questHeight = questStyle.CalcHeight(text, Width);
            var box = new Rect(10f, top, Width, headingHeight + questHeight);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(box.x, box.y, Width, headingHeight), heading, headingStyle);
            GUI.Label(new Rect(box.x, box.y + headingHeight, Width, questHeight), text, questStyle);
        }
    }
}
