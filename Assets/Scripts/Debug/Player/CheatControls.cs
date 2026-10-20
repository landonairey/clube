using Clube.Core;
using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The Cheats section of the lab panel (GL43): coins at a click, any item from the
    /// <see cref="ItemCatalog"/> in any amount, a test ground of every material (<see cref="DebugMaterialChunk"/>,
    /// GL89), and a command line for <see cref="CheatCommands"/>.
    /// Works in Play mode, on the player's inventory and wallet.
    /// </summary>
    public static class CheatControls
    {
        private const string CommandControl = "Clube.CheatCommand";

        /// <summary>What the section remembers between frames: the picked item, the command being typed, the last result.</summary>
        public sealed class State
        {
            public int Item;
            public string Command = "";
            public string Result = CheatCommands.Help;
        }

        public static void Draw(LabPanelFrame frame, PlayerController player, ItemCatalog catalog, State state)
        {
            if (!Application.isPlaying)
            {
                GUILayout.Label("In Play mode: give coins and items, or type a command.", frame.Hint);
                return;
            }
            var inventory = player.GetComponent<PlayerInventory>();
            var wallet = player.GetComponent<PlayerWallet>();

            switch (frame.ButtonRow("+100 coins", "+1,000 coins", "+10,000 coins"))
            {
                case 0:
                    state.Result = CheatCommands.GiveCoins(wallet, 100);
                    break;
                case 1:
                    state.Result = CheatCommands.GiveCoins(wallet, 1000);
                    break;
                case 2:
                    state.Result = CheatCommands.GiveCoins(wallet, 10000);
                    break;
            }

            if (catalog != null && catalog.Items.Count > 0)
            {
                state.Item = (state.Item % catalog.Items.Count + catalog.Items.Count) % catalog.Items.Count;
                ItemDefinition item = catalog.Items[state.Item];
                string name = item != null ? item.DisplayName : "(missing)";
                switch (frame.ButtonRow("◄", name, "►"))
                {
                    case 0:
                        state.Item--;
                        break;
                    case 2:
                        state.Item++;
                        break;
                }
                int stack = item != null ? item.MaxStack : 1;
                switch (frame.ButtonRow("Give 1", "Give 10", "Give 100", $"Give {stack}"))
                {
                    case 0:
                        state.Result = CheatCommands.GiveItems(inventory, item, 1);
                        break;
                    case 1:
                        state.Result = CheatCommands.GiveItems(inventory, item, 10);
                        break;
                    case 2:
                        state.Result = CheatCommands.GiveItems(inventory, item, 100);
                        break;
                    case 3:
                        state.Result = CheatCommands.GiveItems(inventory, item, stack);
                        break;
                }
            }
            else
            {
                GUILayout.Label("No item catalog assigned to the lab panel.", frame.Hint);
            }

            // A test ground with a pile of every material (GL89).
            if (frame.ButtonRow("Debug material chunk") == 0)
            {
                state.Result = DebugMaterialChunk.Make(Object.FindFirstObjectByType<WorldView>(), player.transform);
            }

            // The command line: Enter or Run.
            bool enter = Event.current.type == EventType.KeyDown
                         && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                         && GUI.GetNameOfFocusedControl() == CommandControl;
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(CommandControl);
            state.Command = GUILayout.TextField(state.Command ?? "", GUILayout.ExpandWidth(true));
            bool run = GUILayout.Button("Run", frame.Button, GUILayout.Width(52f));
            GUILayout.EndHorizontal();
            if ((enter || run) && !string.IsNullOrWhiteSpace(state.Command))
            {
                state.Result = CheatCommands.Run(state.Command, inventory, wallet, catalog, player.GetComponent<PlayerSkills>());
                state.Command = "";
                if (enter)
                {
                    Event.current.Use();
                }
            }
            frame.Line(state.Result);
        }
    }
}
