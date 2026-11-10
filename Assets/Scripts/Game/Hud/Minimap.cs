using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Clube.Game
{
    /// <summary>
    /// A minimap in the top-left corner (GL461): a top-down view of the land round the player, north up, with an arrow for
    /// the player facing their way. An orthographic camera above the player renders into a small texture a few times a
    /// second (switched on for one frame at a time), so the map shows the real terrain, trees, water and buildings, edits
    /// included, without drawing the world a second time every frame. The lab HUD box sits under it (<see cref="HudLayout"/>).
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Tooltip("Metres from the player to the map's edge.")]
        [SerializeField, Min(8f)]
        private float radius = 48f;

        [Tooltip("The map's size on screen, in pixels.")]
        [SerializeField, Min(64f)]
        private float size = 260f;

        [Tooltip("Seconds between redraws of the map.")]
        [SerializeField, Min(0.05f)]
        private float interval = 0.4f;

        [Tooltip("How high over the player the map's camera looks down from, in metres: under the clouds.")]
        [SerializeField, Min(10f)]
        private float height = 60f;

        [Tooltip("Pixels of the map's texture a side.")]
        [SerializeField, Min(64)]
        private int resolution = 256;

        private const float Margin = 10f;

        private PlayerController player;
        private Camera mapCamera;
        private RenderTexture texture;
        private Texture2D arrow;
        private float nextDraw;
        private bool drawnThisFrame;

        /// <summary>Something to mark on the map, such as who a quest says to talk to (GL513); null for nothing.</summary>
        public Transform Marker { get; set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            if (mapCamera == null)
            {
                texture = new RenderTexture(resolution, resolution, 24) { name = "Minimap" };
                var host = new GameObject("Minimap camera");
                mapCamera = host.AddComponent<Camera>();
                mapCamera.orthographic = true;
                mapCamera.orthographicSize = radius;
                mapCamera.nearClipPlane = 1f;
                mapCamera.farClipPlane = height + 200f;
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = new Color(0.1f, 0.12f, 0.14f);
                mapCamera.targetTexture = texture;
                mapCamera.depth = -10f;
                mapCamera.enabled = false;
                UniversalAdditionalCameraData data = mapCamera.GetUniversalAdditionalCameraData();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresDepthTexture = false;
                data.requiresColorTexture = false;
            }
        }

        private void OnDisable()
        {
            if (mapCamera != null)
            {
                mapCamera.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (mapCamera != null)
            {
                Destroy(mapCamera.gameObject);
            }
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            if (arrow != null)
            {
                Destroy(arrow);
            }
        }

        private void LateUpdate()
        {
            if (mapCamera == null)
            {
                return;
            }
            // Off again after the frame it drew in.
            if (drawnThisFrame)
            {
                mapCamera.enabled = false;
                drawnThisFrame = false;
            }
            if (Time.unscaledTime < nextDraw || player == null || !player.enabled)
            {
                return;
            }
            nextDraw = Time.unscaledTime + interval;
            Vector3 at = transform.position;
            mapCamera.transform.SetPositionAndRotation(at + Vector3.up * height, Quaternion.Euler(90f, 0f, 0f));
            mapCamera.enabled = true;
            drawnThisFrame = true;
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (texture == null || player == null || !player.enabled || Event.current.type != EventType.Repaint)
            {
                return;
            }
            var frame = new Rect(Margin, Margin, size, size);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(frame.x - 3f, frame.y - 3f, frame.width + 6f, frame.height + 6f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.DrawTexture(frame, texture, ScaleMode.StretchToFill, false);

            // The player: an arrow at the middle, turned to where they face.
            arrow ??= PaintArrow();
            float yaw = transform.eulerAngles.y;
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(yaw, frame.center);
            GUI.DrawTexture(new Rect(frame.center.x - 9f, frame.center.y - 11f, 18f, 22f), arrow);
            GUI.matrix = matrix;
            GUI.Label(new Rect(frame.center.x - 10f, frame.y + 2f, 20f, 22f), "N");
            DrawMarker(frame);
            HudLayout.ReportMinimap(frame.yMax + 3f);
        }

        // The quest marker: a yellow square where it is, held at the map's edge when it's further than the map shows.
        private void DrawMarker(Rect frame)
        {
            if (Marker == null || !Marker.gameObject.activeInHierarchy)
            {
                return;
            }
            Vector3 offset = Marker.position - transform.position;
            // North (+z) is up on the map.
            var onMap = new Vector2(offset.x, -offset.z) / radius * (size * 0.5f);
            float limit = size * 0.5f - 8f;
            float far = Mathf.Max(Mathf.Abs(onMap.x), Mathf.Abs(onMap.y));
            if (far > limit)
            {
                onMap *= limit / far;
            }
            Vector2 at = frame.center + onMap;
            Color previous = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(at.x - 7f, at.y - 7f, 14f, 14f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.15f);
            GUI.DrawTexture(new Rect(at.x - 5f, at.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // A white arrow with a dark edge, pointing up.
        private static Texture2D PaintArrow()
        {
            const int width = 18, tall = 22;
            var texture = new Texture2D(width, tall, TextureFormat.RGBA32, false) { name = "Minimap arrow" };
            for (int y = 0; y < tall; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // A triangle from the bottom corners to the top middle, notched at the bottom.
                    float fromTop = (tall - 1 - y) / (float)(tall - 1);
                    float half = fromTop * (width * 0.5f);
                    float dx = Mathf.Abs(x + 0.5f - width * 0.5f);
                    bool inside = dx <= half && !(y < 5 && dx < (5 - y) * 0.9f);
                    bool edge = inside && (dx > half - 1.5f || y < 1);
                    texture.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.1f, 0.1f, 0.1f) : Color.white);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
