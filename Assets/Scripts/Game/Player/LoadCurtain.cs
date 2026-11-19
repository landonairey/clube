using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Covers the view while the player waits for ground (GL653, GL780): from the scene's start, through the map's survey, until
    /// <see cref="PlayerSpawn"/> has set the player down, on <see cref="LoadCurtainView"/> with one rising "Loading NN%"
    /// (<see cref="LoadProgress"/>). A wait shorter than <see cref="delay"/> seconds (falling out of the world, being dug out of a
    /// hillside) shows nothing. <see cref="PlayerSpawn"/> adds it to its player, so every game scene has one.
    /// </summary>
    [RequireComponent(typeof(PlayerSpawn))]
    public class LoadCurtain : MonoBehaviour
    {
        [Tooltip("Seconds a wait must last before the curtain is drawn; the first wait of the scene is covered at once.")]
        [SerializeField, Min(0f)]
        private float delay = 0.4f;

        private PlayerSpawn spawn;
        private bool placedOnce;
        private float waitingSince = -1f;
        private float shown;

        /// <summary>True while the curtain covers the view.</summary>
        public bool IsUp { get; private set; }

        private void Awake()
        {
            spawn = GetComponent<PlayerSpawn>();
            // GL785: the scene's first wait is always covered, so the game clock holds from the very first frame.
            ClockHold.Hold(this);
        }

        private void Update()
        {
            if (spawn.IsPlaced)
            {
                placedOnce = true;
                waitingSince = -1f;
                IsUp = false;
                ClockHold.Release(this);
                shown = 0f;
                LoadCurtainView.Hide();
                return;
            }
            if (waitingSince < 0f)
            {
                waitingSince = Time.unscaledTime;
                shown = 0f;
            }
            IsUp = !placedOnce || Time.unscaledTime - waitingSince >= delay;
            ClockHold.Set(this, IsUp);
            if (IsUp)
            {
                // Never goes back: a stage that restarts its own count must not pull the bar down.
                shown = Mathf.Max(shown, Fraction());
                LoadCurtainView.Show(shown, placedOnce ? LoadingMessage.Destination + "…" : LoadingMessage.InScene());
            }
        }

        private void OnDisable()
        {
            IsUp = false;
            ClockHold.Release(this);
        }

        // The first wait of a scene is the whole load; a later one (a fall) is only the ground.
        private float Fraction()
        {
            if (placedOnce)
            {
                return spawn.Progress;
            }
            MapSession session = MapSession.Current;
            bool hasSurvey = session != null;
            return LoadProgress.InScene(hasSurvey, hasSurvey && session.IsReady, hasSurvey ? session.BuildProgress : 0f, spawn.Progress);
        }
    }
}
