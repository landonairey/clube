using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>Where a duty is done (GL615).</summary>
    public enum DutyPlace
    {
        /// <summary>Inside the resident's house, by the door.</summary>
        Home,

        /// <summary>In the house, lying down.</summary>
        Bed,

        /// <summary>On the apron in front of the shop the resident works for.</summary>
        Yard,

        /// <summary>Along the settlement's streets, from one place to the next.</summary>
        Street,

        /// <summary>Out of the settlement, wherever the duty's <see cref="Duty.Work"/> finds something to do.</summary>
        Field,
    }

    /// <summary>
    /// One thing a resident does (GL615), as a row: where, in which pose, and (for work that changes the world) which
    /// work, with which tool, on what. A new duty is a new row; work that needs new behaviour names it in
    /// <see cref="Work"/>, and the game looks the behaviour up by that name.
    /// </summary>
    public sealed class Duty
    {
        public string Id;

        /// <summary>What the player reads when looking at someone doing it ("felling trees").</summary>
        public string Name;

        public DutyPlace Place;

        /// <summary>The pose held at the place (and between the strokes of the work).</summary>
        public FigureAction Pose = FigureAction.Stand;

        /// <summary>The work done in the field, by name (the game's table of works); empty for a duty that is only a pose.</summary>
        public string Work;

        /// <summary>The item id of the tool held and struck with; empty for bare hands.</summary>
        public string Tool;

        /// <summary>What the work goes for: species ids for work on animals; unused by other works.</summary>
        public string[] Targets = System.Array.Empty<string>();

        /// <summary>Metres from the settlement's edge within which the work is looked for.</summary>
        public float Range = 80f;

        /// <summary>Real seconds between two strokes of the work.</summary>
        public float StrokeSeconds = 1.2f;

        public const string Sleep = "sleep";
        public const string AtHome = "home";
        public const string Stroll = "stroll";

        /// <summary>Every duty the default occupations use.</summary>
        public static List<Duty> Defaults()
        {
            return new List<Duty>
            {
                new Duty { Id = Sleep, Name = "asleep", Place = DutyPlace.Bed, Pose = FigureAction.Lie },
                new Duty { Id = AtHome, Name = "at home", Place = DutyPlace.Home, Pose = FigureAction.Stand },
                new Duty { Id = Stroll, Name = "out for a walk", Place = DutyPlace.Street, Pose = FigureAction.Stand },
                new Duty { Id = "sweep", Name = "sweeping the yard", Place = DutyPlace.Yard, Pose = FigureAction.Sweep },

                // Lumber: out to the nearest standing tree with an axe; boards are cut in the yard.
                new Duty
                {
                    Id = "fell", Name = "felling trees", Place = DutyPlace.Field, Pose = FigureAction.Mine, Work = "fell", Tool = "Hand axe",
                    Range = 120f, StrokeSeconds = 1.1f,
                },
                new Duty { Id = "saw", Name = "cutting boards", Place = DutyPlace.Yard, Pose = FigureAction.Whittle },

                // Mining: an open pit beside the settlement, and a tunnel into the nearest slope.
                new Duty
                {
                    Id = "dig-pit", Name = "digging the pit", Place = DutyPlace.Field, Pose = FigureAction.Mine, Work = "dig-pit", Tool = "Bronze pick",
                    Range = 40f,
                },
                new Duty
                {
                    Id = "dig-tunnel", Name = "driving the tunnel", Place = DutyPlace.Field, Pose = FigureAction.Mine, Work = "dig-tunnel",
                    Tool = "Bronze pick", Range = 60f,
                },
                new Duty { Id = "sort-ore", Name = "sorting ore", Place = DutyPlace.Yard, Pose = FigureAction.Examine },

                // Smithing: at the furnace and the anvil in the yard.
                new Duty { Id = "stoke", Name = "stoking the furnace", Place = DutyPlace.Yard, Pose = FigureAction.Stoke },
                new Duty { Id = "hammer", Name = "at the anvil", Place = DutyPlace.Yard, Pose = FigureAction.Hammer },

                // Textiles: wool from the paddock's sheep, spun in the yard.
                new Duty
                {
                    Id = "shear", Name = "shearing", Place = DutyPlace.Field, Pose = FigureAction.Whittle, Work = "shear",
                    Targets = new[] { "sheep", "goat", "alpaca", "yak" }, Range = 160f, StrokeSeconds = 6f,
                },
                new Duty { Id = "spin", Name = "spinning yarn", Place = DutyPlace.Yard, Pose = FigureAction.Whittle },

                // Tanning: deer and bison are hunted, cows killed; hides are worked in the yard.
                new Duty
                {
                    Id = "hunt", Name = "hunting", Place = DutyPlace.Field, Pose = FigureAction.Mine, Work = "hunt", Tool = "Spear",
                    Targets = new[] { "deer", "bison", "cow", "elk" }, Range = 220f, StrokeSeconds = 1.4f,
                },
                new Duty { Id = "tan", Name = "scraping hides", Place = DutyPlace.Yard, Pose = FigureAction.Sweep },
            };
        }

        /// <summary>The duty with an id, or null.</summary>
        public static Duty Find(IReadOnlyList<Duty> duties, string id)
        {
            foreach (Duty duty in duties)
            {
                if (duty.Id == id)
                {
                    return duty;
                }
            }
            return null;
        }
    }
}
