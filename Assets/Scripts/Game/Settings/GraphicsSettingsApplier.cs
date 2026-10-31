using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the <see cref="GraphicsSettings"/> on the game (GL209, GL211): the field of view on whichever camera is
    /// drawing (<see cref="Camera.main"/>: the player's eyes, or the lab's fly camera), the frame rate limit and the
    /// render distance of the world's <see cref="WorldView"/> beside it. The frozen time of day is read by the
    /// <see cref="DayNightCycle"/>.
    /// </summary>
    /// <remarks>
    /// In the Editor the frame rate limit and render distance apply only to a game started from the main menu, so the
    /// labs and benchmarks played straight from a scene run as they always have; the Editor's own limits come back
    /// when the scene ends. A build always applies them.
    /// </remarks>
    public class GraphicsSettingsApplier : MonoBehaviour
    {
        private bool appliesPlayerChoices;
        private int vSyncBefore;
        private int targetBefore;
        private WorldView worldView;

        private void OnEnable()
        {
            worldView = GetComponent<WorldView>();
            appliesPlayerChoices = !Application.isEditor || GameSession.IsActive;
            if (appliesPlayerChoices)
            {
                vSyncBefore = QualitySettings.vSyncCount;
                targetBefore = Application.targetFrameRate;
                OnChanged();
                GraphicsSettings.Changed += OnChanged;
            }
        }

        // Again once every OnEnable has run, so the render distance wins over RenderDistanceSetting's start value.
        private void Start()
        {
            if (appliesPlayerChoices)
            {
                OnChanged();
            }
        }

        private void OnDisable()
        {
            if (!appliesPlayerChoices)
            {
                return;
            }
            GraphicsSettings.Changed -= OnChanged;
            if (Application.isEditor)
            {
                // Runtime changes to the quality settings can stick in the Editor: put them back.
                QualitySettings.vSyncCount = vSyncBefore;
                Application.targetFrameRate = targetBefore;
            }
        }

        private void LateUpdate()
        {
            Camera eyes = Camera.main;
            float fieldOfView = GraphicsSettings.FieldOfView;
            if (eyes != null && !Mathf.Approximately(eyes.fieldOfView, fieldOfView))
            {
                eyes.fieldOfView = fieldOfView;
            }
        }

        private void OnChanged()
        {
            GraphicsSettings.ApplyFrameRate();
            if (worldView != null)
            {
                // RenderDistanceSetting carries the change on into the player settings.
                worldView.RenderDistance = GraphicsSettings.RenderDistance;
            }
        }
    }
}
