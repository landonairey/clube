using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The main menu's load page (GL208): the saved games, newest first, with the newest selected. Click one to
    /// select it, then Load (or Enter); a second click on the selected one loads it too. Back (or Esc) returns.
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
        private int selected;
        private Vector2 scroll;

        public LoadGamePage(List<SaveEntry> saves)
        {
            this.saves = saves;
            selected = 0;
        }

        /// <summary>The save picked, or null with none listed.</summary>
        public SaveEntry Selected => selected >= 0 && selected < saves.Count ? saves[selected] : null;

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
                bool isSelected = i == selected;
                string text = $"{(isSelected ? "●" : "○")}  <b>{Escape(save.Name)}</b>\n      <size=12>{Describe(save)}</size>";
                // A click flips the toggle: on another row it selects that one, on the selected row it loads.
                if (GUILayout.Toggle(isSelected, text, styles.ModeButton, GUILayout.Height(52f)) != isSelected)
                {
                    if (isSelected)
                    {
                        choice = Choice.Load;
                    }
                    selected = i;
                }
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(14f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Back;
            }
            GUILayout.Space(12f);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = Selected != null;
            if (GUILayout.Button("Load", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Load;
            }
            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                switch (current.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        choice = Selected != null ? Choice.Load : choice;
                        break;
                    case KeyCode.Escape:
                        choice = Choice.Back;
                        break;
                    case KeyCode.UpArrow:
                        selected = Mathf.Max(0, selected - 1);
                        current.Use();
                        break;
                    case KeyCode.DownArrow:
                        selected = Mathf.Min(saves.Count - 1, selected + 1);
                        current.Use();
                        break;
                }
            }
            return choice;
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
