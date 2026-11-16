using System;
using System.Text;

namespace Clube.Core
{
    /// <summary>
    /// Which pieces of something written into the terrain a piece at a time are done (a road, a tunnel), as text for a
    /// save: the piece count, a colon, and hex digits of four pieces each.
    /// </summary>
    public static class PieceFlags
    {
        public static string Save(bool[] written)
        {
            var text = new StringBuilder();
            text.Append(written.Length).Append(':');
            for (int i = 0; i < written.Length; i += 4)
            {
                int nibble = 0;
                for (int k = 0; k < 4 && i + k < written.Length; k++)
                {
                    nibble |= written[i + k] ? 1 << k : 0;
                }
                text.Append("0123456789abcdef"[nibble]);
            }
            return text.ToString();
        }

        /// <summary>Marks the pieces a save says are written and returns how many were newly marked; text for another count of pieces is ignored.</summary>
        public static int Restore(string text, bool[] written)
        {
            int colon = string.IsNullOrEmpty(text) ? -1 : text.IndexOf(':');
            if (colon < 0 || !int.TryParse(text.Substring(0, colon), out int pieces) || pieces != written.Length
                || text.Length - colon - 1 != (written.Length + 3) / 4)
            {
                return 0;
            }
            int marked = 0;
            for (int i = 0; i < written.Length; i++)
            {
                int nibble = Convert.ToInt32(text[colon + 1 + i / 4].ToString(), 16);
                if ((nibble & (1 << (i % 4))) != 0 && !written[i])
                {
                    written[i] = true;
                    marked++;
                }
            }
            return marked;
        }
    }
}
