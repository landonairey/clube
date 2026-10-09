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
        private PlayerSkills skills;
        private int openedFrame;

        /// <summary>The station being used, or null when closed.</summary>
        public StationObject Current { get; private set; }

        public bool IsOpen => Current != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            skills = GetComponent<PlayerSkills>();
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
            station.Skills = skills != null ? skills.Level : (System.Func<CraftSkill, float>)null;
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
            if (station.BurnsFuel)
            {
                y += slot + gap * 2f;
                GUI.Label(new Rect(x, y + 18f, 40f, 20f), "Fuel");
                int fuel = InventoryGridGui.DrawRow(new Vector2(x + 44f, y), station.Fuel);
                if (fuel >= 0)
                {
                    station.Fuel.MoveTo(fuel, items);
                }
                DrawBurn(new Rect(x + 44f + slot + 12f, y, WorkWidth, slot), station);
            }
            y += slot + 30f;

            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Your items: click a stack to load it");
            y += 22f;
            int mine = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, -1);
            if (mine >= 0)
            {
                station.Load(items, mine);
            }
            InventoryGridGui.DrawTooltip();
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

            // What's been made but isn't a whole item yet, e.g. copper gathering for the next bun.
            string held = Held(station);
            if (held != null)
            {
                GUI.Label(new Rect(area.x, area.y + 36f, area.width, 20f), held);
            }
        }

        // "Held 0.3 / 0.5 Copper bun" for the first recipe by volume with anything held.
        private static string Held(CraftingStation station)
        {
            foreach (Recipe recipe in station.Recipes)
            {
                ItemDefinition item = recipe.Output.Item;
                float volume = recipe.ByVolume ? station.Held(item) : 0f;
                if (volume > 0.001f)
                {
                    return $"Held {volume:0.##} / {item.UnitVolume:0.##}";
                }
            }
            return null;
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

        // "8 Copper ore → 1 Copper bun · 6 s", or by volume (GL33):
        // "1 Copper ore → Copper bun · 50% of its Copper · 0.8 s each"
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
            if (recipe.ByVolume)
            {
                string of = recipe.Content != null ? $" of its {recipe.Content.DisplayName}" : "";
                return $"{text} → {recipe.Output.Item?.DisplayName} · {recipe.Yield(0f) * 100f:0}%{of} · {work}";
            }
            return $"{text} → {recipe.Output.Count} {recipe.Output.Item?.DisplayName} · {work}";
        }
    }
}
