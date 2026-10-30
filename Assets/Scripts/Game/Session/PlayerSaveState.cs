using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's part of a save (GL205, S3): gathers what the player's components hold into a
    /// <see cref="PlayerSave"/> and puts it back. Where they stand and look, the inventory and hotbar slot,
    /// coins, skills, the mining journal and land. Each component keeps its own state; this only reads and
    /// writes it, so none of them knows about saving.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSaveState : MonoBehaviour
    {
        private PlayerController controller;
        private PlayerSpawn spawn;
        private PlayerInventory inventory;
        private Hotbar hotbar;
        private PlayerWallet wallet;
        private PlayerSkills skills;
        private PlayerJournal journal;
        private PlayerParcels parcels;
        private OreKnowledge ores;
        private PlayerQuests quests;

        /// <summary>Makes the hidden ores' "? ore" items again for a loaded game; null when the player has none.</summary>
        public OreKnowledge Ores => ores;

        /// <summary>True once the player's components are set up (the player has been switched on).</summary>
        public bool IsReady => inventory != null && inventory.Inventory != null;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            spawn = GetComponent<PlayerSpawn>();
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            wallet = GetComponent<PlayerWallet>();
            skills = GetComponent<PlayerSkills>();
            journal = GetComponent<PlayerJournal>();
            parcels = GetComponent<PlayerParcels>();
            ores = GetComponent<OreKnowledge>();
            quests = GetComponent<PlayerQuests>();
        }

        public PlayerSave Capture()
        {
            var save = new PlayerSave
            {
                Position = transform.position,
                Yaw = transform.eulerAngles.y,
                Pitch = controller.Head != null ? Mathf.DeltaAngle(0f, controller.Head.localEulerAngles.x) : 0f,
            };
            if (inventory != null)
            {
                save.Slots = StackSaving.Capture(inventory.Inventory);
            }
            if (hotbar != null)
            {
                save.HotbarSelected = hotbar.Selected;
            }
            if (wallet != null && wallet.Wallet != null)
            {
                save.Coins = wallet.Wallet.Coins;
            }
            if (skills != null)
            {
                foreach (SkillType skill in Enum.GetValues(typeof(SkillType)))
                {
                    float experience = skills.Skills.Experience(skill);
                    if (skill != SkillType.None && experience > 0f)
                    {
                        save.Skills.Add(new SkillSave { Skill = skill.ToString(), Experience = experience });
                    }
                }
            }
            if (journal != null)
            {
                foreach (MiningJournal.Entry entry in journal.Journal.Entries)
                {
                    save.Ores.Add(new OreEntrySave
                    {
                        Material = entry.Ore.Id,
                        FirstDepth = entry.FirstDepth,
                        LowestGrade = entry.LowestGrade,
                        HighestGrade = entry.HighestGrade,
                        Samples = entry.Samples,
                    });
                }
                foreach (Recipe recipe in journal.Journal.Made)
                {
                    save.MadeRecipes.Add(recipe.name);
                }
            }
            if (parcels != null && parcels.Parcels != null)
            {
                save.Parcels.AddRange(parcels.Parcels.Owned);
            }
            if (quests != null)
            {
                save.Quests.AddRange(quests.Log.DoneIds);
            }
            return save;
        }

        /// <summary>Puts a save back on the player, who then waits for the ground under the saved position to load.</summary>
        public void Restore(PlayerSave save, ItemLookup items, MaterialRegistry materials)
        {
            // Skills first: what the player knows decides how some items show (OreKnowledge).
            if (skills != null)
            {
                foreach (SkillSave skill in save.Skills)
                {
                    if (Enum.TryParse(skill.Skill, out SkillType type))
                    {
                        skills.Skills.SetExperience(type, skill.Experience);
                    }
                }
            }
            if (inventory != null)
            {
                StackSaving.Restore(inventory.Inventory, save.Slots, items.Find, materials);
            }
            if (hotbar != null)
            {
                hotbar.Select(save.HotbarSelected);
            }
            if (wallet != null && wallet.Wallet != null)
            {
                float change = save.Coins - wallet.Wallet.Coins;
                if (change > 0f)
                {
                    wallet.Wallet.Earn(change);
                }
                else if (change < 0f)
                {
                    wallet.Wallet.Spend(-change);
                }
            }
            if (journal != null && materials != null)
            {
                foreach (OreEntrySave ore in save.Ores)
                {
                    journal.Journal.Restore(materials.Get(ore.Material), ore.FirstDepth, ore.LowestGrade, ore.HighestGrade, ore.Samples);
                }
                var recipes = new Dictionary<string, Recipe>();
                foreach (Recipe recipe in journal.Recipes)
                {
                    if (recipe != null)
                    {
                        recipes[recipe.name] = recipe;
                    }
                }
                foreach (string name in save.MadeRecipes)
                {
                    if (recipes.TryGetValue(name, out Recipe recipe))
                    {
                        journal.Journal.RecordMade(recipe);
                    }
                }
            }
            if (parcels != null)
            {
                parcels.Restore(save.Parcels);
            }
            if (quests != null)
            {
                quests.Log.Restore(save.Quests);
            }

            // Facing as saved; PlayerController reads its look from these when it's switched on.
            transform.rotation = Quaternion.Euler(0f, save.Yaw, 0f);
            if (controller.Head != null)
            {
                controller.Head.localRotation = Quaternion.Euler(save.Pitch, 0f, 0f);
            }
            if (spawn != null)
            {
                spawn.PlaceAt(save.Position);
            }
            else
            {
                controller.Teleport(save.Position);
            }
        }
    }
}
