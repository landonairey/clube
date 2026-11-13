using System;
using System.Collections.Generic;

namespace Clube.Game
{
    /// <summary>
    /// The works duties can name (GL615), by name. A new kind of work is a class and a line here; a duty row then
    /// names it, with its own tool, targets and range.
    /// </summary>
    public static class DutyWorks
    {
        private static readonly Dictionary<string, Func<IDutyWork>> Makers = new Dictionary<string, Func<IDutyWork>>
        {
            { "fell", () => new FellWork() },
            { "dig-pit", () => new DigWork(tunnel: false) },
            { "dig-tunnel", () => new DigWork(tunnel: true) },
            { "hunt", () => new HuntWork() },
            { "shear", () => new ShearWork() },
            // GL617: thieves.
            { "waylay", () => new WaylayWork() },
            { "burgle", () => new BurgleWork() },
            { "rob", () => new RobWork() },
        };

        /// <summary>Adds a work, or replaces the one of that name.</summary>
        public static void Register(string name, Func<IDutyWork> maker)
        {
            Makers[name] = maker;
        }

        /// <summary>A new piece of the work of a name; null for no name or one nobody knows.</summary>
        public static IDutyWork Make(string name)
        {
            return !string.IsNullOrEmpty(name) && Makers.TryGetValue(name, out Func<IDutyWork> maker) ? maker() : null;
        }
    }
}
