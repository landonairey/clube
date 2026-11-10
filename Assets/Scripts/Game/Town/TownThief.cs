using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The thief in the trading post's secret cave (GL475), among crates of what's gone missing from the shops. Posed with
    /// <see cref="KeeperFigure"/>; for now only says something when looked at.
    /// </summary>
    public class TownThief : MonoBehaviour, INoticeOnly
    {
        public string Prompt => "The thief: \"You never saw me. Take a little, and keep it quiet.\"";

        public void Interact(PlayerInteractor by)
        {
        }
    }
}
