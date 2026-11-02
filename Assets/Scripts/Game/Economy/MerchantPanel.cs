using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for trading at a <see cref="MerchantTable"/> (GL14, GL20; first pass of TR4):
    /// the player's inventory in slots like the hotbar, with the merchant's price on every
    /// stack it buys; pick a stack and sell one, all of it, or a part set on a slider (GL57). The player's coins show at the
    /// bottom. Frees the cursor while open;
    /// Interact (E) closes it, as does walking away.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerWallet))]
    public class MerchantPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float RowHeight = 26f;

        // The sell buttons carry what they pay on a second line.
        private const float SellRowHeight = 44f;

        [Tooltip("Closes the panel (E).")]
        [SerializeField]
        private InputActionReference closeAction;

        [Tooltip("The panel closes when the player is this far from the table, in metres.")]
        [SerializeField, Min(1f)]
        private float closeDistance = 5f;

        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private PlayerController player;
        private int openedFrame;
        private int selected = -1;
        private readonly System.Collections.Generic.HashSet<int> group = new System.Collections.Generic.HashSet<int>();
        // How much "Sell part" sells: whole items, or to the hundredth for a part item (GL246).
        private float splitAmount = 1f;
        private GUIStyle countStyle;

        /// <summary>The table being used, or null when closed.</summary>
        public MerchantTable Current { get; private set; }

        public bool IsOpen => Current != null;

        /// <summary>Raised after a sale, with how many items went.</summary>
        public event System.Action<int> Sold;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            wallet = GetComponent<PlayerWallet>();
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

        public void Open(MerchantTable table)
        {
            Current = table;
            selected = -1;
            group.Clear();
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

        /// <summary>Sells up to <paramref name="count"/> of an item to the open table; returns how many sold.</summary>
        public int Sell(ItemDefinition item, int count)
        {
            if (Current == null || Current.Prices == null)
            {
                return 0;
            }
            int sold = Current.Prices.Sell(item, count, inventory.Inventory, wallet.Wallet);
            if (sold > 0)
            {
                Sold?.Invoke(sold);
            }
            return sold;
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
            if (Current == null || Current.Prices == null)
            {
                return;
            }

            Inventory items = inventory.Inventory;
            PriceList prices = Current.Prices;
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float offerRows = Current.Offers.Count > 0 ? Current.Offers.Count * (RowHeight + 4f) + 28f : 0f;
            float height = Padding * 2f + 24f + 22f + InventoryGridGui.PlayerHeight(inventory) + 12f + SellRowHeight + 6f + SellRowHeight + 6f + offerRows + (group.Count > 0 ? SellRowHeight + 6f : 0f);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   (E to close)");
            GUI.Label(new Rect(panel.xMax - Padding - 160f, y, 160f, 20f), $"Your coins: {Wallet.Format(wallet.Wallet.Coins)}");
            y += 24f;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Click one of your items to see what the merchant pays for it.");
            y += 22f;

            // While a group is picked only its stacks are lit (GL310): the last Ctrl-clicked slot may have just left it.
            int clicked = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, group.Count > 0 ? -1 : selected,
                _ => null, group);
            if (clicked >= 0)
            {
                if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
                {
                    PickForGroup(items, clicked);
                }
                else
                {
                    group.Clear();
                    selected = clicked == selected ? -1 : clicked;
                }
                splitAmount = selected >= 0 ? items[selected].Amount : 1f;
            }
            y += InventoryGridGui.PlayerHeight(inventory) + 12f;

            if (group.Count > 0)
            {
                DrawGroup(items, prices, x, y);
                y += SellRowHeight + 6f;
            }

            ItemStack chosen = selected >= 0 ? items[selected] : ItemStack.Empty;
            bool buys = !chosen.IsEmpty && prices.TryGetLot(chosen.Item, out _, out _);
            string what = chosen.IsEmpty ? "Pick a stack to sell"
                : buys ? $"{chosen.Item.DisplayName}: {FormatCoins(prices.PriceOf(chosen, 1))} each"
                : $"The merchant doesn't buy {chosen.Item.DisplayName}";
            GUI.Label(new Rect(x, y + 4f, 260f, 20f), what);
            GUI.enabled = buys;
            string oneLabel = "Sell 1" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, Mathf.Min(1f, chosen.Amount)))}" : "");
            string stackLabel = "Sell stack" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, chosen.Amount))}" : "");
            if (GUI.Button(new Rect(x + 270f, y, 100f, SellRowHeight), oneLabel))
            {
                SellFromSlot(selected, 1f);
            }
            if (GUI.Button(new Rect(x + 376f, y, 100f, SellRowHeight), stackLabel))
            {
                SellStack(selected);
            }
            y += SellRowHeight + 6f;

            // Part of the stack (GL57): a slider for how much, e.g. 100 of 109 stone for exactly 100 coins. Part items
            // sell too (GL246): under 10 the slider moves in hundredths, so 0.99 clay can go.
            float most = buys ? chosen.Amount : 1f;
            float least = Mathf.Min(most, most < 10f ? 0.01f : 1f);
            splitAmount = Mathf.Clamp(splitAmount, least, most);
            GUI.Label(new Rect(x, y + 12f, 60f, 20f), "Sell part");
            splitAmount = SnapAmount(GUI.HorizontalSlider(new Rect(x + 64f, y + 18f, 196f, 16f), splitAmount, least, most), most);
            if (buys)
            {
                // Arrow buttons for the last few, which a slider over a big stack can't hit.
                float step = most < 10f ? 0.01f : 1f;
                if (GUI.Button(new Rect(x + 270f, y + 8f, 24f, 28f), "-"))
                {
                    splitAmount = Mathf.Max(least, SnapAmount(splitAmount - step, most));
                }
                if (GUI.Button(new Rect(x + 346f, y + 8f, 24f, 28f), "+"))
                {
                    splitAmount = Mathf.Min(most, SnapAmount(splitAmount + step, most));
                }
                GUI.Label(new Rect(x + 296f, y + 12f, 48f, 20f), FormatAmount(splitAmount), countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter });
            }
            string partLabel = $"Sell {(buys ? FormatAmount(splitAmount) : "0")}" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, splitAmount))}" : "");
            if (GUI.Button(new Rect(x + 376f, y, 100f, SellRowHeight), partLabel))
            {
                SellFromSlot(selected, splitAmount);
            }
            GUI.enabled = true;
            y += SellRowHeight + 6f;
            DrawOffers(x, y, width - Padding * 2f);
            InventoryGridGui.DrawTooltip();
        }

        // Coins to two places at most: "50c", "0.1c", "0.001c".
        private static string FormatCoins(float coins)
        {
            return coins >= 0.01f || coins == 0f ? $"{coins:0.##}c" : $"{coins:0.###}c";
        }

        // What the merchant sells: one button per offer, greyed out when the player can't pay or has no room.
        private void DrawOffers(float x, float y, float width)
        {
            if (Current.Offers.Count == 0)
            {
                return;
            }
            GUI.Label(new Rect(x, y, width, 20f), "For sale");
            y += 24f;
            foreach (ItemPrice offer in Current.Offers)
            {
                if (offer.Item == null)
                {
                    continue;
                }
                GUI.enabled = wallet.Wallet.Coins >= offer.Coins;
                if (GUI.Button(new Rect(x, y, 260f, RowHeight), $"Buy {offer.Item.DisplayName}  ({offer.Coins}c)"))
                {
                    Buy(offer);
                }
                GUI.enabled = true;
                y += RowHeight + 4f;
            }
        }

        // Pays for one item and puts it in the inventory; a full inventory refuses the sale.
        private void Buy(ItemPrice offer)
        {
            if (inventory.Inventory.Add(offer.Item, 1) > 0)
            {
                return;
            }
            if (!wallet.Wallet.Spend(offer.Coins))
            {
                inventory.Inventory.Remove(offer.Item, 1);
            }
        }

        // The whole stack, a part item included (GL199): nothing is left behind, and the coins come to the hundredth.
        private void SellStack(int slot)
        {
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (stack.IsEmpty || !Current.Prices.TryGetLot(stack.Item, out _, out _))
            {
                return;
            }
            float earned = Current.Prices.PriceOf(stack, stack.Amount);
            items.TakeAll(slot);
            wallet.Wallet.Earn(earned);
            Sold?.Invoke(Mathf.Max(1, stack.Count));
            selected = -1;
        }

        // Ctrl-click (GL305): the first picks every stack of that item; later ones add a stack to the group or take it out.
        private void PickForGroup(Inventory items, int slot)
        {
            if (items[slot].IsEmpty)
            {
                return;
            }
            if (group.Count == 0)
            {
                for (int i = 0; i < items.SlotCount; i++)
                {
                    if (!items[i].IsEmpty && items[i].Item == items[slot].Item)
                    {
                        group.Add(i);
                    }
                }
            }
            else if (!group.Remove(slot))
            {
                group.Add(slot);
            }
            selected = group.Contains(slot) ? slot : group.Count > 0 ? First(group) : -1;
        }

        private static int First(System.Collections.Generic.HashSet<int> slots)
        {
            foreach (int slot in slots)
            {
                return slot;
            }
            return -1;
        }

        // The Ctrl-picked stacks (GL305): how much they come to, and a button selling them all.
        private void DrawGroup(Inventory items, PriceList prices, float x, float y)
        {
            group.RemoveWhere(slot => slot >= items.SlotCount || items[slot].IsEmpty);
            float amount = 0f, coins = 0f;
            foreach (int slot in group)
            {
                amount += items[slot].Amount;
                coins += prices.PriceOf(items[slot], items[slot].Amount);
            }
            GUI.Label(new Rect(x, y + 4f, 360f, 40f),
                $"{group.Count} stack{(group.Count == 1 ? "" : "s")} picked: {FormatAmount(amount)} in all\nCtrl-click to add or take out a stack");
            GUI.enabled = coins > 0f;
            if (GUI.Button(new Rect(x + 376f, y, 100f, SellRowHeight), $"Sell picked\n{FormatCoins(coins)}"))
            {
                foreach (int slot in new System.Collections.Generic.List<int>(group))
                {
                    if (!items[slot].IsEmpty && prices.TryGetLot(items[slot].Item, out _, out _))
                    {
                        SellStack(slot);
                    }
                }
                group.Clear();
                selected = -1;
            }
            GUI.enabled = true;
        }

        // Whole items from a big stack; to the hundredth under 10, and the whole amount at the top of the slider.
        private static float SnapAmount(float amount, float most)
        {
            if (amount >= most - 0.005f)
            {
                return most;
            }
            return most < 10f ? Mathf.Round(amount * 100f) / 100f : Mathf.Round(amount);
        }

        private static string FormatAmount(float amount)
        {
            return Mathf.Approximately(amount, Mathf.Round(amount)) ? $"{Mathf.Round(amount):0}" : $"{amount:0.##}";
        }

        // Sells from one slot, so the stack the player picked is the one that goes.
        private void SellFromSlot(int slot, float amount)
        {
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (stack.IsEmpty || !Current.Prices.TryGetLot(stack.Item, out _, out _))
            {
                return;
            }
            // Any amount sells, a part item included (GL246); a price that isn't a whole coin pays its part of one (GL199).
            float sold = Mathf.Min(amount, stack.Amount);
            float earned = Current.Prices.PriceOf(stack, sold);
            sold = items.RemoveAmount(slot, sold);
            wallet.Wallet.Earn(earned);
            if (sold > 0f)
            {
                Sold?.Invoke(Mathf.Max(1, Mathf.FloorToInt(sold)));
            }
            if (items[slot].IsEmpty)
            {
                selected = -1;
            }
        }
    }
}
