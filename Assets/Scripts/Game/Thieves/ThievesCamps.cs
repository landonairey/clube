using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>One thieves' camp of the map (GL617): its haunts, read off the plan, and what it has stolen.</summary>
    public sealed class ThievesCamp
    {
        public PlannedSettlement Settlement;

        /// <summary>The stretch of cart path the band lies in wait by, when one is in reach.</summary>
        public bool HasAmbush;
        public Vector2 Ambush;
        public int AmbushRoad = -1;

        /// <summary>The settlement the band steals from by night; null with none in reach.</summary>
        public PlannedSettlement Mark;

        /// <summary>The stash: every stolen thing ends here, and anything in it can be taken back.</summary>
        public Inventory Stash = new Inventory(30);

        /// <summary>The stolen coins.</summary>
        public Wallet Hoard = new Wallet();

        /// <summary>The time (<see cref="Time.time"/>) before which the band leaves the player alone.</summary>
        public float LeavesPlayerUntil;

        /// <summary>Hold-ups, burglaries and robberies of the player that came off, since the map was made.</summary>
        public int CartsRobbed, TownsRobbed, PlayerRobbed;

        internal StorageChest Chest;
        internal CoinPouch Pouch;
    }

    /// <summary>
    /// The thieves' camps of the map as places that steal (GL617). The camps themselves are settlements of the plan and
    /// their bands residents with an occupation (<see cref="SettlementPeople"/>, the "bandit" row and its duties); this
    /// keeps what those need of the scene: each camp's haunts (<see cref="ThiefHaunts"/>), its stash (a chest and a coin
    /// pouch by the first hut once the camp stands, holding everything the band has taken), the carts and shops a thief
    /// can reach, the player's bag and purse, and the line on screen that says what was taken. Nothing here is saved.
    /// </summary>
    public class ThievesCamps : MonoBehaviour
    {
        [SerializeField]
        private MapSession session;

        [SerializeField]
        private MapSettlements settlements;

        [SerializeField]
        private SettlementPeople people;

        [SerializeField]
        private MapCarts carts;

        [Tooltip("The low-poly vertex-colour material the coin pouches are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The wooden crate (a storage chest) a camp's stash is.")]
        [SerializeField]
        private GameObject cratePrefab;

        [Tooltip("Real seconds a band leaves the player alone after robbing them, and after one of it is struck.")]
        [SerializeField, Range(10f, 600f)]
        private float robberyRest = 75f, routedRest = 120f;

        private readonly Dictionary<int, ThievesCamp> camps = new Dictionary<int, ThievesCamp>();
        private readonly List<ItemStack> taken = new List<ItemStack>();
        private PlayerInventory playerBag;
        private PlayerWallet playerPurse;
        private DroppedItems dropped;
        private Transform root;
        private int version = -1;
        private string notice;
        private float noticeUntil;
        private float nextLook;
        private GUIStyle noticeStyle;

        public MapCarts Carts => carts;

        /// <summary>Every thieves' camp of the map.</summary>
        public IReadOnlyCollection<ThievesCamp> Camps
        {
            get
            {
                Refresh();
                return camps.Values;
            }
        }

        /// <summary>What the screen last said of the thieves.</summary>
        public string LastNotice => notice;

        /// <summary>The camp a settlement of the plan is; null for an honest one.</summary>
        public ThievesCamp CampOf(int settlement)
        {
            Refresh();
            return camps.TryGetValue(settlement, out ThievesCamp camp) ? camp : null;
        }

        /// <summary>
        /// Where a camp's band is by its duty of the hour, when that is one far place (the ambush by day, the town by
        /// night); false while it is about the camp.
        /// </summary>
        public bool TryPost(int settlement, out Vector2 post)
        {
            post = default;
            ThievesCamp camp = CampOf(settlement);
            if (camp == null || people == null)
            {
                return false;
            }
            Occupation band = Occupation.Find(people.Occupations, camp.Settlement.Type.Residents);
            return band != null && DutyWorks.Make(people.DutyNow(band).Work) is ThiefWork work && work.TryPostOf(camp, out post);
        }

        /// <summary>Where a camp's stash stands: before its first hut, to one side of the door.</summary>
        public Vector2 StashSpot(SettlementSite site)
        {
            return site.Buildings.Count > 0 ? site.Buildings[0].ToWorld(new Vector2(-1.9f, TownLayout.ShopDepth * 0.5f + 1.3f)) : site.Settlement.Position;
        }

        /// <summary>
        /// True, with where the player stands (the world's own metres), when a thief with empty hands may go for them:
        /// within <paramref name="notice"/> metres, carrying something worth taking, and the band not lying low.
        /// </summary>
        public bool TryPlayerInReach(Villager thief, ThievesCamp camp, float notice, out Vector3 player)
        {
            player = default;
            if (notice <= 0f || Time.time < camp.LeavesPlayerUntil || !FindPlayer())
            {
                return false;
            }
            player = people.ToLocal(playerBag.transform.position);
            if ((player - thief.At).sqrMagnitude > notice * notice)
            {
                return false;
            }
            Inventory bag = playerBag.Inventory;
            for (int slot = FirstStealable; slot < bag.SlotCount; slot++)
            {
                if (!bag[slot].IsEmpty && bag[slot].Count > 0)
                {
                    return true;
                }
            }
            return playerPurse != null && playerPurse.Wallet != null && playerPurse.Wallet.Coins >= 1f;
        }

        // The tool in hand is never taken: the player keeps the means to get the rest back.
        private const int FirstStealable = PlayerInventory.ToolSlot + 1;

        /// <summary>A thief within <paramref name="reach"/> of the player takes from bag and purse, as his duty's row says.</summary>
        public void RobPlayer(Villager thief, ThievesCamp camp, float reach)
        {
            if (!FindPlayer() || (people.ToLocal(playerBag.transform.position) - thief.At).magnitude > reach)
            {
                return;
            }
            taken.Clear();
            Robbery.Take(playerBag.Inventory, thief.Sack, thief.Duty.Stacks, thief.Random, taken, FirstStealable);
            float coins = playerPurse != null && playerPurse.Wallet != null ? Robbery.TakeCoins(playerPurse.Wallet, thief.Purse, thief.Duty.Share, 500f) : 0f;
            if (taken.Count == 0 && coins <= 0f)
            {
                return;
            }
            camp.PlayerRobbed++;
            camp.LeavesPlayerUntil = Time.time + robberyRest;
            var what = new List<string>();
            if (coins > 0f)
            {
                what.Add($"{Wallet.Format(coins)} coins");
            }
            foreach (ItemStack stack in taken)
            {
                what.Add($"{stack.Item.DisplayName} x{stack.Count}");
            }
            Say("A thief robbed you: " + string.Join(", ", what) + ". Strike him and he drops it.");
        }

        /// <summary>A thief at his camp's stash puts down everything he carries.</summary>
        public void StashLoot(Villager thief, ThievesCamp camp)
        {
            Robbery.TakeAll(thief.Sack, camp.Stash);
            // What the stash has no room for is lost on the way.
            StorageText.Fill(thief.Sack, "");
            float coins = thief.Purse.Coins;
            if (coins > 0f && thief.Purse.Spend(coins))
            {
                camp.Hoard.Earn(coins);
            }
        }

        /// <summary>Everything a thief carries falls where he stands: each stack a bag (GL611), the coins a pouch.</summary>
        public void Spill(Villager thief)
        {
            if (!thief.IsLoaded)
            {
                return;
            }
            Vector3 feet = thief.transform.position;
            if (dropped == null)
            {
                dropped = FindAnyObjectByType<DroppedItems>();
            }
            int bags = 0;
            for (int slot = 0; dropped != null && slot < thief.Sack.SlotCount; slot++)
            {
                ItemStack stack = thief.Sack.TakeAll(slot);
                if (!stack.IsEmpty)
                {
                    float turn = bags++ * 1.9f;
                    dropped.Spawn(stack, true, feet + new Vector3(Mathf.Cos(turn) * 0.6f, 0.9f, Mathf.Sin(turn) * 0.6f), Quaternion.Euler(0f, turn * 57f, 0f),
                        new Vector3(Mathf.Cos(turn), 1.5f, Mathf.Sin(turn)));
                }
            }
            float coins = thief.Purse.Coins;
            if (coins > 0f && thief.Purse.Spend(coins))
            {
                var pouch = new GameObject("Dropped coin pouch");
                pouch.transform.SetParent(root, true);
                pouch.transform.position = feet + thief.transform.forward * 0.7f;
                pouch.AddComponent<CoinPouch>().Hold(new Wallet(coins), material, goesWhenEmpty: true);
            }
            Say("The thief drops what he carried.");
        }

        /// <summary>A camp whose thief was struck lies low a while.</summary>
        public void Routed(int settlement)
        {
            ThievesCamp camp = CampOf(settlement);
            if (camp != null)
            {
                camp.LeavesPlayerUntil = Mathf.Max(camp.LeavesPlayerUntil, Time.time + routedRest);
            }
        }

        /// <summary>
        /// The cart on a camp's ambush road within <paramref name="sight"/> metres of the ambush, in the world and with
        /// something in its crates; null with none.
        /// </summary>
        public RoadCart LoadedCartNear(ThievesCamp camp, float sight)
        {
            for (int i = 0; carts != null && i < carts.Runs.Count; i++)
            {
                RoadCart cart = carts.Runs[i].Road == camp.AmbushRoad ? carts.CartOf(i) : null;
                if (cart == null)
                {
                    continue;
                }
                Vector3 at = people.ToLocal(cart.transform.position);
                if ((new Vector2(at.x, at.z) - camp.Ambush).sqrMagnitude > sight * sight)
                {
                    continue;
                }
                foreach (StorageChest crate in cart.Crates)
                {
                    if (crate != null && !Robbery.IsEmpty(crate.Storage))
                    {
                        return cart;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// A shop of a settlement that stands in the world, for a thief to go at (each of a band its own, in turn): where
        /// to stand before it, its door, and its keeper. False while the settlement is not built or has no keeper.
        /// </summary>
        public bool TryShopOf(PlannedSettlement mark, int turn, out Vector2 front, out Vector2 door, out SettlementKeeper keeper)
        {
            front = door = default;
            keeper = null;
            SettlementSite site = settlements != null ? settlements.BuiltSite(mark.Index) : null;
            if (site == null)
            {
                return false;
            }
            var shops = new List<SettlementBuilding>();
            foreach (SettlementBuilding building in site.Buildings)
            {
                if (building.Kind == SettlementBuildingKind.Shop)
                {
                    shops.Add(building);
                }
            }
            if (shops.Count == 0)
            {
                return false;
            }
            SettlementBuilding shop = shops[turn % shops.Count];
            door = shop.ToWorld(new Vector2(0f, TownLayout.ShopDepth * 0.5f));
            front = shop.ToWorld(new Vector2(0.9f * (turn % 3 - 1), TownLayout.ShopDepth * 0.5f + 1.5f));
            float nearest = 6f * 6f;
            foreach (SettlementKeeper each in settlements.Keepers)
            {
                if (each == null)
                {
                    continue;
                }
                Vector3 at = people.ToLocal(each.Position);
                float distance = (new Vector2(at.x, at.z) - shop.Centre).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    keeper = each;
                }
            }
            return keeper != null;
        }

        /// <summary>Says something of the thieves on screen when the player is near enough to the thief to know of it.</summary>
        public void Heard(Villager thief, string text)
        {
            if (FindPlayer() && (people.ToLocal(playerBag.transform.position) - thief.At).magnitude < 90f)
            {
                Say(text);
            }
        }

        private void Say(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 7f;
        }

        private bool FindPlayer()
        {
            if (playerBag == null)
            {
                playerBag = FindAnyObjectByType<PlayerInventory>();
                playerPurse = playerBag != null ? playerBag.GetComponent<PlayerWallet>() : null;
            }
            return playerBag != null && playerBag.isActiveAndEnabled && playerBag.Inventory != null;
        }

        // Reads the camps off the plan, once per map.
        private void Refresh()
        {
            if (session == null || !session.IsReady || session.Plan == null || session.Version == version)
            {
                return;
            }
            version = session.Version;
            camps.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Thieves").transform;
            root.SetParent(session.WorldView.transform, false);
            List<Duty> duties = people != null ? people.Duties : Duty.Defaults();
            float roadRange = Duty.Find(duties, "waylay")?.Range ?? 900f, townRange = Duty.Find(duties, "burgle")?.Range ?? 1400f;
            foreach (PlannedSettlement settlement in session.Plan.Settlements)
            {
                if (!settlement.Type.Hostile)
                {
                    continue;
                }
                var camp = new ThievesCamp { Settlement = settlement, Mark = ThiefHaunts.Mark(session.Plan, settlement, townRange) };
                camp.HasAmbush = ThiefHaunts.TryAmbush(session.Plan, settlement, roadRange, out camp.Ambush, out camp.AmbushRoad);
                camps[settlement.Index] = camp;
            }
        }

        private void Update()
        {
            Refresh();
            Camera view = Camera.main;
            if (camps.Count == 0 || settlements == null || view == null || Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 1f;
            // A camp that stands in the world has its stash by the first hut.
            foreach (ThievesCamp camp in camps.Values)
            {
                SettlementSite site = settlements.BuiltSite(camp.Settlement.Index);
                if (site == null && camp.Chest != null)
                {
                    Destroy(camp.Chest.gameObject);
                    Destroy(camp.Pouch.gameObject);
                    camp.Chest = null;
                    camp.Pouch = null;
                }
                else if (site != null && camp.Chest == null && cratePrefab != null)
                {
                    PutStash(camp, site);
                }
            }
        }

        private void PutStash(ThievesCamp camp, SettlementSite site)
        {
            Vector2 spot = StashSpot(site);
            float yaw = site.Buildings.Count > 0 ? site.Buildings[0].Yaw : 0f;
            var at = new Vector3(spot.x, site.HeightAt(spot.x, spot.y), spot.y);
            GameObject crate = Instantiate(cratePrefab, root);
            crate.name = $"{camp.Settlement.Name}: stash";
            crate.transform.localPosition = at;
            crate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (crate.TryGetComponent(out PlacedObject placed))
            {
                placed.Fixed = true;
                placed.Item = null;
            }
            camp.Chest = crate.GetComponent<StorageChest>();
            if (camp.Chest == null)
            {
                camp.Chest = crate.AddComponent<StorageChest>();
            }
            camp.Chest.Hold("Thieves' stash", camp.Stash);
            var pouch = new GameObject($"{camp.Settlement.Name}: hoard");
            pouch.transform.SetParent(root, false);
            Vector2 beside = spot + TownLayout.Turn(new Vector2(-0.9f, 0f), yaw);
            pouch.transform.localPosition = new Vector3(beside.x, site.HeightAt(beside.x, beside.y), beside.y);
            camp.Pouch = pouch.AddComponent<CoinPouch>();
            camp.Pouch.Hold(camp.Hoard, material, goesWhenEmpty: false);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(notice) || Time.unscaledTime > noticeUntil)
            {
                return;
            }
            noticeStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(16), fontStyle = FontStyle.Bold, wordWrap = true };
            GameUi.OutlinedLabel(new Rect(Screen.width * 0.2f, Screen.height * 0.16f, Screen.width * 0.6f, 60f), notice, noticeStyle);
        }
    }
}
