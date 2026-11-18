namespace Clube.Game
{
    /// <summary>The player's map screen on M, whichever map the scene has (GL760): the pause menu closes it, the key sheet lists it.</summary>
    public interface IMapScreen
    {
        /// <summary>True while the map is on screen.</summary>
        bool IsOpen { get; }

        /// <summary>Closes the map.</summary>
        void Close();
    }
}
