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
    /// player's inventory below. Click one of your stacks to load it (Shift half of it, Ctrl one, GL176), or drag it onto an
    /// input or fuel slot, between your own slots, or right-click to split it (GL191, GL195); click an input stack to take it
    /// back, the output to collect it. The empty output slot shows what's about to be made,
    /// greyed out (GL58); a made tool moves to the inventory by itself (GL59). The furnace keeps
    /// working through what's loaded with the screen closed; a worktop or anvil hands its
    /// leftovers back when the screen closes (GL60). Frees the cursor while open; Interact (E)
    /// closes it, as does walking away.
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
        private int madeFrame = -1;
        private PlayerController player;
        private int openedFrame;
        private GUIStyle recipeStyle;
        private readonly InventoryDrag drag = new InventoryDrag();

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
            drag.Reset();
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
            CraftingStation station = Current.Station;
            // Leftovers come back (GL60) from stations that only work while used; smelters keep
            // theirs and work on. Made tools come along too (GL59).
            if (!station.WorksUnattended)
            {
                station.ReturnInputs(inventory.Inventory);
            }
            CollectUnstackable(station);
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
            // A made tool or station shows in the output slot for one frame, then moves to the
            // inventory (GL59, GL68).
            ItemStack made = Current.Station.Output[0];
            if (made.IsEmpty || made.Item.MaxStack > 1)
            {
                madeFrame = -1;
            }
            else if (madeFrame < 0)
            {
                madeFrame = Time.frameCount;
            }
            else if (Time.frameCount > madeFrame)
            {
                CollectUnstackable(Current.Station);
                madeFrame = -1;
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
            float fuelRow = station.BurnsFuel ? slot + gap * 2f : 0f;
            float height = Padding * 2f + 24f + 22f * station.Recipes.Count + slot + fuelRow + 30f + 22f + InventoryGridGui.PlayerHeight(inventory);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            // How well it was built and what that works on (GL135): a furnace's heat and yield.
            string works = station.BurnsFuel ? "Heat/Yield" : "Yield";
            // And what the loaded ore yields (GL127, GL128), in the header so it never covers the fuel row.
            string yield = YieldNote(station);
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   Efficiency {Current.Station.Quality * 100f:0}%  {works}" + (yield != null ? "   " + yield : "") + "   (E to close)");
            y += 24f;
            // Click a recipe to make only it (ML1: sticks and stone make a spear, a hatchet or a hammer)
            // and pull one batch of its ingredients from your items (GL80); click it again to let any start.
            foreach (Recipe recipe in station.Recipes)
            {
                var row = new Rect(x, y, width - Padding * 2f, 20f);
                if (!recipe.IsUnlockedFor(station.Learner))
                {
                    // Locked (GL48): shown, greyed, with the level it needs.
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = false;
                    GUI.Label(row, $"Needs {recipe.UnlockSkill} {recipe.UnlockLevel}:  {Describe(recipe)}");
                    GUI.enabled = wasEnabled;
                }
                else
                {
                    bool chosen = station.Chosen == recipe;
                    recipeStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
                    if (GUI.Button(row, (chosen ? "\u25B6 " : "   ") + Describe(recipe), recipeStyle))
                    {
                        station.Chosen = chosen ? null : recipe;
                        if (!chosen)
                        {
                            station.LoadFor(recipe, items);
                        }
                    }
                }
                y += 22f;
            }

            // Input slots, the work in the middle, the output slot.
            // Where the station's slots are, for stacks dragged onto them (GL195).
            var inputSlots = new Rect(x, y, station.Input.SlotCount * (slot + gap), slot);
            var fuelSlots = Rect.zero;
            int input = InventoryGridGui.DrawRow(new Vector2(x, y), station.Input);
            if (input >= 0)
            {
                station.Input.MoveTo(input, items);
            }
            float workX = x + station.Input.SlotCount * (slot + gap) + 12f;
            DrawWork(new Rect(workX, y, WorkWidth, slot), station);
            var outputAt = new Vector2(workX + WorkWidth + 12f, y);
            int output = InventoryGridGui.DrawRow(outputAt, station.Output);
            if (output >= 0)
            {
                station.Output.MoveTo(output, items);
            }
            // What it's about to make, greyed out in the empty output slot (GL58).
            Recipe upcoming = station.Upcoming;
            if (upcoming != null && station.Output[0].IsEmpty)
            {
                ItemSlotGui.Ghost(new Rect(outputAt.x, outputAt.y, slot, slot), upcoming.Output.Item, upcoming.Output.Count);
            }
            // Why a loaded recipe waits: a different result sits in the output slot (GL61).
            // Why a loaded recipe waits.
            string blocked = Blocked(station);
            if (blocked != null)
            {
                GUI.Label(new Rect(workX, y + slot + 4f, width - (workX - panel.x) - Padding, 20f), blocked);
            }
            if (station.BurnsFuel)
            {
                y += slot + gap * 2f;
                GUI.Label(new Rect(x, y + 18f, 40f, 20f), "Fuel");
                fuelSlots = new Rect(x + 44f, y, slot, slot);
                int fuel = InventoryGridGui.DrawRow(new Vector2(x + 44f, y), station.Fuel);
                if (fuel >= 0)
                {
                    station.Fuel.MoveTo(fuel, items);
                }
                DrawBurn(new Rect(x + 44f + slot + 12f, y, WorkWidth, slot), station);
            }
            y += slot + 30f;

            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Your items: click to load (Shift half, Ctrl one), drag onto a slot, right-click to split");
            y += 22f;
            // Dragging works here as in the inventory (GL195): between your slots, or onto the station's input and
            // fuel slots, Shift carrying half and Ctrl one (GL191).
            var grid = new Vector2(x, y);
            InventoryDragResult mouse = drag.Handle(inventory, grid);
            if (mouse.Kind == InventoryDragKind.Clicked)
            {
                station.Load(items, mouse.Slot, mouse.Count);
            }
            else if (mouse.Kind == InventoryDragKind.DroppedOutside)
            {
                DropOnStation(station, items, mouse, inputSlots, fuelSlots);
            }
            InventoryGridGui.DrawPlayer(grid, inventory, drag.Pressed);
            if (drag.IsDragging)
            {
                drag.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // A stack dragged from the player's slots and let go over the station (GL191, GL195): onto the input slots,
        // or onto the fuel slot if it burns. How many go is what the drag carried.
        private static void DropOnStation(CraftingStation station, Inventory items, InventoryDragResult drop, Rect inputRow, Rect fuelRow)
        {
            ItemStack stack = items[drop.Slot];
            if (stack.IsEmpty)
            {
                return;
            }
            if (inputRow.Contains(drop.Position))
            {
                items.MoveTo(drop.Slot, station.Input, drop.Count);
            }
            else if (station.BurnsFuel && stack.Item.IsFuel && fuelRow.Contains(drop.Position))
            {
                items.MoveTo(drop.Slot, station.Fuel, drop.Count);
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

            string status = station.NeedsFuel ? "Needs fuel"
                : station.IsBusy ? $"Working {Mathf.RoundToInt(station.Progress * 100f)}%"
                : "Idle";
            GUI.Label(new Rect(area.x, area.y, area.width, 20f), status);
            var bar = new Rect(area.x, area.y + 22f, area.width, 12f);
            GUI.Box(bar, GUIContent.none);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * station.Progress, bar.height), Texture2D.whiteTexture);
        }

        // Moves an item that doesn't stack (a tool, a station) from the output into the inventory, if there's room.
        private void CollectUnstackable(CraftingStation station)
        {
            ItemStack made = station.Output[0];
            if (!made.IsEmpty && made.Item.MaxStack <= 1)
            {
                station.Output.MoveTo(0, inventory.Inventory);
            }
        }

        // "Take the Tin bun out first" when the upcoming recipe's result doesn't fit on what's waiting.
        private static string Blocked(CraftingStation station)
        {
            Recipe next = station.Upcoming;
            if (next == null || station.IsBusy || !next.HasInputs(station.Input) || station.OutputFits(next))
            {
                return null;
            }
            ItemStack waiting = station.Output[0];
            return waiting.Item == next.Output.Item ? "Output full: take it out" : $"Take the {waiting.Item.DisplayName} out first";
        }

        // How much the next run yields, when that isn't simply the recipe's count (GL127, GL128).
        private static string YieldNote(CraftingStation station)
        {
            Recipe next = station.Chosen ?? station.Upcoming;
            if (next == null)
            {
                return null;
            }
            float yield = station.YieldOf(next);
            if (Mathf.Abs(yield - 1f) < 0.005f)
            {
                return null;
            }
            // A smelt's count is what pure mineral gives (GL192), so its yield is the ore's share times the efficiency.
            return $"Yield {yield * 100f:0}%: {next.Output.Count * yield:0.##} of {next.Output.Count} a run";
        }

        // The fuel burning now as a bar that empties (GL35).
        private static void DrawBurn(Rect area, CraftingStation station)
        {
            string status = station.IsBurning ? $"Burning {station.BurnLeft:0} s" : "Out";
            GUI.Label(new Rect(area.x, area.y + 6f, area.width, 20f), status);
            var bar = new Rect(area.x, area.y + 30f, area.width, 12f);
            GUI.Box(bar, GUIContent.none);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.15f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * station.BurnFraction, bar.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // "5 Chalcopyrite → 1 Copper bun · 4 s each"
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
