using System;

namespace Clube.Game
{
    /// <summary>
    /// A stump or boulder nobody has claimed yet (prototype, backlog 2026-11-24 item 8): a notice under the crosshair
    /// ("Right click  Claim stump"), with no use on E. <see cref="WorksiteClaimer"/> claims it on the first right click.
    /// </summary>
    public sealed class UnclaimedWorksite : INoticeOnly
    {
        private readonly Action claim;

        public UnclaimedWorksite(string name, Action claim)
        {
            Name = name;
            this.claim = claim;
        }

        /// <summary>What it is, lower case ("stump", "boulder").</summary>
        public string Name { get; }

        public string Prompt => $"Right click  Claim {Name}";

        /// <summary>Claims it: the stump or boulder changes in the terrain and can be used from now on.</summary>
        public void Claim()
        {
            claim?.Invoke();
        }

        public void Interact(PlayerInteractor by)
        {
        }
    }
}
