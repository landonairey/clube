using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// The demo exe's welcome screen (K34): a title, a choice of demo, and Exit to close
    /// the program. "Single voxel" launches VoxelLab (V22), "Single chunk" launches
    /// ChunkLab; 1 and 2 do the same, Esc exits. Drawn with IMGUI like the lab panels,
    /// centred and sized to fit small windows.
    /// </summary>
    public class DemoMenu : MonoBehaviour
    {
        private const float ButtonWidth = 260f;
        private const float ButtonHeight = 44f;

        [SerializeField]
        private string title = "Welcome to clube";

        [SerializeField]
        private string subtitle = "Destructible voxel terrain demo";

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle captionStyle;
        private GUIStyle hintStyle;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                DemoScenes.LoadLab(DemoScenes.VoxelLab);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                DemoScenes.LoadLab(DemoScenes.ChunkLab);
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                DemoScenes.Quit();
            }
        }

        private void OnGUI()
        {
            CreateStyles();

            // A fixed-width column in the middle of the screen; text wraps if the window is narrower.
            float width = Mathf.Min(Screen.width - 32f, 520f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 0f, width, Screen.height));
            GUILayout.FlexibleSpace();
            GUILayout.Label(title, titleStyle);
            GUILayout.Label(subtitle, subtitleStyle);
            GUILayout.Space(28f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(ButtonWidth, width)));
            DrawLaunch("Single voxel", "One voxel and its 256 cases, step by step.", DemoScenes.VoxelLab);
            GUILayout.Space(10f);
            DrawLaunch("Single chunk", "A chunk of terrain to fly over, dig and build.", DemoScenes.ChunkLab);
            GUILayout.Space(10f);
            if (GUILayout.Button("Exit", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                DemoScenes.Quit();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20f);
            GUILayout.Label("1 single voxel · 2 single chunk · Esc exit", hintStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawLaunch(string label, string caption, string scene)
        {
            if (GUILayout.Button(label, buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                DemoScenes.LoadLab(scene);
            }
            GUILayout.Label(caption, captionStyle);
        }

        private void CreateStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
            subtitleStyle = new GUIStyle(titleStyle) { fontSize = 16, fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } };
            captionStyle = new GUIStyle(subtitleStyle) { fontSize = 12, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            hintStyle = new GUIStyle(subtitleStyle) { fontSize = 12, normal = { textColor = new Color(0.65f, 0.65f, 0.65f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        }
    }
}
