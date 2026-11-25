using System;

namespace Clube.Game
{
    /// <summary>
    /// A stump or boulder nobody has claimed yet (prototype, backlog 2026-11-24 item 8), with no use on E.
    /// <see cref="WorksiteClaimer"/> claims it on a right click with no tool in hand. It shows no prompt, which got in the
    /// way while mining (backlog 2026-11-24b item 1), and tools work on it as on any ground.
    /// </summary>
    public sealed class UnclaimedWorksite : INoticeOnly, IGroundInteractable
    {
        private readonly Action claim;

        public UnclaimedWorksite(string name, Action claim)
        {
            Name = name;
            this.claim = claim;
        }

        /// <summary>What it is, lower case ("stump", "boulder").</summary>
        public string Name { get; }

        /// <summary>None: <see cref="PlayerInteractor"/> draws nothing for an empty prompt.</summary>
        public string Prompt => null;

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
