using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for trading at a <see cref="MerchantTable"/> (GL14, GL20; first pass of TR4):
    /// the player's inventory in slots like the hotbar, with the merchant's price on every
    /// stack it buys; pick a stack and sell one or all of it. The player's coins show at the
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

        /// <summary>The table being used, or null when closed.</summary>
        public MerchantTable Current { get; private set; }

        public bool IsOpen => Current != null;

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
            return Current.Prices.Sell(item, count, inventory.Inventory, wallet.Wallet);
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
            float height = Padding * 2f + 24f + 22f + InventoryGridGui.PlayerHeight(inventory) + 12f + SellRowHeight + 6f + offerRows;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   (E to close)");
            GUI.Label(new Rect(panel.xMax - Padding - 160f, y, 160f, 20f), $"Your coins: {wallet.Wallet.Coins}");
            y += 24f;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Click one of your items to see what the merchant pays for it.");
            y += 22f;

            int clicked = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, selected,
                _ => null);
            if (clicked >= 0)
            {
                selected = clicked == selected ? -1 : clicked;
            }
            y += InventoryGridGui.PlayerHeight(inventory) + 12f;

            ItemStack chosen = selected >= 0 ? items[selected] : ItemStack.Empty;
            bool buys = !chosen.IsEmpty && prices.TryGetLot(chosen.Item, out _, out _);
            string what = chosen.IsEmpty ? "Pick a stack to sell"
                : buys ? $"{chosen.Item.DisplayName}: {FormatCoins(prices.PriceOf(chosen, 1))} each"
                : $"The merchant doesn't buy {chosen.Item.DisplayName}";
            GUI.Label(new Rect(x, y + 4f, 260f, 20f), what);
            GUI.enabled = buys;
            string oneLabel = "Sell 1" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, 1))}" : "");
            string stackLabel = "Sell stack" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, chosen.Count))}" : "");
            if (GUI.Button(new Rect(x + 270f, y, 100f, SellRowHeight), oneLabel))
            {
                SellFromSlot(selected, 1);
            }
            if (GUI.Button(new Rect(x + 376f, y, 100f, SellRowHeight), stackLabel))
            {
                SellFromSlot(selected, chosen.Count);
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

        // Sells from one slot, so the stack the player picked is the one that goes.
        private void SellFromSlot(int slot, int count)
        {
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (stack.IsEmpty || !Current.Prices.TryGetLot(stack.Item, out _, out _))
            {
                return;
            }
            // Any number sells; a price that isn't a whole coin is kept as a part of one (Wallet.EarnFraction).
            int sold = Mathf.Min(count, stack.Count);
            float earned = Current.Prices.PriceOf(stack, sold);
            items.RemoveAt(slot, sold);
            wallet.Wallet.EarnFraction(earned);
            if (items[slot].IsEmpty)
            {
                selected = -1;
            }
        }
    }
}
