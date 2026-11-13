using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Ends the playable map for real (GL640): past the map's edge the player comes into the limit of the side they
    /// are on (<see cref="MapLayout.LimitAt"/>), where each step outwards carries them less far, down to nothing at
    /// the side's stop line, while they are told why in that side's own words
    /// (<see cref="MapBoundaryType.LimitMessage"/>): the sea too cold and strong, the desert too hot, the range too
    /// high, the world's end. Steps back in, and along the line, are never slowed. Everything inside the stop line
    /// is voxel ground (<see cref="MapShape.Floor"/> to <see cref="MapShape.Ceiling"/>), so nothing the player
    /// reaches is surface mesh alone.
    /// </summary>
    /// <remarks>
    /// Added to the player by <see cref="MapSession"/> when a map is ready. It runs after the player's own movement
    /// (<c>LateUpdate</c>) and takes back the outward part of the frame's step, through the
    /// <see cref="CharacterController"/> so the correction collides like any other move. A player who is somehow
    /// past the line (set down there, knocked there) is walked back to it.
    /// </remarks>
    [DisallowMultipleComponent]
    public class MapBoundaryLimit : MonoBehaviour
    {
        // Metres a second a player past the stop line is carried back in.
        private const float ReturnSpeed = 3f;

        private MapSession session;
        private PlayerController controller;
        private Vector3 last;
        private bool haveLast;
        private GUIStyle style;
        private Texture2D shade;

        /// <summary>How far into the limit the player is: 0 free, 1 at the stop line.</summary>
        public float Limit { get; private set; }

        /// <summary>What the player is being told, or null when they are clear of every limit.</summary>
        public string Message { get; private set; }

        /// <summary>The map session whose layout limits this player.</summary>
        public void Bind(MapSession mapSession)
        {
            session = mapSession;
            haveLast = false;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            MapLayout layout = session != null && session.IsReady ? session.Layout : null;
            if (layout == null || controller == null || !controller.Body.enabled)
            {
                Limit = 0f;
                Message = null;
                haveLast = false;
                return;
            }
            Transform world = session.WorldView.transform;
            Vector3 now = world.InverseTransformPoint(transform.position);

            Limit = layout.LimitAt(now.x, now.z);
            if (Limit <= 0f)
            {
                Message = null;
                last = now;
                haveLast = true;
                return;
            }
            Vector2 out2 = layout.OutwardAt(now.x, now.z);
            Vector2 correction = Vector2.zero;
            if (haveLast)
            {
                var step = new Vector2(now.x - last.x, now.z - last.z);
                correction = MapLimit.Held(step, out2, Limit) - step;
            }
            if (Limit >= 1f)
            {
                correction -= out2 * (ReturnSpeed * Time.deltaTime);
            }
            if (correction.sqrMagnitude > 1e-10f)
            {
                controller.Body.Move(world.TransformVector(new Vector3(correction.x, 0f, correction.y)));
                now = world.InverseTransformPoint(transform.position);
                Limit = layout.LimitAt(now.x, now.z);
            }
            Message = layout.BoundaryAt(now.x, now.z).LimitMessage;
            last = now;
            haveLast = true;
        }

        private void OnGUI()
        {
            if (Limit <= 0.02f || string.IsNullOrEmpty(Message) || controller == null || controller.IsInMenu)
            {
                return;
            }
            if (shade == null)
            {
                shade = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                shade.SetPixel(0, 0, Color.white);
                shade.Apply();
            }
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            style.fontSize = GameUi.Font(22);
            Color before = GUI.color;
            // The view dims as the limit closes in, and the side's own words come up over it.
            GUI.color = new Color(0f, 0f, 0f, 0.35f * MapLimit.Resistance(Limit));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), shade);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Limit * 3f));
            float width = Mathf.Min(Screen.width * 0.7f, 900f);
            GameUi.OutlinedLabel(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.22f, width, 80f), Message, style);
            GUI.color = before;
        }

        private void OnDestroy()
        {
            if (shade != null)
            {
                Destroy(shade);
            }
        }
    }
}
