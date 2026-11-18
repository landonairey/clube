namespace Clube.Core
{
    /// <summary>What a clam held when it was opened (GL735).</summary>
    public enum ClamFind
    {
        Empty,
        Pearl,
        BlackPearl,
    }

    /// <summary>
    /// The odds inside a clam (GL735): one in four holds a pearl, and one in a hundred of those pearls is black. Pure, so
    /// tests (and later a luck skill) can drive it with chosen rolls.
    /// </summary>
    public static class ClamPearls
    {
        public const float PearlChance = 0.25f;
        public const float BlackShare = 0.01f;

        /// <summary>The item ids of the finds, as in the item catalog.</summary>
        public const string PearlId = "pearl", BlackPearlId = "black-pearl";

        /// <summary>What a clam holds for two rolls in [0, 1): whether there is a pearl, then whether that pearl is black.</summary>
        public static ClamFind Open(float pearlRoll, float blackRoll)
        {
            if (pearlRoll >= PearlChance)
            {
                return ClamFind.Empty;
            }
            return blackRoll < BlackShare ? ClamFind.BlackPearl : ClamFind.Pearl;
        }

        /// <summary>The catalog id of a find; null for an empty clam.</summary>
        public static string IdOf(ClamFind find)
        {
            switch (find)
            {
                case ClamFind.Pearl:
                    return PearlId;
                case ClamFind.BlackPearl:
                    return BlackPearlId;
                default:
                    return null;
            }
        }
    }
}
