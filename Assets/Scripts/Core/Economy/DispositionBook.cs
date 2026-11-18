using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Clube.Core
{
    /// <summary>
    /// How every shopkeeper of the map feels about the player (GL729), by settlement and shop (indexes into the world
    /// plan), so a keeper's <see cref="Disposition"/> outlives its shop being built and lets goods stolen on their way to
    /// a shop not yet in the world count against it. A keeper nobody has dealt with is at its settlement's start (<see cref="StartOf"/>)
    /// and is not written. Saves as one line of text.
    /// </summary>
    public sealed class DispositionBook
    {
        private readonly Dictionary<long, Disposition> keepers = new Dictionary<long, Disposition>();

        /// <summary>
        /// GL734: where the keepers of a settlement start with the player (the tutorial's quarry at 0), by settlement index;
        /// null for <see cref="Disposition.Start"/> everywhere. Set before any keeper is asked for.
        /// </summary>
        public System.Func<int, int> StartOf { get; set; }

        private int StartFor(int settlement) => StartOf != null ? StartOf(settlement) : Disposition.Start;

        /// <summary>Keepers written so far.</summary>
        public int Count => keepers.Count;

        /// <summary>The disposition of the keeper of shop <paramref name="shop"/> of settlement <paramref name="settlement"/>; made on first asking.</summary>
        public Disposition Of(int settlement, int shop)
        {
            long key = Key(settlement, shop);
            if (!keepers.TryGetValue(key, out Disposition disposition))
            {
                disposition = new Disposition(StartFor(settlement));
                keepers[key] = disposition;
            }
            return disposition;
        }

        /// <summary>The keepers away from where they started, as "settlement,shop,value,carried;...".</summary>
        public string Save()
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<long, Disposition> pair in keepers)
            {
                if (pair.Value.Value == StartFor((int)(pair.Key >> 32)) && pair.Value.Carried <= 0f)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:R}", (int)(pair.Key >> 32), (int)(pair.Key & 0xffffffffL), pair.Value.Value, pair.Value.Carried);
            }
            return text.ToString();
        }

        /// <summary>Puts back what <see cref="Save"/> wrote; keepers it does not name go back to the start. Bad entries are skipped.</summary>
        public void Restore(string saved)
        {
            foreach (KeyValuePair<long, Disposition> pair in keepers)
            {
                pair.Value.Restore(StartFor((int)(pair.Key >> 32)), 0f);
            }
            if (string.IsNullOrEmpty(saved))
            {
                return;
            }
            foreach (string entry in saved.Split(';'))
            {
                string[] parts = entry.Split(',');
                if (parts.Length == 4
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int settlement)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int shop)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float carried))
                {
                    Of(settlement, shop).Restore(value, carried);
                }
            }
        }

        private static long Key(int settlement, int shop)
        {
            return ((long)settlement << 32) | (uint)shop;
        }
    }
}
