using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for trading with an <see cref="ITrader"/>, the merchant table or a town shopkeeper (GL14, GL20, GL401; first pass of TR4):
    /// the player's inventory in slots like the hotbar, with the merchant's price on every
    /// stack it buys; pick a stack and sell one, all of it, or a part set on a slider (GL57). The player's coins show at the
    /// bottom, then what the trader sells and the lessons they give (GL402). Frees the cursor while open;
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
        private PlayerSkills skills;
        private Vector2 offerScroll;
        private string refusal;
        private float refusalUntil;

        /// <summary>The trader being used, or null when closed.</summary>
        public ITrader Current { get; private set; }

        public bool IsOpen => Current != null;

        /// <summary>Raised after a sale, with how many items went.</summary>
        public event System.Action<int> Sold;

        /// <summary>Raised after a lesson is bought (GL402), with the skill trained.</summary>
        public event System.Action<SkillType> Trained;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            wallet = GetComponent<PlayerWallet>();
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

        public void Open(ITrader table)
        {
            Current = table;
            selected = -1;
            group.Clear();
            offerScroll = Vector2.zero;
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
            // Into a holding wallet first, so a trader with a purse (GL426) pays only what they have.
            if (Current.Purse != null && Current.Prices.TryGetLot(item, out int coins, out int lot)
                && !CanPay(Mathf.Min(count, inventory.Inventory.Count(item)) / lot * coins * inventory.Inventory.QualityOf(item)))
            {
                return 0;
            }
            var earned = new Wallet();
            int sold = Current.Prices.Sell(item, count, inventory.Inventory, earned);
            Pay(earned.Coins);
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
            bool tooFar = (Current.Position - transform.position).sqrMagnitude > closeDistance * closeDistance;
            if (closePressed || tooFar || !Current.IsTrading || (player != null && !player.enabled))
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
            float offerRows = OffersHeight() + TrainingHeight();
            float height = Padding * 2f + 24f + 22f + InventoryGridGui.PlayerHeight(inventory) + 12f + SellRowHeight + 6f + SellRowHeight + 6f + offerRows + (group.Count > 0 ? SellRowHeight + 6f : 0f);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), $"{Current.DisplayName}   (E to close)");
            GUI.Label(new Rect(panel.xMax - Padding - 160f, y, 160f, 20f), $"Your coins: {Wallet.Format(wallet.Wallet.Coins)}");
            y += 24f;
            // What the trader has left to buy with today (GL426), or why a sale was refused.
            string note = Time.unscaledTime < refusalUntil ? refusal
                : Current.Purse != null ? $"Click one of your items to see what they pay. They have {Wallet.Format(Current.Purse.Coins)} to buy with today."
                : "Click one of your items to see what they pay for it.";
            GUI.Label(new Rect(x, y, width - Padding * 2f, 20f), note);
            y += 22f;

            // While a group is picked only its stacks are lit (GL310): the last Ctrl-clicked slot may have just left it.
            int clicked = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, group.Count > 0 ? -1 : selected,
                _ => null, group);
            if (clicked >= 0)
            {
                if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed && Keyboard.current.shiftKey.isPressed)
                {
                    PickAllOfItem(items, clicked);
                }
                else if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
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
                // With a group picked, the only sale is the group's (GL358): the one-stack row's price read as the total.
                y = DrawOffers(x, y, width - Padding * 2f);
                DrawTraining(x, y, width - Padding * 2f);
                InventoryGridGui.DrawTooltip();
                return;
            }

            ItemStack chosen = selected >= 0 ? items[selected] : ItemStack.Empty;
            bool buys = !chosen.IsEmpty && prices.TryGetLot(chosen.Item, out _, out _);
            string what = chosen.IsEmpty ? "Pick a stack to sell"
                : buys ? $"{chosen.Item.DisplayName}: {FormatCoins(prices.PriceOf(chosen, 1))} each"
                : $"{Current.DisplayName} doesn't buy {chosen.Item.DisplayName}";
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
            y = DrawOffers(x, y, width - Padding * 2f);
            DrawTraining(x, y, width - Padding * 2f);
            InventoryGridGui.DrawTooltip();
        }

        // The trader can pay this many coins today (GL426); a trader with no purse always can.
        private bool CanPay(float coins)
        {
            if (Current.Purse == null || Current.Purse.Coins + 1e-3f >= coins)
            {
                return true;
            }
            refusal = $"{Current.DisplayName} has only {Wallet.Format(Current.Purse.Coins)} left today";
            refusalUntil = Time.unscaledTime + 3f;
            return false;
        }

        // Coins from the trader's purse (if they have one) to the player.
        private void Pay(float coins)
        {
            Current.Purse?.Spend(Mathf.Min(coins, Current.Purse.Coins));
            wallet.Wallet.Earn(coins);
        }

        // Coins to two places at most: "50c", "0.1c", "0.001c".
        private static string FormatCoins(float coins)
        {
            return coins >= 0.01f || coins == 0f ? $"{coins:0.##}c" : $"{coins:0.###}c";
        }

        // Rows of offers shown at once; more scroll (the wares shop sells everything, GL401).
        private const int VisibleOfferRows = 5;

        private int OfferRows => (Current.Offers.Count + 1) / 2;

        private float OffersHeight()
        {
            return Current.Offers.Count == 0 ? 0f : 24f + Mathf.Min(OfferRows, VisibleOfferRows) * (RowHeight + 4f) + 4f;
        }

        private float TrainingHeight()
        {
            return Current.Trains.Count == 0 ? 0f : 24f + Current.Trains.Count * (RowHeight + 4f) + 4f;
        }

        // What the trader sells, two buttons to a row and scrolling past a few rows: each buys one lot (e.g. 2 sticks),
        // greyed out when the player can't pay. Returns the y under it.
        private float DrawOffers(float x, float y, float width)
        {
            if (Current.Offers.Count == 0)
            {
                return y;
            }
            GUI.Label(new Rect(x, y, width, 20f), "For sale");
            y += 24f;
            float rowStep = RowHeight + 4f;
            float shown = Mathf.Min(OfferRows, VisibleOfferRows) * rowStep;
            bool scrolls = OfferRows > VisibleOfferRows;
            float inner = scrolls ? width - 18f : width;
            float button = (inner - 6f) * 0.5f;
            offerScroll = GUI.BeginScrollView(new Rect(x, y, width, shown), offerScroll, new Rect(0f, 0f, inner, OfferRows * rowStep));
            for (int i = 0; i < Current.Offers.Count; i++)
            {
                ItemPrice offer = Current.Offers[i];
                if (offer.Item == null)
                {
                    continue;
                }
                int lot = offer.Lot;
                string what = lot > 1 ? $"{lot} {offer.Item.DisplayName}" : offer.Item.DisplayName;
                GUI.enabled = wallet.Wallet.Coins >= offer.Coins;
                if (GUI.Button(new Rect(i % 2 * (button + 6f), i / 2 * rowStep, button, RowHeight), $"Buy {what}  ({offer.Coins}c)"))
                {
                    Buy(offer);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            return y + shown + 4f;
        }

        // Pays for one lot and puts it in the inventory; a full inventory refuses the sale.
        private void Buy(ItemPrice offer)
        {
            int lot = offer.Lot;
            int left = inventory.Inventory.Add(offer.Item, lot);
            if (left > 0)
            {
                inventory.Inventory.Remove(offer.Item, lot - left);
                return;
            }
            if (wallet.Wallet.Spend(offer.Coins))
            {
                Current.Purse?.Earn(offer.Coins);
            }
            else
            {
                inventory.Inventory.Remove(offer.Item, lot);
            }
        }

        // Lessons (GL402): one button per skill the trader teaches, raising it a level for coins in proportion to the
        // experience the level still needs.
        private void DrawTraining(float x, float y, float width)
        {
            if (Current.Trains.Count == 0 || skills == null)
            {
                return;
            }
            GUI.Label(new Rect(x, y, width, 20f), "Lessons");
            y += 24f;
            SkillSet set = skills.Skills;
            foreach (SkillType skill in Current.Trains)
            {
                int level = set.Level(skill);
                int cost = SkillTraining.Cost(set, skill, Current.CoinsPerExperience);
                bool top = cost <= 0;
                GUI.enabled = !top && wallet.Wallet.Coins >= cost;
                string label = top ? $"{skill}: level {level}, nothing left to teach"
                    : $"Train {skill} to level {level + 1}  ({cost}c, {SkillTraining.ExperienceToNext(set, skill):0} xp)";
                if (GUI.Button(new Rect(x, y, width, RowHeight), label)
                    && SkillTraining.Train(set, wallet.Wallet, skill, Current.CoinsPerExperience))
                {
                    Trained?.Invoke(skill);
                }
                GUI.enabled = true;
                y += RowHeight + 4f;
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
            if (!CanPay(earned))
            {
                return;
            }
            items.TakeAll(slot);
            Pay(earned);
            Sold?.Invoke(Mathf.Max(1, stack.Count));
            selected = -1;
        }

        // Ctrl-click (GL305, GL374): adds the clicked stack to the group, with the stack already highlighted, or takes it out.
        // Only the stacks clicked are picked, never every stack of the item.
        private void PickForGroup(Inventory items, int slot)
        {
            if (items[slot].IsEmpty)
            {
                return;
            }
            if (group.Count == 0)
            {
                if (selected >= 0 && selected < items.SlotCount && selected != slot && !items[selected].IsEmpty)
                {
                    group.Add(selected);
                }
                group.Add(slot);
            }
            else if (!group.Remove(slot))
            {
                group.Add(slot);
            }
            selected = group.Contains(slot) ? slot : group.Count > 0 ? First(group) : -1;
        }

        // Ctrl+Shift-click (GL393): every stack of the clicked item joins the group, whatever its quality.
        private void PickAllOfItem(Inventory items, int slot)
        {
            ItemDefinition item = items[slot].Item;
            if (item == null)
            {
                return;
            }
            if (selected >= 0 && selected < items.SlotCount && !items[selected].IsEmpty)
            {
                group.Add(selected);
            }
            for (int i = 0; i < items.SlotCount; i++)
            {
                if (items[i].Item == item)
                {
                    group.Add(i);
                }
            }
            selected = slot;
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
                $"{group.Count} stack{(group.Count == 1 ? "" : "s")} picked: {FormatAmount(amount)} in all\nCtrl-click: one stack in or out; Ctrl+Shift: all of an item");
            GUI.enabled = coins > 0f;
            if (GUI.Button(new Rect(x + 376f, y, 100f, SellRowHeight), $"Sell picked\n{FormatCoins(coins)}") && CanPay(coins))
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
            if (!CanPay(earned))
            {
                return;
            }
            sold = items.RemoveAmount(slot, sold);
            Pay(earned);
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
