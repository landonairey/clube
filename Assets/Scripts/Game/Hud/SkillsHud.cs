using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's skills on screen (GL47): each skill's level and a bar to the next in the top
    /// right while walking, and a notice in the middle when one goes up, naming what it unlocks:
    /// the recipes listed here that need that level (GL48), and the ore it reveals (GL54).
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class SkillsHud : MonoBehaviour
    {
        private const float NoticeSeconds = 5f;
        private const float Width = 210f;
        private const float RowHeight = 22f;

        private static readonly SkillType[] Shown = { SkillType.Crafting, SkillType.Woodcutting, SkillType.Mining, SkillType.Smelting };

        [Tooltip("Recipes to name when the level they need is reached.")]
        [SerializeField]
        private List<Recipe> unlocks = new List<Recipe>();

        private PlayerSkills skills;
        private PlayerController player;
        private OreKnowledge knowledge;
        private string notice;
        private float noticeUntil;
        private GUIStyle label;
        private GUIStyle noticeStyle;

        private void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            player = GetComponent<PlayerController>();
            knowledge = GetComponent<OreKnowledge>();
        }

        private void OnEnable()
        {
            skills.Skills.LevelReached += OnLevelReached;
        }

        private void OnDisable()
        {
            skills.Skills.LevelReached -= OnLevelReached;
        }

        private void OnLevelReached(SkillType skill, int level)
        {
            var unlocked = new List<string>();
            foreach (Recipe recipe in unlocks)
            {
                if (recipe != null && recipe.UnlockSkill == skill && recipe.UnlockLevel == level && recipe.Output.Item != null)
                {
                    unlocked.Add(recipe.Output.Item.DisplayName);
                }
            }
            if (knowledge != null && knowledge.Skill == skill && knowledge.RevealLevel == level)
            {
                unlocked.Add("you can tell copper ore apart");
            }
            notice = $"{skill} level {level}!" + (unlocked.Count > 0 ? "  Unlocked: " + string.Join(", ", unlocked) : "");
            noticeUntil = Time.time + NoticeSeconds;
        }

        private void OnGUI()
        {
            if (player != null && !player.enabled)
            {
                return;
            }
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            noticeStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.9f, 0.45f) },
            };

            float x = Screen.width - Width - 12f;
            float y = 12f;
            GUI.Box(new Rect(x - 6f, y - 4f, Width + 12f, Shown.Length * RowHeight + 8f), GUIContent.none);
            foreach (SkillType skill in Shown)
            {
                int level = skills.Skills.Level(skill);
                float from = SkillSet.ExperienceFor(level);
                float to = SkillSet.ExperienceFor(level + 1);
                float xp = skills.Skills.Experience(skill);
                float progress = to > from ? Mathf.Clamp01((xp - from) / (to - from)) : 1f;
                GUI.Label(new Rect(x, y, 120f, RowHeight), $"{skill} {level}", label);
                var bar = new Rect(x + 110f, y + 7f, Width - 110f, 8f);
                GUI.Box(bar, GUIContent.none);
                Color previous = GUI.color;
                GUI.color = new Color(0.45f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
                GUI.color = previous;
                y += RowHeight;
            }

            if (notice != null && Time.time < noticeUntil)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 30f), notice, noticeStyle);
            }
        }
    }
}
