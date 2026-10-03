using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// An "Exit to menu" button in the bottom-right corner of the lab scene (K34), which
    /// returns to the demo's welcome screen; Esc does the same. Hidden when the menu
    /// scene isn't in the build's scene list (e.g. ChunkLab opened on its own). Clicks
    /// on it never reach the brush or voxel selection (<see cref="LabGuiBlocker"/>).
    /// </summary>
    public class DemoExitButton : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float Width = 130f;
        private const float Height = 32f;

        private bool hasMenu;
        private GUIStyle style;

        private void Awake()
        {
            hasMenu = DemoScenes.HasMenu;
        }

        private void OnDisable()
        {
            LabGuiBlocker.SetArea(this, null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (hasMenu && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                DemoScenes.LoadMenu();
            }
        }

        private void OnGUI()
        {
            if (!hasMenu)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.button) { fontSize = 13 };
            Rect rect = ButtonRect();
            if (Event.current.type == EventType.Repaint)
            {
                // Re-reported each repaint: the window can be resized, which moves the corner.
                LabGuiBlocker.SetArea(this, rect);
            }
            if (GUI.Button(rect, "Exit to menu (Esc)", style))
            {
                DemoScenes.LoadMenu();
            }
        }

        private static Rect ButtonRect()
        {
            return new Rect(Screen.width - Width - Margin, Screen.height - Height - Margin, Width, Height);
        }
    }
}
