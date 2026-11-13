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

        /// <summary>GL617, for work that takes what is not the worker's: where the loot ends up (<see cref="Stash"/>: the hoard at the worker's settlement).</summary>
        public string LootTo = string.Empty;

        /// <summary>Stacks of goods taken at a stroke, and the share of a purse's coins.</summary>
        public int Stacks = 1;
        public float Share = 0.25f;

        /// <summary>Metres within which the worker notices the player and goes for them instead; 0 for never.</summary>
        public float Notice;

        public const string Sleep = "sleep";
        public const string AtHome = "home";
        public const string Stroll = "stroll";
        public const string Stash = "stash";

        /// <summary>The work of a thieves' boat on a sortie: boarding the vessels of a water route.</summary>
        public const string Board = "board";

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
                    Range = 250f, StrokeSeconds = 1.1f,
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
                // Thieves (GL617): by a cart path to stop the carts and take their crates, into a town by night for its
                // shops' goods and coins, and about the camp, going for the player who comes near. All of it to the camp's stash.
                new Duty
                {
                    Id = "waylay", Name = "lying in wait by the road", Place = DutyPlace.Field, Pose = FigureAction.Examine, Work = "waylay",
                    Range = 900f, StrokeSeconds = 2.5f, LootTo = Stash, Stacks = 2, Share = 0.3f, Notice = 28f,
                },
                new Duty
                {
                    Id = "burgle", Name = "stealing from the town", Place = DutyPlace.Field, Pose = FigureAction.Examine, Work = "burgle",
                    Range = 1400f, StrokeSeconds = 5f, LootTo = Stash, Stacks = 2, Share = 0.15f, Notice = 18f,
                },
                new Duty
                {
                    Id = "lurk", Name = "keeping watch", Place = DutyPlace.Field, Pose = FigureAction.Stand, Work = "rob",
                    Range = 40f, StrokeSeconds = 2f, LootTo = Stash, Stacks = 2, Share = 0.3f, Notice = 32f,
                },
                // Their boat's crew: out to the nearest water route to board what comes by (the work is the boat's, GL617).
                new Duty
                {
                    Id = "prowl", Name = "out after a vessel", Place = DutyPlace.Field, Pose = FigureAction.Stand, Work = Board,
                    Range = 700f, StrokeSeconds = 3f, LootTo = Stash, Stacks = 3,
                },
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
