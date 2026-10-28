using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A short line under the crosshair (GL146, GL152): why a hit took nothing ("Maple wood needs a bronze
    /// tool"), or what bashing made ("Made a hand axe"). Shows the latest message for a couple of seconds.
    /// </summary>
    public class ToolHint : MonoBehaviour
    {
        private const float Seconds = 2.5f;

        [SerializeField]
        private PlayerToolUser tools;

        [SerializeField]
        private BoulderBashing bashing;

        private string message;
        private float until;
        private GUIStyle style;

        /// <summary>Shows a message for a couple of seconds.</summary>
        public void Show(string text)
        {
            message = text;
            until = Time.time + Seconds;
        }

        private void Awake()
        {
            // Unity objects: compare with ==, not ??, which misses destroyed and unassigned ones.
            if (tools == null)
            {
                tools = GetComponent<PlayerToolUser>();
            }
            if (bashing == null)
            {
                bashing = GetComponent<BoulderBashing>();
            }
        }

        private void OnEnable()
        {
            if (tools != null)
            {
                tools.Blocked += Show;
            }
            if (bashing != null)
            {
                bashing.Message += Show;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.Blocked -= Show;
            }
            if (bashing != null)
            {
                bashing.Message -= Show;
            }
        }

        private void OnGUI()
        {
            if (message == null || Time.time >= until)
            {
                return;
            }
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
            var area = new Rect(0f, Screen.height * 0.5f + 48f, Screen.width, 24f);
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), message, new GUIStyle(style) { normal = { textColor = Color.black } });
            GUI.Label(area, message, style);
        }
    }
}
