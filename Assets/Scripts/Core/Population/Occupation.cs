using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>One stretch of an occupation's week (GL615): on these days, between these hours, this duty.</summary>
    public struct DutyShift
    {
        /// <summary>First and last day of the week it holds on (0 Monday to 6 Sunday), inclusive.</summary>
        public int FromDay, ToDay;

        /// <summary>The hours of the day it runs between (from 22 to 6 runs through midnight).</summary>
        public float From, To;

        /// <summary>Only every other day of its span (0 all of them, 1 the 1st, 3rd, ..., 2 the 2nd, 4th, ...).</summary>
        public int Alternate;

        public string Duty;

        public DutyShift(int fromDay, int toDay, float from, float to, string duty, int alternate = 0)
        {
            FromDay = fromDay;
            ToDay = toDay;
            From = from;
            To = to;
            Duty = duty;
            Alternate = alternate;
        }

        /// <summary>True when the shift holds on a day of the week at an hour.</summary>
        public bool Holds(int dayOfWeek, float hour)
        {
            if (dayOfWeek < FromDay || dayOfWeek > ToDay)
            {
                return false;
            }
            if (Alternate != 0 && (dayOfWeek - FromDay) % 2 != Alternate - 1)
            {
                return false;
            }
            return From <= To ? hour >= From && hour < To : hour >= From || hour < To;
        }
    }

    /// <summary>
    /// What a resident of a settlement does for a living (GL615), as a row: the shop specialties it works for, the look
    /// it wears and its week, shift by shift ("they cycle duties throughout the hours in the week"). The first shift
    /// that holds names the duty; with none, the resident sleeps at night and is at home by day. A new occupation is a
    /// new row.
    /// </summary>
    public sealed class Occupation
    {
        /// <summary>Days in the week the shifts are written for; day 1 of the game is a Monday.</summary>
        public const int DaysInWeek = 7;

        public const float NightFrom = 21f, NightTo = 6f;

        public static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public string Id;

        /// <summary>What the worker is called ("lumberjack").</summary>
        public string Name;

        /// <summary>Ids of the <see cref="EconomySpecialty"/> rows whose shops employ it; empty for the one anybody else is.</summary>
        public string[] Specialties = Array.Empty<string>();

        public FigurePreset Look = FigurePreset.Worker;

        /// <summary>True for thieves (GL617): no shop employs them, and a blow from the player's tool makes them drop what they carry.</summary>
        public bool Outlaw;

        public DutyShift[] Week = Array.Empty<DutyShift>();

        /// <summary>The day of the week (0 Monday to 6 Sunday) of a day of the game (1 the first).</summary>
        public static int DayOfWeek(int day)
        {
            return ((day - 1) % DaysInWeek + DaysInWeek) % DaysInWeek;
        }

        /// <summary>The id of the duty at a day of the week and an hour of the day.</summary>
        public string DutyAt(int dayOfWeek, float hour)
        {
            foreach (DutyShift shift in Week)
            {
                if (shift.Holds(dayOfWeek, hour))
                {
                    return shift.Duty;
                }
            }
            return hour >= NightFrom || hour < NightTo ? Duty.Sleep : Duty.AtHome;
        }

        /// <summary>The occupation a shop of a specialty employs: the row naming it, else the row naming none.</summary>
        public static Occupation For(IReadOnlyList<Occupation> occupations, string specialtyId)
        {
            Occupation anybody = null;
            foreach (Occupation occupation in occupations)
            {
                if (Array.IndexOf(occupation.Specialties, specialtyId) >= 0)
                {
                    return occupation;
                }
                if (occupation.Specialties.Length == 0 && anybody == null && !occupation.Outlaw)
                {
                    anybody = occupation;
                }
            }
            return anybody;
        }

        /// <summary>The occupation with an id, or null.</summary>
        public static Occupation Find(IReadOnlyList<Occupation> occupations, string id)
        {
            foreach (Occupation occupation in occupations)
            {
                if (occupation.Id == id)
                {
                    return occupation;
                }
            }
            return null;
        }

        // A working week: the morning's and the afternoon's duty Monday to Saturday (the afternoon's alternating with a
        // second one, when given), a walk at midday and in the evening, and Sunday off.
        private static DutyShift[] WorkingWeek(string morning, string afternoon, string otherAfternoon = null, float start = 7f)
        {
            var week = new List<DutyShift>
            {
                new DutyShift(0, 5, start, 12f, morning),
                new DutyShift(0, 5, 12f, 13f, Duty.Stroll),
            };
            if (otherAfternoon != null)
            {
                week.Add(new DutyShift(0, 5, 13f, 17f, otherAfternoon, 2));
            }
            week.Add(new DutyShift(0, 5, 13f, 17f, afternoon));
            week.Add(new DutyShift(0, 5, 17f, 19.5f, Duty.Stroll));
            week.Add(new DutyShift(6, 6, 9f, 12f, "sweep"));
            week.Add(new DutyShift(6, 6, 13f, 18f, Duty.Stroll));
            return week.ToArray();
        }

        /// <summary>The occupations of the default specialties, and the resident anybody else is.</summary>
        public static List<Occupation> Defaults()
        {
            return new List<Occupation>
            {
                new Occupation
                {
                    Id = "lumberjack", Name = "lumberjack", Look = FigurePreset.Woodsman,
                    Specialties = new[] { "lumber-supplies", "carpentry", "tool-componentry" },
                    Week = WorkingWeek("fell", "fell", "saw"),
                },
                new Occupation
                {
                    Id = "miner", Name = "miner", Look = FigurePreset.Worker,
                    Specialties = new[]
                    {
                        "mining", "crushing", "aggregate-supplies", "masonry", "stone-quarry", "limestone-quarry", "sand-pit", "clay-pit",
                    },
                    Week = WorkingWeek("dig-pit", "dig-tunnel", "sort-ore"),
                },
                new Occupation
                {
                    Id = "smith", Name = "smith", Look = FigurePreset.Blacksmith,
                    Specialties = new[] { "smelting", "blacksmith", "goldsmith", "jeweler" },
                    Week = WorkingWeek("stoke", "hammer"),
                },
                new Occupation
                {
                    Id = "shepherd", Name = "shepherd", Look = FigurePreset.Worker,
                    Specialties = new[] { "fabrics", "clothing" },
                    Week = WorkingWeek("shear", "spin"),
                },
                new Occupation
                {
                    Id = "hunter", Name = "hunter", Look = FigurePreset.Adventurer,
                    Specialties = new[] { "tannery", "leathery" },
                    Week = WorkingWeek("hunt", "tan", start: 6f),
                },
                new Occupation
                {
                    Id = "resident", Name = "townsman", Look = FigurePreset.Merchant,
                    Week = WorkingWeek("sweep", Duty.Stroll, start: 8f),
                },
                // A thieves' camp's band (GL617): every day of the week the same, the roads by day and the towns by night.
                new Occupation
                {
                    Id = "bandit", Name = "thief", Look = FigurePreset.Thief, Outlaw = true,
                    Week = new[]
                    {
                        new DutyShift(0, 6, 6f, 11f, "waylay"),
                        new DutyShift(0, 6, 11f, 13f, "lurk"),
                        new DutyShift(0, 6, 13f, 18f, "waylay"),
                        new DutyShift(0, 6, 18f, 22f, "lurk"),
                        new DutyShift(0, 6, 22f, 3f, "burgle"),
                    },
                },
            };
        }
    }
}
