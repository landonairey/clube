using System.Text;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for a <see cref="StationObject"/> the player is using (GL10, GL11, GL19, GL20),
    /// in slots like the hotbar: the station's input slots, then the work (a smelting bar, or a
    /// Strike button on the anvil, which supplies the hammer), then its output slot, and the
    /// player's inventory below. Click one of your stacks to load it, an input stack to take it
    /// back, the output to collect it. The furnace keeps working through what's loaded with
    /// the screen closed. Frees the cursor while open; Interact (E) closes it, as does
    /// walking away.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class StationPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float WorkWidth = 150f;

        [Tooltip("Closes the panel (E).")]
        [SerializeField]
        private InputActionReference closeAction;

        [Tooltip("The panel closes when the player is this far from the station, in metres.")]
        [SerializeField, Min(1f)]
        private float closeDistance = 5f;

        private PlayerInventory inventory;
        private PlayerController player;
        private int openedFrame;

        /// <summary>The station being used, or null when closed.</summary>
        public StationObject Current { get; private set; }

        public bool IsOpen => Current != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        public void Open(StationObject station)
        {
            Current = station;
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (Current == null)
            {
                return;
            }
            Current = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Update()
        {
            if (Current == null)
            {
                return;
            }
            // The press that opened the panel mustn't close it in the same frame.
            bool closePressed = Time.frameCount != openedFrame && closeAction != null && closeAction.action.WasPressedThisFrame();
            bool tooFar = (Current.transform.position - transform.position).sqrMagnitude > closeDistance * closeDistance;
            if (closePressed || tooFar || (player != null && !player.enabled))
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (Current == null)
            {
                return;
            }

            CraftingStation station = Current.Station;
            Inventory items = inventory.Inventory;
            float slot = InventoryGridGui.SlotSize;
            float gap = InventoryGridGui.Gap;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float height = Padding * 2f + 24f + 22f * station.Recipes.Count + slot + 30f + 22f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   (E to close)");
            y += 24f;
            foreach (Recipe recipe in station.Recipes)
            {
                GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), Describe(recipe));
                y += 22f;
            }

            // Input slots, the work in the middle, the output slot.
            int input = InventoryGridGui.DrawRow(new Vector2(x, y), station.Input);
            if (input >= 0)
            {
                station.Input.MoveTo(input, items);
            }
            float workX = x + station.Input.SlotCount * (slot + gap) + 12f;
            DrawWork(new Rect(workX, y, WorkWidth, slot), station);
            int output = InventoryGridGui.DrawRow(new Vector2(workX + WorkWidth + 12f, y), station.Output);
            if (output >= 0)
            {
                station.Output.MoveTo(output, items);
            }
            y += slot + 30f;

            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Your items: click a stack to load it");
            y += 22f;
            int mine = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, -1);
            if (mine >= 0)
            {
                items.MoveTo(mine, station.Input);
            }
        }

        // The smelting bar, or the anvil's Strike button, between input and output.
        private static void DrawWork(Rect area, CraftingStation station)
        {
            bool strikes = station.Active != null ? station.Active.Work == RecipeWork.Strikes : station.Ready(RecipeWork.Strikes) != null;
            if (strikes)
            {
                string label = station.IsBusy ? $"Strike ({station.StrikesLeft} left)" : "Strike";
                if (GUI.Button(new Rect(area.x, area.y + 8f, area.width, area.height - 16f), label))
                {
                    station.Strike();
                }
                return;
            }

            string status = station.IsBusy ? $"Working {Mathf.RoundToInt(station.Progress * 100f)}%" : "Idle";
            GUI.Label(new Rect(area.x, area.y + 6f, area.width, 20f), status);
            var bar = new Rect(area.x, area.y + 30f, area.width, 12f);
            GUI.Box(bar, GUIContent.none);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * station.Progress, bar.height), Texture2D.whiteTexture);
        }

        // "8 Copper ore → 1 Copper bun · 6 s"
        private static string Describe(Recipe recipe)
        {
            var text = new StringBuilder();
            foreach (ItemAmount input in recipe.Inputs)
            {
                if (input.Item == null)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(" + ");
                }
                text.Append($"{input.Count} {input.Item.DisplayName}");
            }
            string work = recipe.Work == RecipeWork.Time ? $"{recipe.Amount:0.#} s each" : $"{recipe.Amount:0} strikes each";
            return $"{text} → {recipe.Output.Count} {recipe.Output.Item?.DisplayName} · {work}";
        }
    }
}
