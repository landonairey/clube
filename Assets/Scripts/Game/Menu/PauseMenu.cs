using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Esc in the game (GL204): a small popup over a stopped game with Back, Save and Exit, and Exit Without
    /// Saving. Esc first closes whatever player screen is open (inventory, journal, a station, the merchant);
    /// with none open it opens the popup, and Esc again goes back. Saving goes through
    /// <see cref="GameSessionHost"/>; both exits return to the main menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private const float Width = 300f;
        private const float ButtonHeight = 40f;

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
        private CursorLockMode cursorBefore;
        private bool cursorVisibleBefore;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
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
            if (IsOpen)
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

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            float height = 56f + 3f * (ButtonHeight + 10f) + (problem != null ? 40f : 0f) + 16f;
            var area = new Rect((Screen.width - Width) * 0.5f, (Screen.height - height) * 0.5f, Width, height);
            InventoryGridGui.Panel(area);
            GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 12f, area.width - 40f, area.height - 24f));
            GUILayout.Label("Paused", titleStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                SetOpen(false);
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Save and Exit", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                SaveAndExit();
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Exit Without Saving", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                Exit();
            }
            if (problem != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Couldn't save: " + problem, problemStyle);
            }
            GUILayout.EndArea();
        }

        private void SaveAndExit()
        {
            if (host == null)
            {
                problem = "nothing here to save.";
                return;
            }
            if (!host.Save(out string why))
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
            problemStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.6f, 0.5f) } };
        }
    }
}
