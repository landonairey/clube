using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The opening tutorial (GL479), run when the new-game page chose it (<see cref="GameSession.Tutorial"/>): the player
    /// starts a day's work at the Mining Corp's quarry (<see cref="CompanyQuarry"/>) by the trading post. Steps teach looking,
    /// walking, jumping, sprinting, crouching, the key sheet, talking (Interact) to the crew lead (<see cref="CrewLead"/>),
    /// the hotbar, digging, the inventory and handing stone in at the ore cart (<see cref="OreCart"/>), which pays
    /// <see cref="CompanyWork.Wage"/> of its price. Then the lead heads out, and the first theft
    /// (<see cref="CompanyWork.IsStealing"/>) gets the player caught: a fade to black, everything but their coins taken,
    /// and the player set down on their starting parcel to play on as a normal new game. Played without a session, or
    /// with the tutorial skipped, it does nothing.
    /// </summary>
    public class MiningCorpTutorial : MonoBehaviour
    {
        private const string BindingGroup = "Keyboard&Mouse";

        private enum Step
        {
            Off,
            Arriving,
            Look,
            Move,
            Jump,
            Sprint,
            Crouch,
            KeySheet,
            Talk,
            Hotbar,
            Dig,
            Inventory,
            HandIn,
            Free,
            Caught,
        }

        [SerializeField]
        private WorldView worldView;

        [Tooltip("The trading post: the quarry is dug out of its gate. Without one it's dug near the spawn.")]
        [SerializeField]
        private TownSite town;

        [Tooltip("The player, with its spawn, inventory, wallet, tools, inventory screen, key sheet and quest box.")]
        [SerializeField]
        private PlayerController player;

        [Tooltip("The player's input actions, for the keys named in the steps.")]
        [SerializeField]
        private InputActionAsset actions;

        [Tooltip("What the company's stone is worth; the worker gets a tenth (GL479).")]
        [SerializeField]
        private PriceList prices;

        [Tooltip("The company pickaxe the crew lead hands out.")]
        [SerializeField]
        private ItemDefinition pick;

        [Tooltip("The Clube/Low Poly material for the crew lead and the ore cart.")]
        [SerializeField]
        private Material material;

        [Tooltip("Seconds to wait for the town before digging the quarry near the spawn instead.")]
        [SerializeField, Min(1f)]
        private float townWaitSeconds = 60f;

        [Tooltip("Volume of stone to dig before the inventory step.")]
        [SerializeField, Min(0.1f)]
        private float digTarget = 4f;

        private PlayerSpawn spawn;
        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private PlayerToolUser tools;
        private InventoryScreen inventoryScreen;
        private QuestHud questHud;

        private Step step;
        private float startedAt;
        private Vector3 home;
        private CompanyQuarry quarry;
        private CrewLead lead;
        private OreCart cart;
        private readonly HashSet<ItemDefinition> companyGoods = new HashSet<ItemDefinition>();
        private float dug;
        private bool pickGiven;
        private float looked;
        private float walked;
        private float sprinted;
        private Vector3 lastPosition;
        private Quaternion lastLook;
        private string speech = "";
        private string note = "";
        private float caughtAt;
        private bool movedHome;
        private float fade = 1f;
        private GUIStyle cardStyle;
        private GUIStyle storyStyle;

        /// <summary>True while the tutorial runs (until the player wakes on their parcel).</summary>
        public bool IsRunning => step != Step.Off;

        public string TalkPrompt => step == Step.Talk ? "Talk to the crew lead" : "Hand in the company's stone";

        public string HandInPrompt => "Hand in the company's stone";

        private void Start()
        {
            if (!GameSession.IsActive || !GameSession.Tutorial || player == null || worldView == null)
            {
                return;
            }
            spawn = player.GetComponentInChildren<PlayerSpawn>();
            inventory = player.GetComponentInChildren<PlayerInventory>();
            wallet = player.GetComponentInChildren<PlayerWallet>();
            tools = player.GetComponentInChildren<PlayerToolUser>();
            inventoryScreen = player.GetComponentInChildren<InventoryScreen>();
            questHud = player.GetComponentInChildren<QuestHud>(true);
            if (questHud != null)
            {
                questHud.enabled = false;
            }
            if (tools != null)
            {
                tools.Collected += OnCollected;
            }
            home = player.transform.position;
            startedAt = Time.time;
            step = Step.Arriving;
            speech = "Mining Corp, Quarry Three. Your first day.";
        }

        private void OnDestroy()
        {
            if (tools != null)
            {
                tools.Collected -= OnCollected;
            }
        }

        private void Update()
        {
            if (step == Step.Off)
            {
                return;
            }
            fade = Mathf.MoveTowards(fade, TargetFade(), Time.deltaTime / 1.2f);
            if (step == Step.Arriving)
            {
                Arrive();
                return;
            }
            if (step == Step.Caught)
            {
                Thrown();
                return;
            }
            if (lead != null && lead.gameObject.activeSelf)
            {
                lead.Face(player.transform.position);
            }
            if (pickGiven && IsStealing())
            {
                Catch();
                return;
            }
            Teach();
            lastPosition = player.transform.position;
            lastLook = LookRotation;
        }

        private float TargetFade()
        {
            if (step == Step.Arriving)
            {
                return 1f;
            }
            if (step == Step.Caught)
            {
                return movedHome && spawn.IsPlaced && Time.time - caughtAt > 7f ? 0f : 1f;
            }
            return 0f;
        }

        private Quaternion LookRotation => player.Head != null ? player.Head.rotation : player.transform.rotation;

        // Waits for the town, digs the quarry, sets the player down on its rim and puts the crew lead and the cart there.
        private void Arrive()
        {
            if (!worldView.IsReady || !player.gameObject.activeInHierarchy)
            {
                return;
            }
            if (quarry == null)
            {
                bool hasPlan = town != null && town.Plan != null;
                if (!hasPlan && town != null && Time.time - startedAt < townWaitSeconds)
                {
                    return;
                }
                Vector3 local = worldView.transform.InverseTransformPoint(home);
                Vector2 centre = new Vector2(local.x + 24f, local.z);
                if (hasPlan)
                {
                    TownPlan plan = town.Plan;
                    Vector2 gate = plan.ToWorld(new Vector2(0f, -TownLayout.OuterHalf));
                    centre = CompanyWork.QuarryCentre(gate, gate - plan.ToWorld(Vector2.zero));
                }
                quarry = new CompanyQuarry(worldView, centre);
                spawn.SetDownOver(quarry.OnRim(TownAngle, 2.5f) + Vector3.up);
            }
            if (!quarry.Dig() || !spawn.IsPlaced)
            {
                return;
            }
            Vector3 middle = worldView.transform.TransformPoint(new Vector3(quarry.Centre.x, quarry.Floor, quarry.Centre.y));
            lead = CrewLead.Build(quarry.OnRim(TownAngle + 28f), middle, material, this);
            cart = CompanyQuarry.BuildCart(quarry.OnRim(TownAngle - 35f, 1.8f), middle - quarry.OnRim(TownAngle - 35f, 1.8f), material, this);
            lastPosition = player.transform.position;
            lastLook = LookRotation;
            speech = "Crew lead: \"Morning, new hire. Get your bearings, then come and see me.\"";
            step = Step.Look;
        }

        // Degrees round the quarry from its centre towards the town (or the spawn).
        private float TownAngle
        {
            get
            {
                Vector3 local = worldView.transform.InverseTransformPoint(home);
                Vector2 towards = town != null && town.Plan != null ? town.Plan.ToWorld(Vector2.zero) : new Vector2(local.x, local.z);
                Vector2 d = towards - quarry.Centre;
                return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
        }

        private void Teach()
        {
            switch (step)
            {
                case Step.Look:
                    looked += Quaternion.Angle(lastLook, LookRotation);
                    Advance(looked > 120f);
                    break;
                case Step.Move:
                    if (player.IsGrounded)
                    {
                        walked += Vector3.ProjectOnPlane(player.transform.position - lastPosition, Vector3.up).magnitude;
                    }
                    Advance(walked > 4f);
                    break;
                case Step.Jump:
                    Advance(Pressed("Jump"));
                    break;
                case Step.Sprint:
                    if (Held("Sprint") && player.HorizontalVelocity.magnitude > 1f)
                    {
                        sprinted += Time.deltaTime;
                    }
                    Advance(sprinted > 1f);
                    break;
                case Step.Crouch:
                    Advance(player.IsCrouching);
                    break;
                case Step.KeySheet:
                    Advance(Pressed("Help"));
                    break;
                case Step.Hotbar:
                    Advance(tools != null && tools.Current == pick);
                    break;
                case Step.Dig:
                    Advance(dug >= digTarget);
                    break;
                case Step.Inventory:
                    Advance(inventoryScreen == null || inventoryScreen.IsOpen);
                    break;
            }
        }

        private void Advance(bool done)
        {
            if (done)
            {
                step++;
                note = "";
            }
        }

        /// <summary>The player talked to the crew lead: the pickaxe at the talk step, a hand-in after it.</summary>
        public void TalkToLead()
        {
            if (step == Step.Talk)
            {
                if (pick != null)
                {
                    inventory.Inventory.Add(pick);
                    companyGoods.Add(pick);
                }
                pickGiven = true;
                speech = "Crew lead: \"Here's a company pick. You dig, the company keeps every stone: ten coins for every hundred you bring in.\"";
                step = Step.Hotbar;
                return;
            }
            if (step > Step.Talk && step < Step.Caught)
            {
                HandIn();
            }
        }

        /// <summary>Takes the company's stone from the player and pays the wage for it.</summary>
        public void HandIn()
        {
            if (step <= Step.Talk || step >= Step.Caught)
            {
                return;
            }
            Inventory items = inventory.Inventory;
            float price = 0f;
            float volume = 0f;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (stack.IsEmpty || stack.Item == pick || !companyGoods.Contains(stack.Item))
                {
                    continue;
                }
                price += prices != null ? prices.PriceOf(stack, stack.Amount) : 0f;
                volume += stack.Volume;
                items.Set(slot, ItemStack.Empty);
            }
            if (volume <= 0f)
            {
                note = "Nothing to hand in yet: dig some stone first.";
                return;
            }
            float wage = CompanyWork.Wage(price);
            wallet?.Wallet?.Earn(wage);
            note = $"Handed in stone worth {Wallet.Format(price)}. The company pays you {Wallet.Format(wage)}.";
            if (step < Step.Free)
            {
                step = Step.Free;
                speech = "Crew lead (heading out): \"That's the way. Mine all you want and hand it in at the cart. And don't touch anything.\"";
                if (lead != null && town != null && town.Plan != null)
                {
                    Vector2 gate = town.Plan.ToWorld(new Vector2(0f, -TownLayout.OuterHalf));
                    lead.Leave(worldView.transform.TransformPoint(new Vector3(gate.x, town.Plan.GateGround, gate.y)));
                }
                else if (lead != null)
                {
                    lead.Leave(lead.transform.position + lead.transform.forward * -40f);
                }
            }
        }

        private void OnCollected(ItemStack collected)
        {
            if (!pickGiven || step >= Step.Caught || collected.IsEmpty || quarry == null)
            {
                return;
            }
            // Dug in the quarry: the company's. Dug anywhere else, it's the player's own theft (caught by IsStealing).
            if (quarry.DistanceTo(player.transform.position) <= quarry.RimRadius + CompanyWork.Margin)
            {
                companyGoods.Add(collected.Item);
                dug += collected.Volume;
            }
        }

        private bool IsStealing()
        {
            bool carries = false;
            bool other = false;
            Inventory items = inventory.Inventory;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemDefinition item = items[slot].Item;
                if (item == null)
                {
                    continue;
                }
                if (companyGoods.Contains(item))
                {
                    carries = true;
                }
                else
                {
                    other = true;
                }
            }
            return CompanyWork.IsStealing(quarry.DistanceTo(player.transform.position), quarry.RimRadius, carries, other);
        }

        private void Catch()
        {
            step = Step.Caught;
            caughtAt = Time.time;
            note = "";
            speech = lead != null && lead.gameObject.activeSelf && !lead.IsLeaving
                ? "Crew lead: \"Thief! I said don't touch anything!\""
                : "A company guard: \"Hey! That's Mining Corp property!\"";
            player.IsInMenu = true;
        }

        // Caught: once the screen is black, everything but the coins is taken and the player is set down on their parcel.
        private void Thrown()
        {
            if (!movedHome && fade >= 1f)
            {
                Inventory items = inventory.Inventory;
                for (int slot = 0; slot < items.SlotCount; slot++)
                {
                    items.Set(slot, ItemStack.Empty);
                }
                if (lead != null)
                {
                    Destroy(lead.gameObject);
                }
                if (cart != null)
                {
                    Destroy(cart.gameObject);
                }
                player.IsInMenu = false;
                spawn.SetDownOver(home);
                if (questHud != null)
                {
                    questHud.enabled = true;
                }
                GameSession.FinishTutorial();
                movedHome = true;
            }
            if (movedHome && fade <= 0f)
            {
                step = Step.Off;
            }
        }

        private bool Pressed(string action)
        {
            InputAction found = actions != null ? actions.FindAction(action) : null;
            return found != null && found.WasPressedThisFrame();
        }

        private bool Held(string action)
        {
            InputAction found = actions != null ? actions.FindAction(action) : null;
            return found != null && found.IsPressed();
        }

        private string Keys(string action, string fallback)
        {
            InputAction found = actions != null ? actions.FindAction(action) : null;
            string keys = found != null ? found.GetBindingDisplayString(InputBinding.MaskByGroup(BindingGroup)) : "";
            return $"<b>{(string.IsNullOrEmpty(keys) ? fallback : keys)}</b>";
        }

        private string Task()
        {
            switch (step)
            {
                case Step.Look: return "Move the mouse to look around.";
                case Step.Move: return $"Walk with {Keys("Move", "WASD")}.";
                case Step.Jump: return $"Jump with {Keys("Jump", "Space")}.";
                case Step.Sprint: return $"Hold {Keys("Sprint", "Shift")} while walking to run.";
                case Step.Crouch: return $"Crouch with {Keys("Crouch", "C")}.";
                case Step.KeySheet: return $"Every key is listed in the lower left. {Keys("Help", "F1")} hides and shows the list: try it.";
                case Step.Talk: return $"Walk up to the crew lead and press {Keys("Interact", "E")} to talk.";
                case Step.Hotbar: return $"Hold the company pick: pick its slot with {Keys("Hotbar", "1")} (or scroll).";
                case Step.Dig: return $"Aim at the rock in the pit and dig with {Keys("Attack", "LMB")}.";
                case Step.Inventory: return $"Open your inventory with {Keys("Inventory", "Tab")} to see what you've dug.";
                case Step.HandIn: return $"Close it, then hand the stone in at the ore cart or to the crew lead ({Keys("Interact", "E")}).";
                case Step.Free: return "Mine all you want and hand it in at the ore cart: 10c for every 100c of stone.";
                default: return "";
            }
        }

        private void OnGUI()
        {
            if (step == Step.Off)
            {
                return;
            }
            GameUi.Apply();
            GUI.depth = -100;
            if (cardStyle == null)
            {
                cardStyle = new GUIStyle(GUI.skin.box) { richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = GameUi.Font(17), padding = new RectOffset(16, 16, 12, 12) };
                storyStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(22) };
            }
            if (step != Step.Arriving && fade < 1f)
            {
                string text = $"<i>{speech}</i>\n\n{Task()}";
                if (note.Length > 0)
                {
                    text += $"\n<size={GameUi.Font(14)}>{note}</size>";
                }
                float width = Mathf.Min(Screen.width - 32f, 680f * GameUi.FontScale);
                var content = new GUIContent(text);
                float height = cardStyle.CalcHeight(content, width);
                GUI.Box(new Rect((Screen.width - width) * 0.5f, 18f, width, height), content, cardStyle);
            }
            if (fade > 0f)
            {
                Color before = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, fade);
                var middle = new Rect(Screen.width * 0.15f, Screen.height * 0.3f, Screen.width * 0.7f, Screen.height * 0.4f);
                GUI.Label(middle, StoryText(), storyStyle);
                GUI.color = before;
            }
        }

        private string StoryText()
        {
            if (step == Step.Arriving)
            {
                return "<b>Mining Corp</b>\n\nYou took the job: dig stone at the company quarry by the trading post.\nWhatever you dig belongs to the company.";
            }
            if (step == Step.Caught)
            {
                return movedHome
                    ? "Caught stealing from the Mining Corp, you are marched out of town\nand left on the only land your family still owns.\n\nTime to start something of your own."
                    : speech;
            }
            return "";
        }
    }
}
