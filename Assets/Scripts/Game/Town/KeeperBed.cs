using Clube.Core;

namespace Clube.Game
{
    /// <summary>
    /// A shopkeeper's bed the player can sleep in (GL798): resting on it wakes the keeper for the rest of the night and costs
    /// <see cref="Disposition.BedPenalty"/> of their disposition towards the player. The town's <see cref="Shopkeeper"/> and a
    /// planned settlement's <see cref="SettlementKeeper"/> each hold one for the roll <see cref="TownBuilder.FitShop"/> laid.
    /// </summary>
    public sealed class KeeperBed
    {
        private readonly Disposition disposition;

        public KeeperBed(SleepingRoll roll, Disposition disposition)
        {
            Roll = roll;
            this.disposition = disposition;
            roll.Slept += OnSlept;
        }

        /// <summary>The roll in the shop.</summary>
        public SleepingRoll Roll { get; }

        /// <summary>True from the player lying down in it until the keeper's night is over: they stay up meanwhile.</summary>
        public bool Woken { get; private set; }

        /// <summary>Tells the bed whether it is the keeper's sleeping time; once it isn't, they sleep as usual the next night.</summary>
        public void Night(bool sleepTime)
        {
            if (!sleepTime)
            {
                Woken = false;
            }
        }

        /// <summary>Opens the rest screen on the roll, as if the player had used the roll itself.</summary>
        public void Interact(PlayerInteractor by)
        {
            if (Roll != null)
            {
                Roll.Interact(by);
            }
        }

        /// <summary>Lets go of the roll; call when the keeper is destroyed.</summary>
        public void Release()
        {
            if (Roll != null)
            {
                Roll.Slept -= OnSlept;
            }
        }

        private void OnSlept()
        {
            Woken = true;
            disposition?.SleptInBed();
        }
    }
}
