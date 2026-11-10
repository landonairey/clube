using UnityEngine;

namespace Clube.Game
{
    /// <summary>A shut door in the tutorial's warehouse (GL514) that only says it's locked.</summary>
    public class LockedDoor : MonoBehaviour, INoticeOnly
    {
        public string Prompt => "Company stores: locked";

        public void Interact(PlayerInteractor by)
        {
        }
    }
}
