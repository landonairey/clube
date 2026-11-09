using System;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// How the player's journals look (GL73, GL137): a field notebook open on two pages of graph paper,
    /// cream ruled in fine blue, with Back and Next under it when there are more pages. The mining and
    /// crafting journals draw their pages into it, so both read as the same kind of book.
    /// </summary>
    public static class JournalBook
    {
        public static readonly Color Ink = new Color(0.1f, 0.1f, 0.16f, 1f);

        private static readonly Color Paper = new Color(0.96f, 0.94f, 0.86f, 1f);
        private static readonly Color FineLine = new Color(0.66f, 0.79f, 0.9f, 1f);
        private static readonly Color BoldLine = new Color(0.5f, 0.66f, 0.84f, 1f);

        private static Texture2D graphPaper;
        private static GUIStyle title;
        private static GUIStyle text;
        private static GUIStyle small;

        /// <summary>Room left of the book for the field log's tabs (GL215).</summary>
        public const float TabWidth = 150f;

        /// <summary>Where the open book is on screen: centred in what's left beside the tabs.</summary>
        public static Rect Area
        {
            get
            {
                float width = Mathf.Min(Screen.width - 60f - TabWidth, 1120f);
                float height = Mathf.Min(Screen.height - 60f, 700f);
                return new Rect(TabWidth + (Screen.width - TabWidth - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            }
        }

        /// <summary>Paper, for a tab drawn like a page edge.</summary>
        public static Color PaperColor => Paper;

        /// <summary>A page or entry heading, in bold italic ink.</summary>
        public static GUIStyle Title => EnsureStyles(ref title);

        /// <summary>Notes, in italic ink, wrapped.</summary>
        public static GUIStyle Text => EnsureStyles(ref text);

        /// <summary>Page numbers and asides, in faded ink.</summary>
        public static GUIStyle Small => EnsureStyles(ref small);

        /// <summary>
        /// Draws the open book at <paramref name="spread"/> (two pages to a spread) with Back and Next
        /// under it, each page drawn by <paramref name="drawPage"/> (its area and number). Returns the
        /// spread to show next, moved by the buttons and kept in range.
        /// </summary>
        public static int Draw(int spread, int pages, Action<Rect, int> drawPage)
        {
            pages = Mathf.Max(1, pages);
            int spreads = (pages + 1) / 2;
            spread = Mathf.Clamp(spread, 0, spreads - 1);

            Rect book = Area;
            InventoryGridGui.Panel(new Rect(book.x - 10f, book.y - 10f, book.width + 20f, book.height + 20f));
            float pageWidth = book.width * 0.5f;
            for (int side = 0; side < 2; side++)
            {
                int page = spread * 2 + side;
                var area = new Rect(book.x + side * pageWidth, book.y, pageWidth, book.height);
                DrawPaper(area);
                if (page < pages)
                {
                    drawPage(area, page);
                }
            }
            // The fold between the pages.
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(book.x + pageWidth - 2f, book.y, 4f, book.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (spreads > 1)
            {
                GUI.enabled = spread > 0;
                if (GUI.Button(new Rect(book.x, book.yMax + 14f, 90f, 26f), "< Back"))
                {
                    spread--;
                }
                GUI.enabled = spread < spreads - 1;
                if (GUI.Button(new Rect(book.xMax - 90f, book.yMax + 14f, 90f, 26f), "Next >"))
                {
                    spread++;
                }
                GUI.enabled = true;
            }
            return spread;
        }

        /// <summary>The writing area of a page: inside its margins.</summary>
        public static Rect Inner(Rect page)
        {
            return new Rect(page.x + 28f, page.y + 18f, page.width - 48f, page.height - 36f);
        }

        /// <summary>A page's heading and its number in the corner; returns where the page's content starts.</summary>
        public static float Heading(Rect inner, string heading, int page)
        {
            FitLabel(new Rect(inner.x, inner.y, inner.width - 70f, 32f), heading, Title);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), $"p. {page + 1}", new GUIStyle(Small) { alignment = TextAnchor.UpperRight });
            return inner.y + 36f;
        }

        /// <summary>
        /// Puts every character of a page's text into the font's atlas before any of it is drawn (GL187). A dynamic
        /// font rebuilds its atlas when it meets new glyphs, and text IMGUI had already laid out against the old atlas
        /// could stay blank until the journal was closed and opened again. Call on the Layout event, before drawing.
        /// </summary>
        public static void Prepare(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Request(Title, text);
            Request(Text, text);
            Request(Small, text);
        }

        private static void Request(GUIStyle style, string text)
        {
            Font font = style.font != null ? style.font : GUI.skin.font;
            if (font != null && font.dynamic)
            {
                font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
            }
        }

        /// <summary>
        /// A label that shrinks its text until it fits the area (GL438: at the larger HUD font, titles and long notes ran
        /// past their boxes and were cut off). One line when the style doesn't wrap.
        /// </summary>
        public static void FitLabel(Rect area, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            fitted ??= new GUIStyle(style);
            fitted.font = style.font;
            fitted.fontStyle = style.fontStyle;
            fitted.wordWrap = style.wordWrap;
            fitted.alignment = style.alignment;
            fitted.padding = style.padding;
            fitted.normal.textColor = style.normal.textColor;
            int size = style.fontSize > 0 ? style.fontSize : GameUi.Font(13);
            int smallest = Mathf.Max(8, size / 2);
            do
            {
                fitted.fontSize = size--;
            }
            while (size >= smallest && !Fits(area, content, fitted));
            GUI.Label(area, content, fitted);
        }

        // The style FitLabel draws with, set to each call's style and size.
        private static GUIStyle fitted;

        private static bool Fits(Rect area, GUIContent content, GUIStyle style)
        {
            return style.wordWrap
                ? style.CalcHeight(content, area.width) <= area.height + 0.5f
                : style.CalcSize(content).x <= area.width + 0.5f && style.lineHeight + style.padding.vertical <= area.height + 0.5f;
        }

        /// <summary>A ruled ink line, e.g. between entries.</summary>
        public static void InkLine(Rect area)
        {
            Color previous = GUI.color;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>A short notice near the top of the screen (a new journal entry).</summary>
        public static void Notice(string notice)
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            // As wide as the text needs (a quest title can be long, GL216), at least 400.
            float width = Mathf.Min(Screen.width - 20f, Mathf.Max(400f, style.CalcSize(new GUIContent(notice)).x + 40f));
            var area = new Rect((Screen.width - width) * 0.5f, 70f, width, 30f);
            InventoryGridGui.Panel(area);
            GUI.Label(area, notice, style);
        }

        private static void DrawPaper(Rect area)
        {
            if (graphPaper == null)
            {
                graphPaper = PaintGraphPaper();
            }
            GUI.DrawTextureWithTexCoords(area, graphPaper, new Rect(0f, 0f, area.width / graphPaper.width, area.height / graphPaper.height));
        }

        // Cream paper ruled in a fine blue grid, bolder every fifth line.
        private static Texture2D PaintGraphPaper()
        {
            const int size = 80;
            const int cell = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Graph paper", hideFlags = HideFlags.DontSave };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Paper;
                    if (x % cell == 0 || y % cell == 0)
                    {
                        color = FineLine;
                    }
                    if (x == 0 || y == 0)
                    {
                        color = BoldLine;
                    }
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Gives a style one ink colour in every state (GL243): the skin's label turns white under the mouse, which on the
        /// pale page made the text vanish.
        /// </summary>
        public static GUIStyle Inked(GUIStyle style, Color colour)
        {
            style.normal.textColor = colour;
            style.hover.textColor = colour;
            style.active.textColor = colour;
            style.focused.textColor = colour;
            style.onNormal.textColor = colour;
            style.onHover.textColor = colour;
            style.onActive.textColor = colour;
            style.onFocused.textColor = colour;
            return style;
        }

        // The styles are made inside OnGUI, where the skin exists.
        private static GUIStyle EnsureStyles(ref GUIStyle style)
        {
            if (title == null)
            {
                title = Inked(new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(18), fontStyle = FontStyle.BoldAndItalic }, Ink);
                text = Inked(new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(13), fontStyle = FontStyle.Italic, wordWrap = true }, Ink);
                small = Inked(new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(12), fontStyle = FontStyle.Italic }, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            }
            return style;
        }
    }
}
