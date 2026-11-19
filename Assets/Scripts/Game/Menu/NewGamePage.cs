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
        }

        // Demo: the one game, on the bounded map (GL655). Debug: the four labs, each in its own scene.
        private static readonly ModeEntry[] DemoModes =
        {
            new ModeEntry { Mode = GameMode.Demo, Name = "Game Demo", About = "A 5 km map to mine, smelt and trade in." },
        };

        private static readonly ModeEntry[] DebugModes =
        {
            new ModeEntry { Mode = GameMode.Voxel, Name = "Voxel", About = "One voxel and its 256 cases." },
            new ModeEntry { Mode = GameMode.Chunk, Name = "Chunk", About = "A single chunk to dig and build." },
            new ModeEntry { Mode = GameMode.World, Name = "World", About = "The 300 m valley lab." },
            new ModeEntry { Mode = GameMode.Procedural, Name = "Infinite", About = "An endless streamed world." },
        };

        private bool debug;
        private GameMode debugSelected = GameMode.World;
        private string worldName = SaveNames.DefaultWorldName;
        private string seedText;
        private bool withTutorial = true;

        public NewGamePage(int defaultSeed)
        {
            seedText = defaultSeed.ToString();
        }

        public GameMode Mode => debug ? debugSelected : GameMode.Demo;

        /// <summary>True on the Demo side, which has the map preview and the opening choice.</summary>
        public bool IsDemo => !debug;

        /// <summary>The world's name as typed (GL206); <see cref="GameSession"/> takes the default for an empty one.</summary>
        public string WorldName => worldName;

        /// <summary>True when the game opens with the Mining Corp tutorial (GL479); the default.</summary>
        public bool WithTutorial => !debug && withTutorial;

        private static string OpeningText(bool chosen, string name, string about)
        {
            // The description on its own line (GL788): a wrapped line of mixed sizes was cut off by the row.
            return string.IsNullOrEmpty(about)
                ? $"{(chosen ? "●" : "○")}  <b>{name}</b>"
                : $"{(chosen ? "●" : "○")}  <b>{name}</b>\n      <size=13>{about}</size>";
        }

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
            GUILayout.BeginHorizontal();
            if (GameUi.LayoutButton(OpeningText(!debug, "Demo", ""), styles.ModeButton, GUILayout.MinHeight(36f)))
            {
                debug = false;
            }
            GUILayout.Space(4f);
            if (GameUi.LayoutButton(OpeningText(debug, "Debug", ""), styles.ModeButton, GUILayout.MinHeight(36f)))
            {
                debug = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            foreach (ModeEntry entry in debug ? DebugModes : DemoModes)
            {
                if (GameUi.LayoutButton(OpeningText(Mode == entry.Mode, entry.Name, entry.About), styles.ModeButton, GUILayout.MinHeight(40f)) && debug)
                {
                    debugSelected = entry.Mode;
                }
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
            if (GameUi.LayoutButton("Random", styles.Button, GUILayout.MinWidth(110f), GUILayout.MinHeight(36f)))
            {
                seedText = Random.Range(1, 1000000).ToString();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            if (!int.TryParse(seedText.Trim(), out _))
            {
                GUILayout.Label($"Makes seed {Seed}", styles.Caption);
            }

            // The labs have no opening: only the demo game starts with the tutorial or the cart arrival.
            if (!debug)
            {
                GUILayout.Space(14f);
                GUILayout.Label("Opening", styles.Label);
                GUILayout.Space(4f);
                if (GameUi.LayoutButton(OpeningText(withTutorial, "Tutorial", "A day's work at the Mining Corp quarry."), styles.ModeButton, GUILayout.MinHeight(40f)))
                {
                    withTutorial = true;
                }
                GUILayout.Space(4f);
                if (GameUi.LayoutButton(OpeningText(!withTutorial, "Skip it", "Start on your family's parcel."), styles.ModeButton, GUILayout.MinHeight(40f)))
                {
                    withTutorial = false;
                }
            }

            GUILayout.Space(22f);
            GUILayout.BeginHorizontal();
            if (GameUi.LayoutButton("Back", styles.Button, GUILayout.MinHeight(46f)))
            {
                choice = Choice.Back;
            }
            GUILayout.Space(12f);
            if (GameUi.LayoutButton("Start", styles.Button, GUILayout.MinHeight(46f)))
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
