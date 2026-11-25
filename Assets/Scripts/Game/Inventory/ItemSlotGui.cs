using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws one inventory slot with IMGUI (GL7): a dark square, the item's icon (or its
    /// name when it has none), its count, a bright border when selected, and an optional
    /// corner note such as a price. Shared by the
    /// <see cref="Hotbar"/> and the <see cref="InventoryScreen"/> so slots look the same in both.
    /// </summary>
    public static class ItemSlotGui
    {
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedBorder = new Color(1f, 0.85f, 0.3f, 1f);
        internal static readonly Color PurityColour = new Color(1f, 0.8f, 0.35f);

        private static GUIStyle nameStyle;
        private static GUIStyle countStyle;
        private static GUIStyle purityStyle;

        /// <summary>The terrain materials, for how pure a stack is (GL115); set by whoever has them, null shows no purity.</summary>
        public static MaterialRegistry Materials { get; set; }

        /// <summary>What the merchant pays, for tooltips (GL366); set by the merchant table when it's placed.</summary>
        public static PriceList Prices { get; set; }

        /// <summary>
        /// The player's Prospecting level, for how much of a gathered stack they can read (GL457); set by
        /// <c>PlayerSkills</c>. Exact shares until something sets it (the labs).
        /// </summary>
        public static int ProspectingLevel { get; set; } = ProspectReading.ExactLevel;

        /// <summary>True when a stack's shares read as grades, not numbers (GL457): a gathered mix below Prospecting 10.</summary>
        public static bool ReadsAsGrades(ItemStack stack)
        {
            // Better than ordinary (maple, GL151) reads as a grade too (GL564): above 100% is PURE, not "140%".
            return !stack.IsEmpty && (stack.Contents != null || stack.Quality > 1.005f) && ProspectingLevel < ProspectReading.ExactLevel;
        }

        // Every stack shows how well it was made, in the upper right, 100% included (GL158): its quality
        // (GL118: sticks of 60% wood), or for mined material that isn't all its own material, how much of it
        // is (GL115: ore of a grade).
        /// <summary>The text in a stack's upper right corner, and its colour (the ordinary gold unless it is a grade, GL457).</summary>
        internal static string PurityLabel(ItemStack stack, out Color colour)
        {
            float purity = stack.Quality;
            if (purity >= 0.995f && purity <= 1.005f && Materials != null && stack.Contents != null && !stack.Item.HidesContents && Materials.IsMaterialItem(stack.Item))
            {
                purity = Materials.Quality(stack.Contents, stack.Item);
            }
            // Sticks always read as a grade (GL795).
            if (ReadsAsGrades(stack) || stack.Item.QualityAsGrade)
            {
                // A grade, in its colour, until Prospecting reads numbers (GL457).
                Grade grade = ProspectReading.GradeOf(purity);
                colour = ProspectReading.ColourOf(grade);
                return ProspectReading.ShortName(grade);
            }
            colour = PurityColour;
            return QualityText.Percent(purity);
        }

        /// <summary>True when a slot shows the stack's quality in its corner: everything but tools, whose durability is the bar (GL795).</summary>
        internal static bool ShowsPurity(ItemStack stack)
        {
            return !stack.IsEmpty && !(stack.Item is ToolDefinition);
        }

        /// <summary>The count in a stack's lower right corner: a part item's amount (GL136), a stack's count, else null.</summary>
        internal static string CountLabel(ItemStack stack)
        {
            // A part item (GL136): 0.48 of a tin bun.
            return stack.Part > 0f ? stack.Amount.ToString("0.##") : stack.Count > 1 ? stack.Count.ToString() : null;
        }

        /// <summary>
        /// The wear bar along a stack's bottom (GL118, GL286): how much of the tool's full hits is left, and how much of the
        /// bar repairs have lost for good (grey at its end). False when the stack shows none.
        /// </summary>
        internal static bool WearBar(ItemStack stack, out float left, out float best)
        {
            left = best = 1f;
            // Only below 100% durability (GL795): a full tool shows just its icon.
            if (!ToolDurability.ShowsBar(stack))
            {
                return false;
            }
            // Against its full hits, so a tool of 60% quality starts with 60% of the bar (GL118).
            left = Mathf.Clamp01((float)(stack.MaxHits - stack.Wear) / ((ToolDefinition)stack.Item).MaxHits);
            best = Mathf.Clamp01((float)stack.MaxHits / ((ToolDefinition)stack.Item).MaxHits);
            return true;
        }

        private static void DrawPurity(Rect rect, ItemStack stack)
        {
            // No padding or clipping: the skin's label padding cut the bottom off the digits (GL222).
            purityStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 20, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow, normal = { textColor = PurityColour } };
            var corner = new Rect(rect.x, rect.y + 3f, rect.width - 6f, 26f);
            string label = PurityLabel(stack, out Color colour);
            purityStyle.normal.textColor = colour;
            GameUi.OutlinedLabel(corner, label, purityStyle);
            purityStyle.normal.textColor = PurityColour;
        }

        /// <summary>
        /// Draws a stack's icon in its <see cref="ItemStack.IconTint"/> (item 11: a birch stick pale, a maple one red-brown);
        /// most stacks draw as their icon is.
        /// </summary>
        public static void DrawIcon(Rect rect, ItemStack stack)
        {
            if (stack.IsEmpty || stack.Item.Icon == null)
            {
                return;
            }
            Color previous = GUI.color;
            GUI.color = previous * stack.IconTint;
            GUI.DrawTexture(rect, stack.Item.Icon, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        /// <param name="corner">Small text for the lower left, e.g. a merchant's price; null for none.</param>
        public static void Draw(Rect rect, ItemStack stack, bool selected, string key = null, string corner = null)
        {
            EnsureStyles();
            Fill(rect, Background);
            Outline(rect, selected ? SelectedBorder : Border, selected ? 2f : 1f);

            if (!stack.IsEmpty)
            {
                Rect inner = Inset(rect, 6f);
                if (stack.Item.Icon != null)
                {
                    DrawIcon(inner, stack);
                }
                else
                {
                    GUI.Label(inner, stack.Name, nameStyle);
                }
                string countText = CountLabel(stack);
                if (countText != null)
                {
                    GameUi.OutlinedLabel(Inset(rect, 3f), countText, countStyle);
                }
                if (ShowsPurity(stack))
                {
                    DrawPurity(rect, stack);
                }
                if (stack.Item is BucketDefinition bucket)
                {
                    // The water in a bucket (GL319): a blue bar, and the units in the corner.
                    float full = (float)bucket.WaterIn(stack) / bucket.CapacityOf(stack);
                    Fill(new Rect(rect.x + 4f, rect.yMax - 12f, rect.width - 8f, 7f), new Color(0.2f, 0.2f, 0.22f));
                    Fill(new Rect(rect.x + 4f, rect.yMax - 12f, (rect.width - 8f) * full, 7f), new Color(0.3f, 0.6f, 0.85f));
                    GUI.Label(Inset(rect, 3f), bucket.WaterIn(stack).ToString(), countStyle);
                }
                if (WearBar(stack, out float left, out float best))
                {
                    // Wear bar along the bottom: green when fresh, red when nearly gone. What repairs have lost for good
                    // (GL286) shows grey at the bar's end: it can't be mended back.
                    if (best < 0.995f)
                    {
                        float width = rect.width - 8f;
                        Fill(new Rect(rect.x + 4f + width * best, rect.yMax - 12f, width * (1f - best), 7f), new Color(0.45f, 0.45f, 0.47f));
                    }
                    var bar = new Rect(rect.x + 4f, rect.yMax - 12f, (rect.width - 8f) * left, 7f);
                    Fill(bar, Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.35f, 0.8f, 0.25f), left));
                }
            }

            if (key != null)
            {
                GUI.Label(new Rect(rect.x + 5f, rect.y + 2f, 30f, 26f), key);
            }
            if (!string.IsNullOrEmpty(corner))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(rect.x + 5f, rect.yMax - 30f, rect.width - 10f, 26f), corner);
                GUI.color = previous;
            }
        }

        /// <summary>
        /// An item drawn greyed out over an empty slot (GL58): what a station is about to make.
        /// Draw it after the slot itself.
        /// </summary>
        public static void Ghost(Rect rect, ItemDefinition item, int count, Color? tint = null)
        {
            if (item == null)
            {
                return;
            }
            EnsureStyles();
            Color previous = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.4f) * (tint ?? Color.white);
            Rect inner = Inset(rect, 6f);
            if (item.Icon != null)
            {
                GUI.DrawTexture(inner, item.Icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(inner, item.DisplayName, nameStyle);
            }
            if (count > 1)
            {
                GUI.Label(Inset(rect, 3f), count.ToString(), countStyle);
            }
            GUI.color = previous;
        }

        private static void EnsureStyles()
        {
            if (nameStyle != null)
            {
                return;
            }
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 20,
            };
            countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontStyle = FontStyle.Bold,
                fontSize = 24,
            };
        }

        internal static Rect Inset(Rect rect, float by)
        {
            return new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);
        }

        internal static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static void Outline(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
