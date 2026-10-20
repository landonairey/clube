using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Ore the player can't tell apart yet (GL54, GL72; first pass of PS6): each group of
    /// materials (the copper ores at Mining 3, silver and gold higher up) stays hidden until the
    /// skill reaches the group's level. Until then its materials are drawn as the rock around them
    /// (<see cref="MaterialRegistry.ShowAs"/>), and what the player mines of them comes out as
    /// "? ore", keeping its contents: one "? ore" item per ore (GL90), so different ores never share a stack. At the level the group shows, and every "? ore" in the
    /// inventory whose contents are mostly that group's turns into the ore it is.
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class OreKnowledge : MonoBehaviour
    {
        /// <summary>Materials the player learns to see together, at one level.</summary>
        [Serializable]
        public class HiddenOres
        {
            [Tooltip("What the notice calls them when they show, e.g. \"copper ore\".")]
            public string Name = "copper ore";

            [Range(0, SkillSet.MaxLevel)]
            public int Level = 3;

            [Tooltip("The ores, with their cracked and loose stages.")]
            public List<VoxelMaterial> Materials = new List<VoxelMaterial>();
        }

        [SerializeField]
        private WorldView worldView;

        [Tooltip("Groups of hidden ore, each shown at its own level of the skill below.")]
        [SerializeField]
        private List<HiddenOres> groups = new List<HiddenOres>();

        [Tooltip("What hidden materials look like until then (the host rock).")]
        [SerializeField]
        private VoxelMaterial shownAs;

        [SerializeField]
        private SkillType skill = SkillType.Mining;

        [Tooltip("What mining a hidden material gives until then.")]
        [SerializeField]
        private ItemDefinition unknownOre;

        private readonly HashSet<HiddenOres> hiding = new HashSet<HiddenOres>();

        // One "? ore" per real ore item (GL90), made the first time it's needed, so each hidden ore stacks
        // apart; and what each stands for: the real item and the material it was mined as.
        private readonly Dictionary<ItemDefinition, ItemDefinition> disguises = new Dictionary<ItemDefinition, ItemDefinition>();
        private readonly Dictionary<ItemDefinition, (ItemDefinition Item, VoxelMaterial Material)> hiddenAs = new Dictionary<ItemDefinition, (ItemDefinition, VoxelMaterial)>();
        private PlayerSkills skills;
        private PlayerInventory inventory;
        private MaterialRegistry registry;

        /// <summary>The skill whose levels reveal the ore, for notices.</summary>
        public SkillType Skill => skill;

        public IReadOnlyList<HiddenOres> Groups => groups;

        /// <summary>True when the player can tell this material apart: it's in no group, or its group's level is reached.</summary>
        public bool Knows(VoxelMaterial material)
        {
            HiddenOres group = GroupOf(material);
            return group == null || Reached(group);
        }

        /// <summary>The level of <see cref="Skill"/> at which the player can tell this material apart; 0 when it's never hidden.</summary>
        public int LevelToKnow(VoxelMaterial material)
        {
            HiddenOres group = GroupOf(material);
            return group != null ? group.Level : 0;
        }

        /// <summary>The names of the groups that show at a level of the skill, e.g. "copper ore".</summary>
        public IEnumerable<string> RevealedAt(SkillType raised, int level)
        {
            if (raised != skill)
            {
                yield break;
            }
            foreach (HiddenOres group in groups)
            {
                if (group.Level == level)
                {
                    yield return group.Name;
                }
            }
        }

        private void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            skills.Skills.LevelReached += OnLevelReached;
        }

        private void OnDisable()
        {
            skills.Skills.LevelReached -= OnLevelReached;
        }

        private void Update()
        {
            // The registry is ready once the world is.
            if (registry == null && worldView != null && worldView.IsReady)
            {
                registry = worldView.Config.Materials;
                foreach (HiddenOres group in groups)
                {
                    SetHidden(group, !Reached(group));
                }
            }
        }

        private void OnDestroy()
        {
            // The registry is an asset: leave it showing every material as itself.
            foreach (HiddenOres group in groups)
            {
                SetHidden(group, false);
            }
        }

        /// <summary>A mined stack as the player gets it: "? ore" when its material is still hidden.</summary>
        public ItemStack Disguise(ItemStack stack, VoxelMaterial material)
        {
            if (unknownOre == null || stack.IsEmpty || material == null || Knows(material))
            {
                return stack;
            }
            return new ItemStack(DisguiseFor(stack.Item, material), stack.Count, stack.Contents);
        }

        /// <summary>True for a "? ore" item: the generic one or one of the per-ore copies.</summary>
        public bool IsUnknown(ItemDefinition item)
        {
            return item != null && (item == unknownOre || hiddenAs.ContainsKey(item));
        }

        // The "? ore" standing for one real ore item: a copy of the generic one, alike in name and icon,
        // but its own item, so silver and gold mined unknown never share a stack.
        private ItemDefinition DisguiseFor(ItemDefinition real, VoxelMaterial material)
        {
            if (real == null)
            {
                return unknownOre;
            }
            if (!disguises.TryGetValue(real, out ItemDefinition disguise))
            {
                disguise = unknownOre.Variant("unknown-" + real.Id);
                disguises.Add(real, disguise);
                hiddenAs.Add(disguise, (real, material));
            }
            return disguise;
        }

        private bool Reached(HiddenOres group)
        {
            return skills != null && skills.Skills.Has(skill, group.Level);
        }

        private HiddenOres GroupOf(VoxelMaterial material)
        {
            if (material == null)
            {
                return null;
            }
            foreach (HiddenOres group in groups)
            {
                if (group.Materials.Contains(material))
                {
                    return group;
                }
            }
            return null;
        }

        private void OnLevelReached(SkillType raised, int reached)
        {
            if (raised != skill)
            {
                return;
            }
            foreach (HiddenOres group in groups)
            {
                if (reached >= group.Level)
                {
                    SetHidden(group, false);
                }
            }
            Identify();
        }

        private void SetHidden(HiddenOres group, bool hide)
        {
            if (registry == null || hide == hiding.Contains(group))
            {
                return;
            }
            if (hide)
            {
                hiding.Add(group);
            }
            else
            {
                hiding.Remove(group);
            }
            foreach (VoxelMaterial material in group.Materials)
            {
                registry.ShowAs(material, hide ? shownAs : null);
            }
        }

        // Every "? ore" stack whose ore can now be told apart becomes that ore: a per-ore copy by what it
        // stands for (GL90), the generic one (stacks from before) by what its contents hold most of.
        private void Identify()
        {
            if (inventory == null || inventory.Inventory == null || unknownOre == null)
            {
                return;
            }
            Inventory items = inventory.Inventory;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (stack.Item != null && hiddenAs.TryGetValue(stack.Item, out var real))
                {
                    if (Knows(real.Material))
                    {
                        int known = items.RemoveAt(slot, stack.Count);
                        items.Add(real.Item, known, stack.Contents);
                    }
                    continue;
                }
                if (stack.Item != unknownOre)
                {
                    continue;
                }
                VoxelMaterial ore = OreIn(stack.Contents);
                if (ore == null || !Knows(ore))
                {
                    continue;
                }
                int count = items.RemoveAt(slot, stack.Count);
                items.Add(ore.Drop, count, stack.Contents);
            }
        }

        // The hidden material the contents hold most of; with no contents, the first that drops anything.
        private VoxelMaterial OreIn(Composition contents)
        {
            VoxelMaterial best = null;
            VoxelMaterial first = null;
            float most = 0f;
            foreach (HiddenOres group in groups)
            {
                foreach (VoxelMaterial material in group.Materials)
                {
                    if (material == null || material.Drop == null)
                    {
                        continue;
                    }
                    first ??= material;
                    float share = contents != null ? contents.FractionOf(material) : 0f;
                    if (share > most)
                    {
                        most = share;
                        best = material;
                    }
                }
            }
            return best ?? first;
        }
    }
}
