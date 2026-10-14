using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Ore the player can't tell apart yet (GL54, first pass of PS6): until a skill reaches a
    /// level, the listed materials (chalcopyrite and malachite, with their cracked and loose
    /// stages) are drawn as the rock around them (<see cref="MaterialRegistry.ShowAs"/>), and
    /// what the player mines of them comes out as "? ore", keeping its contents. At that level
    /// the ore shows, and every "? ore" in the inventory turns into the ore it is, by the largest
    /// of the listed materials in its contents.
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class OreKnowledge : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Materials hidden until the level below: shown as the material after.")]
        [SerializeField]
        private List<VoxelMaterial> hidden = new List<VoxelMaterial>();

        [Tooltip("What hidden materials look like until then (the host rock).")]
        [SerializeField]
        private VoxelMaterial shownAs;

        [SerializeField]
        private SkillType skill = SkillType.Mining;

        [SerializeField, Range(0, SkillSet.MaxLevel)]
        private int level = 3;

        [Tooltip("What mining a hidden material gives until then.")]
        [SerializeField]
        private ItemDefinition unknownOre;

        private PlayerSkills skills;
        private PlayerInventory inventory;
        private MaterialRegistry registry;
        private bool hiding;

        /// <summary>True once the player can tell the hidden materials apart.</summary>
        public bool Knows => skills != null && skills.Skills.Has(skill, level);

        /// <summary>The skill and level that reveal the ore, for notices.</summary>
        public SkillType Skill => skill;

        public int RevealLevel => level;

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
                SetHidden(!Knows);
            }
        }

        private void OnDestroy()
        {
            // The registry is an asset: leave it showing every material as itself.
            SetHidden(false);
        }

        /// <summary>A mined stack as the player gets it: "? ore" when its material is still hidden.</summary>
        public ItemStack Disguise(ItemStack stack, VoxelMaterial material)
        {
            if (unknownOre == null || stack.IsEmpty || Knows || material == null || !hidden.Contains(material))
            {
                return stack;
            }
            return new ItemStack(unknownOre, stack.Count, stack.Contents);
        }

        private void OnLevelReached(SkillType raised, int reached)
        {
            if (raised == skill && reached >= level)
            {
                SetHidden(false);
                Identify();
            }
        }

        private void SetHidden(bool hide)
        {
            if (registry == null || hide == hiding)
            {
                return;
            }
            hiding = hide;
            foreach (VoxelMaterial material in hidden)
            {
                registry.ShowAs(material, hide ? shownAs : null);
            }
        }

        // Every "? ore" stack becomes the ore its contents hold most of.
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
                if (stack.Item != unknownOre)
                {
                    continue;
                }
                ItemDefinition ore = OreIn(stack.Contents);
                if (ore == null)
                {
                    continue;
                }
                int count = items.RemoveAt(slot, stack.Count);
                items.Add(ore, count, stack.Contents);
            }
        }

        private ItemDefinition OreIn(Composition contents)
        {
            VoxelMaterial best = null;
            float most = 0f;
            foreach (VoxelMaterial material in hidden)
            {
                float share = contents != null ? contents.FractionOf(material) : 0f;
                if (share > most && material.Drop != null)
                {
                    most = share;
                    best = material;
                }
            }
            if (best == null)
            {
                // No contents to go by: the first hidden ore that drops anything.
                best = hidden.Find(material => material != null && material.Drop != null);
            }
            return best != null ? best.Drop : null;
        }
    }
}
