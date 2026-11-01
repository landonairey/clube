using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A sleeping roll laid on the ground (GL290), bought from the merchant and placed like a station: Interact opens the
    /// player's <see cref="RestScreen"/> to rest for some hours.
    /// </summary>
    public class SleepingRoll : MonoBehaviour, IInteractable
    {
        public string Prompt => "Rest";

        public void Interact(PlayerInteractor by)
        {
            RestScreen rest = by.GetComponent<RestScreen>();
            if (rest != null && !rest.IsOpen)
            {
                rest.Open(this);
            }
        }
    }
}
