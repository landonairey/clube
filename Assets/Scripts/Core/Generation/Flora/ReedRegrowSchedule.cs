using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// When picked reed clumps grow back (GL879): each picked clump comes back after a random span of game days
    /// (<see cref="MinDays"/> to <see cref="MaxDays"/>). Plain data: the cell of the clump (the bush grid's) and the game
    /// day it is due, kept in the save's named values through <see cref="Encode"/> and <see cref="Decode"/>. Who rolls the
    /// span, reads the clock and puts the clump back is the game side (<c>ReedRegrower</c>).
    /// </summary>
    public sealed class ReedRegrowSchedule
    {
        /// <summary>Fewest game days before a picked clump is back.</summary>
        public const float MinDays = 1f;

        /// <summary>Most game days before a picked clump is back.</summary>
        public const float MaxDays = 3f;

        private readonly Dictionary<Vector2Int, double> due = new Dictionary<Vector2Int, double>();

        /// <summary>How many clumps are waiting to grow back.</summary>
        public int Count => due.Count;

        /// <summary>The span (days) for a roll of 0-1: 1 to 3 days.</summary>
        public static double SpanFor(float roll) => MinDays + Mathf.Clamp01(roll) * (MaxDays - MinDays);

        /// <summary>A clump was picked on day <paramref name="now"/>: it is due back after the span for <paramref name="roll"/>.</summary>
        public void Picked(Vector2Int cell, double now, float roll)
        {
            due[cell] = now + SpanFor(roll);
        }

        /// <summary>Takes out and returns (into <paramref name="result"/>) every clump due by day <paramref name="now"/>.</summary>
        public void TakeDue(double now, List<Vector2Int> result)
        {
            result.Clear();
            foreach (KeyValuePair<Vector2Int, double> entry in due)
            {
                if (entry.Value <= now)
                {
                    result.Add(entry.Key);
                }
            }
            foreach (Vector2Int cell in result)
            {
                due.Remove(cell);
            }
        }

        /// <summary>The day a clump is due back, or false when it isn't waiting.</summary>
        public bool TryGetDue(Vector2Int cell, out double day) => due.TryGetValue(cell, out day);

        public void Clear() => due.Clear();

        /// <summary>The schedule as text for the save: "x,y,day;x,y,day".</summary>
        public string Encode()
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<Vector2Int, double> entry in due)
            {
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.Append(entry.Key.x.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.Key.y.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>Replaces the schedule with a saved one; entries that don't read are skipped.</summary>
        public void Decode(string text)
        {
            due.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string entry in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(',');
                if (parts.Length == 3
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                    && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double day))
                {
                    due[new Vector2Int(x, y)] = day;
                }
            }
        }
    }
}
