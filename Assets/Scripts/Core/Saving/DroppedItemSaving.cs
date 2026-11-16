using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A dropped item lying in the world (GL610): what it is and where it rests.</summary>
    public struct DroppedSave
    {
        public StackSave Stack;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Dropped items as one line of text each (GL610), for a save's named values: the stack (item id, count, part, wear,
    /// quality), where it rests (position, rotation) and what it's made of (material id and fraction pairs). Fields are
    /// split by '|', so an item id must not hold one; a line that doesn't read is skipped.
    /// </summary>
    public static class DroppedItemSaving
    {
        private const char Field = '|';
        private const char Line = '\n';
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        public static string Write(IEnumerable<DroppedSave> items)
        {
            var text = new StringBuilder();
            foreach (DroppedSave item in items)
            {
                if (item.Stack.IsEmpty)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(Line);
                }
                text.Append(item.Stack.Item).Append(Field).Append(item.Stack.Count.ToString(Plain));
                Append(text, item.Stack.Part);
                text.Append(Field).Append(item.Stack.Wear.ToString(Plain));
                Append(text, item.Stack.Quality);
                Append(text, item.Position.x);
                Append(text, item.Position.y);
                Append(text, item.Position.z);
                Append(text, item.Rotation.x);
                Append(text, item.Rotation.y);
                Append(text, item.Rotation.z);
                Append(text, item.Rotation.w);
                text.Append(Field);
                if (item.Stack.Contents != null)
                {
                    for (int i = 0; i < item.Stack.Contents.Count; i++)
                    {
                        ContentSave part = item.Stack.Contents[i];
                        text.Append(i > 0 ? "," : string.Empty).Append(part.Material.ToString(Plain)).Append(':').Append(part.Fraction.ToString("R", Plain));
                    }
                }
            }
            return text.ToString();
        }

        public static List<DroppedSave> Read(string text)
        {
            var items = new List<DroppedSave>();
            if (string.IsNullOrEmpty(text))
            {
                return items;
            }
            foreach (string line in text.Split(Line))
            {
                if (TryRead(line, out DroppedSave item))
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private static bool TryRead(string line, out DroppedSave item)
        {
            item = default;
            string[] f = line.Split(Field);
            var n = new float[10];
            if (f.Length < 13 || f[0].Length == 0 || !int.TryParse(f[1], NumberStyles.Integer, Plain, out int count)
                || !int.TryParse(f[3], NumberStyles.Integer, Plain, out int wear))
            {
                return false;
            }
            // Part, quality, then position and rotation: fields 2, 4 and 5-11.
            int[] at = { 2, 4, 5, 6, 7, 8, 9, 10, 11 };
            for (int i = 0; i < at.Length; i++)
            {
                if (!float.TryParse(f[at[i]], NumberStyles.Float, Plain, out n[i]))
                {
                    return false;
                }
            }
            List<ContentSave> contents = null;
            foreach (string pair in f[12].Split(','))
            {
                string[] halves = pair.Split(':');
                if (halves.Length == 2 && byte.TryParse(halves[0], NumberStyles.Integer, Plain, out byte material)
                    && float.TryParse(halves[1], NumberStyles.Float, Plain, out float fraction))
                {
                    contents ??= new List<ContentSave>();
                    contents.Add(new ContentSave { Material = material, Fraction = fraction });
                }
            }
            item = new DroppedSave
            {
                Stack = new StackSave { Item = f[0], Count = count, Part = n[0], Wear = wear, Quality = n[1], Contents = contents },
                Position = new Vector3(n[2], n[3], n[4]),
                Rotation = new Quaternion(n[5], n[6], n[7], n[8]),
            };
            return true;
        }

        private static void Append(StringBuilder text, float value)
        {
            text.Append(Field).Append(value.ToString("R", Plain));
        }
    }
}
