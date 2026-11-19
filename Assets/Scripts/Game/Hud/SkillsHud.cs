using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

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
        private const float Width = 400f;
        private const float RowHeight = 36f;
        private const float LabelWidth = 190f;
        private static readonly Color PanelColour = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        private static readonly SkillType[] Shown = { SkillType.Crafting, SkillType.Woodcutting, SkillType.Mining, SkillType.Smelting, SkillType.Blacksmithing, SkillType.Prospecting, SkillType.Goldsmithing };

        [Tooltip("Recipes to name when the level they need is reached.")]
        [SerializeField]
        private List<Recipe> unlocks = new List<Recipe>();

        private PlayerSkills skills;
        private PlayerController player;
        private OreKnowledge knowledge;
        private string notice;
        private float noticeUntil;
        private RectTransform panel;
        private Text[] labels;
        private Image[] fills;
        private int[] shownLevels;
        private float[] shownProgress;
        private Text noticeText;

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
            if (knowledge != null)
            {
                foreach (string ores in knowledge.RevealedAt(skill, level))
                {
                    unlocked.Add($"you can tell {ores} apart");
                }
            }
            if (skill == SkillType.Prospecting && ProspectReading.UnlockAt(level) != null)
            {
                unlocked.Add(ProspectReading.UnlockAt(level));
            }
            notice = $"{skill} level {level}!" + (unlocked.Count > 0 ? "  Unlocked: " + string.Join(", ", unlocked) : "");
            noticeUntil = Time.time + NoticeSeconds;
        }

        // On the HUD canvas (GL777): built once, and a row or the notice is touched only when it changes.
        private void LateUpdate()
        {
            bool visible = player == null || player.enabled;
            if (panel == null)
            {
                Build();
            }
            HudCanvas.SetShown(panel.gameObject, visible);
            if (!visible)
            {
                HudCanvas.SetShown(noticeText.gameObject, false);
                return;
            }
            for (int i = 0; i < Shown.Length; i++)
            {
                SkillType skill = Shown[i];
                int level = skills.Skills.Level(skill);
                float from = SkillSet.ExperienceFor(level);
                float to = SkillSet.ExperienceFor(level + 1);
                float xp = skills.Skills.Experience(skill);
                float progress = to > from ? Mathf.Clamp01((xp - from) / (to - from)) : 1f;
                if (level != shownLevels[i])
                {
                    shownLevels[i] = level;
                    labels[i].text = $"{skill} {level}";
                }
                if (progress != shownProgress[i])
                {
                    shownProgress[i] = progress;
                    HudCanvas.Place(fills[i].rectTransform, 0f, 0f, (Width - LabelWidth) * progress, 18f);
                }
            }
            bool noticeUp = notice != null && Time.time < noticeUntil;
            HudCanvas.SetShown(noticeText.gameObject, noticeUp);
            if (noticeUp)
            {
                HudCanvas.SetText(noticeText, notice);
            }
        }

        private void Build()
        {
            panel = HudCanvas.Fill(HudCanvas.Root, "Skills", PanelColour).rectTransform;
            labels = new Text[Shown.Length];
            fills = new Image[Shown.Length];
            shownLevels = new int[Shown.Length];
            shownProgress = new float[Shown.Length];
            for (int i = 0; i < Shown.Length; i++)
            {
                shownLevels[i] = -1;
                shownProgress[i] = -1f;
                // Rows are placed from the panel's corner, 6 and 4 pixels in from the box edge.
                labels[i] = HudCanvas.Label(panel, "Label", GameUi.Font(13), FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
                HudCanvas.Place(labels[i].rectTransform, 6f, 4f + i * RowHeight, LabelWidth, RowHeight);
                Image back = HudCanvas.Fill(panel, "Bar", PanelColour);
                HudCanvas.Place(back.rectTransform, 6f + LabelWidth, 4f + i * RowHeight + 9f, Width - LabelWidth, 18f);
                fills[i] = HudCanvas.Fill(back.transform, "Fill", new Color(0.45f, 0.85f, 0.4f));
            }
            noticeText = HudCanvas.Label(HudCanvas.Root, "SkillNotice", GameUi.Font(18), FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.45f));
            HudCanvas.Resized += Layout;
            Layout();
        }

        private void Layout()
        {
            if (panel == null)
            {
                return;
            }
            HudCanvas.Place(panel, Screen.width - Width - 12f - 6f, 12f - 4f, Width + 12f, Shown.Length * RowHeight + 8f);
            HudCanvas.Place(noticeText.rectTransform, 0f, Screen.height * 0.25f, Screen.width, 44f);
        }

        private void OnDestroy()
        {
            HudCanvas.Resized -= Layout;
            if (panel != null)
            {
                Destroy(panel.gameObject);
                Destroy(noticeText.gameObject);
            }
        }
    }
}
