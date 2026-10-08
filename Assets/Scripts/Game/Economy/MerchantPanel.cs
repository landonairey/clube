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
            float height = Padding * 2f + 24f + 22f + InventoryGridGui.PlayerHeight(inventory) + 12f + RowHeight + 6f;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   (E to close)");
            GUI.Label(new Rect(panel.xMax - Padding - 160f, y, 160f, 20f), $"Your coins: {wallet.Wallet.Coins}");
            y += 24f;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), "Your items: prices show on what the merchant buys. Click one to sell it.");
            y += 22f;

            int clicked = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, selected,
                stack => !stack.IsEmpty && prices.TryGetPrice(stack.Item, out int coins) ? $"{coins}c" : null);
            if (clicked >= 0)
            {
                selected = clicked == selected ? -1 : clicked;
            }
            y += InventoryGridGui.PlayerHeight(inventory) + 12f;

            ItemStack chosen = selected >= 0 ? items[selected] : ItemStack.Empty;
            bool buys = !chosen.IsEmpty && prices.TryGetPrice(chosen.Item, out _);
            string what = chosen.IsEmpty ? "Pick a stack to sell" : buys ? chosen.Item.DisplayName : $"The merchant doesn't buy {chosen.Item.DisplayName}";
            GUI.Label(new Rect(x, y + 4f, 260f, 20f), what);
            GUI.enabled = buys;
            if (GUI.Button(new Rect(x + 270f, y, 90f, RowHeight), "Sell 1"))
            {
                SellFromSlot(selected, 1);
            }
            if (GUI.Button(new Rect(x + 366f, y, 90f, RowHeight), "Sell stack"))
            {
                SellFromSlot(selected, chosen.Count);
            }
            GUI.enabled = true;
        }

        // Sells from one slot, so the stack the player picked is the one that goes.
        private void SellFromSlot(int slot, int count)
        {
            Inventory items = inventory.Inventory;
            ItemStack stack = items[slot];
            if (stack.IsEmpty || !Current.Prices.TryGetPrice(stack.Item, out int coins))
            {
                return;
            }
            int sold = items.RemoveAt(slot, count);
            wallet.Wallet.Earn(sold * coins);
            if (items[slot].IsEmpty)
            {
                selected = -1;
            }
        }
    }
}
