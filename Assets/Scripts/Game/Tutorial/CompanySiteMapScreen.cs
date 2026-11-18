using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The map on M during the tutorial (GL760): the Mining Corp's site (<see cref="CompanySiteMap"/>) with its places named,
    /// the player, and the minimap's quest mark. It is on the player in the Tutorial scene; on the bounded map the tutorial
    /// adds it for as long as the job lasts (<see cref="Bind"/>), in place of <see cref="WorldMapScreen"/>.
    /// </summary>
    public class CompanySiteMapScreen : MonoBehaviour, IMapScreen
    {
        private const int Resolution = 512;

        [Tooltip("Opens and closes the map (M).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerController player;
        private Minimap minimap;
        private Transform site;
        private bool hasPauseMenu;
        private int openedFrame = -1;
        private Texture2D picture;
        private Texture2D arrow;
        private GUIStyle nameStyle, hintStyle;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            minimap = GetComponent<Minimap>();
        }

        /// <summary>Sets the key and where the site's map space sits, for a screen added at runtime.</summary>
        public void Bind(InputActionReference toggle, Transform siteOrigin)
        {
            toggleAction = toggle;
            site = siteOrigin;
            toggleAction?.action.Enable();
        }

        private void Start()
        {
            if (site == null)
            {
                WorldView world = FindAnyObjectByType<WorldView>();
                site = world != null ? world.transform : null;
            }
            // With a pause menu in the scene, Esc reaches the map through it (GL204).
            hasPauseMenu = FindFirstObjectByType<PauseMenu>() != null;
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            if (picture != null)
            {
                Destroy(picture);
            }
            if (arrow != null)
            {
                Destroy(arrow);
            }
        }

        private void Update()
        {
            if (PauseMenu.IsPaused || player == null || !player.enabled || toggleAction == null)
            {
                return;
            }
            bool pressed = toggleAction.action.WasPressedThisFrame();
            if (IsOpen)
            {
                bool escape = !hasPauseMenu && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                if (escape || (pressed && openedFrame != Time.frameCount))
                {
                    Close();
                }
            }
            else if (pressed && !player.IsInMenu && !player.MenuClosedThisFrame)
            {
                Open();
            }
        }

        /// <summary>Opens the map.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            IsOpen = true;
            openedFrame = Time.frameCount;
            player.IsInMenu = true;
        }

        /// <summary>Closes the map (M, or Esc).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            player.IsInMenu = false;
        }

        private void EnsureAssets()
        {
            if (picture == null)
            {
                picture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false)
                {
                    name = "Company site map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                picture.SetPixels32(CompanySiteMap.Render(Resolution));
                picture.Apply(false);
            }
            arrow ??= Minimap.PaintArrow();
            if (nameStyle == null || nameStyle.fontSize != GameUi.Font(14))
            {
                nameStyle = new GUIStyle(GUI.skin.label) { font = GameFont.Get(), alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(14), fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = Color.white;
                hintStyle = new GUIStyle(nameStyle) { fontSize = GameUi.Font(13), fontStyle = FontStyle.Normal };
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }
            GameUi.Apply();
            EnsureAssets();
            GUI.depth = GameUi.MapScreenDepth;
            float side = Mathf.Min(Screen.width, Screen.height) * 0.86f;
            var view = new Rect((Screen.width - side) * 0.5f, (Screen.height - side) * 0.5f, side, side);

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(view, picture, ScaleMode.StretchToFill, false);

            Place(view, "Warehouse", CompanyWork.Warehouse.center);
            Place(view, "Work fields", CompanyWork.Fields.center);
            Place(view, "Tool shed", Flat(CompanyWarehouse.ShedSpot));
            Place(view, "Ore cart", Flat(CompanyWarehouse.CartSpot));
            Place(view, "The rock face", new Vector2(CompanyWork.Fields.center.x, CompanyWork.Fields.yMax + 16f));
            DrawQuestMark(view);
            DrawPlayer(view);

            float hintHeight = hintStyle.lineHeight + 8f;
            GUI.Label(new Rect(view.x, Mathf.Min(view.yMax + 2f, Screen.height - hintHeight), view.width, hintHeight), "M or Esc: close", hintStyle);
        }

        private static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);

        private static Vector2 ToScreen(Rect view, Vector2 point)
        {
            Vector2 fraction = CompanySiteMap.Fraction(point);
            return new Vector2(view.x + Mathf.Clamp01(fraction.x) * view.width, view.yMax - Mathf.Clamp01(fraction.y) * view.height);
        }

        // A point of the world in the site's map space.
        private Vector2 OnSite(Vector3 position)
        {
            return Flat(site != null ? site.InverseTransformPoint(position) : position);
        }

        private void Place(Rect view, string text, Vector2 point)
        {
            Vector2 at = ToScreen(view, point);
            GameUi.OutlinedLabel(new Rect(at.x - 110f, at.y - GameUi.Font(14), 220f, GameUi.Font(14) * 2f), text, nameStyle);
        }

        private void DrawQuestMark(Rect view)
        {
            Transform marker = minimap != null ? minimap.Marker : null;
            if (marker == null || !marker.gameObject.activeInHierarchy)
            {
                return;
            }
            Vector2 at = ToScreen(view, OnSite(marker.position));
            GUI.color = new Color(0.1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(at.x - 8f, at.y - 8f, 16f, 16f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.15f);
            GUI.DrawTexture(new Rect(at.x - 6f, at.y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawPlayer(Rect view)
        {
            Vector2 at = ToScreen(view, OnSite(transform.position));
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(transform.eulerAngles.y, at);
            GUI.DrawTexture(new Rect(at.x - 9f, at.y - 11f, 18f, 22f), arrow);
            GUI.matrix = matrix;
        }
    }
}
