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
    /// experience. From a Mining level on, holding Place with ore selected crushes it an item every 0.25 s (GL221): crushed ore
    /// holding half the stone it had (<see cref="Crushing"/>), and the crushed stone knocked off it. A
    /// boulder is stone too big to be a loose stone (<see cref="ToolStrike.IsSmallPiece"/>). Holding Place with
    /// any other mixed stack selected (clay with dirt and grass in it) uses the boulder as a worktop to pick the
    /// impurities out, an item's volume every 0.25 s (GL177, GL220, <see cref="Refining"/>); what
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

        [Tooltip("The flat rock (GL454): crushing and refining work on it too, two batches a cycle.")]
        [SerializeField]
        private VoxelMaterial flatRock;

        [Tooltip("How many batches a cycle on the flat rock (GL454): the boulder does one.")]
        [SerializeField, Min(1)]
        private int flatRockBatches = 2;

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
        private float crushExperience = 0.1f;

        [Header("Refining (GL177) and crushing (GL221)")]
        [Tooltip("Seconds to refine or crush one item's volume (GL220, GL221): a refining pass picks out that much, a crush takes one item of ore. Holding Place keeps going.")]
        [SerializeField, Min(0.05f)]
        private float secondsPerVolume = 0.25f;

        [Tooltip("Items' worth of impurities each pass picks out (GL190), all of them once only a trace is left.")]
        [SerializeField, Min(0.05f)]
        private float refineVolume = 1f;

        [Tooltip("Crafting experience per pass.")]
        [SerializeField, Min(0f)]
        private float refineExperience = 0.1f;

        [Tooltip("Blacksmithing experience each refining pass gives too (GL362): a little, for working the stone.")]
        [SerializeField, Min(0f)]
        private float refineSmithingExperience = 0.02f;

        private PlayerController player;
        private PlayerInventory inventory;
        private PlayerSkills skills;
        private readonly List<Content> pickedOut = new List<Content>();
        private float refineProgress;
        private bool refining;

        /// <summary>Raised with what a bash did, or why it couldn't ("Made a hand axe", "Crushing ore needs Mining 2").</summary>
        public event Action<string> Message;

        /// <summary>A stone was knapped into a hand axe (GL381), for its sound.</summary>
        public event Action HandAxeMade;

        /// <summary>Raised on each crush of ore (GL459), with where on the boulder and what was crushed: for the sound, the swing and the fragments.</summary>
        public event Action<Vector3, VoxelMaterial> OreCrushed;

        /// <summary>A pass of picking over a stack while refining (GL493).</summary>
        public event Action RefinePassed;

        /// <summary>While true a stone is not knocked into a hand axe (the tutorial, GL581: it could not be sold).</summary>
        public bool HandAxeDisabled { get; set; }

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
            bool pressed = placeAction.action.WasPressedThisFrame();
            if (pressed && HoldsSomethingToBash(out _, out Crush crush) && AimsAtBoulder())
            {
                // A stone becomes a hand axe at once; ore is crushed while Place is held (GL221).
                if (crush.Raw == null)
                {
                    Bash();
                    return;
                }
                if (skills != null && !skills.Skills.Has(crushSkill, crushLevel))
                {
                    Message?.Invoke($"Crushing ore needs {crushSkill} {crushLevel}");
                    return;
                }
            }
            UpdateHold();
        }

        // Holding Place on a boulder works the selected stack an item's volume at a time (GL220, GL221): ore is crushed,
        // a mixed stack picked over (GL177).
        private void UpdateHold()
        {
            int slot = -1;
            bool crushing = placeAction.action.IsPressed() && HoldsSomethingToBash(out slot, out Crush crush) && crush.Raw != null
                && (skills == null || skills.Skills.Has(crushSkill, crushLevel));
            refining = crushing || (placeAction.action.IsPressed() && HoldsSomethingToRefine(out slot));
            if (!refining || !AimsAtBoulder())
            {
                refining = false;
                refineProgress = 0f;
                return;
            }
            refineProgress += Time.deltaTime;
            if (refineProgress >= secondsPerVolume)
            {
                refineProgress = 0f;
                // The flat rock works twice the batch of a boulder (GL454).
                int batches = AimsAtFlatRock() ? flatRockBatches : 1;
                for (int batch = 0; batch < batches; batch++)
                {
                    if (crushing ? !Bash() : !RefinePass(slot))
                    {
                        break;
                    }
                }
            }
        }

        // The player's Prospecting level: below ExactLevel qualities read as grade words (GL562).
        private int ProspectingLevel => skills != null ? skills.Skills.Level(SkillType.Prospecting) : 0;

        // One pass, false when there was nothing to refine or nothing is left: the stack keeps its slot, smaller and purer; what came out goes back as stacks of its own materials.
        private bool RefinePass(int slot)
        {
            Inventory items = inventory.Inventory;
            MaterialRegistry materials = worldView.Config.Materials;
            ItemStack held = items[slot];
            if (held.IsEmpty || !Refining.CanRefine(held, materials))
            {
                return false;
            }
            pickedOut.Clear();
            RefinePassed?.Invoke();
            ItemStack refined = Refining.Pass(held, materials, refineVolume, pickedOut, UnityEngine.Random.value, UnityEngine.Random.value, out bool failed);
            // Crushed ore is prospecting work (GL388); anything else picked over is crafting.
            bool prospecting = held.Item.PurityAs != null;
            skills?.Skills.Award(prospecting ? SkillType.Prospecting : SkillType.Crafting, refineExperience);
            skills?.Skills.Award(SkillType.Blacksmithing, refineSmithingExperience);
            if (failed)
            {
                // The purer the stack, the more passes find nothing to pick out (GL386).
                Message?.Invoke($"Found nothing to pick out of the {held.Item.DisplayName.ToLowerInvariant()} ({ProspectReading.QualityAt(held.Quality, ProspectingLevel)})");
                return true;
            }
            items.Replace(slot, refined);
            foreach (Content picked in pickedOut)
            {
                ItemDefinition drop = picked.Material != null ? picked.Material.FinalDrop : null;
                if (drop == null)
                {
                    continue;
                }
                // Ore picked out of crushed ore is crushed already (GL440): silver out of crushed chalcopyrite is crushed silver ore.
                if (prospecting)
                {
                    drop = CrushedFormOf(drop);
                }
                int whole = (int)(picked.Fraction + 1e-4f);
                items.Add(drop, whole, Composition.From(new Content(picked.Material, 1f)), 0, 1f, Mathf.Max(0f, picked.Fraction - whole));
                inventory.Notify(drop, picked.Fraction);
            }
            Message?.Invoke(refined.IsEmpty
                ? $"Nothing of the {held.Item.DisplayName.ToLowerInvariant()} was left"
                : $"Picked over the {held.Item.DisplayName.ToLowerInvariant()}: {ProspectReading.QualityAt(refined.Quality, ProspectingLevel)} now");
            return !refined.IsEmpty;
        }

        // The crushed item for a raw ore, from the crush table; the item itself when it has none (stone).
        private ItemDefinition CrushedFormOf(ItemDefinition raw)
        {
            foreach (Crush entry in crushes)
            {
                if (entry.Raw == raw && entry.Crushed != null)
                {
                    return entry.Crushed;
                }
            }
            return raw;
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
            GameUi.Apply();
            if (!refining || inventory == null || hotbar == null || hotbar.SelectedSlot < 0)
            {
                return;
            }
            ItemStack stack = inventory.Inventory[hotbar.SelectedSlot];
            if (stack.IsEmpty)
            {
                return;
            }
            // Above the crosshair (GL244): under it are the tool hint and the pickup notices, which wrote over this.
            var bar = new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.5f - 52f, 180f, 10f);
            string doing = HoldsSomethingToBash(out _, out Crush crush) && crush.Raw != null ? "Crushing" : "Refining";
            GUI.Label(new Rect(bar.x - 30f, bar.y - 22f, bar.width + 60f, 28f), $"{doing} {stack.Item.DisplayName.ToLowerInvariant()} ({ProspectReading.QualityAt(stack.Quality, ProspectingLevel)})");
            GUI.Box(bar, GUIContent.none);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(refineProgress / secondsPerVolume), bar.height), Texture2D.whiteTexture);
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
                HandAxeMade?.Invoke();
                return true;
            }
            if (skills != null && !skills.Skills.Has(crushSkill, crushLevel))
            {
                Message?.Invoke($"Crushing ore needs {crushSkill} {crushLevel}");
                return false;
            }
            // One item of ore, or the part of one that's left (GL194): its minerals stay, half the rock comes off as
            // crushed stone (GL148).
            float amount = held.Count >= 1 ? 1f : held.Part;
            Composition crushed = Crushing.Crush(held.Contents, out float knockedOff);
            MaterialRegistry materials = worldView.Config.Materials;
            float quality = crushed != null && materials != null ? materials.Quality(crushed, crush.Raw) : held.Quality;
            // What's knocked off goes: the crushed ore is the rest of the item, so its mineral stays what it was (GL377;
            // keeping a whole item made copper out of nothing).
            float left = amount * (1f - knockedOff);
            int whole = (int)Mathf.Floor(left + 1e-4f);
            if (held.Count >= 1)
            {
                items.RemoveAt(slot, 1);
            }
            else
            {
                items.TakeAll(slot);
            }
            items.Add(crush.Crushed, whole, crushed, 0, quality, Mathf.Max(0f, left - whole));
            inventory.Notify(crush.Crushed, left);
            if (crushedStone != null && knockedOff > 0f)
            {
                // Crushed stone is a material of its own now (GL571), so its stack reads as grades and drops as ground.
                VoxelMaterial gravel = materials != null ? materials.Find("Crushed stone") : null;
                Composition gravelContents = gravel != null ? Composition.From(new Content(gravel, 1f)) : null;
                items.Add(crushedStone, 0, gravelContents, 0, 1f, knockedOff * amount);
                inventory.Notify(crushedStone, knockedOff * amount);
            }
            // Crushing needs Mining (crushSkill) but trains Prospecting (GL388).
            skills?.Skills.Award(SkillType.Prospecting, crushExperience);
            // The ore's own material, for the colour of the fragments and the grade named in the message.
            VoxelMaterial ore = null;
            if (held.Contents != null && materials != null)
            {
                foreach (Content part in held.Contents.Parts)
                {
                    if (materials.IsSourceOf(part.Material, crush.Raw))
                    {
                        ore = part.Material;
                    }
                }
            }
            // A grade name, not the exact share, until Prospecting reads exactly (GL485), as PlayerProspecting.Taste does.
            int level = ProspectingLevel;
            float share = crushed != null && ore != null ? crushed.FractionOf(ore) : quality;
            string grade = level >= ProspectReading.ExactLevel ? QualityText.Percent(share) : ProspectReading.Name(ProspectReading.GradeOf(share));
            Message?.Invoke($"Crushed into {crush.Crushed.DisplayName.ToLowerInvariant()} ({grade})");
            if (OreCrushed != null && TryGetAimedBoulder(out Vector3Int boulder))
            {
                OreCrushed(worldView.transform.TransformPoint(worldView.World.Grid.SampleToWorld(boulder)), ore);
            }
            return true;
        }

        // A stone, or ore with a crushed form, in the selected slot. A stone with a mix in it is picked over instead (GL487); a clean one is knapped into a hand axe.
        private bool HoldsSomethingToBash(out int slot, out Crush crush)
        {
            crush = default;
            slot = hotbar != null ? hotbar.SelectedSlot : -1;
            if (slot < 0 || inventory == null || inventory.Inventory == null)
            {
                return false;
            }
            ItemStack stack = inventory.Inventory[slot];
            ItemDefinition item = stack.Item;
            if (item == null)
            {
                return false;
            }
            if (item == stone && handAxe != null && !HandAxeDisabled && !(worldView != null && worldView.IsReady && Refining.CanRefine(stack, worldView.Config.Materials)))
            {
                return stack.Count > 0;
            }
            // Raw ore stays something to crush when less than an item of it is left (GL194): the part is crushed,
            // rather than the boulder picking the ore over instead.
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
            return TryGetAimedBoulder(out _);
        }

        // Boulder material, or the flat rock (GL454).
        private bool IsWorkSurface(VoxelMaterial material)
        {
            return material != null && (boulderMaterials.Contains(material) || material == flatRock);
        }

        // The crosshair on the flat rock (GL454), where crushing and refining go two batches a cycle.
        private bool AimsAtFlatRock()
        {
            if (flatRock == null || !TryGetAimedBoulder(out Vector3Int sample))
            {
                return false;
            }
            byte? id = worldView.World.GetMaterial(sample);
            return id.HasValue && worldView.Config.Materials.Get(id.Value) == flatRock;
        }

        /// <summary>
        /// The boulder sample under the crosshair within reach (boulder material in a piece too big to be a loose stone), for
        /// the interact outline (GL368); false when the crosshair isn't on one.
        /// </summary>
        public bool TryGetAimedBoulder(out Vector3Int boulder)
        {
            boulder = default;
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
                if (!density.HasValue || density.Value < iso || !id.HasValue || materials == null || !IsWorkSurface(materials.Get(id.Value)))
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
            if (!found.HasValue || ToolStrike.IsSmallPiece(world, found.Value, world.GetMaterial(found.Value).Value, loosePieceSize, iso))
            {
                return false;
            }
            boulder = found.Value;
            return true;
        }
    }
}
