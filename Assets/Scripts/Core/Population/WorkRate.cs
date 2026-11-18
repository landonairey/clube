using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// How fast a work gets on while nobody watches (GL730), as a row: so much done in a game hour by one worker on the
    /// duty. A settlement's progress is these rates times the hours its people spent on the work since the game began
    /// (<see cref="DutyHours"/>), whether or not it was loaded. A new work that changes the world gets a row.
    /// </summary>
    public sealed class WorkRate
    {
        /// <summary>The work's name, as <see cref="Duty.Work"/> gives it.</summary>
        public string Work;

        /// <summary>Done in a game hour by one worker: trees felled and cleared, or voxels of ground dug out.</summary>
        public float PerHour;

        public const string Fell = "fell";
        public const string DigPit = "dig-pit";
        public const string DigTunnel = "dig-tunnel";

        /// <summary>
        /// The default rates, near what the people do on screen: a game hour is 100 real seconds at the default day
        /// length, a stroke about 1.2 s; felling a tree and chopping up what landed takes about four game hours, and a
        /// pick takes out about a cubic metre of ground (64 voxels at 0.25 m) an hour.
        /// </summary>
        public static List<WorkRate> Defaults()
        {
            return new List<WorkRate>
            {
                new WorkRate { Work = Fell, PerHour = 0.25f },
                new WorkRate { Work = DigPit, PerHour = 64f },
                new WorkRate { Work = DigTunnel, PerHour = 64f },
            };
        }

        /// <summary>The rate of a work, or 0 when no row names it.</summary>
        public static float Of(IReadOnlyList<WorkRate> rates, string work)
        {
            foreach (WorkRate rate in rates)
            {
                if (rate.Work == work)
                {
                    return rate.PerHour;
                }
            }
            return 0f;
        }
    }
}
