using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The demo's main menu (GL201, GL202), the first scene of the exe: a splash of the world with the title,
    /// New, Load and Exit, and the build's version in the corner (<see cref="BuildInfo"/>). New opens the
    /// new-game page (<see cref="NewGamePage"/>); Load opens the list of saved games (<see cref="LoadGamePage"/>,
    /// GL208), and is greyed out without any. Drawn with IMGUI like the rest of the game's screens, and sized
    /// to fit small windows.
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
        private LoadGamePage loadGame;
        private List<SaveEntry> saves;
        private string problem;

        private void Awake()
        {
            // Back from a game: nothing stopped, the cursor free.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSession.End();
            saves = SaveSlots.List();
        }

        private void OnGUI()
        {
            styles ??= new MenuStyles();
            DrawBackground();

            float width = Mathf.Min(Screen.width - 32f, newGame != null || loadGame != null ? 460f : 520f);
            var column = new Rect((Screen.width - width) * 0.5f, 16f, width, Screen.height - 48f);
            if (newGame != null)
            {
                DrawNewGame(column);
            }
            else if (loadGame != null)
            {
                DrawLoadGame(column);
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
            GUI.enabled = saves.Count > 0;
            if (GUILayout.Button("Load", styles.Button, GUILayout.Height(48f)))
            {
                OpenLoadGame();
            }
            GUI.enabled = wasEnabled;
            GUILayout.Label(SavesCaption(), styles.Caption);
            GUILayout.Space(10f);
            if (GUILayout.Button("Exit", styles.Button, GUILayout.Height(48f)))
            {
                GameScenes.Quit();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawNewGame(Rect column)
        {
            Rect panel = Page(column, 650f);
            switch (newGame.Draw(Inside(panel), styles))
            {
                case NewGamePage.Choice.Back:
                    newGame = null;
                    break;
                case NewGamePage.Choice.Start:
                    GameSession.StartNew(newGame.Mode, newGame.Seed, newGame.WorldName);
                    GameScenes.LoadGame();
                    break;
            }
        }

        private void DrawLoadGame(Rect column)
        {
            Rect panel = Page(column, 620f);
            LoadGamePage.Choice choice = loadGame.Draw(Inside(panel), styles);
            if (problem != null)
            {
                GUI.Label(new Rect(panel.x, panel.yMax + 4f, panel.width, 40f), problem, styles.Caption);
            }
            switch (choice)
            {
                case LoadGamePage.Choice.Back:
                    loadGame = null;
                    problem = null;
                    break;
                case LoadGamePage.Choice.Load:
                    Load(loadGame.Selected);
                    break;
            }
        }

        private void OpenNewGame()
        {
            newGame = new NewGamePage(worldConfig != null ? worldConfig.Terrain.Seed : 1);
        }

        private void OpenLoadGame()
        {
            saves = SaveSlots.List();
            loadGame = new LoadGamePage(saves);
            problem = null;
        }

        private void Load(SaveEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            GameSave loaded = SaveSlots.Read(entry.Path, out string why);
            if (loaded == null)
            {
                problem = "Couldn't load the save: " + why;
                return;
            }
            GameSession.StartLoaded(loaded);
            GameScenes.LoadGame();
        }

        private string SavesCaption()
        {
            if (saves.Count == 0)
            {
                return "No saved game yet";
            }
            return saves.Count == 1 ? "1 saved game" : $"{saves.Count} saved games";
        }

        // A shaded page in the middle of the column, as tall as it wants or as the window allows.
        private Rect Page(Rect column, float height)
        {
            height = Mathf.Min(height, column.height);
            var panel = new Rect(column.x - 12f, column.y + (column.height - height) * 0.5f, column.width + 24f, height);
            Shade(panel);
            return panel;
        }

        private static Rect Inside(Rect panel)
        {
            return new Rect(panel.x + 24f, panel.y + 16f, panel.width - 48f, panel.height - 32f);
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
            // A little darker all over, so the text reads.
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
    }
}
