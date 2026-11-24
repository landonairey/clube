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
    public class MerchantPanel : MonoBehaviour, IGuiDrawer
    {
        private const float Padding = 12f;
        private const float RowHeight = 34f;

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
        // The list inventory (prototype): your list on the left, the trader's stock on the right; what you sold them today
        // waits on their shelf to be bought back until the day turns.
        private readonly InventoryListGui playerList = new InventoryListGui();
        private readonly InventoryListGui stockList = new InventoryListGui();
        private readonly System.Collections.Generic.Dictionary<ITrader, Inventory> shelves = new System.Collections.Generic.Dictionary<ITrader, Inventory>();
        private Inventory stock;
        private int shelfDay = -1;
        private DayNightCycle dayNight;
        private PlayerName playerName;
        private const int ShelfSlots = 64;

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
            playerName = GetComponent<PlayerName>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Close();
        }

        public void Open(ITrader table)
        {
            Current = table;
            selected = -1;
            group.Clear();
            playerList.Reset();
            stockList.Reset();
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
                && !CanPay(Current.Prices.Paid(Mathf.Min(count, inventory.Inventory.Count(item)) / lot * coins * inventory.Inventory.QualityOf(item), Mathf.Min(count, inventory.Inventory.Count(item)) / (float)lot)))
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

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (Current == null || Current.Prices == null)
            {
                return;
            }

            Inventory items = inventory.Inventory;
            PriceList prices = Current.Prices;
            if (inventory.UsesList)
            {
                DrawListTrade(items, prices);
                return;
            }
            float width = InventoryGridGui.RowWidth + Padding * 2f;
            float offerRows = OffersHeight() + TrainingHeight();
            Disposition disposition = Current.Disposition;
            float height = Padding * 2f + 32f + 34f + 30f + InventoryGridGui.PlayerHeight(inventory) + 12f + SellRowHeight + 6f + SellRowHeight + 6f + offerRows + (group.Count > 0 ? SellRowHeight + 6f : 0f)
                + (disposition != null ? 30f : 0f);
            ITraderDeal deal = Current.Deal != null && Current.Deal.IsOffered(wallet.Wallet) ? Current.Deal : null;
            height += deal != null ? RowHeight + 6f : 0f;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 28f), $"{Current.DisplayName}   (E to close)");
            y += 32f;
            DrawCoinBar(new Rect(x, y, width - Padding * 2f, 30f));
            y += 34f;
            if (disposition != null)
            {
                GUI.Label(new Rect(x, y, width - Padding * 2f, 28f), DispositionText(disposition));
                y += 30f;
            }
            // Something special on offer (GL473: the trading post itself, to a player with 10k).
            if (deal != null)
            {
                if (GameUi.Button(new Rect(x, y, width - Padding * 2f, RowHeight), deal.Label))
                {
                    if (!deal.Accept(wallet.Wallet) && deal.RefusalText(wallet.Wallet) is string why)
                    {
                        refusal = why;
                        refusalUntil = Time.unscaledTime + 3f;
                    }
                }
                y += RowHeight + 6f;
            }
            // What the trader has left to buy with today (GL426), or why a sale was refused.
            string note = Time.unscaledTime < refusalUntil ? refusal
                : Current.Purse != null ? $"Click one of your items to see what they pay. They have {Wallet.Format(Current.Purse.Coins)} to buy with today."
                : "Click one of your items to see what they pay for it.";
            GUI.Label(new Rect(x, y, width - Padding * 2f, 28f), note);
            y += 30f;

            // While a group is picked only its stacks are lit (GL310): the last Ctrl-clicked slot may have just left it.
            int clicked = InventoryGridGui.DrawPlayer(new Vector2(x, y), inventory, group.Count > 0 ? -1 : selected,
                _ => null, group);
            if (clicked >= 0)
            {
                if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed && Keyboard.current.shiftKey.isPressed)
                {
                    PickForGroup(items, clicked);
                }
                else if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
                {
                    // Ctrl-click picks every stack of the item, whatever its quality (GL433, GL434); on a picked stack, takes it out.
                    if (group.Contains(clicked))
                    {
                        PickForGroup(items, clicked);
                    }
                    else
                    {
                        PickAllOfItem(items, clicked);
                    }
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
            GUI.Label(new Rect(x, y + 4f, 260f, 28f), what);
            GUI.enabled = buys;
            string oneLabel = "Sell 1" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, Mathf.Min(1f, chosen.Amount)))}" : "");
            string stackLabel = "Sell stack" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, chosen.Amount))}" : "");
            if (GameUi.Button(new Rect(x + 270f, y, 100f, SellRowHeight), oneLabel))
            {
                SellFromSlot(selected, 1f);
            }
            if (GameUi.Button(new Rect(x + 376f, y, 100f, SellRowHeight), stackLabel))
            {
                SellStack(selected);
            }
            y += SellRowHeight + 6f;

            // Part of the stack (GL57): a slider for how much, e.g. 100 of 109 stone for exactly 100 coins. Part items
            // sell too (GL246): under 10 the slider moves in hundredths, so 0.99 clay can go.
            float most = buys ? chosen.Amount : 1f;
            float least = Mathf.Min(most, most < 10f ? 0.01f : 1f);
            splitAmount = Mathf.Clamp(splitAmount, least, most);
            GUI.Label(new Rect(x, y + 12f, 60f, 28f), "Sell part");
            splitAmount = SnapAmount(GUI.HorizontalSlider(new Rect(x + 64f, y + 18f, 196f, 16f), splitAmount, least, most), most);
            if (buys)
            {
                // Arrow buttons for the last few, which a slider over a big stack can't hit.
                float step = most < 10f ? 0.01f : 1f;
                if (GameUi.Button(new Rect(x + 270f, y + 8f, 24f, 28f), "-"))
                {
                    splitAmount = Mathf.Max(least, SnapAmount(splitAmount - step, most));
                }
                if (GameUi.Button(new Rect(x + 346f, y + 8f, 24f, 28f), "+"))
                {
                    splitAmount = Mathf.Min(most, SnapAmount(splitAmount + step, most));
                }
                GUI.Label(new Rect(x + 296f, y + 12f, 48f, 28f), FormatAmount(splitAmount), countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter });
            }
            string partLabel = $"Sell {(buys ? FormatAmount(splitAmount) : "0")}" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, splitAmount))}" : "");
            if (GameUi.Button(new Rect(x + 376f, y, 100f, SellRowHeight), partLabel))
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
            Current.Disposition?.Record(coins);
        }

        // GL831: both purses on one bold line under the title, so what you hold and what the keeper can pay is always in view.
        private void DrawCoinBar(Rect bar)
        {
            var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(1f, 0.88f, 0.4f);
            float half = bar.width * 0.5f;
            GUI.Label(new Rect(bar.x, bar.y, half, bar.height), $"You have: {Wallet.Format(wallet.Wallet.Coins)}", style);
            if (Current.Purse != null)
            {
                style.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(bar.x + half, bar.y, half, bar.height), $"{Current.DisplayName} has: {Wallet.Format(Current.Purse.Coins)}", style);
            }
        }

        // How the trader feels about the player (GL471) and what it does to their prices.
        private static string DispositionText(Disposition disposition)
        {
            if (disposition.Value >= Disposition.Max)
            {
                return $"Disposition {disposition.Value}/{Disposition.Max}: fair prices.";
            }
            int points = Disposition.Max - disposition.Value;
            float toNext = Disposition.CoinsForPoint(disposition.Value) - disposition.Carried;
            return $"Disposition {disposition.Value}/{Disposition.Max}: prices {points}% against you; {Wallet.Format(toNext)} more trade raises it (a level costs {Wallet.Format(Disposition.CoinsForPoint(disposition.Value))}).";
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
            return Current.Offers.Count == 0 ? 0f : 32f + Mathf.Min(OfferRows, VisibleOfferRows) * (RowHeight + 4f) + 4f;
        }

        private float TrainingHeight()
        {
            return Current.Trains.Count == 0 ? 0f : 32f + Current.Trains.Count * (RowHeight + 4f) + 4f;
        }

        // What the trader sells, two buttons to a row and scrolling past a few rows: each buys one lot (e.g. 2 sticks),
        // greyed out when the player can't pay. Returns the y under it.
        private float DrawOffers(float x, float y, float width)
        {
            if (Current.Offers.Count == 0)
            {
                return y;
            }
            GUI.Label(new Rect(x, y, width, 28f), "For sale");
            y += 32f;
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
                if (GameUi.Button(new Rect(i % 2 * (button + 6f), i / 2 * rowStep, button, RowHeight), $"Buy {what}  ({offer.Coins}c)"))
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
                Current.Disposition?.Record(offer.Coins);
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
            GUI.Label(new Rect(x, y, width, 28f), "Lessons");
            y += 32f;
            SkillSet set = skills.Skills;
            foreach (SkillType skill in Current.Trains)
            {
                int level = set.Level(skill);
                int cost = SkillTraining.Cost(set, skill, Current.CoinsPerExperience);
                bool top = cost <= 0;
                GUI.enabled = !top && wallet.Wallet.Coins >= cost;
                string label = top ? $"{skill}: level {level}, nothing left to teach"
                    : $"Train {skill} to level {level + 1}  ({cost}c, {SkillTraining.ExperienceToNext(set, skill):0} xp)";
                if (GameUi.Button(new Rect(x, y, width, RowHeight), label)
                    && SkillTraining.Train(set, wallet.Wallet, skill, Current.CoinsPerExperience))
                {
                    // A lesson is trade with them too (GL471).
                    Current.Disposition?.Record(cost);
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
            Shelve(stack, stack.Amount);
            Pay(earned);
            Sold?.Invoke(Mathf.Max(1, stack.Count));
            selected = -1;
        }

        // Ctrl+Shift-click (GL305, GL374, GL433): adds the clicked stack to the group, with the stack already highlighted, or takes it out.
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

        // Ctrl-click (GL393, GL433): every stack of the clicked item joins the group, whatever its quality.
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
                $"{group.Count} stack{(group.Count == 1 ? "" : "s")} picked: {FormatAmount(amount)} in all\nCtrl-click: all of an item, or one picked stack out; Ctrl+Shift: one stack");
            GUI.enabled = coins > 0f;
            if (GameUi.Button(new Rect(x + 376f, y, 100f, SellRowHeight), $"Sell picked\n{FormatCoins(coins)}") && CanPay(coins))
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
            Shelve(stack, sold);
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

        // ---- The list inventory's trade screen (prototype) ----

        // What was sold goes on the trader's shelf for the rest of the day, to be bought back.
        private void Shelve(ItemStack stack, float amount)
        {
            if (!inventory.UsesList || stack.IsEmpty || amount <= ItemStack.PartEpsilon || Current == null)
            {
                return;
            }
            int whole = Mathf.FloorToInt(amount + ItemStack.PartEpsilon);
            float part = Mathf.Max(0f, amount - whole);
            Shelf().Add(stack.Item, whole, stack.Contents, stack.Wear, stack.Quality, part);
        }

        private Inventory Shelf()
        {
            int day = Today();
            if (day != shelfDay)
            {
                // A new day: what was sold yesterday is gone from every shelf.
                shelves.Clear();
                shelfDay = day;
            }
            if (!shelves.TryGetValue(Current, out Inventory shelf))
            {
                shelf = new Inventory(ShelfSlots);
                shelves.Add(Current, shelf);
            }
            return shelf;
        }

        private int Today()
        {
            if (dayNight == null)
            {
                dayNight = FindFirstObjectByType<DayNightCycle>();
            }
            return dayNight != null ? dayNight.Day : 1;
        }

        // The trader's list: a lot of each thing they sell, then their shelf of what was sold to them today.
        private void RefreshStock(Inventory shelf)
        {
            int offers = Current.Offers.Count;
            if (stock == null || stock.SlotCount != offers + shelf.SlotCount)
            {
                stock = new Inventory(offers + shelf.SlotCount);
            }
            for (int i = 0; i < offers; i++)
            {
                ItemPrice offer = Current.Offers[i];
                stock.Set(i, offer.Item != null ? new ItemStack(offer.Item, offer.Lot) : ItemStack.Empty);
            }
            for (int i = 0; i < shelf.SlotCount; i++)
            {
                stock.Set(offers + i, shelf[i]);
            }
        }

        // Coins one item costs from the trader: an offer's lot price shared out, or what they paid for what's on the shelf.
        private float StockValue(int slot)
        {
            int offers = Current.Offers.Count;
            if (slot < offers)
            {
                ItemPrice offer = Current.Offers[slot];
                return offer.Lot > 0 ? offer.Coins / (float)offer.Lot : offer.Coins;
            }
            return EachPrice(stock[slot]);
        }

        // What the trader pays for one item of a stack; 0 when they don't buy it.
        private float EachPrice(ItemStack stack)
        {
            if (stack.IsEmpty || !Current.Prices.TryGetLot(stack.Item, out _, out _))
            {
                return 0f;
            }
            float one = Mathf.Min(1f, stack.Amount);
            return one > 0f ? Current.Prices.PriceOf(stack, one) / one : 0f;
        }

        // Left: your name and coins in bold over your list, and the sale of what's picked under it. Right: the trader's
        // name and coins over what they sell (and what you sold them today), buying under it, then their lessons.
        private void DrawListTrade(Inventory items, PriceList prices)
        {
            Inventory shelf = Shelf();
            if (Event.current.type == EventType.Layout || stock == null)
            {
                RefreshStock(shelf);
            }
            float width = Mathf.Min(Screen.width - 40f, 2000f);
            float half = (width - Padding * 3f) * 0.5f;
            float height = Mathf.Clamp(Screen.height - 280f, 600f, 1150f);
            var panel = new Rect((Screen.width - width) * 0.5f, Mathf.Max(10f, (Screen.height - height) * 0.5f - 60f), width, height);
            InventoryGridGui.SolidPanel(panel);
            float top = panel.y + Padding;
            var left = new Rect(panel.x + Padding, top, half, panel.height - Padding * 2f);
            var right = new Rect(left.xMax + Padding, top, half, left.height);

            var bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = GameUi.Font(18), alignment = TextAnchor.MiddleLeft };
            var gold = new GUIStyle(bold) { alignment = TextAnchor.MiddleRight };
            gold.normal.textColor = new Color(1f, 0.85f, 0.35f);
            string me = playerName != null && !string.IsNullOrEmpty(playerName.Name) ? playerName.Name : "You";
            GUI.Label(new Rect(left.x, top, half * 0.6f, 36f), me, bold);
            GUI.Label(new Rect(left.x + half * 0.4f, top, half * 0.6f, 36f), $"{Wallet.Format(wallet.Wallet.Coins)} COINS", gold);
            GUI.Label(new Rect(right.x, top, half * 0.6f, 36f), $"{Current.DisplayName}   (E to close)", bold);
            GUI.Label(new Rect(right.x + half * 0.4f, top, half * 0.6f, 36f),
                Current.Purse != null ? $"{Wallet.Format(Current.Purse.Coins)} COINS" : "Buys without limit", gold);
            float y = top + 42f;

            string note = Time.unscaledTime < refusalUntil ? refusal : "Click one of your items to sell it; Val is what they pay for one.";
            GUI.Label(new Rect(left.x, y, half, 28f), note);
            float rightY = y;
            Disposition disposition = Current.Disposition;
            if (disposition != null)
            {
                GUI.Label(new Rect(right.x, rightY, half, 28f), DispositionText(disposition));
                rightY += 30f;
            }
            ITraderDeal deal = Current.Deal != null && Current.Deal.IsOffered(wallet.Wallet) ? Current.Deal : null;
            if (deal != null)
            {
                if (GameUi.Button(new Rect(right.x, rightY, half, RowHeight), deal.Label)
                    && !deal.Accept(wallet.Wallet) && deal.RefusalText(wallet.Wallet) is string why)
                {
                    refusal = why;
                    refusalUntil = Time.unscaledTime + 3f;
                }
                rightY += RowHeight + 6f;
            }
            if (disposition == null && deal == null)
            {
                GUI.Label(new Rect(right.x, rightY, half, 28f), "Click something of theirs to buy it; right-click buys one lot.");
                rightY += 30f;
            }
            y += 30f;

            const float sellHeight = SellRowHeight * 2f + 12f;
            var playerArea = new Rect(left.x, y, half, left.yMax - sellHeight - 8f - y);
            playerList.Value = slot => EachPrice(items[slot]);
            playerList.Equipped = inventory.IsHandSlot;
            playerList.Draw(playerArea, items);
            DrawListSell(left.x, playerArea.yMax + 8f, half, items, prices);

            float training = TrainingHeight();
            const float buyHeight = SellRowHeight + 8f;
            var stockArea = new Rect(right.x, rightY, half, right.yMax - training - buyHeight - rightY);
            stockList.Value = StockValue;
            ListResult bought = stockList.Draw(stockArea, stock);
            if (bought.Kind == InventoryDragKind.RightClicked)
            {
                BuyRow(bought.Row, shelf, false);
            }
            DrawListBuy(right.x, stockArea.yMax + 8f, half, shelf);
            DrawTraining(right.x, stockArea.yMax + 8f + buyHeight, half);
            InventoryGridGui.DrawTooltip();
        }

        private int splitFor = -1;

        // Selling what's picked in your list: a collection all at once, a stack whole, one, or a part on the slider.
        private void DrawListSell(float x, float y, float width, Inventory items, PriceList prices)
        {
            ListRow row = playerList.Selected;
            if (row == null || items[row.Slot].IsEmpty)
            {
                GUI.Label(new Rect(x, y + 8f, width, 28f), "Pick something to sell.");
                return;
            }
            float buttonWidth = 130f;
            float buttonsX = x + width - buttonWidth * 2f - 6f;
            if (row.IsCollection)
            {
                float coins = 0f, amount = 0f;
                foreach (int slot in row.Slots)
                {
                    if (prices.TryGetLot(items[slot].Item, out _, out _))
                    {
                        coins += prices.PriceOf(items[slot], items[slot].Amount);
                        amount += items[slot].Amount;
                    }
                }
                GUI.Label(new Rect(x, y + 4f, buttonsX - x - 8f, 40f), coins > 0f
                    ? $"All {FormatAmount(amount)} {row.Item.DisplayName} ({row.Slots.Count} stacks)"
                    : $"{Current.DisplayName} doesn't buy {row.Item.DisplayName}");
                GUI.enabled = coins > 0f;
                if (GameUi.Button(new Rect(buttonsX + buttonWidth + 6f, y, buttonWidth, SellRowHeight), $"Sell all\n{FormatCoins(coins)}") && CanPay(coins))
                {
                    foreach (int slot in new System.Collections.Generic.List<int>(row.Slots))
                    {
                        SellStack(slot);
                    }
                    playerList.ClearSelection();
                }
                GUI.enabled = true;
                return;
            }

            int chosenSlot = row.Slot;
            ItemStack chosen = items[chosenSlot];
            if (splitFor != chosenSlot)
            {
                splitFor = chosenSlot;
                splitAmount = chosen.Amount;
            }
            bool buys = prices.TryGetLot(chosen.Item, out _, out _);
            string what = buys ? $"{chosen.Item.DisplayName}: {FormatCoins(prices.PriceOf(chosen, Mathf.Min(1f, chosen.Amount)))} each"
                : $"{Current.DisplayName} doesn't buy {chosen.Item.DisplayName}";
            GUI.Label(new Rect(x, y + 4f, buttonsX - x - 8f, 28f), what);
            GUI.enabled = buys;
            if (GameUi.Button(new Rect(buttonsX, y, buttonWidth, SellRowHeight), "Sell 1" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, Mathf.Min(1f, chosen.Amount)))}" : "")))
            {
                SellFromSlot(chosenSlot, 1f);
            }
            if (GameUi.Button(new Rect(buttonsX + buttonWidth + 6f, y, buttonWidth, SellRowHeight), "Sell stack" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, chosen.Amount))}" : "")))
            {
                SellStack(chosenSlot);
                playerList.ClearSelection();
            }
            y += SellRowHeight + 6f;
            float most = buys ? chosen.Amount : 1f;
            float least = Mathf.Min(most, most < 10f ? 0.01f : 1f);
            splitAmount = Mathf.Clamp(splitAmount, least, most);
            GUI.Label(new Rect(x, y + 12f, 80f, 28f), "Sell part");
            float sliderWidth = buttonsX - x - 90f - 90f;
            splitAmount = SnapAmount(GUI.HorizontalSlider(new Rect(x + 84f, y + 18f, Mathf.Max(60f, sliderWidth), 16f), splitAmount, least, most), most);
            GUI.Label(new Rect(buttonsX - 86f, y + 12f, 80f, 28f), FormatAmount(splitAmount), countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter });
            if (GameUi.Button(new Rect(buttonsX + buttonWidth + 6f, y, buttonWidth, SellRowHeight),
                $"Sell {(buys ? FormatAmount(splitAmount) : "0")}" + (buys ? $"\n{FormatCoins(prices.PriceOf(chosen, splitAmount))}" : "")))
            {
                SellFromSlot(chosenSlot, splitAmount);
            }
            GUI.enabled = true;
        }

        // Buying what's picked in the trader's list: a lot of an offer, or one or all of a shelf stack.
        private void DrawListBuy(float x, float y, float width, Inventory shelf)
        {
            ListRow row = stockList.Selected;
            if (row == null || stock[row.Slot].IsEmpty)
            {
                GUI.Label(new Rect(x, y + 8f, width, 28f), "Pick something of theirs to buy.");
                return;
            }
            float buttonWidth = 170f;
            float buttonsX = x + width - buttonWidth * 2f - 6f;
            if (row.IsCollection)
            {
                GUI.Label(new Rect(x, y + 8f, width, 28f), "Open the collection with its arrow and pick one to buy.");
                return;
            }
            int offers = Current.Offers.Count;
            if (row.Slot < offers)
            {
                ItemPrice offer = Current.Offers[row.Slot];
                GUI.Label(new Rect(x, y + 8f, buttonsX - x - 8f, 28f), $"{offer.Item.DisplayName}: {offer.Coins}c for {offer.Lot}");
                GUI.enabled = wallet.Wallet.Coins >= offer.Coins;
                if (GameUi.Button(new Rect(buttonsX + buttonWidth + 6f, y, buttonWidth, SellRowHeight), $"Buy {offer.Lot}\n{offer.Coins}c"))
                {
                    Buy(offer);
                }
                GUI.enabled = true;
                return;
            }
            ItemStack stack = stock[row.Slot];
            GUI.Label(new Rect(x, y + 8f, buttonsX - x - 8f, 28f), $"{stack.Item.DisplayName}: sold to them today");
            float one = Mathf.Min(1f, stack.Amount);
            if (GameUi.Button(new Rect(buttonsX, y, buttonWidth, SellRowHeight), $"Buy back {FormatAmount(one)}\n{FormatCoins(Current.Prices.PriceOf(stack, one))}"))
            {
                BuyRow(row, shelf, false);
            }
            if (GameUi.Button(new Rect(buttonsX + buttonWidth + 6f, y, buttonWidth, SellRowHeight), $"Buy back all\n{FormatCoins(Current.Prices.PriceOf(stack, stack.Amount))}"))
            {
                BuyRow(row, shelf, true);
            }
        }

        // A lot of an offer, or one (or all) of a shelf stack back, at what they paid for it.
        private void BuyRow(ListRow row, Inventory shelf, bool all)
        {
            if (row == null || row.IsCollection)
            {
                return;
            }
            int offers = Current.Offers.Count;
            if (row.Slot < offers)
            {
                if (wallet.Wallet.Coins >= Current.Offers[row.Slot].Coins)
                {
                    Buy(Current.Offers[row.Slot]);
                }
                return;
            }
            int shelfSlot = row.Slot - offers;
            ItemStack stack = shelf[shelfSlot];
            if (stack.IsEmpty)
            {
                return;
            }
            float amount = all ? stack.Amount : Mathf.Min(1f, stack.Amount);
            float coins = Current.Prices.PriceOf(stack, amount);
            if (wallet.Wallet.Coins + 1e-3f < coins)
            {
                refusal = "You don't have the coins for that";
                refusalUntil = Time.unscaledTime + 3f;
                return;
            }
            int whole = Mathf.FloorToInt(amount + ItemStack.PartEpsilon);
            float part = Mathf.Max(0f, amount - whole);
            int left = inventory.Inventory.Add(stack.Item, whole, stack.Contents, stack.Wear, stack.Quality, part);
            float added = whole - left + (left == 0 ? part : 0f);
            if (added <= ItemStack.PartEpsilon)
            {
                refusal = "Your inventory is full";
                refusalUntil = Time.unscaledTime + 3f;
                return;
            }
            float paid = Current.Prices.PriceOf(stack, added);
            shelf.RemoveAmount(shelfSlot, added);
            wallet.Wallet.Spend(Mathf.Min(paid, wallet.Wallet.Coins));
            Current.Purse?.Earn(paid);
            Current.Disposition?.Record(paid);
            if (Event.current != null && Event.current.type != EventType.Layout)
            {
                RefreshStock(shelf);
            }
        }
    }
}
