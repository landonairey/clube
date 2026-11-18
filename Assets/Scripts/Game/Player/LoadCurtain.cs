using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Covers the view while the player waits for ground (GL653): from the scene's start, through the map's survey, until
    /// <see cref="PlayerSpawn"/> has set the player down, with how far the ground round the spawn has got. A wait shorter
    /// than <see cref="delay"/> seconds (falling out of the world, being dug out of a hillside) shows nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerSpawn))]
    public class LoadCurtain : MonoBehaviour
    {
        [Tooltip("Seconds a wait must last before the curtain is drawn; the first wait of the scene is covered at once.")]
        [SerializeField, Min(0f)]
        private float delay = 0.4f;

        [SerializeField]
        private Color colour = new Color(0.05f, 0.06f, 0.07f, 1f);

        private PlayerSpawn spawn;
        private bool placedOnce;
        private float waitingSince = -1f;

        /// <summary>True while the curtain covers the view.</summary>
        public bool IsUp { get; private set; }

        private void Awake()
        {
            spawn = GetComponent<PlayerSpawn>();
        }

        private void Update()
        {
            if (spawn.IsPlaced)
            {
                placedOnce = true;
                waitingSince = -1f;
                IsUp = false;
                return;
            }
            if (waitingSince < 0f)
            {
                waitingSince = Time.unscaledTime;
            }
            IsUp = !placedOnce || Time.unscaledTime - waitingSince >= delay;
        }

        private void OnDisable()
        {
            IsUp = false;
        }

        private void OnGUI()
        {
            if (!IsUp || Event.current.type != EventType.Repaint)
            {
                return;
            }
            Color was = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.color = Color.white;
            MapSession session = MapSession.Current;
            LoadingMessage.Draw(session != null && !session.IsReady ? LoadingMessage.Surveying : LoadingMessage.Ground(spawn.Progress));
            GUI.color = was;
        }
    }
}
