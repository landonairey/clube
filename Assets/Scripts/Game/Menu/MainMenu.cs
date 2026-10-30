using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The demo's main menu (GL201, GL202), the first scene of the exe: a splash of the world with the title,
    /// New, Load and Exit, and the build's version in the corner (<see cref="BuildInfo"/>). New opens the
    /// new-game page (<see cref="NewGamePage"/>); Load carries on the saved game, and is greyed out without
    /// one. Drawn with IMGUI like the rest of the game's screens, and sized to fit small windows.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private const float ColumnWidth = 300f;

        [Tooltip("The World mode's config: its seed is the new-game page's starting seed.")]
        [SerializeField]
        private WorldConfig worldConfig;

        [Tooltip("The picture behind the menu; a plain colour without one.")]
        [SerializeField]
        private Texture2D splash;

        [SerializeField]
        private string title = "clube";

        [SerializeField]
        private string subtitle = "Dig, smelt and build in a world of voxels";

        private MenuStyles styles;
        private NewGamePage newGame;
        private SaveHeader save;
        private string problem;

        private void Awake()
        {
            // Back from a game: nothing stopped, the cursor free.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSession.End();
            save = SaveSlot.ReadHeader();
        }

        private void OnGUI()
        {
            styles ??= new MenuStyles();
            DrawBackground();

            float width = Mathf.Min(Screen.width - 32f, newGame != null ? 460f : 520f);
            var column = new Rect((Screen.width - width) * 0.5f, 16f, width, Screen.height - 48f);
            if (newGame != null)
            {
                DrawNewGame(column);
            }
            else
            {
                DrawTitle(column);
            }
            GUI.Label(new Rect(16f, Screen.height - 30f, Screen.width - 32f, 20f), BuildInfo.Version, styles.Corner);
        }

        private void DrawTitle(Rect column)
        {
            Shade(new Rect(column.x - 12f, column.y + column.height * 0.5f - 195f, column.width + 24f, 390f));
            GUILayout.BeginArea(column);
            GUILayout.FlexibleSpace();
            GUILayout.Label(title, styles.Title);
            GUILayout.Label(subtitle, styles.Subtitle);
            GUILayout.Space(36f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(ColumnWidth, column.width)));
            if (GUILayout.Button("New", styles.Button, GUILayout.Height(48f)))
            {
                OpenNewGame();
            }
            GUILayout.Space(10f);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = save != null;
            if (GUILayout.Button("Load", styles.Button, GUILayout.Height(48f)))
            {
                Load();
            }
            GUI.enabled = wasEnabled;
            GUILayout.Label(save != null ? Describe(save) : "No saved game yet", styles.Caption);
            GUILayout.Space(10f);
            if (GUILayout.Button("Exit", styles.Button, GUILayout.Height(48f)))
            {
                GameScenes.Quit();
            }
            if (problem != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(problem, styles.Caption);
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawNewGame(Rect column)
        {
            // A darker panel behind the page, so the options read over the picture.
            var panel = new Rect(column.x - 12f, column.y + column.height * 0.5f - 290f, column.width + 24f, 580f);
            Shade(panel);

            switch (newGame.Draw(new Rect(column.x + 12f, panel.y + 10f, column.width - 24f, panel.height - 20f), styles))
            {
                case NewGamePage.Choice.Back:
                    newGame = null;
                    break;
                case NewGamePage.Choice.Start:
                    GameSession.StartNew(newGame.Mode, newGame.Seed);
                    GameScenes.LoadGame();
                    break;
            }
        }

        private void OpenNewGame()
        {
            newGame = new NewGamePage(worldConfig != null ? worldConfig.Terrain.Seed : 1);
        }

        private void Load()
        {
            GameSave loaded = SaveSlot.Read(out string why);
            if (loaded == null)
            {
                problem = "Couldn't load the save: " + why;
                return;
            }
            GameSession.StartLoaded(loaded);
            GameScenes.LoadGame();
        }

        private void DrawBackground()
        {
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (splash != null)
            {
                GUI.DrawTexture(screen, splash, ScaleMode.ScaleAndCrop);
            }
            else
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.1f, 0.12f, 0.14f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = previous;
            }
            // Darker towards the middle column, where the text is.
            Color before = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = before;
        }

        // A darker panel behind text, so it reads over the picture.
        private static void Shade(Rect panel)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.05f, 0.06f, 0.07f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string Describe(SaveHeader header)
        {
            DateTime saved = header.SavedAt.ToLocalTime();
            return $"{header.Mode} · seed {header.Seed} · saved {saved:d MMM yyyy, HH:mm}";
        }
    }
}
