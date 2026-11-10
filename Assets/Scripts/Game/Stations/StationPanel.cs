using System.Text;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for a <see cref="StationObject"/> the player is using (GL10, GL11, GL19, GL20). Two halves (GL448): the
    /// recipes on the left (what can be made, and with a lock the next level's, GL450), and on the right the recipe being
    /// made: its input slots (a red X and "Need 3 more Stick" for what's missing, GL449), the work (a smelting bar, or a
    /// Strike button on the anvil, which supplies the hammer), its output slot and the fuel; the player's inventory below. Click one of your stacks to load it (Shift half of it, Ctrl one, GL176), or drag it onto an
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
        private GUIStyle lockedStyle;
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;
        private GUIStyle missingStyle;
        private GUIStyle xStyle;
        private Vector2 listScroll;
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
                // And what it made (GL352): leaving a worktop, anvil or flat rock takes everything along.
                if (!station.Output[0].IsEmpty)
                {
                    station.Output.MoveTo(0, inventory.Inventory, keepApart: true);
                }
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
            GameUi.Apply();
            if (Current == null)
            {
                return;
            }

            CraftingStation station = Current.Station;
            Inventory items = inventory.Inventory;
            float slot = InventoryGridGui.SlotSize;
            float gap = InventoryGridGui.Gap;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float inner = width - Padding * 2f;
            // Two halves over your items (GL448): the recipes on the left, the one being made on the right.
            float listWidth = Mathf.Floor(inner * 0.42f);
            float fuelRow = station.BurnsFuel ? slot + gap * 2f : 0f;
            float rightHeight = 34f + 34f + slot + 8f + 60f + fuelRow;
            // The trash slot (GL224) sits in a row under your items.
            float height = Padding * 2f + 32f + rightHeight + 12f + 30f + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap + slot;
            var panel = new Rect((Screen.width - width) * 0.5f, Mathf.Max(0f, (Screen.height - height) * 0.5f), width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            // How well it was built and what that works on (GL135): a furnace's heat and yield.
            string works = station.BurnsFuel ? "Heat/Yield" : "Yield";
            GUI.Label(new Rect(x, y, inner, 28f), $"{Current.DisplayName}   Efficiency {Current.Station.Quality * 100f:0}%  {works}   (E to close)");
            y += 32f;

            DrawRecipeList(new Rect(x, y, listWidth, rightHeight), station, items);
            var right = new Rect(x + listWidth + 16f, y, inner - listWidth - 16f, rightHeight);
            DrawActive(right, station, items, out Rect inputSlots, out Rect fuelSlots);
            y += rightHeight + 12f;

            GUI.Label(new Rect(x, y, inner, 28f), "Your items: click to load (Shift half, Ctrl one), drag onto a slot, right-click to split");
            y += 30f;
            // Dragging works here as in the inventory (GL195): between your slots, or onto the station's input and
            // fuel slots, Shift carrying half and Ctrl one (GL191).
            var grid = new Vector2(x, y);
            var trashSlot = new Rect(panel.xMax - Padding - slot, y + InventoryGridGui.PlayerHeight(inventory) + InventoryGridGui.RowGap, slot, slot);
            InventoryDragResult mouse = drag.Handle(inventory, grid);
            if (mouse.Kind == InventoryDragKind.Clicked)
            {
                LoadClicked(station, items, mouse.Slot, mouse.Count);
            }
            else if (mouse.Kind == InventoryDragKind.DroppedOutside && !InventoryGridGui.Trash(items, mouse, trashSlot))
            {
                DropOnStation(station, items, mouse, inputSlots, fuelSlots);
            }
            InventoryGridGui.DrawPlayer(grid, inventory, drag.Pressed);
            InventoryGridGui.DrawTrash(trashSlot, drag.IsDragging);
            if (drag.IsDragging)
            {
                drag.DrawCarried(items);
            }
            else
            {
                InventoryGridGui.DrawTooltip();
            }
        }

        // The left half (GL448): what can be made now and, with a lock, what the next level brings (GL450); recipes further
        // off stay hidden. Click a recipe to make only it (ML1) and pull one batch of its ingredients from your items
        // (GL80); click it again to let any start. Scrolls when the list is long.
        private void DrawRecipeList(Rect area, CraftingStation station, Inventory items)
        {
            recipeStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
            lockedStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            int shown = 0;
            foreach (Recipe recipe in station.Recipes)
            {
                if (recipe.IsInSightFor(station.Learner))
                {
                    shown++;
                }
            }
            const float rowHeight = 30f;
            var view = new Rect(0f, 0f, area.width - 18f, shown * rowHeight);
            listScroll = GUI.BeginScrollView(area, listScroll, view);
            float y = 0f;
            foreach (Recipe recipe in station.Recipes)
            {
                if (!recipe.IsInSightFor(station.Learner))
                {
                    continue;
                }
                var row = new Rect(0f, y, view.width, rowHeight - 2f);
                string name = ListName(station, recipe);
                if (!recipe.IsUnlockedFor(station.Learner))
                {
                    // Locked (GL48, GL450): a lock, greyed, with the level it needs.
                    GUI.DrawTexture(new Rect(row.x + 4f, row.y + 4f, row.height - 8f, row.height - 8f), LockIcon, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(row.x + row.height + 2f, row.y, row.width - row.height, row.height), $"{name}  ({recipe.UnlockSkill} {recipe.UnlockLevel})", lockedStyle);
                }
                else
                {
                    bool chosen = station.Chosen == recipe;
                    if (GUI.Button(row, (chosen ? "▶ " : "   ") + name, recipeStyle))
                    {
                        station.Chosen = chosen ? null : recipe;
                        if (!chosen)
                        {
                            // What the last recipe made goes to the inventory, so it can't block the new one (GL444).
                            if (!station.Output[0].IsEmpty)
                            {
                                station.Output.MoveTo(0, items, keepApart: true);
                            }
                            station.LoadFor(recipe, items);
                        }
                    }
                }
                y += rowHeight;
            }
            GUI.EndScrollView();
        }

        // The right half (GL448): the recipe being made, its inputs, the work and the output, then the fuel. Missing
        // ingredients get a red X in their slot and a line saying how many more are needed (GL449).
        private void DrawActive(Rect area, CraftingStation station, Inventory items, out Rect inputSlots, out Rect fuelSlots)
        {
            float slot = InventoryGridGui.SlotSize;
            float gap = InventoryGridGui.Gap;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = GameUi.Font(16), clipping = TextClipping.Clip };
            detailStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(12), wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.78f) } };
            missingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(13), wordWrap = true, normal = { textColor = new Color(1f, 0.45f, 0.4f) } };

            float x = area.x;
            float y = area.y;
            Recipe shown = station.Chosen ?? station.Upcoming;
            string title = shown == null ? "Pick a recipe on the left" : shown.Output.Item != null ? shown.Output.Item.DisplayName : "?";
            GUI.Label(new Rect(x, y, area.width, 32f), title, titleStyle);
            y += 34f;
            // What it takes and, for the loaded ore, what it yields (GL127, GL128).
            string yield = YieldNote(station);
            string detail = shown != null ? Describe(shown) + (yield != null ? "   " + yield : "") : "Or load what you have: the station makes what it can.";
            JournalBook.FitLabel(new Rect(x, y, area.width, 32f), detail, detailStyle);
            y += 34f;

            // Input slots, the work in the middle, the output slot. Where the input slots are, for stacks dragged onto
            // them (GL195).
            inputSlots = new Rect(x, y, station.Input.SlotCount * (slot + gap), slot);
            fuelSlots = Rect.zero;
            int input = InventoryGridGui.DrawRow(new Vector2(x, y), station.Input);
            if (input >= 0)
            {
                station.Input.MoveTo(input, items, keepApart: true);
            }
            InventoryGridGui.PullHalf(station.Input, items, true);
            string missing = shown != null && station.Chosen == shown ? DrawMissing(new Vector2(x, y), station, shown) : null;
            float workX = x + station.Input.SlotCount * (slot + gap) + 8f;
            float workWidth = Mathf.Max(80f, area.xMax - workX - slot - 8f);
            DrawWork(new Rect(workX, y, workWidth, slot), station);
            var outputAt = new Vector2(area.xMax - slot, y);
            int output = InventoryGridGui.DrawRow(outputAt, station.Output);
            if (output >= 0)
            {
                station.Output.MoveTo(output, items, keepApart: true);
            }
            InventoryGridGui.PullHalf(station.Output, items, true);
            // What it's about to make, greyed out in the empty output slot (GL58).
            Recipe upcoming = station.Upcoming;
            if (upcoming != null && station.Output[0].IsEmpty)
            {
                ItemSlotGui.Ghost(new Rect(outputAt.x, outputAt.y, slot, slot), upcoming.Output.Item, upcoming.Output.Count);
            }
            y += slot + 8f;
            // Why it can't start: ingredients missing (GL449), or a different result in the output slot (GL61).
            string blocked = missing ?? Blocked(station);
            if (blocked != null)
            {
                GUI.Label(new Rect(x, y, area.width, 58f), blocked, missingStyle);
            }
            y += 60f;
            if (station.BurnsFuel)
            {
                GUI.Label(new Rect(x, y + 18f, 40f, 28f), "Fuel");
                fuelSlots = new Rect(x + 44f, y, slot, slot);
                int fuel = InventoryGridGui.DrawRow(new Vector2(x + 44f, y), station.Fuel);
                if (fuel >= 0)
                {
                    station.Fuel.MoveTo(fuel, items, keepApart: true);
                }
                InventoryGridGui.PullHalf(station.Fuel, items, true);
                float burnX = x + 44f + slot + 12f;
                DrawBurn(new Rect(burnX, y, Mathf.Min(WorkWidth, area.xMax - burnX), slot), station);
            }
        }

        // A red X where each missing ingredient's count would be (GL449): on the slot holding some of it, else on the next
        // empty slot, over a ghost of the item. Returns "Need 3 more Stick" lines, or null when nothing is missing.
        private string DrawMissing(Vector2 topLeft, CraftingStation station, Recipe recipe)
        {
            float slot = InventoryGridGui.SlotSize;
            float gap = InventoryGridGui.Gap;
            xStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = GameUi.Font(20), normal = { textColor = new Color(1f, 0.2f, 0.15f) } };
            var lines = new StringBuilder();
            int nextEmpty = 0;
            foreach (ItemAmount need in recipe.Inputs)
            {
                if (need.Item == null)
                {
                    continue;
                }
                int have = station.Input.Count(need.Item);
                if (have >= need.Count)
                {
                    continue;
                }
                int at = -1;
                for (int i = 0; i < station.Input.SlotCount && at < 0; i++)
                {
                    if (station.Input[i].Item == need.Item)
                    {
                        at = i;
                    }
                }
                for (; at < 0 && nextEmpty < station.Input.SlotCount; nextEmpty++)
                {
                    if (station.Input[nextEmpty].IsEmpty)
                    {
                        at = nextEmpty;
                    }
                }
                if (at >= 0)
                {
                    var cell = new Rect(topLeft.x + at * (slot + gap), topLeft.y, slot, slot);
                    if (have == 0)
                    {
                        ItemSlotGui.Ghost(cell, need.Item, need.Count);
                    }
                    GameUi.OutlinedLabel(new Rect(cell.x + 3f, cell.y + 3f, slot - 9f, slot - 6f), "X", xStyle);
                }
                if (lines.Length > 0)
                {
                    lines.Append('\n');
                }
                lines.Append($"Need {need.Count - have} more {need.Item.DisplayName}");
            }
            return lines.Length > 0 ? lines.ToString() : null;
        }

        // "4 Stick", and when another recipe here makes the same thing, what from: "4 Stick (Maple wood)".
        private static string ListName(CraftingStation station, Recipe recipe)
        {
            if (recipe.Output.Item == null)
            {
                return "?";
            }
            string name = $"{recipe.Output.Count} {recipe.Output.Item.DisplayName}";
            foreach (Recipe other in station.Recipes)
            {
                if (other != recipe && other.Output.Item == recipe.Output.Item && other.IsInSightFor(station.Learner))
                {
                    ItemDefinition from = recipe.Inputs.Count > 0 ? recipe.Inputs[0].Item : null;
                    return from != null ? $"{name} ({from.DisplayName})" : name;
                }
            }
            return name;
        }

        // A padlock, drawn once (GL450).
        private static Texture2D LockIcon
        {
            get
            {
                if (lockIcon != null)
                {
                    return lockIcon;
                }
                const int size = 32;
                lockIcon = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "Lock icon" };
                var colour = new Color(0.85f, 0.75f, 0.45f);
                for (int py = 0; py < size; py++)
                {
                    for (int px = 0; px < size; px++)
                    {
                        // The body with a keyhole, and the shackle: a ring standing on it.
                        bool body = px >= 6 && px <= 25 && py >= 2 && py <= 16;
                        bool keyhole = Mathf.Abs(px - 15.5f) <= 1.5f && py >= 6 && py <= 12;
                        float dx = px - 15.5f, dy = py - 18f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        bool shackle = py >= 16 && r >= 6f && r <= 9f;
                        lockIcon.SetPixel(px, py, (body && !keyhole) || shackle ? colour : Color.clear);
                    }
                }
                lockIcon.Apply();
                return lockIcon;
            }
        }

        private static Texture2D lockIcon;

        // A click on one of the player's stacks (GL299): fuel (wood, sticks) goes to the fire first, and only what the
        // fuel slot can't take is loaded as an input; dragging it onto an input slot loads it there instead.
        private static void LoadClicked(CraftingStation station, Inventory items, int slot, int count)
        {
            ItemStack stack = items[slot];
            if (stack.IsEmpty)
            {
                return;
            }
            if (station.BurnsFuel && stack.Item.IsFuel)
            {
                count -= items.MoveTo(slot, station.Fuel, count);
                if (count <= 0 || items[slot].IsEmpty)
                {
                    return;
                }
            }
            station.Load(items, slot, count);
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
            // The flat rock mends a tool with its ingot (GL360): a tool and its repair item in the inputs.
            if (station.Kind == StationKind.FlatRock && ToolRepair.FindPair(station.Input, out int toolSlot, out int itemSlot))
            {
                string ingot = station.Input[itemSlot].Item.DisplayName;
                if (GUI.Button(new Rect(area.x, area.y + 8f, area.width, area.height - 16f), $"Repair (1 {ingot})"))
                {
                    ToolRepair.RepairFully(station.Input, toolSlot, itemSlot);
                }
                return;
            }
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
            GUI.Label(new Rect(area.x, area.y, area.width, 28f), status);
            var bar = new Rect(area.x, area.y + 30f, area.width, 12f);
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
            // Smelts always show it, a full 100% too (GL443: the clay and brick furnaces showed nothing at 100%).
            if (next.Extracts == null && Mathf.Abs(yield - 1f) < 0.005f)
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
            GUI.Label(new Rect(area.x, area.y + 6f, area.width, 28f), status);
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
