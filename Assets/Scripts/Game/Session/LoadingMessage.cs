using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The words under "Loading NN%" (GL742, GL743, GL780): what is being loaded right now. <see cref="GameLoadScreen"/>
    /// shows them while a scene loads, <see cref="LoadCurtain"/> while the map is surveyed and the ground loads; both draw on
    /// <see cref="LoadCurtainView"/>.
    /// </summary>
    public static class LoadingMessage
    {
        public const string Surveying = "Surveying the map…";
        public const string Charting = "Charting the rivers…";

        /// <summary>Where the game is going: the quarry, the family's parcel, or the land of a loaded game.</summary>
        public static string Destination => GameSession.Tutorial ? "Loading the quarry"
            : GameSession.ArrivesByCart || ArrivalCutscene.IsPlaying ? "Travelling to your family's parcel"
            : "Loading the land";

        /// <summary>The stage inside a scene: the map's survey until it is ready, then the ground.</summary>
        public static string InScene()
        {
            MapSession session = MapSession.Current;
            return session != null && !session.IsReady ? Surveying : Destination + "…";
        }
    }
}
