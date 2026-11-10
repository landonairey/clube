using System;

namespace Clube.Core
{
    /// <summary>
    /// How a quality or purity reads as a percentage (GL431): rounded down, so a stack reads 100% only when it is pure
    /// (99.85% tin from 99% ore reads 99%).
    /// </summary>
    public static class QualityText
    {
        /// <summary>The whole percent, rounded down (with a little slack for float error), e.g. "99".</summary>
        public static int WholePercent(float quality)
        {
            return (int)Math.Floor(quality * 100f + 0.01f);
        }

        /// <summary>The percentage with its sign, e.g. "99%".</summary>
        public static string Percent(float quality)
        {
            return WholePercent(quality) + "%";
        }
    }
}
