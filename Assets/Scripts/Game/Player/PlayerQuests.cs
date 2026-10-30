using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's quest checklist (GL216): a <see cref="QuestLog"/> of the beginning path ("Main Objectives") and
    /// goals beside it ("Side Quests"), ticked off by watching the game: what the player carries, makes, places,
    /// sells, buys, carves and finds. The field log's quest page lists them (<see cref="FieldLogScreen"/>) and the
    /// HUD shows the active one under the lab HUD box (<see cref="QuestHud"/>). Saved with the player (save version 3).
    /// </summary>
    /// <remarks>
    /// Most quests are checked against the inventory a few times a second, so one counts however the item came
    /// (picked up, made, bought or loaded from a save); the rest listen for the moment they happen.
    /// </remarks>
    public class PlayerQuests : MonoBehaviour
    {
        public const string MainSection = "Main Objectives";
        public const string SideSection = "Side Quests";

        // Seconds between looks at the inventory.
        private const float CheckInterval = 0.25f;
        private const float CoinGoal = 10000f;
        private const float FurnaceQualityGoal = 0.9f;
        private const float NoticeSeconds = 4f;

        private static readonly string[] Ingots = { "tin-ingot", "copper-ingot", "bronze-ingot", "silver-ingot", "gold-ingot" };

        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private PlayerJournal journal;
        private PlayerSkills skills;
        private PlayerBuilder builder;
        private MerchantPanel merchant;
        private PlayerParcels parcels;
        private FlatRockCarver carver;
        private float nextCheck;
        private string notice;
        private float noticeUntil;
        private readonly HashSet<string> carried = new HashSet<string>();

        /// <summary>The checklist.</summary>
        public QuestLog Log { get; } = CreateLog();

        /// <summary>The checklist every game starts with.</summary>
        public static QuestLog CreateLog()
        {
            var log = new QuestLog();
            log.Add("pick-up-stone", "Pick up a stone", MainSection);
            log.Add("hand-axe", "Craft a stone into a hand axe on a boulder", MainSection);
            log.Add("gather-wood", "Gather wood", MainSection);
            log.Add("campfire", "Build a campfire", MainSection);
            log.Add("first-ore", "Gather your first ore", MainSection);
            log.Add("smelt-bun", "Smelt ore into a bun", MainSection);
            log.Add("bronze", "Smelt copper and tin into bronze", MainSection);
            log.Add("flat-rock", "Chisel a stone boulder into a flat rock", MainSection);
            log.Add("sell", "Sell an item to the merchant", SideSection);
            log.Add("coins", "Have 10k coins", SideSection);
            log.Add("parcel", "Buy another parcel of land", SideSection);
            log.Add("clay-furnace", "Build a clay furnace of over 90% quality", SideSection);
            log.Add("smelt-crushed", "Smelt crushed ore into a bun", SideSection);
            log.Add("find-gold", "Find gold!", SideSection);
            log.Add("collect-all", "Collect them all! Have an ingot of tin, copper, bronze, silver and gold at once", SideSection);
            return log;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            wallet = GetComponent<PlayerWallet>();
            journal = GetComponent<PlayerJournal>();
            skills = GetComponent<PlayerSkills>();
            builder = GetComponent<PlayerBuilder>();
            merchant = GetComponent<MerchantPanel>();
            parcels = GetComponent<PlayerParcels>();
            carver = GetComponent<FlatRockCarver>();
        }

        private void OnEnable()
        {
            Log.Completed += OnCompleted;
            if (skills != null)
            {
                skills.RecipeFinished += OnRecipeFinished;
            }
            if (builder != null)
            {
                builder.Placed += OnPlaced;
            }
            if (merchant != null)
            {
                merchant.Sold += OnSold;
            }
            if (parcels != null)
            {
                parcels.Bought += OnBought;
            }
            if (carver != null)
            {
                carver.Made += OnFlatRock;
            }
        }

        private void OnDisable()
        {
            Log.Completed -= OnCompleted;
            if (skills != null)
            {
                skills.RecipeFinished -= OnRecipeFinished;
            }
            if (builder != null)
            {
                builder.Placed -= OnPlaced;
            }
            if (merchant != null)
            {
                merchant.Sold -= OnSold;
            }
            if (parcels != null)
            {
                parcels.Bought -= OnBought;
            }
            if (carver != null)
            {
                carver.Made -= OnFlatRock;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + CheckInterval;
            CheckInventory();
            if (wallet != null && wallet.Wallet != null && wallet.Wallet.Coins >= CoinGoal)
            {
                Log.Complete("coins");
            }
            if (journal != null && HasFoundOre())
            {
                Log.Complete("first-ore");
            }
        }

        // What the player carries now, by item id.
        private void CheckInventory()
        {
            Inventory items = inventory != null ? inventory.Inventory : null;
            if (items == null)
            {
                return;
            }
            carried.Clear();
            for (int i = 0; i < items.SlotCount; i++)
            {
                ItemStack stack = items[i];
                if (!stack.IsEmpty && stack.Item != null && !string.IsNullOrEmpty(stack.Item.Id))
                {
                    carried.Add(stack.Item.Id);
                }
            }
            if (carried.Contains("stone"))
            {
                Log.Complete("pick-up-stone");
            }
            if (carried.Contains("hand-axe"))
            {
                Log.Complete("hand-axe");
            }
            if (carried.Contains("wood") || carried.Contains("maple-wood"))
            {
                Log.Complete("gather-wood");
            }
            if (carried.Contains("gold-ore") || carried.Contains("gold-bun") || carried.Contains("gold-ingot"))
            {
                Log.Complete("find-gold");
            }
            bool all = true;
            foreach (string ingot in Ingots)
            {
                all &= carried.Contains(ingot);
            }
            if (all)
            {
                Log.Complete("collect-all");
            }
        }

        // Any ore in the mining journal: known or not, the first ore has been mined.
        private bool HasFoundOre()
        {
            foreach (MiningJournal.Entry _ in journal.Journal.Entries)
            {
                return true;
            }
            return false;
        }

        private void OnRecipeFinished(Recipe recipe)
        {
            string made = recipe.Output.Item != null ? recipe.Output.Item.Id : null;
            if (made == null || !made.EndsWith("-bun"))
            {
                return;
            }
            Log.Complete("smelt-bun");
            if (made == "bronze-bun")
            {
                Log.Complete("bronze");
            }
            foreach (ItemAmount input in recipe.Inputs)
            {
                if (input.Item != null && input.Item.Id != null && input.Item.Id.StartsWith("crushed-") && input.Item.Id != "crushed-stone")
                {
                    Log.Complete("smelt-crushed");
                }
            }
        }

        private void OnPlaced(PlacedObject placed)
        {
            string id = placed.Item != null ? placed.Item.Id : null;
            if (id == "campfire")
            {
                Log.Complete("campfire");
            }
            if (id == "clay-furnace" && placed.Quality > FurnaceQualityGoal)
            {
                Log.Complete("clay-furnace");
            }
        }

        private void OnSold(int count)
        {
            Log.Complete("sell");
        }

        private void OnBought(Vector2Int parcel)
        {
            Log.Complete("parcel");
        }

        private void OnFlatRock(IReadOnlyList<Vector3Int> top)
        {
            Log.Complete("flat-rock");
        }

        private void OnCompleted(QuestLog.Quest quest)
        {
            notice = $"Quest done: {quest.Title}";
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        private void OnGUI()
        {
            if (notice != null && Time.unscaledTime < noticeUntil)
            {
                JournalBook.Notice(notice);
            }
        }
    }
}
