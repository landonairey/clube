using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The main menu's load page (GL208): the saved games, newest first, none selected (GL786). A click selects one
    /// (<see cref="SaveListSelection"/>); only Load (or Enter, or a double-click) loads it, and Load and Delete stay off
    /// until a save is selected. Delete removes the selected save after asking once (GL230). Back (or Esc) returns.
    /// </summary>
    public sealed class LoadGamePage
    {
        public enum Choice
        {
            None,
            Load,
            Back,
        }

        private readonly List<SaveEntry> saves;
        private readonly SaveListSelection selection;
        private Vector2 scroll;

        // True while Delete asks whether it's meant (GL230).
        private bool confirmingDelete;
        private string problem;

        public LoadGamePage(List<SaveEntry> saves)
        {
            this.saves = saves;
            selection = new SaveListSelection(saves.Count);
        }

        /// <summary>The save picked, or null with none selected.</summary>
        public SaveEntry Selected => selection.HasSelection ? saves[selection.Selected] : null;

        /// <summary>Draws the page in an area; returns what the player chose this frame.</summary>
        public Choice Draw(Rect area, MenuStyles styles)
        {
            Choice choice = Choice.None;
            GUILayout.BeginArea(area);
            GUILayout.Label("Load game", styles.Heading);
            GUILayout.Space(14f);

            if (saves.Count == 0)
            {
                GUILayout.Label("No saved games yet.", styles.Caption);
            }
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < saves.Count; i++)
            {
                SaveEntry save = saves[i];
                bool isSelected = i == selection.Selected;
                string text = $"{(isSelected ? "●" : "○")}  <b>{Escape(save.Name)}</b>\n      <size=12>{Describe(save)}</size>";
                // Any click flips the toggle: it selects the row, and only a double-click on it loads (GL786).
                if (GUILayout.Toggle(isSelected, text, styles.ModeButton, GUILayout.MinHeight(52f)) != isSelected)
                {
                    bool load = selection.Click(i, Time.realtimeSinceStartup);
                    if (load && !confirmingDelete)
                    {
                        choice = Choice.Load;
                    }
                    confirmingDelete = false;
                }
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();

            if (problem != null)
            {
                GUILayout.Label(problem, styles.Caption);
            }
            GUILayout.Space(14f);
            if (confirmingDelete && Selected != null)
            {
                DrawConfirmDelete(styles);
            }
            else
            {
                choice = DrawButtons(styles, choice);
            }
            GUILayout.EndArea();

            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                switch (current.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        choice = Selected != null && !confirmingDelete ? Choice.Load : choice;
                        break;
                    case KeyCode.Escape:
                        if (confirmingDelete)
                        {
                            confirmingDelete = false;
                            current.Use();
                        }
                        else
                        {
                            choice = Choice.Back;
                        }
                        break;
                    case KeyCode.UpArrow:
                        selection.Move(-1);
                        confirmingDelete = false;
                        current.Use();
                        break;
                    case KeyCode.DownArrow:
                        selection.Move(1);
                        confirmingDelete = false;
                        current.Use();
                        break;
                }
            }
            return choice;
        }

        // Back, Delete and Load.
        private Choice DrawButtons(MenuStyles styles, Choice choice)
        {
            GUILayout.BeginHorizontal();
            if (GameUi.LayoutButton("Back", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Back;
            }
            GUILayout.Space(12f);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = Selected != null;
            if (GameUi.LayoutButton("Delete", styles.Button, GUILayout.Height(46f)))
            {
                confirmingDelete = true;
                problem = null;
            }
            GUILayout.Space(12f);
            if (GameUi.LayoutButton("Load", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Load;
            }
            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
            return choice;
        }

        // "Delete this save?" with Cancel and Delete: a save can't come back.
        private void DrawConfirmDelete(MenuStyles styles)
        {
            GUILayout.Label($"Delete \"{Escape(Selected.Name)}\"? This can't be undone.", styles.Caption);
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GameUi.LayoutButton("Cancel", styles.Button, GUILayout.Height(46f)))
            {
                confirmingDelete = false;
            }
            GUILayout.Space(12f);
            if (GameUi.LayoutButton("Delete", styles.Button, GUILayout.Height(46f)))
            {
                confirmingDelete = false;
                if (SaveSlots.Delete(Selected.Path, out string why))
                {
                    saves.RemoveAt(selection.Selected);
                    selection.RemoveSelected();
                }
                else
                {
                    problem = "Couldn't delete the save: " + why;
                }
            }
            GUILayout.EndHorizontal();
        }

        private static string Describe(SaveEntry save)
        {
            string world = !string.IsNullOrEmpty(save.Header.WorldName) ? save.Header.WorldName + " · " : "";
            DateTime saved = save.Header.SavedAt.ToLocalTime();
            return Escape($"{world}{save.Header.Mode} · seed {save.Header.Seed} · saved {saved:d MMM yyyy, HH:mm}");
        }

        // Names are the player's text: keep a "<" in one from reading as rich text.
        private static string Escape(string text)
        {
            return text.Replace("<", "<​");
        }
    }
}
