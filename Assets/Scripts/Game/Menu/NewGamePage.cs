using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The main menu's new-game page (GL203, GL206): the game mode, with only the playable ones enabled (the rest
    /// greyed out as work in progress), the world's name, and its seed, typed or rolled. Any text makes a seed: a number
    /// is used as it is, other text is hashed (<see cref="SeedFrom"/>).
    /// </summary>
    public sealed class NewGamePage
    {
        public enum Choice
        {
            None,
            Start,
            Back,
        }

        private struct ModeEntry
        {
            public GameMode Mode;
            public string Name;
            public string About;
            public bool Playable;
        }

        private static readonly ModeEntry[] Modes =
        {
            new ModeEntry { Mode = GameMode.Voxel, Name = "Voxel", About = "One voxel and its 256 cases.", Playable = false },
            new ModeEntry { Mode = GameMode.Chunk, Name = "Chunk", About = "A single chunk to dig and build.", Playable = false },
            new ModeEntry { Mode = GameMode.World, Name = "World", About = "A valley to mine, smelt and trade in.", Playable = true },
            new ModeEntry { Mode = GameMode.Procedural, Name = "Procedural", About = "An endless streamed world.", Playable = false },
        };

        private GameMode selected = GameMode.World;
        private string worldName = SaveNames.DefaultWorldName;
        private string seedText;

        public NewGamePage(int defaultSeed)
        {
            seedText = defaultSeed.ToString();
        }

        public GameMode Mode => selected;

        /// <summary>The world's name as typed (GL206); <see cref="GameSession"/> takes the default for an empty one.</summary>
        public string WorldName => worldName;

        /// <summary>The seed the text makes.</summary>
        public int Seed => SeedFrom(seedText);

        /// <summary>A number as it is; other text hashed (FNV-1a), so a word always makes the same world; empty text makes 0.</summary>
        public static int SeedFrom(string text)
        {
            text = text?.Trim() ?? "";
            if (int.TryParse(text, out int number))
            {
                return number;
            }
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        /// <summary>Draws the page in an area; returns what the player chose this frame.</summary>
        public Choice Draw(Rect area, MenuStyles styles)
        {
            Choice choice = Choice.None;
            GUILayout.BeginArea(area);
            GUILayout.FlexibleSpace();
            GUILayout.Label("New game", styles.Heading);
            GUILayout.Space(18f);

            GUILayout.Label("Game mode", styles.Label);
            GUILayout.Space(4f);
            foreach (ModeEntry entry in Modes)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = entry.Playable;
                string mark = selected == entry.Mode ? "●" : "○";
                string text = entry.Playable
                    ? $"{mark}  <b>{entry.Name}</b>    <size=13>{entry.About}</size>"
                    : $"{mark}  <b>{entry.Name}</b>    <size=13>{entry.About}  (WIP)</size>";
                if (GUILayout.Button(text, styles.ModeButton, GUILayout.Height(40f)) && entry.Playable)
                {
                    selected = entry.Mode;
                }
                GUI.enabled = wasEnabled;
                GUILayout.Space(4f);
            }

            GUILayout.Space(14f);
            GUILayout.Label("World name", styles.Label);
            GUILayout.Space(4f);
            worldName = GUILayout.TextField(worldName, SaveNames.MaxLength, styles.Field, GUILayout.Height(36f));

            GUILayout.Space(10f);
            GUILayout.Label("World seed", styles.Label);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("Seed");
            seedText = GUILayout.TextField(seedText, 24, styles.Field, GUILayout.Height(36f));
            if (GUILayout.Button("Random", styles.Button, GUILayout.Width(110f), GUILayout.Height(36f)))
            {
                seedText = Random.Range(1, 1000000).ToString();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            if (!int.TryParse(seedText.Trim(), out _))
            {
                GUILayout.Label($"Makes seed {Seed}", styles.Caption);
            }

            GUILayout.Space(22f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Back;
            }
            GUILayout.Space(12f);
            if (GUILayout.Button("Start", styles.Button, GUILayout.Height(46f)))
            {
                choice = Choice.Start;
            }
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();

            // Enter starts, Esc goes back (the text field takes keys, so read them from the event).
            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                {
                    choice = Choice.Start;
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    choice = Choice.Back;
                }
            }
            return choice;
        }
    }
}
