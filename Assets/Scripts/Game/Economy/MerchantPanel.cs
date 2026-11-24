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
    public class MerchantPanel : MonoBehaviour, IGuiDrawer, MerchantCart.ISource
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
        // What the player picked to sell and to buy, and how much of each offer they bought today, per trader.
        private readonly MerchantCart cart = new MerchantCart();
        private readonly System.Collections.Generic.Dictionary<ITrader, float[]> boughtToday = new System.Collections.Generic.Dictionary<ITrader, float[]>();
        // A trader has this many lots of each thing they sell a day, at most (fewer when a lot won't stack that high).
        private const int LotsADay = 5;

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
            cart.Clear();
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

        // Pays for one lot and puts it in the inventory; a full inventory refuses the sale. True when it was bought. Bought
        // material comes pure, made of its own material, so it reads and stacks like gathered material.
        private bool Buy(ItemPrice offer)
        {
            int lot = offer.Lot;
            Composition contents = PureContents(offer.Item);
            int left = inventory.Inventory.Add(offer.Item, lot, contents);
            if (left > 0)
            {
                inventory.Inventory.Remove(offer.Item, lot - left);
                return false;
            }
            if (wallet.Wallet.Spend(offer.Coins))
            {
                Current.Purse?.Earn(offer.Coins);
                Current.Disposition?.Record(offer.Coins);
                return true;
            }
            inventory.Inventory.Remove(offer.Item, lot);
            return false;
        }

        private static Composition PureContents(ItemDefinition item)
        {
            return ItemSlotGui.Materials != null ? ItemSlotGui.Materials.PureContents(item) : null;
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
                // A new day: what was sold yesterday is gone from every shelf, and every trader has their full stock again.
                shelves.Clear();
                boughtToday.Clear();
                shelfDay = day;
            }
            if (!shelves.TryGetValue(Current, out Inventory shelf))
            {
                shelf = new Inventory(ShelfSlots);
                shelves.Add(Current, shelf);
            }
            return shelf;
        }

        // How much of an offer the trader has a day: up to LotsADay lots, never more than one slot holds.
        private float SupplyOf(int offer)
        {
            ItemPrice price = Current.Offers[offer];
            int lot = Mathf.Max(1, price.Lot);
            return price.Item == null ? 0f : Mathf.Clamp(price.Item.MaxStack / lot, 1, LotsADay) * lot;
        }

        // How much of an offer is still for sale today.
        private float LeftToday(int offer)
        {
            return Mathf.Max(0f, SupplyOf(offer) - BoughtToday()[offer]);
        }

        // How much of each offer the player bought from the open trader today (Shelf() turns the day).
        private float[] BoughtToday()
        {
            Shelf();
            int offers = Current.Offers.Count;
            if (!boughtToday.TryGetValue(Current, out float[] bought) || bought.Length != offers)
            {
                bought = new float[offers];
                boughtToday[Current] = bought;
            }
            return bought;
        }

        // Coins an offer asks for one item: its lot's price shared out.
        private float OfferEach(int offer)
        {
            ItemPrice price = Current.Offers[offer];
            return price.Lot > 0 ? price.Coins / (float)price.Lot : price.Coins;
        }

        private int Today()
        {
            if (dayNight == null)
            {
                dayNight = FindFirstObjectByType<DayNightCycle>();
            }
            return dayNight != null ? dayNight.Day : 1;
        }

        // The trader's list: what's left today of each thing they sell, then their shelf of what was sold to them today.
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
                float left = offer.Item != null ? LeftToday(i) : 0f;
                int whole = Mathf.FloorToInt(left + ItemStack.PartEpsilon);
                float part = Mathf.Max(0f, left - whole);
                stock.Set(i, left > ItemStack.PartEpsilon ? new ItemStack(offer.Item, whole, PureContents(offer.Item), 0, 1f, part > ItemStack.PartEpsilon ? part : 0f) : ItemStack.Empty);
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
                return OfferEach(slot);
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

            string note = Time.unscaledTime < refusalUntil ? refusal : "Click your items to sell them; Val is what they pay for one.";
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
                GUI.Label(new Rect(right.x, rightY, half, 28f), "Click something of theirs to buy it.");
                rightY += 30f;
            }
            y += 30f;

            // Both lists over the one cart along the bottom: a click puts a stack (or every stack of a collection) in the cart,
            // and it leaves its list until it comes out of the cart or the trade is made.
            float cartTop = panel.yMax - Padding - Mathf.Clamp(panel.height * 0.36f, 280f, 420f);
            var playerArea = new Rect(left.x, y, half, cartTop - 8f - y);
            playerList.Value = slot => EachPrice(items[slot]);
            playerList.Equipped = inventory.IsHandSlot;
            playerList.Include = slot => !cart.Contains(MerchantCart.Side.Sell, slot);
            ListResult picked = playerList.Draw(playerArea, items);
            if (picked.Kind == InventoryDragKind.Clicked && picked.Row != null)
            {
                cart.Add(MerchantCart.Side.Sell, picked.Row.Slots, picked.Row.Item, picked.Row.Amount);
                playerList.ClearSelection();
            }

            float training = TrainingHeight();
            var stockArea = new Rect(right.x, rightY, half, cartTop - 8f - training - rightY);
            stockList.Value = StockValue;
            stockList.Include = slot => !cart.Contains(MerchantCart.Side.Buy, slot);
            ListResult bought = stockList.Draw(stockArea, stock);
            if ((bought.Kind == InventoryDragKind.Clicked || bought.Kind == InventoryDragKind.RightClicked) && bought.Row != null)
            {
                float first = 0f;
                foreach (int slot in bought.Row.Slots)
                {
                    first += BuyFirst(slot);
                }
                cart.Add(MerchantCart.Side.Buy, bought.Row.Slots, bought.Row.Item, first);
                stockList.ClearSelection();
            }
            DrawTraining(right.x, stockArea.yMax + 8f, half);

            if (cart.Draw(new Rect(panel.x + Padding, cartTop, panel.width - Padding * 2f, panel.yMax - Padding - cartTop), this))
            {
                MakeTrade(items, shelf);
            }
            InventoryGridGui.DrawTooltip();
        }

        // What a click on the trader's list puts in the cart: one lot of an offer (or what's left of it), all of a shelf stack.
        private float BuyFirst(int slot)
        {
            ItemStack stack = stock[slot];
            int offers = Current.Offers.Count;
            return slot < offers ? Mathf.Min(stack.Amount, Mathf.Max(1, Current.Offers[slot].Lot)) : stack.Amount;
        }

        // What the trader pays for an amount of a stack; 0 when they don't buy it.
        private static float SalePrice(PriceList prices, ItemStack stack, float amount)
        {
            return !stack.IsEmpty && prices.TryGetLot(stack.Item, out _, out _) ? prices.PriceOf(stack, amount) : 0f;
        }

        ItemStack MerchantCart.ISource.StackOf(MerchantCart.Side side, int slot)
        {
            Inventory from = side == MerchantCart.Side.Sell ? inventory.Inventory : stock;
            return from != null && slot >= 0 && slot < from.SlotCount ? from[slot] : ItemStack.Empty;
        }

        float MerchantCart.ISource.CoinsFor(MerchantCart.Side side, int slot, float amount)
        {
            ItemStack stack = ((MerchantCart.ISource)this).StackOf(side, slot);
            if (stack.IsEmpty)
            {
                return 0f;
            }
            if (side == MerchantCart.Side.Sell)
            {
                return SalePrice(Current.Prices, stack, amount);
            }
            // An offer at its lot's price shared out; a shelf stack back at what they paid for it.
            return slot < Current.Offers.Count ? amount * OfferEach(slot) : Current.Prices.PriceOf(stack, amount);
        }

        // The whole cart as one trade: refused when the side that comes out behind can't pay the net. What's sold goes
        // first (making room), then what's bought, as much as fits; the coins settle on the net of what actually moved.
        private void MakeTrade(Inventory items, Inventory shelf)
        {
            var source = (MerchantCart.ISource)this;
            float net = cart.Net(source);
            if (net < 0f && wallet.Wallet.Coins + 1e-3f < -net)
            {
                refusal = "You don't have the coins for that";
                refusalUntil = Time.unscaledTime + 3f;
                return;
            }
            if (net > 0f && !CanPay(net))
            {
                return;
            }
            var lines = new System.Collections.Generic.List<MerchantCart.Line>(cart.Lines);
            float got = 0f, paid = 0f;
            foreach (MerchantCart.Line line in lines)
            {
                if (line.Side != MerchantCart.Side.Sell)
                {
                    continue;
                }
                foreach (var part in MerchantCart.Allocate(line, source))
                {
                    ItemStack stack = items[part.Key];
                    if (stack.IsEmpty || !Current.Prices.TryGetLot(stack.Item, out _, out _))
                    {
                        continue;
                    }
                    float sold = part.Value >= stack.Amount - ItemStack.PartEpsilon ? TakeWhole(items, part.Key) : items.RemoveAmount(part.Key, part.Value);
                    Shelve(stack, sold);
                    got += Current.Prices.PriceOf(stack, sold);
                    Sold?.Invoke(Mathf.Max(1, Mathf.FloorToInt(sold)));
                }
            }
            bool full = false;
            foreach (MerchantCart.Line line in lines)
            {
                if (line.Side != MerchantCart.Side.Buy)
                {
                    continue;
                }
                foreach (var part in MerchantCart.Allocate(line, source))
                {
                    int slot = part.Key;
                    ItemStack stack = source.StackOf(MerchantCart.Side.Buy, slot);
                    if (stack.IsEmpty)
                    {
                        continue;
                    }
                    bool offer = slot < Current.Offers.Count;
                    float amount = Mathf.Min(part.Value, stack.Amount);
                    float added = AddToItems(items, stack, amount);
                    full |= added < amount - ItemStack.PartEpsilon;
                    if (added <= ItemStack.PartEpsilon)
                    {
                        continue;
                    }
                    paid += source.CoinsFor(MerchantCart.Side.Buy, slot, added);
                    if (offer)
                    {
                        BoughtToday()[slot] += added;
                    }
                    else
                    {
                        shelf.RemoveAmount(slot - Current.Offers.Count, added);
                    }
                }
            }
            float settle = got - paid;
            if (settle > 0f)
            {
                Pay(settle);
            }
            else if (settle < 0f)
            {
                wallet.Wallet.Spend(Mathf.Min(-settle, wallet.Wallet.Coins));
                Current.Purse?.Earn(-settle);
                Current.Disposition?.Record(-settle);
            }
            if (full)
            {
                refusal = "Your inventory is full: not everything bought fit";
                refusalUntil = Time.unscaledTime + 3f;
            }
            cart.Clear();
            RefreshStock(shelf);
        }

        private static float TakeWhole(Inventory items, int slot)
        {
            float amount = items[slot].Amount;
            items.TakeAll(slot);
            return amount;
        }

        // Puts an amount of a stack (whole items and a part) in the player's items; returns how much went in.
        private static float AddToItems(Inventory items, ItemStack stack, float amount)
        {
            int whole = Mathf.FloorToInt(amount + ItemStack.PartEpsilon);
            float part = Mathf.Max(0f, amount - whole);
            if (part <= ItemStack.PartEpsilon)
            {
                part = 0f;
            }
            int left = items.Add(stack.Item, whole, stack.Contents, stack.Wear, stack.Quality, part);
            return whole - left + (left == 0 ? part : 0f);
        }
    }
}
