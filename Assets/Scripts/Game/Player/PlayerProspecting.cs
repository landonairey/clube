using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Prospecting by hand (GL458): with nothing selected in the hotbar, Place (right click) on the ground within reach reads
    /// the material there as far as the player's Prospecting level allows (<see cref="ProspectReading"/>: the material and its
    /// grade, one more component a level) and gives a little Prospecting xp. An ore the player can't tell apart yet reads as a
    /// question mark and gives more. Each sample gives xp once (kept in saves); reading it again still tells what it is.
    /// With an ore selected instead, away from a boulder, right click licks it (GL478): one is used up for Prospecting xp.
    /// </summary>
    public class PlayerProspecting : MonoBehaviour, ISavedValues
    {
        private const string SamplesKey = "prospected.samples";

        [Tooltip("Reads the ground (Place, right click) with nothing selected.")]
        [SerializeField]
        private InputActionReference placeAction;

        [Tooltip("How far the player can reach to prospect, in metres.")]
        [SerializeField, Min(0.5f)]
        private float reach = 3f;

        [Tooltip("Prospecting xp for a sample of a known material.")]
        [SerializeField, Min(0f)]
        private float experience = 0.5f;

        [Tooltip("Prospecting xp for a sample of an ore the player can't tell apart yet.")]
        [SerializeField, Min(0f)]
        private float unknownExperience = 2f;

        [Tooltip("Prospecting xp for licking one ore item the player knows (GL478).")]
        [SerializeField, Min(0f)]
        private float lickExperience = 1f;

        [Tooltip("Prospecting xp for licking one ore item the player can't tell apart yet.")]
        [SerializeField, Min(0f)]
        private float unknownLickExperience = 3f;

        private PlayerController player;
        private PlayerInventory inventory;
        private Hotbar hotbar;
        private PlayerSkills skills;
        private OreKnowledge knowledge;
        private ToolHint hint;
        private BoulderBashing bashing;
        private WorldView worldView;

        // Samples already prospected for xp.
        private readonly HashSet<Vector3Int> prospected = new HashSet<Vector3Int>();

        /// <summary>Raised after a reading, with the text shown.</summary>
        public event System.Action<string> Read;

        /// <summary>Raised when an ore is licked (GL478), with what was licked.</summary>
        public event System.Action<ItemDefinition> Licked;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            skills = GetComponent<PlayerSkills>();
            knowledge = GetComponent<OreKnowledge>();
            hint = GetComponent<ToolHint>();
            bashing = GetComponent<BoulderBashing>();
        }

        private void OnEnable()
        {
            placeAction?.action.Enable();
        }

        private void Update()
        {
            if (worldView == null)
            {
                worldView = FindFirstObjectByType<WorldView>();
            }
            if (player == null || !player.enabled || player.IsInMenu || player.IsCursorFree || placeAction == null
                || worldView == null || !worldView.IsReady || !placeAction.action.WasPressedThisFrame())
            {
                return;
            }
            if (!HandsEmpty())
            {
                // An ore in hand, away from a boulder (where right click crushes it): a lick (GL478).
                if (bashing == null || !bashing.CanBash)
                {
                    TryLick();
                }
                return;
            }
            if (TryGetAimedSample(out Vector3Int sample, out VoxelMaterial material))
            {
                Prospect(sample, material);
            }
        }

        // Licks the selected ore (GL478): one of it (or the part that's left) is used up for Prospecting xp, more for an ore
        // the player can't tell apart yet, and the reading their level allows of what it's made of.
        private bool TryLick()
        {
            int slot = hotbar.SelectedSlot;
            ItemStack held = inventory.Inventory[slot];
            if (!IsOre(held, out VoxelMaterial ore))
            {
                return false;
            }
            bool known = !held.Item.HidesContents && (ore == null || knowledge == null || knowledge.Knows(ore));
            float amount = Mathf.Min(1f, held.Amount);
            float xp = (known ? lickExperience : unknownLickExperience) * amount;
            string taste = known && ore != null ? Taste(held, ore) : "a taste I can't place yet";
            inventory.Inventory.RemoveAmount(slot, amount);
            skills?.Skills.Award(SkillType.Prospecting, xp);
            string text = $"Licked the {held.Item.DisplayName.ToLowerInvariant()}: {taste}  (+{xp:0.#} Prospecting)";
            hint?.Show(text);
            Licked?.Invoke(held.Item);
            Read?.Invoke(text);
            return true;
        }

        // Ore: a hidden "? ore", or a stack whose own material is an ore (raw or crushed).
        private bool IsOre(ItemStack stack, out VoxelMaterial ore)
        {
            ore = null;
            if (stack.IsEmpty)
            {
                return false;
            }
            if (stack.Item.HidesContents)
            {
                return true;
            }
            MaterialRegistry materials = worldView.Config.Materials;
            if (stack.Contents == null || materials == null)
            {
                return false;
            }
            foreach (Content part in stack.Contents.Parts)
            {
                if (part.Material != null && part.Material.Category == VoxelMaterialCategory.Ore && materials.IsSourceOf(part.Material, stack.Item))
                {
                    ore = part.Material;
                    return true;
                }
            }
            return false;
        }

        // "High Grade chalcopyrite" below Prospecting 10, "92% chalcopyrite" at it.
        private string Taste(ItemStack stack, VoxelMaterial ore)
        {
            float share = stack.Contents != null ? stack.Contents.FractionOf(ore) : 1f;
            int level = skills != null ? skills.Skills.Level(SkillType.Prospecting) : 0;
            string grade = level >= ProspectReading.ExactLevel ? QualityText.Percent(share) : ProspectReading.Name(ProspectReading.GradeOf(share));
            return $"{grade} {ore.DisplayName.ToLowerInvariant()}";
        }

        // Nothing selected in the hotbar: bare hands.
        private bool HandsEmpty()
        {
            if (hotbar == null || inventory == null || inventory.Inventory == null)
            {
                return false;
            }
            int slot = hotbar.SelectedSlot;
            return slot < 0 || inventory.Inventory[slot].IsEmpty;
        }

        private void Prospect(Vector3Int sample, VoxelMaterial material)
        {
            bool known = knowledge == null || knowledge.Knows(material);
            string text = known ? Reading(sample, material) : "Some ore I can't yet tell apart: ?";
            if (prospected.Add(sample))
            {
                float xp = known ? experience : unknownExperience;
                skills?.Skills.Award(SkillType.Prospecting, xp);
                text += $"  (+{xp:0.#} Prospecting)";
            }
            hint?.Show(text);
            Read?.Invoke(text);
        }

        // "Chalcopyrite: Good · Stone: Medium · ?" by the level's reading (GL457); plain material for ground with no contents.
        private string Reading(Vector3Int sample, VoxelMaterial material)
        {
            OreField ores = worldView.Ores;
            Composition contents = ores != null ? ores.ContentsAt(material, worldView.World.Grid.SampleToWorld(sample)) : null;
            if (contents == null)
            {
                return material.DisplayName;
            }
            int level = skills != null ? skills.Skills.Level(SkillType.Prospecting) : 0;
            List<Content> parts = ProspectReading.InReadingOrder(contents, m => m == material);
            int read = ProspectReading.ComponentsRead(level);
            var text = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (text.Length > 0)
                {
                    text.Append(" · ");
                }
                if (i >= read)
                {
                    text.Append('?');
                    continue;
                }
                string share = level >= ProspectReading.ExactLevel
                    ? QualityText.Percent(parts[i].Fraction)
                    : ProspectReading.Name(ProspectReading.GradeOf(parts[i].Fraction));
                text.Append($"{parts[i].Material.DisplayName}: {share}");
            }
            return text.ToString();
        }

        // The solid sample nearest the crosshair's hit within reach.
        private bool TryGetAimedSample(out Vector3Int sample, out VoxelMaterial material)
        {
            sample = default;
            material = null;
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
            float nearest = float.MaxValue;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3Int at = voxel + new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                float? density = world.GetDensity(at);
                byte? id = world.GetMaterial(at);
                if (!density.HasValue || density.Value < iso || !id.HasValue || materials == null)
                {
                    continue;
                }
                float distance = (world.Grid.SampleToWorld(at) - hit.Point).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    sample = at;
                    material = materials.Get(id.Value);
                }
            }
            return material != null;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            var text = new StringBuilder();
            foreach (Vector3Int s in prospected)
            {
                text.Append(s.x.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.y.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.z.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
            values[SamplesKey] = text.ToString();
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            prospected.Clear();
            if (!values.TryGetValue(SamplesKey, out string text))
            {
                return;
            }
            foreach (string entry in text.Split(';'))
            {
                string[] xyz = entry.Split(',');
                if (xyz.Length == 3 && int.TryParse(xyz[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(xyz[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                    && int.TryParse(xyz[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                {
                    prospected.Add(new Vector3Int(x, y, z));
                }
            }
        }
    }
}
