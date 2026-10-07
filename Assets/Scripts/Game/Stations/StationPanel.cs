using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for a <see cref="StationObject"/> the player is using (GL10, GL11): each
    /// recipe with what it needs and how many the player has, a button to start it, the work
    /// in progress (a smelting bar, or a Strike button on the anvil, which supplies the
    /// hammer), and the finished output to take. Frees the cursor while open; Interact (E)
    /// closes it, as does walking away.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class StationPanel : MonoBehaviour
    {
        private const float Width = 380f;
        private const float RowHeight = 26f;

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
                player.IsCursorFree = true;
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
                player.IsCursorFree = false;
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
            float height = 90f + RowHeight * (Current.Recipes.Count * 2 + 3);
            var area = new Rect((Screen.width - Width) * 0.5f, (Screen.height - height) * 0.5f, Width, height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label($"{Current.DisplayName}   (E to close)");

            foreach (Recipe recipe in Current.Recipes)
            {
                if (recipe == null || recipe.Station != station.Kind)
                {
                    continue;
                }
                GUILayout.Space(6f);
                GUILayout.Label(Describe(recipe, items));
                GUI.enabled = station.CanStart(recipe, items);
                if (GUILayout.Button(StartLabel(recipe), GUILayout.Height(RowHeight)))
                {
                    station.Start(recipe, items);
                }
                GUI.enabled = true;
            }

            GUILayout.Space(8f);
            if (station.IsBusy)
            {
                Recipe active = station.Active;
                if (active.Work == RecipeWork.Strikes)
                {
                    GUILayout.Label($"On the anvil: {Name(active.Inputs)}");
                    if (GUILayout.Button($"Strike  ({station.StrikesLeft} left)", GUILayout.Height(RowHeight * 1.4f)))
                    {
                        station.Strike();
                    }
                }
                else
                {
                    GUILayout.Label($"Smelting {active.Output.Item.DisplayName}: {Mathf.RoundToInt(station.Progress * 100f)}%");
                    Rect bar = GUILayoutUtility.GetRect(Width - 24f, 12f);
                    GUI.Box(bar, GUIContent.none);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * station.Progress, bar.height), Texture2D.whiteTexture);
                }
            }
            else
            {
                GUILayout.Label("Idle");
            }

            GUI.enabled = !station.Output.IsEmpty;
            string take = station.Output.IsEmpty ? "Nothing to take" : $"Take {station.Output.Count} {station.Output.Item.DisplayName}";
            if (GUILayout.Button(take, GUILayout.Height(RowHeight)))
            {
                station.TakeOutput(items);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // "8 Copper ore (you have 23) → 1 Copper bun · 6 s"
        private static string Describe(Recipe recipe, Inventory items)
        {
            var text = new System.Text.StringBuilder();
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
                text.Append($"{input.Count} {input.Item.DisplayName} (you have {items.Count(input.Item)})");
            }
            string work = recipe.Work == RecipeWork.Time ? $"{recipe.Amount:0.#} s" : $"{recipe.Amount:0} strikes";
            return $"{text} → {recipe.Output.Count} {recipe.Output.Item?.DisplayName} · {work}";
        }

        private static string StartLabel(Recipe recipe)
        {
            return recipe.Work == RecipeWork.Time
                ? $"Smelt {recipe.Output.Item?.DisplayName}"
                : $"Place {Name(recipe.Inputs)} on the anvil";
        }

        private static string Name(System.Collections.Generic.IReadOnlyList<ItemAmount> amounts)
        {
            return amounts.Count > 0 && amounts[0].Item != null ? amounts[0].Item.DisplayName.ToLowerInvariant() : "it";
        }
    }
}
