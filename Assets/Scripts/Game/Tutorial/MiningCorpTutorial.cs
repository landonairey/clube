using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The opening tutorial (GL479, GL514), on its own map (the Tutorial scene): the Mining Corp's warehouse
    /// (<see cref="CompanyWarehouse"/>) across the end of its work fields, which a stone mountain closes in on three sides.
    /// The player wakes behind the warehouse talking to the crew lead (<see cref="CrewLead"/>), who asks their name
    /// (<see cref="GameSession.CharacterName"/>). Steps teach looking, walking, jumping, sprinting, crouching, the key
    /// sheet, talking (a <see cref="QuestArrow"/> over the lead and a mark on the <see cref="Minimap"/>), the hotbar,
    /// digging the mountain with the company pick, the inventory, and handing the stone in at the ore cart
    /// (<see cref="OreCart"/>), which pays <see cref="CompanyWork.Wage"/> of its price. All the land is the company's, so
    /// any of it can be dug. Taking from a crate (<see cref="CompanyCrate"/>) gets the player caught: a fade to black,
    /// everything but their coins taken, and the game goes on in the world scene, on their family's parcel.
    /// </summary>
    public class MiningCorpTutorial : MonoBehaviour, ISavedValues
    {
        private const string BindingGroup = "Keyboard&Mouse";
        private const int NameLength = 20;

        private enum Step
        {
            Off,
            Arriving,
            Name,
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

        [Tooltip("The player, with its spawn, inventory, wallet, tools, inventory screen, key sheet, quest box and minimap.")]
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

        [Tooltip("The Clube/Low Poly material for the warehouse, the crew lead and the ore cart.")]
        [SerializeField]
        private Material material;

        [Tooltip("The Clube/World Text material for the words on signs.")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("The game's wooden crate prefab (Wooden Chest) the company's crates are made from (GL553).")]
        [SerializeField]
        private GameObject cratePrefab;

        [Tooltip("What the supply crates round the yard hold, one item each.")]
        [SerializeField]
        private ItemDefinition[] supplies = new ItemDefinition[0];

        [Tooltip("The valuables in the crate at the end of the crew lead's bed.")]
        [SerializeField]
        private ItemDefinition[] valuables = new ItemDefinition[0];

        [Tooltip("Volume of stone to dig before the inventory step.")]
        [SerializeField, Min(0.1f)]
        private float digTarget = 2f;

        [Tooltip("Seconds between being caught and the screen going black.")]
        [SerializeField, Min(0f)]
        private float caughtSeconds = 2.5f;

        [Tooltip("Seconds the story card shows on black before the world scene loads.")]
        [SerializeField, Min(0f)]
        private float storySeconds = 6f;

        private PlayerSpawn spawn;
        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private PlayerToolUser tools;
        private InventoryScreen inventoryScreen;
        private QuestHud questHud;
        private Minimap minimap;

        private Step step;
        private CrewLead lead;
        private QuestArrow arrow;
        private OreCart cart;
        private bool placing;
        private float dug;
        private float looked;
        private float walked;
        private float sprinted;
        private Vector3 lastPosition;
        private Quaternion lastLook;
        private string typedName = "";
        private CompanyWarehouse warehouse;
        private float regroundAt;
        private string speech = "";
        private string note = "";
        private float caughtAt;
        private float blackAt = -1f;
        private float fade = 1f;
        private GUIStyle cardStyle;
        private GUIStyle storyStyle;
        private DayNightCycle dayNight;
        private ToolShed shed;
        private readonly System.Collections.Generic.List<string> foundOres = new System.Collections.Generic.List<string>();
        private bool talking;
        private bool oreDiscussed;
        private readonly DailyQuota quota = new DailyQuota();
        private readonly System.Collections.Generic.HashSet<string> discussedOres = new System.Collections.Generic.HashSet<string>();
        private FieldLogScreen fieldLog;
        private string outcome = "";
        private GUIStyle quotaStyle;

        /// <summary>True while the tutorial runs.</summary>
        public bool IsRunning => step != Step.Off;

        /// <summary>The step the player is on, by name (for tests and lab checks).</summary>
        public string Stage => step.ToString();

        public string TalkPrompt => step <= Step.Talk ? "Talk to the crew lead" : HandInPrompt;

        public string HandInPrompt => "Hand in the company's stone";

        private string Name => GameSession.CharacterName.Length > 0 ? GameSession.CharacterName : "new hire";

        private void Start()
        {
            // Its own scene: it runs for a new game that chose it, or when the scene is played on its own.
            if ((GameSession.IsActive && !GameSession.Tutorial) || player == null || worldView == null)
            {
                return;
            }
            spawn = player.GetComponentInChildren<PlayerSpawn>();
            inventory = player.GetComponentInChildren<PlayerInventory>();
            wallet = player.GetComponentInChildren<PlayerWallet>();
            tools = player.GetComponentInChildren<PlayerToolUser>();
            // GL527, GL529: a new hire sees no skill levels and earns no xp, and can't climb.
            PlayerSkills playerSkills = player.GetComponentInChildren<PlayerSkills>();
            if (playerSkills != null)
            {
                playerSkills.Skills.Suppressed = true;
            }
            SkillsHud skillsHud = player.GetComponentInChildren<SkillsHud>(true);
            if (skillsHud != null)
            {
                skillsHud.enabled = false;
            }
            player.ClimbingDisabled = true;
            inventoryScreen = player.GetComponentInChildren<InventoryScreen>();
            questHud = player.GetComponentInChildren<QuestHud>(true);
            // GL540: the journals are for the family's own parcel, after the job.
            fieldLog = player.GetComponentInChildren<FieldLogScreen>(true);
            if (fieldLog != null)
            {
                fieldLog.Blocked = true;
            }
            minimap = player.GetComponentInChildren<Minimap>();
            dayNight = FindAnyObjectByType<DayNightCycle>();
            if (questHud != null)
            {
                questHud.enabled = false;
            }
            if (tools != null)
            {
                tools.Collected += OnCollected;
                // GL555: digging only 09:00-17:00 once the job is on; GL565: the early bird's bonus is the first two work hours, every day.
                tools.Lockout = DiggingLockout;
                tools.SetEarlyBirdWindow(CompanyWork.WorkStartHour, CompanyWork.WorkStartHour + 2f);
            }
            step = Step.Arriving;
        }

        private void OnDestroy()
        {
            if (tools != null)
            {
                tools.Collected -= OnCollected;
                tools.Lockout = null;
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
            if (regroundAt > 0f && Time.time > regroundAt)
            {
                // The ground streams in over the first seconds: clear it once more so nothing grows in the building (GL520).
                regroundAt = 0f;
                CompanyWarehouse.ClearGround(worldView);
                TutorialTerrain.Apply(worldView);
            }
            if (lead != null)
            {
                lead.Face(player.transform.position);
            }
            bool talk = step == Step.Talk;
            if (arrow != null && arrow.gameObject.activeSelf != talk)
            {
                arrow.gameObject.SetActive(talk);
            }
            if (minimap != null)
            {
                minimap.Marker = talk && lead != null ? lead.transform : null;
            }
            if (inventoryScreen != null)
            {
                // GL539: no inventory while typing the name.
                inventoryScreen.Blocked = step == Step.Name;
            }
            CheckQuota();
            Teach();
            lastPosition = player.transform.position;
            lastLook = LookRotation;
        }

        // The hint while digging isn't allowed (GL555): by hand or tool, outside work hours, once the job has begun.
        private string DiggingLockout()
        {
            if (dayNight == null || step < Step.Free || step >= Step.Caught || CompanyWork.IsWorkHour(dayNight.ClockHour))
            {
                return null;
            }
            return $"Work hours are {(int)CompanyWork.WorkStartHour}:00 to {(int)CompanyWork.QuotaHour}:00: no digging now.";
        }

        private int QuotaDay => dayNight != null ? dayNight.Day : 1;

        /// <summary>The stone handed in today (GL544: the count starts again each day).</summary>
        public int StoneDelivered
        {
            get
            {
                quota.Advance(QuotaDay);
                return quota.Stone;
            }
        }

        /// <summary>Stone that must have been handed in by 17:00 today: 1024 x the day (GL534, GL544).</summary>
        public int QuotaToday => CompanyWork.Quota(QuotaDay);

        // From the first hand-in on the company wants its quota by 17:00 every day, or the player is thrown out.
        private void CheckQuota()
        {
            if (dayNight == null || step < Step.Hotbar || step >= Step.Caught || dayNight.ClockHour < CompanyWork.QuotaHour
                || StoneDelivered >= QuotaToday)
            {
                return;
            }
            outcome = $"You didn't make the quota of {QuotaToday} stone,\nand the Mining Corp has no use for you.\n\nYou are thrown out and left on the only land your family still owns.";
            ThrowOut($"Crew lead: \"{StoneDelivered} stone. The quota was {QuotaToday}, {Name}. You're done here. Out!\"");
        }

        public void SaveValues(System.Collections.Generic.IDictionary<string, string> values)
        {
            values["quotaStone"] = quota.Stone.ToString();
            values["quotaDay"] = quota.Day.ToString();
            values["oreDiscussed"] = oreDiscussed ? "1" : "0";
        }

        public void RestoreValues(System.Collections.Generic.IReadOnlyDictionary<string, string> values)
        {
            oreDiscussed = values.TryGetValue("oreDiscussed", out string said) && said == "1";
            if (values.TryGetValue("quotaStone", out string saved) && int.TryParse(saved, out int count))
            {
                values.TryGetValue("quotaDay", out string savedDay);
                quota.Restore(int.TryParse(savedDay, out int day) ? day : QuotaDay, count);
            }
        }

        private float TargetFade()
        {
            if (step == Step.Arriving)
            {
                return 1f;
            }
            if (step == Step.Caught)
            {
                return Time.time - caughtAt > caughtSeconds ? 1f : 0f;
            }
            return 0f;
        }

        private Quaternion LookRotation => player.Head != null ? player.Head.rotation : player.transform.rotation;

        private Vector3 OnMap(Vector3 point) => worldView.transform.TransformPoint(point);

        // Waits for the world, builds the warehouse, sets the player down behind it and puts the crew lead and the cart out.
        private void Arrive()
        {
            if (!worldView.IsReady || !player.gameObject.activeInHierarchy)
            {
                return;
            }
            if (!placing)
            {
                warehouse = CompanyWarehouse.Build(worldView.transform, material, this, supplies, valuables, textMaterial, cratePrefab);
                spawn.SetDownOver(OnMap(CompanyWarehouse.Spawn) + Vector3.up);
                placing = true;
            }
            if (!spawn.IsPlaced)
            {
                return;
            }
            // The ground round the player is loaded now: no grass under the warehouse floor.
            CompanyWarehouse.ClearGround(worldView);
            TutorialTerrain.Apply(worldView);
            regroundAt = Time.time + 4f;
            Vector3 leadAt = OnMap(CompanyWarehouse.LeadSpot);
            lead = CrewLead.Build(leadAt, player.transform.position, material, this);
            arrow = QuestArrow.Build(lead.transform, 2.6f, material);
            Vector3 cartAt = OnMap(CompanyWarehouse.CartSpot);
            cart = OreCart.Build(cartAt, Vector3.right, material, this);
            shed = ToolShed.Build(OnMap(CompanyWarehouse.ShedSpot), Vector3.right, material, this, textMaterial);
            WorkerCrew.Build(worldView.transform, material, worldView, tools, pick as ToolDefinition);
            player.LookTowards(leadAt);
            // A new hire owns nothing: whatever a new game hands out waits for the player's own land.
            EmptyInventory();
            lastPosition = player.transform.position;
            lastLook = LookRotation;
            speech = "Crew lead: \"So you're the new hire. Welcome to Mining Corp. What's your name?\"";
            player.IsInMenu = true;
            player.InputLocked = true;
            step = Step.Name;
        }

        /// <summary>The player answered the crew lead with their character's name (typed in the card at the name step).</summary>
        public void GiveName(string typed)
        {
            if (step != Step.Name)
            {
                return;
            }
            string name = (typed ?? "").Trim();
            GameSession.CharacterName = name.Length > NameLength ? name.Substring(0, NameLength) : name;
            player.IsInMenu = false;
            player.InputLocked = false;
            warehouse?.NameBunk(Name);
            speech = $"Crew lead: \"{Name}, then. Your bunk's in the dorm hall, through that door; you sleep there nights. "
                + "Work's out back, at the rock face. Get your bearings, then come and see me for a pick.\"";
            step = Step.Look;
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
                }
                speech = $"Crew lead: \"Here's a company pick, {Name}. Dig the mountain out back; every stone is the company's: "
                    + "ten coins for every hundred you bring in. And don't take anything that isn't yours. "
                    + "Report back to me if you find anything interesting. A broken pick? Get a new one at the tool shed.\"";
                step = Step.Hotbar;
                return;
            }
            if (step > Step.Talk && step < Step.Caught)
            {
                // GL554: E always opens the choice: discuss ore (once), hand in, or exit.
                talking = true;
                player.IsInMenu = true;
                return;
            }
            else if (step < Step.Talk && step > Step.Name)
            {
                note = "Crew lead: \"Get your bearings first.\"";
            }
        }

        /// <summary>The tool shed (GL533): a new company pick for a player who has none, one at a time (GL522).</summary>
        public void TakePickFromShed()
        {
            if (pick == null || step <= Step.Talk || step >= Step.Caught)
            {
                note = "Tool shed: \"Ask the crew lead for your pick first.\"";
                return;
            }
            Inventory items = inventory.Inventory;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (!stack.IsEmpty && stack.Item is ToolDefinition && stack.Item.DisplayName == pick.DisplayName)
                {
                    note = "Tool shed: \"You've got a pick already. Come back when it breaks.\"";
                    return;
                }
            }
            if (inventory.Inventory.Add(pick) > 0)
            {
                note = "Tool shed: \"Pick's full. Make room.\"";
                return;
            }
            note = "Tool shed: \"Here's a new company pick. Mind it.\"";
        }

        /// <summary>The crew lead's one answer to anything the player found (GL528).</summary>
        private const string NotInterested = "Crew lead: \"We don't care about any of that, we only do Stone here. Now get back to work.\"";

        /// <summary>What the lead adds while the player is off mining (GL541).</summary>
        private const string OutThere = "Let me know if you find anything interesting too while you're out there.";

        private void CloseTalk()
        {
            talking = false;
            player.IsInMenu = false;
        }

        // Talking to the lead once ore has been found: hand in, or discuss what was found (he never cares).
        private void DrawTalk()
        {
            Event e = Event.current;
            bool closeKey = e.type == EventType.KeyDown && (e.keyCode == KeyCode.E || e.keyCode == KeyCode.Escape);
            float width = Mathf.Min(Screen.width - 32f, 420f * GameUi.FontScale);
            float row = GameUi.Font(17) + 14f;
            float height = 70f + row * 3;
            var panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height);
            GUI.Box(panel, "", cardStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, width - 28f, 28f), "The crew lead");
            float y = panel.y + 44f;
            if (GUI.Button(new Rect(panel.x + 14f, y, width - 28f, row - 4f), "Hand in the stone"))
            {
                CloseTalk();
                HandIn();
                return;
            }
            y += row;
            GUI.enabled = !oreDiscussed;
            if (GUI.Button(new Rect(panel.x + 14f, y, width - 28f, row - 4f), "Discuss ore"))
            {
                // GL542, GL554: he never cares, and the topic is closed for good (greyed out afterwards).
                speech = NotInterested;
                oreDiscussed = true;
                foreach (string ore in foundOres)
                {
                    discussedOres.Add(ore);
                }
                foundOres.Clear();
                CloseTalk();
                GUI.enabled = true;
                return;
            }
            GUI.enabled = true;
            y += row;
            if (GUI.Button(new Rect(panel.x + 14f, y, width - 28f, row - 4f), "Exit (E)") || closeKey)
            {
                CloseTalk();
            }
        }

        /// <summary>Takes the company's stone (everything but the pick) from the player and pays the wage for it.</summary>
        public void HandIn()
        {
            if (step <= Step.Talk || step >= Step.Caught)
            {
                return;
            }
            Inventory items = inventory.Inventory;
            float price = 0f;
            float volume = 0f;
            int stone = 0;
            bool handedOre = false;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (stack.IsEmpty || stack.Item == pick || stack.Item is ToolDefinition)
                {
                    continue;
                }
                if (stack.Contents != null)
                {
                    foreach (Content part in stack.Contents.Parts)
                    {
                        if (part.Material != null && part.Material.Category == VoxelMaterialCategory.Ore)
                        {
                            handedOre = true;
                            discussedOres.Add(part.Material.DisplayName);
                        }
                    }
                }
                price += prices != null ? prices.PriceOf(stack, stack.Amount) : 0f;
                volume += stack.Volume;
                if (stack.Item.DisplayName == "Stone")
                {
                    stone += Mathf.RoundToInt(stack.Amount);
                }
                items.Set(slot, ItemStack.Empty);
            }
            if (volume <= 0f)
            {
                note = "Nothing to hand in yet: dig some stone first.";
                return;
            }
            quota.Add(QuotaDay, stone);
            // GL543: ore handed in with the rest gets the same brush-off as ore discussed, and is settled for good (GL542).
            foundOres.RemoveAll(discussedOres.Contains);
            float wage = CompanyWork.Wage(price);
            wallet?.Wallet?.Earn(wage);
            note = $"Handed in stone worth {Wallet.Format(price)}. The company pays you {Wallet.Format(wage)}.";
            if (step < Step.Free)
            {
                step = Step.Free;
                speech = "Crew lead: \"That's the way. Mine all you want and hand it in at the cart. Sleep in your bunk when it's dark. "
                    + "And remember: don't take anything that isn't yours. " + OutThere + "\"";
            }
            else
            {
                speech = handedOre ? NotInterested : $"Crew lead: \"Good. {OutThere}\"";
            }
        }

        // Anything dug counts towards the dig step: the whole map is company land.
        private void OnCollected(ItemStack collected)
        {
            if (step > Step.Talk && step < Step.Caught && !collected.IsEmpty)
            {
                dug += collected.Volume;
                // GL528: remember the ores the player turns up, for the crew lead to wave away.
                if (collected.Contents != null)
                {
                    foreach (Content part in collected.Contents.Parts)
                    {
                        string ore = part.Material != null && part.Material.Category == VoxelMaterialCategory.Ore ? part.Material.DisplayName : null;
                        if (ore != null && !foundOres.Contains(ore) && !discussedOres.Contains(ore))
                        {
                            foundOres.Add(ore);
                            note = $"You found some {ore}. The crew lead wants to hear about interesting finds.";
                        }
                    }
                }
            }
        }

        /// <summary>The player took from a company crate (GL553: the goods are already theirs): they get caught.</summary>
        public void Steal()
        {
            if (step <= Step.Name || step >= Step.Caught)
            {
                return;
            }
            outcome = "Caught stealing from the Mining Corp, you are thrown out\nand left on the only land your family still owns.\n\nTime to start something of your own.";
            ThrowOut($"Crew lead: \"Thief! I told you not to take anything that isn't yours, {Name}! Get out!\"");
        }

        // The player is thrown out (caught, or short of the quota): black screen, the story, then the world scene.
        private void ThrowOut(string line)
        {
            step = Step.Caught;
            caughtAt = Time.time;
            note = "";
            speech = line;
            if (talking)
            {
                talking = false;
            }
            if (arrow != null)
            {
                arrow.gameObject.SetActive(false);
            }
            if (minimap != null)
            {
                minimap.Marker = null;
            }
            // GL567: the player freezes and the crew lead turns up beside them (front right); Thrown() swings the view to him.
            ChestPanel chestPanel = player.GetComponent<ChestPanel>();
            if (chestPanel != null && chestPanel.IsOpen)
            {
                chestPanel.Close();
            }
            player.InputLocked = true;
            if (lead != null)
            {
                Vector3 here = player.transform.position;
                Vector3 beside = (player.transform.forward + player.transform.right).normalized;
                lead.Appear(here + beside * 1.8f, here);
            }
            player.IsInMenu = true;
        }

        // Caught: once the screen is black, everything but the coins is taken, the story shows, and the world scene loads.
        private void Thrown()
        {
            if (lead != null && lead.gameObject.activeSelf)
            {
                // About half a second for a half turn (GL567).
                player.TurnTowards(lead.transform.position + Vector3.up * 1.6f, 360f);
                lead.Face(player.transform.position);
            }
            if (fade < 1f)
            {
                return;
            }
            if (blackAt < 0f)
            {
                blackAt = Time.time;
                EmptyInventory();
            }
            if (Time.time - blackAt < storySeconds)
            {
                return;
            }
            step = Step.Off;
            string name = GameSession.CharacterName;
            float coins = wallet != null && wallet.Wallet != null ? wallet.Wallet.Coins : 0f;
            if (!GameSession.IsActive)
            {
                GameSession.StartNew(GameMode.World, 1);
            }
            GameSession.FinishTutorial();
            GameSession.CharacterName = name;
            GameSession.CarriedCoins = coins;
            GameScenes.LoadGame();
        }

        private void EmptyInventory()
        {
            Inventory items = inventory.Inventory;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                items.Set(slot, ItemStack.Empty);
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
                case Step.Name: return "Type your character's name and press <b>Enter</b>.";
                case Step.Look: return "Move the mouse to look around.";
                case Step.Move: return $"Walk with {Keys("Move", "WASD")}.";
                case Step.Jump: return $"Jump with {Keys("Jump", "Space")}.";
                case Step.Sprint: return $"Hold {Keys("Sprint", "Shift")} while walking to run.";
                case Step.Crouch: return $"Crouch with {Keys("Crouch", "C")}.";
                case Step.KeySheet: return $"Every key is listed in the lower left. {Keys("Help", "F1")} hides and shows the list: try it.";
                case Step.Talk: return $"Walk up to the crew lead (the yellow arrow, and the square on the map) and press {Keys("Interact", "E")} to talk.";
                case Step.Hotbar: return $"Hold the company pick: pick its slot with {Keys("Hotbar", "1")} (or scroll).";
                case Step.Dig: return $"Aim at the rock face behind the yard and dig with {Keys("Attack", "LMB")}.";
                case Step.Inventory: return $"Open your inventory with {Keys("Interact", "E")} (facing nothing you can use) to see what you've dug.";
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
                DrawCard();
                DrawQuota();
                if (talking)
                {
                    DrawTalk();
                }
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

        // The quota box at the top right (GL534): stone handed in against today's quota, and the hour it's due.
        private void DrawQuota()
        {
            if (step < Step.Hotbar || step >= Step.Caught)
            {
                return;
            }
            if (quotaStyle == null)
            {
                quotaStyle = new GUIStyle(cardStyle) { alignment = TextAnchor.MiddleLeft, fontSize = GameUi.Font(16) };
            }
            string due = $"{(int)CompanyWork.QuotaHour}:00";
            string text = $"<b>Quota, day {QuotaDay}</b>\nStone handed in today: {StoneDelivered} / {QuotaToday}\nDue by {due}";
            float width = 230f * GameUi.FontScale;
            float height = quotaStyle.CalcHeight(new GUIContent(text), width);
            GUI.Box(new Rect(Screen.width - width - 14f, 14f, width, height), text, quotaStyle);
        }

        // The card at the top: what's said, the task, a note, and the name box at the name step.
        private void DrawCard()
        {
            string text = $"<i>{speech}</i>\n\n{Task()}";
            if (note.Length > 0)
            {
                text += $"\n<size={GameUi.Font(14)}>{note}</size>";
            }
            float width = Mathf.Min(Screen.width - 32f, 680f * GameUi.FontScale);
            var content = new GUIContent(text);
            float height = cardStyle.CalcHeight(content, width);
            float field = step == Step.Name ? GameUi.Font(17) + 22f : 0f;
            var card = new Rect((Screen.width - width) * 0.5f, 18f, width, height + field);
            GUI.Box(card, content, cardStyle);
            if (step != Step.Name)
            {
                return;
            }
            bool enter = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("CharacterName");
            var box = new Rect(card.x + 16f, card.yMax - field, width * 0.5f, field - 10f);
            typedName = GUI.TextField(box, typedName, NameLength);
            GUI.FocusControl("CharacterName");
            if (GUI.Button(new Rect(box.xMax + 8f, box.y, 80f, box.height), "OK") || (enter && typedName.Trim().Length > 0))
            {
                GiveName(typedName);
            }
        }

        private string StoryText()
        {
            if (step == Step.Arriving)
            {
                return "<b>Mining Corp</b>\n\nYou took the job: dig stone for the company, and keep a tenth of its worth.\nWhatever you dig belongs to the company.";
            }
            if (step == Step.Caught || step == Step.Off)
            {
                return blackAt >= 0f ? outcome : speech;
            }
            return "";
        }
    }
}
