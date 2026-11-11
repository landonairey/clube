using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Marks a tutorial <see cref="StorageChest"/> as company property (GL514, GL553): the player can open it and look
    /// (GL521), but it's theft the moment anything leaves it (<see cref="MiningCorpTutorial.Steal"/>).
    /// </summary>
    public class CompanyCrate : MonoBehaviour
    {
        private StorageChest chest;
        private MiningCorpTutorial tutorial;
        private float total;

        /// <summary>Starts watching the chest's contents; call after it has been filled.</summary>
        public void Watch(StorageChest watched, MiningCorpTutorial sees)
        {
            chest = watched;
            tutorial = sees;
            total = Total();
            chest.Storage.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (chest != null && chest.Storage != null)
            {
                chest.Storage.Changed -= OnChanged;
            }
        }

        private void OnChanged()
        {
            float now = Total();
            bool lost = now < total - 1e-4f;
            total = now;
            if (lost && tutorial != null)
            {
                tutorial.Steal();
            }
        }

        private float Total()
        {
            float sum = 0f;
            for (int slot = 0; slot < chest.Storage.SlotCount; slot++)
            {
                sum += chest.Storage[slot].IsEmpty ? 0f : chest.Storage[slot].Amount;
            }
            return sum;
        }
    }
}
