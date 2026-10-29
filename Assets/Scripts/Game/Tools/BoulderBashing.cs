using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Bashing what's in hand on a boulder (GL146, GL148): with a stone selected, Place (right click)
    /// aimed at a boulder or rock face knocks it into a hand axe, the first tool, for a little Crafting
    /// experience. From a Mining level on, a bash with ore selected crushes one item of it: crushed ore
    /// holding half the stone it had (<see cref="Crushing"/>), and the crushed stone knocked off it. A
    /// boulder is stone too big to be a loose stone (<see cref="ToolStrike.IsSmallPiece"/>). Holding Place with
    /// any other mixed stack selected (clay with dirt and grass in it) uses the boulder as a worktop to pick the
    /// impurities out, a pass at a time, each pass longer for a bigger stack (GL177, <see cref="Refining"/>); what
    /// comes out goes back as stacks of its own. While a bash is possible, Place does this instead of adding
    /// ground (<see cref="PlayerDigTool"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class BoulderBashing : MonoBehaviour
    {
        /// <summary>Raw ore and what crushing one item of it gives.</summary>
        [Serializable]
        public struct Crush
        {
            public ItemDefinition Raw;
            public ItemDefinition Crushed;
        }

        [SerializeField]
        private WorldView worldView;

        [Tooltip("Bashes (right click).")]
        [SerializeField]
        private InputActionReference placeAction;

        [SerializeField]
        private Hotbar hotbar;

        [Tooltip("How far from the head a boulder can be bashed, in metres.")]
        [SerializeField, Min(0.5f)]
        private float reach = 3f;

        [Tooltip("What a boulder is made of: bashing works on these when the piece is too big to be loose.")]
        [SerializeField]
        private List<VoxelMaterial> boulderMaterials = new List<VoxelMaterial>();

        [Tooltip("The largest piece of boulder material that counts as a loose stone, not a boulder (GL149).")]
        [SerializeField, Range(1, 32)]
        private int loosePieceSize = 3;

        [Header("Hand axe (GL146)")]
        [Tooltip("What's knocked into a hand axe.")]
        [SerializeField]
        private ItemDefinition stone;

        [SerializeField]
        private ToolDefinition handAxe;

        [SerializeField, Min(0f)]
        private float handAxeExperience = 5f;

        [Header("Crushing ore (GL148)")]
        [SerializeField]
        private List<Crush> crushes = new List<Crush>();

        [Tooltip("The crushed stone knocked off the ore, as a part item.")]
        [SerializeField]
        private ItemDefinition crushedStone;

        [SerializeField]
        private SkillType crushSkill = SkillType.Mining;

        [SerializeField, Range(0, SkillSet.MaxLevel)]
        private int crushLevel = 2;

        [SerializeField, Min(0f)]
        private float crushExperience = 1f;

        [Header("Refining (GL177)")]
        [Tooltip("Seconds a pass over a mixed stack takes at least; holding Place keeps passing.")]
        [SerializeField, Min(0.05f)]
        private float refineSeconds = 1f;

        [Tooltip("More seconds a pass takes for each item in the stack.")]
        [SerializeField, Min(0f)]
        private float refineSecondsPerItem = 0.04f;

        [Tooltip("Share of the impurities each pass picks out.")]
        [SerializeField, Range(0.05f, 1f)]
        private float refineShare = 0.3f;

        [Tooltip("Crafting experience per pass.")]
        [SerializeField, Min(0f)]
        private float refineExperience = 0.5f;

        private PlayerController player;
        private PlayerInventory inventory;
        private PlayerSkills skills;
        private readonly List<Content> pickedOut = new List<Content>();
        private float refineProgress;
        private bool refining;

        /// <summary>Raised with what a bash did, or why it couldn't ("Made a hand axe", "Crushing ore needs Mining 2").</summary>
        public event Action<string> Message;

        /// <summary>True when Place would bash this frame: a stone or crushable ore in hand, aimed at a boulder within reach.</summary>
        public bool CanBash => (HoldsSomethingToBash(out _, out _) || HoldsSomethingToRefine(out _)) && AimsAtBoulder();

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            skills = GetComponent<PlayerSkills>();
        }

        private void OnEnable()
        {
            placeAction?.action.Enable();
        }

        private void Update()
        {
            if (!player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady || placeAction == null)
            {
                refining = false;
                refineProgress = 0f;
                return;
            }
            if (placeAction.action.WasPressedThisFrame() && HoldsSomethingToBash(out _, out _) && AimsAtBoulder())
            {
                Bash();
                return;
            }
            UpdateRefining();
        }

        // Holding Place on a boulder with a mixed stack selected picks it over, a pass at a time (GL177).
        private void UpdateRefining()
        {
            int slot = -1;
            refining = placeAction.action.IsPressed() && HoldsSomethingToRefine(out slot) && AimsAtBoulder();
            if (!refining)
            {
                refineProgress = 0f;
                return;
            }
            refineProgress += Time.deltaTime;
            if (refineProgress >= Refining.PassSeconds(inventory.Inventory[slot], refineSeconds, refineSecondsPerItem))
            {
                refineProgress = 0f;
                RefinePass(slot);
            }
        }

        // One pass: the stack keeps its slot, smaller and purer; what came out goes back as stacks of its own materials.
        private void RefinePass(int slot)
        {
            Inventory items = inventory.Inventory;
            MaterialRegistry materials = worldView.Config.Materials;
            ItemStack held = items[slot];
            pickedOut.Clear();
            ItemStack refined = Refining.Pass(held, materials, refineShare, pickedOut);
            items.Replace(slot, refined);
            foreach (Content picked in pickedOut)
            {
                ItemDefinition drop = picked.Material != null ? picked.Material.FinalDrop : null;
                if (drop == null)
                {
                    continue;
                }
                int whole = (int)(picked.Fraction + 1e-4f);
                items.Add(drop, whole, Composition.From(new Content(picked.Material, 1f)), 0, 1f, Mathf.Max(0f, picked.Fraction - whole));
            }
            skills?.Skills.Award(SkillType.Crafting, refineExperience);
            Message?.Invoke(refined.IsEmpty
                ? $"Nothing of the {held.Item.DisplayName.ToLowerInvariant()} was left"
                : $"Picked over the {held.Item.DisplayName.ToLowerInvariant()}: {refined.Quality * 100f:0}% now");
        }

        // A mixed stack in the selected slot that isn't bashed instead: gathered clay, dirt, wood with what came with it.
        private bool HoldsSomethingToRefine(out int slot)
        {
            slot = hotbar != null ? hotbar.SelectedSlot : -1;
            if (slot < 0 || inventory == null || inventory.Inventory == null || worldView == null || !worldView.IsReady)
            {
                return false;
            }
            ItemStack stack = inventory.Inventory[slot];
            return !HoldsSomethingToBash(out _, out _) && Refining.CanRefine(stack, worldView.Config.Materials);
        }

        private void OnGUI()
        {
            if (!refining || inventory == null || hotbar == null || hotbar.SelectedSlot < 0)
            {
                return;
            }
            ItemStack stack = inventory.Inventory[hotbar.SelectedSlot];
            if (stack.IsEmpty)
            {
                return;
            }
            float seconds = Refining.PassSeconds(stack, refineSeconds, refineSecondsPerItem);
            var bar = new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.5f + 70f, 180f, 10f);
            GUI.Label(new Rect(bar.x - 30f, bar.y - 22f, bar.width + 60f, 20f), $"Refining {stack.Item.DisplayName.ToLowerInvariant()} ({stack.Quality * 100f:0}%)");
            GUI.Box(bar, GUIContent.none);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(refineProgress / seconds), bar.height), Texture2D.whiteTexture);
        }

        /// <summary>Bashes what's selected on the boulder aimed at: a stone becomes a hand axe, ore is crushed. False when nothing happened.</summary>
        public bool Bash()
        {
            if (!HoldsSomethingToBash(out int slot, out Crush crush) || !AimsAtBoulder())
            {
                return false;
            }
            Inventory items = inventory.Inventory;
            ItemStack held = items[slot];
            if (held.Item == stone)
            {
                items.RemoveAt(slot, 1);
                items.Add(handAxe, 1);
                skills?.Skills.Award(SkillType.Crafting, handAxeExperience);
                Message?.Invoke($"Knocked the stone into a {handAxe.DisplayName.ToLowerInvariant()}");
                return true;
            }
            if (skills != null && !skills.Skills.Has(crushSkill, crushLevel))
            {
                Message?.Invoke($"Crushing ore needs {crushSkill} {crushLevel}");
                return false;
            }
            // One item of ore: its minerals stay, half the rock comes off as crushed stone (GL148).
            Composition crushed = Crushing.Crush(held.Contents, out float knockedOff);
            MaterialRegistry materials = worldView.Config.Materials;
            float quality = crushed != null && materials != null ? materials.Quality(crushed, crush.Raw) : held.Quality;
            items.RemoveAt(slot, 1);
            items.Add(crush.Crushed, 1, crushed, 0, quality);
            if (crushedStone != null && knockedOff > 0f)
            {
                items.Add(crushedStone, 0, null, 0, 1f, knockedOff);
            }
            skills?.Skills.Award(crushSkill, crushExperience);
            Message?.Invoke($"Crushed into {crush.Crushed.DisplayName.ToLowerInvariant()} ({quality * 100f:0}%)");
            return true;
        }

        // A stone, or ore with a crushed form, in the selected slot.
        private bool HoldsSomethingToBash(out int slot, out Crush crush)
        {
            crush = default;
            slot = hotbar != null ? hotbar.SelectedSlot : -1;
            if (slot < 0 || inventory == null || inventory.Inventory == null)
            {
                return false;
            }
            ItemDefinition item = inventory.Inventory[slot].Item;
            if (item == null || inventory.Inventory[slot].Count == 0)
            {
                return false;
            }
            if (item == stone && handAxe != null)
            {
                return true;
            }
            foreach (Crush entry in crushes)
            {
                if (entry.Raw == item && entry.Crushed != null)
                {
                    crush = entry;
                    return true;
                }
            }
            return false;
        }

        // The crosshair on boulder material, within reach, in a piece too big to be a loose stone.
        private bool AimsAtBoulder()
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out WorldHit hit)
                || (worldView.transform.TransformPoint(hit.Point) - head.position).sqrMagnitude > reach * reach)
            {
                return false;
            }
            World world = worldView.World;
            MaterialRegistry materials = worldView.Config.Materials;
            float iso = worldView.Config.IsoLevel;
            Vector3Int voxel = world.Grid.ChunkFirstSample(hit.Chunk) + hit.Voxel;
            // The hit voxel's solid corners: the nearest of boulder material decides.
            float nearest = float.MaxValue;
            Vector3Int? found = null;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3Int sample = voxel + new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                float? density = world.GetDensity(sample);
                byte? id = world.GetMaterial(sample);
                if (!density.HasValue || density.Value < iso || !id.HasValue || materials == null || !boulderMaterials.Contains(materials.Get(id.Value)))
                {
                    continue;
                }
                float distance = (world.Grid.SampleToWorld(sample) - hit.Point).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    found = sample;
                }
            }
            return found.HasValue && !ToolStrike.IsSmallPiece(world, found.Value, world.GetMaterial(found.Value).Value, loosePieceSize, iso);
        }
    }
}
