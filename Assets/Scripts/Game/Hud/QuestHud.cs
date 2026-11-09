using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The active quest on the HUD (GL216): under "Active Quest", the first quest of the checklist not done
    /// (<see cref="PlayerQuests"/>), in the top-left corner under the lab HUD box (<see cref="HudLayout"/>). The
    /// field log (Q) has the whole list. The early quests show a sketch of where they happen (GL446) and a tip box
    /// underneath (GL447). Gone once every quest is done.
    /// </summary>
    [RequireComponent(typeof(PlayerQuests))]
    public class QuestHud : MonoBehaviour
    {
        private const float Width = 400f * GameUi.FontScale;

        private PlayerQuests quests;
        private PlayerController player;
        private GUIStyle headingStyle;
        private GUIStyle questStyle;
        private GUIStyle tipHeadingStyle;
        private GUIStyle tipStyle;

        private void Awake()
        {
            quests = GetComponent<PlayerQuests>();
            player = GetComponent<PlayerController>();
        }

        private void OnGUI()
        {
            GameUi.Apply();
            // It stays up while the inventory, a station screen or the field log is open (GL437), drawn over them.
            if (Event.current.type != EventType.Repaint || (player != null && !player.enabled))
            {
                return;
            }
            GUI.depth = -50;
            QuestLog.Quest active = quests.Log.Active;
            if (active == null)
            {
                return;
            }
            headingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(12), fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 6, 0), normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            questStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(13), wordWrap = true, padding = new RectOffset(10, 10, 2, 8), normal = { textColor = Color.white } };

            tipHeadingStyle ??= new GUIStyle(headingStyle) { normal = { textColor = new Color(0.6f, 0.85f, 1f) } };
            tipStyle ??= new GUIStyle(questStyle) { fontSize = GameUi.Font(12), normal = { textColor = new Color(0.88f, 0.9f, 0.92f) } };

            string heading = active.Section == PlayerQuests.MainSection ? "Active Quest" : "Active Quest (side)";
            var text = new GUIContent(active.Title);
            // A sketch of the place the quest happens at (GL446): the boulder, a tree, the stump.
            Texture2D sketch = SketchFor(active.Id);
            float sketchSize = sketch != null ? 64f * GameUi.FontScale : 0f;
            float textWidth = Width - sketchSize;
            float top = HudLayout.TopLeftFree;
            float headingHeight = 30f;
            float questHeight = Mathf.Max(questStyle.CalcHeight(text, textWidth), sketchSize > 0f ? sketchSize + 6f : 0f);
            var box = new Rect(10f, top, Width, headingHeight + questHeight);
            Panel(box);
            GUI.Label(new Rect(box.x, box.y, Width, headingHeight), heading, headingStyle);
            if (sketch != null)
            {
                // A paper card behind the ink, so the sketch reads on the dark box.
                var card = new Rect(box.x + 8f, box.y + headingHeight, sketchSize - 6f, sketchSize - 6f);
                Color previous = GUI.color;
                GUI.color = new Color(0.93f, 0.89f, 0.8f);
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.DrawTexture(card, sketch, ScaleMode.ScaleToFit);
            }
            GUI.Label(new Rect(box.x + sketchSize, box.y + headingHeight, textWidth, questHeight), text, questStyle);

            // The quest's tip in its own box underneath (GL447).
            string tip = PlayerQuests.TipFor(active.Id);
            if (tip == null)
            {
                return;
            }
            var tipText = new GUIContent(tip);
            float tipHeight = tipStyle.CalcHeight(tipText, Width);
            var tipBox = new Rect(10f, box.yMax + 6f, Width, headingHeight + tipHeight);
            Panel(tipBox);
            GUI.Label(new Rect(tipBox.x, tipBox.y, Width, headingHeight), "Tip", tipHeadingStyle);
            GUI.Label(new Rect(tipBox.x, tipBox.y + headingHeight, Width, tipHeight), tipText, tipStyle);
        }

        private static Texture2D SketchFor(string questId)
        {
            switch (questId)
            {
                case "hand-axe": return PlaceSketch.Boulder;
                case "gather-wood": return PlaceSketch.Tree;
                case "campfire": return PlaceSketch.Stump;
                default: return null;
            }
        }

        private static void Panel(Rect box)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
