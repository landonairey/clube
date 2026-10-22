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

            float width = Mathf.Min(Screen.width - 60f, 1120f);
            float height = Mathf.Min(Screen.height - 60f, 700f);
            var book = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
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
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), heading, Title);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), $"p. {page + 1}", new GUIStyle(Small) { alignment = TextAnchor.UpperRight });
            return inner.y + 36f;
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
            var area = new Rect(Screen.width * 0.5f - 200f, 70f, 400f, 30f);
            InventoryGridGui.Panel(area);
            GUI.Label(area, notice, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic });
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

        // The styles are made inside OnGUI, where the skin exists.
        private static GUIStyle EnsureStyles(ref GUIStyle style)
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.BoldAndItalic, normal = { textColor = Ink } };
                text = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, wordWrap = true, normal = { textColor = Ink } };
                small = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic, normal = { textColor = new Color(Ink.r, Ink.g, Ink.b, 0.7f) } };
            }
            return style;
        }
    }
}
