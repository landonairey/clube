using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's figure for the inventory's equipment panel (list inventory prototype): a <see cref="FigureRig"/> in the
    /// player's look standing far above the world with its own camera and light, drawn into a texture while the screen is
    /// open. Dragging over it turns the figure round.
    /// </summary>
    public sealed class CharacterPreview
    {
        private const string ShaderName = "Clube/Low Poly";

        // High above anything the world draws, so the main camera never sees it and the terrain never shades it.
        private static readonly Vector3 Stage = new Vector3(0f, 9000f, 0f);

        private readonly GameObject root;
        private readonly Transform turntable;
        private readonly Camera camera;
        private RenderTexture texture;
        private float yaw;

        public CharacterPreview()
        {
            root = new GameObject("Inventory character preview");
            root.transform.position = Stage;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);

            Shader shader = Shader.Find(ShaderName);
            if (shader != null)
            {
                var figure = new GameObject("Figure");
                figure.transform.SetParent(turntable, false);
                var rig = figure.AddComponent<FigureRig>();
                rig.Build(FigureLooks.Of(FigurePreset.Player), new Material(shader) { name = "Preview figure" }, 0);
                rig.Pose = FigureAction.Stand;
                // Seen close up by its own camera: the distance level of detail would measure from the main camera (GL662).
                if (figure.TryGetComponent(out FigureLod lod))
                {
                    Object.Destroy(lod);
                }
                rig.Detail = FigureDetail.Full;
                foreach (Collider body in figure.GetComponentsInChildren<Collider>())
                {
                    Object.Destroy(body);
                }
            }

            var lightObject = new GameObject("Preview light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(1.2f, 2.2f, 2.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 3f;

            var cameraObject = new GameObject("Preview camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.0f, 3.4f);
            cameraObject.transform.LookAt(root.transform.position + Vector3.up * 0.9f);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 12f;
            camera.enabled = false;
            Turn(0f);
        }

        /// <summary>Draws the figure into <paramref name="area"/> (on Repaint) and turns it while the mouse drags across it.</summary>
        public void Draw(Rect area)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDrag && current.button == 0 && area.Contains(current.mousePosition - current.delta))
            {
                Turn(current.delta.x * 0.6f);
                current.Use();
            }
            if (current.type != EventType.Repaint)
            {
                return;
            }
            int width = Mathf.Max(16, Mathf.RoundToInt(area.width));
            int height = Mathf.Max(16, Mathf.RoundToInt(area.height));
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null)
                {
                    texture.Release();
                    Object.Destroy(texture);
                }
                texture = new RenderTexture(width, height, 24) { name = "Character preview" };
                camera.targetTexture = texture;
                camera.aspect = width / (float)height;
            }
            camera.enabled = true;
            GUI.DrawTexture(area, texture, ScaleMode.StretchToFill, false);
        }

        /// <summary>Stops rendering (the screen closed).</summary>
        public void Hide()
        {
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        public void Destroy()
        {
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
            }
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        private void Turn(float degrees)
        {
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
