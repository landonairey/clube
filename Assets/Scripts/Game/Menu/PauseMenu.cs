using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Esc in the game (GL204): a small popup over a stopped game with Back, Graphics (a smaller popup of its own,
    /// <see cref="GraphicsSettingsPage"/>, GL209), Save and Exit, and Exit Without Saving. Esc first closes whatever player screen is open (inventory, journal, a station, the merchant);
    /// with none open it opens the popup, and Esc again goes back. Save and Exit first asks for the save's name
    /// (GL207), filled in with the default (world, mode, time); saving goes through <see cref="GameSessionHost"/>.
    /// Both exits return to the main menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private const float Width = 300f;
        private const float NamingWidth = 460f;
        private const float ButtonHeight = 40f;
        private const string NameField = "SaveName";

        [Tooltip("Saves the game for Save and Exit.")]
        [SerializeField]
        private GameSessionHost host;

        [Tooltip("The player, whose screens Esc closes and whose cursor the popup frees.")]
        [SerializeField]
        private PlayerController player;

        private InventoryScreen inventoryScreen;
        private JournalScreen journalScreen;
        private CraftingJournalScreen craftingJournal;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private string problem;

        // The save name being typed, while Save and Exit asks for one (GL207); null otherwise.
        private string saveName;
        private bool focusName;

        // The graphics popup while it's open (GL209); null otherwise.
        private GraphicsSettingsPage graphics;
        private CursorLockMode cursorBefore;
        private bool cursorVisibleBefore;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle hintStyle;
        private GUIStyle fieldStyle;
        private GUIStyle problemStyle;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (player != null)
            {
                inventoryScreen = player.GetComponent<InventoryScreen>();
                journalScreen = player.GetComponent<JournalScreen>();
                craftingJournal = player.GetComponent<CraftingJournalScreen>();
                stationPanel = player.GetComponent<StationPanel>();
                merchantPanel = player.GetComponent<MerchantPanel>();
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }
            if (IsOpen && saveName != null)
            {
                // Naming the save: back to the popup.
                saveName = null;
                problem = null;
            }
            else if (IsOpen && graphics != null)
            {
                graphics = null;
            }
            else if (IsOpen)
            {
                SetOpen(false);
            }
            else if (!CloseOpenScreen())
            {
                SetOpen(true);
            }
        }

        // Closes the player's open screen, if any; true when one was open.
        private bool CloseOpenScreen()
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (inventoryScreen != null && inventoryScreen.IsOpen)
            {
                inventoryScreen.Close();
                return true;
            }
            if (journalScreen != null && journalScreen.IsOpen)
            {
                journalScreen.Close();
                return true;
            }
            if (craftingJournal != null && craftingJournal.IsOpen)
            {
                craftingJournal.SetOpen(false);
                return true;
            }
            if (stationPanel != null && stationPanel.IsOpen)
            {
                stationPanel.Close();
                return true;
            }
            if (merchantPanel != null && merchantPanel.IsOpen)
            {
                merchantPanel.Close();
                return true;
            }
            return false;
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            problem = null;
            saveName = null;
            graphics = null;
            // The game stops under the popup: the clock, stations and the player.
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                cursorBefore = Cursor.lockState;
                cursorVisibleBefore = Cursor.visible;
            }
            if (player != null && player.isActiveAndEnabled)
            {
                player.IsInMenu = open;
            }
            else if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = cursorBefore;
                Cursor.visible = cursorVisibleBefore;
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }
            CreateStyles();
            // Over the lab panel and the HUDs.
            GUI.depth = -100;

            // Lighter under the graphics popup, so a change to the field of view shows behind it.
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, graphics != null ? 0.15f : 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (saveName != null)
            {
                DrawNaming();
            }
            else if (graphics != null)
            {
                BeginPopup(GraphicsSettingsPage.Width, GraphicsSettingsPage.Height);
                if (graphics.Draw(titleStyle, labelStyle, hintStyle, buttonStyle))
                {
                    graphics = null;
                }
                GUILayout.EndArea();
            }
            else
            {
                DrawPaused();
            }
        }

        private void DrawPaused()
        {
            float height = 56f + 4f * (ButtonHeight + 10f) + 16f;
            BeginPopup(Width, height);
            GUILayout.Label("Paused", titleStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                SetOpen(false);
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Graphics", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                OpenGraphics();
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Save and Exit", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                StartNaming();
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Exit Without Saving", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                Exit();
            }
            GUILayout.EndArea();
        }

        // The save's name (GL207), filled in with the default: world, mode and the time.
        private void DrawNaming()
        {
            // Enter saves; read before the text field takes the key.
            Event current = Event.current;
            bool enter = current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);

            float height = 56f + 22f + 40f + 22f + ButtonHeight + 28f + (problem != null ? 40f : 0f);
            BeginPopup(NamingWidth, height);
            GUILayout.Label("Save game", titleStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Save name", labelStyle);
            GUI.SetNextControlName(NameField);
            saveName = GUILayout.TextField(saveName, SaveNames.MaxLength, fieldStyle, GUILayout.Height(34f));
            if (focusName)
            {
                GUI.FocusControl(NameField);
                focusName = false;
            }
            string name = SaveNames.Clean(saveName);
            GUILayout.Label(name.Length == 0 ? "Empty: saved as " + host.DefaultSaveName
                : SaveSlots.Exists(name) ? "Replaces the save with this name." : " ", hintStyle);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                saveName = null;
                problem = null;
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Height(ButtonHeight)) || enter)
            {
                SaveAndExit();
            }
            GUILayout.EndHorizontal();
            if (problem != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Couldn't save: " + problem, problemStyle);
            }
            GUILayout.EndArea();
        }

        // A dark panel in the middle of the screen, with a layout area inside it (end it with GUILayout.EndArea).
        private static void BeginPopup(float width, float height)
        {
            width = Mathf.Min(width, Screen.width - 32f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(area);
            GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 12f, area.width - 40f, area.height - 24f));
        }

        private void OpenGraphics()
        {
            graphics = new GraphicsSettingsPage();
        }

        private void StartNaming()
        {
            if (host == null)
            {
                return;
            }
            saveName = host.DefaultSaveName;
            focusName = true;
            problem = null;
        }

        private void SaveAndExit()
        {
            if (!host.Save(saveName, out string why))
            {
                problem = why;
                return;
            }
            Exit();
        }

        private void Exit()
        {
            Time.timeScale = 1f;
            IsOpen = false;
            GameScenes.LoadMainMenu();
        }

        private void CreateStyles()
        {
            if (titleStyle != null)
            {
                return;
            }
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.72f, 0.72f, 0.7f) } };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            problemStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.6f, 0.5f) } };
        }
    }
}
