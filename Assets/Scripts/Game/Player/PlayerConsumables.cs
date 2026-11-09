using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Drinks what's selected (GL456): with a <see cref="ConsumableDefinition"/> in the selected hotbar slot, Place (right
    /// click) uses one up and its effect lasts its hours of game time (<see cref="GameClock"/>). Another drunk while it
    /// lasts starts the time again; it never adds up. While it lasts <see cref="CooldownShare"/> quickens the tools
    /// (<see cref="PlayerToolUser.CooldownFor"/>), and a line over the hotbar says until when. Kept in saves.
    /// </summary>
    public class PlayerConsumables : MonoBehaviour, ISavedValues
    {
        private const string UntilKey = "consumable.until";
        private const string ItemKey = "consumable.item";

        [Tooltip("Uses the selected consumable (Place, right click).")]
        [SerializeField]
        private InputActionReference placeAction;

        private PlayerController player;
        private PlayerInventory inventory;
        private Hotbar hotbar;
        private GameClock clock;
        private DayNightCycle dayNight;
        private GUIStyle noteStyle;

        // The effect running: what was drunk, and the game day (fractions included) it ends.
        private ConsumableDefinition active;
        private double until;

        /// <summary>The tool cooldown share now: the drink's while it lasts, else 1.</summary>
        public float CooldownShare => IsActive ? active.CooldownShare : 1f;

        /// <summary>True while a drink's effect lasts.</summary>
        public bool IsActive => active != null && Days < until;

        private double Days => clock != null ? clock.Days : Time.time / 2400d;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
        }

        private void OnEnable()
        {
            placeAction?.action.Enable();
        }

        private void Update()
        {
            if (clock == null)
            {
                clock = FindFirstObjectByType<GameClock>();
                dayNight = FindFirstObjectByType<DayNightCycle>();
            }
            if (player == null || !player.enabled || player.IsInMenu || player.IsCursorFree || placeAction == null || hotbar == null || inventory == null)
            {
                return;
            }
            if (!placeAction.action.WasPressedThisFrame())
            {
                return;
            }
            int slot = hotbar.SelectedSlot;
            ItemStack held = slot >= 0 ? inventory.Inventory[slot] : ItemStack.Empty;
            if (held.IsEmpty || !(held.Item is ConsumableDefinition drink))
            {
                return;
            }
            inventory.Inventory.RemoveAt(slot, 1);
            Drink(drink);
        }

        /// <summary>Starts a drink's effect, or starts its time again (GL456: it doesn't add up).</summary>
        public void Drink(ConsumableDefinition drink)
        {
            active = drink;
            until = Days + drink.EffectHours / 24d;
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (!IsActive || player == null || !player.enabled || player.IsInMenu || Event.current.type != EventType.Repaint)
            {
                return;
            }
            noteStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = GameUi.Font(13),
                normal = { textColor = new Color(0.95f, 0.75f, 0.5f) },
            };
            float percent = (1f - active.CooldownShare) * 100f;
            string ends = "";
            if (dayNight != null)
            {
                float hour = Mathf.Repeat(dayNight.ClockHour + (float)((until - Days) * 24d), 24f);
                ends = $" until {Mathf.FloorToInt(hour):00}:{Mathf.FloorToInt(hour % 1f * 60f):00}";
            }
            // Over the early bird's line (HeldToolView), so both show at dawn.
            GameUi.OutlinedLabel(new Rect(Screen.width * 0.5f - 300f, Screen.height - GameUi.SlotSize - 112f, 600f, 30f), $"{active.EffectName}: tools {percent:0}% quicker{ends}", noteStyle);
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            if (IsActive)
            {
                values[UntilKey] = until.ToString("R", CultureInfo.InvariantCulture);
                values[ItemKey] = active.Id;
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            if (!values.TryGetValue(UntilKey, out string text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double end)
                || !values.TryGetValue(ItemKey, out string id))
            {
                return;
            }
            // The drink by id among the loaded items (the shops' wares load it with the scene).
            foreach (ConsumableDefinition drink in Resources.FindObjectsOfTypeAll<ConsumableDefinition>())
            {
                if (drink.Id == id)
                {
                    active = drink;
                    until = end;
                    return;
                }
            }
        }
    }
}
