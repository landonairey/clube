using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the <see cref="GraphicsSettings"/> on the game (GL209): the field of view on whichever camera is
    /// drawing (<see cref="Camera.main"/>: the player's eyes, or the lab's fly camera), and the frame rate limit.
    /// </summary>
    /// <remarks>
    /// In the Editor the frame rate limit applies only to a game started from the main menu, so the labs and
    /// benchmarks played straight from a scene run as they always have; the Editor's own limits come back when
    /// the scene ends. A build always applies it.
    /// </remarks>
    public class GraphicsSettingsApplier : MonoBehaviour
    {
        private bool limitsFrameRate;
        private int vSyncBefore;
        private int targetBefore;

        private void OnEnable()
        {
            limitsFrameRate = !Application.isEditor || GameSession.IsActive;
            if (limitsFrameRate)
            {
                vSyncBefore = QualitySettings.vSyncCount;
                targetBefore = Application.targetFrameRate;
                GraphicsSettings.ApplyFrameRate();
                GraphicsSettings.Changed += OnChanged;
            }
        }

        private void OnDisable()
        {
            if (!limitsFrameRate)
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
        }
    }
}
