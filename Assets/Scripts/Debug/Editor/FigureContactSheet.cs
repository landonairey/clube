using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Draws every figure preset to one picture (GL608), to judge the looks side by side without playing: the back row is
    /// each <see cref="FigurePreset"/> as designed, in enum order, at its trade; the front row is seeded people in other
    /// poses. Nothing is left in the open scene.
    /// </summary>
    public static class FigureContactSheet
    {
        private const string MaterialPath = "Assets/Materials/LowPoly.mat";

        private static readonly FigureAction[] Trades =
        {
            FigureAction.Hammer, FigureAction.Mine, FigureAction.Sweep, FigureAction.Whittle, FigureAction.Read, FigureAction.Mine,
            FigureAction.Stand, FigureAction.Stand, FigureAction.Stand, FigureAction.Walk, FigureAction.Stand,
        };

        private static readonly FigureAction[] Others =
        {
            FigureAction.Walk, FigureAction.Stand, FigureAction.Sit, FigureAction.Stand, FigureAction.Examine, FigureAction.Walk, FigureAction.Stoke,
        };

        [MenuItem("Clube/Models/Figure contact sheet")]
        private static void RenderToTemp()
        {
            string path = "Temp/figure-contact-sheet.png";
            Render(path);
            UnityEngine.Debug.Log($"Figure contact sheet written to {System.IO.Path.GetFullPath(path)}");
        }

        /// <summary>Renders the sheet to a PNG file.</summary>
        public static void Render(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var root = new GameObject("Figure contact sheet") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(0f, 5000f, 0f);
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.45f, 0.55f, 0.38f) };
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(60f, 0.1f, 40f);
            ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            FigurePreset[] presets = FigureLooks.All;
            for (int i = 0; i < presets.Length; i++)
            {
                float x = (i - (presets.Length - 1) * 0.5f) * 1.3f;
                Place(root.transform, new Vector3(x, 0f, 1.5f), 200f, Trades[i % Trades.Length]).Build(FigureLooks.Of(presets[i]), material, i);
                Place(root.transform, new Vector3(x + 0.3f, 0f, -1.7f), 150f + (i % 3) * 30f, Others[i % Others.Length])
                    .Build(i < 6 ? FigurePreset.Worker : presets[(i * 3) % 5], material, 600 + i * 7);
            }
            foreach (FigureRig rig in root.GetComponentsInChildren<FigureRig>())
            {
                rig.Show(rig.Pose, rig.transform.localPosition.z > 0f ? 0.5f : 1.3f);
            }
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.86f);
            camera.fieldOfView = 20f;
            camera.transform.localPosition = new Vector3(0f, 9f, -22f);
            camera.transform.LookAt(root.transform.position + new Vector3(0f, 0.9f, 0f));
            var target = new RenderTexture(2600, 1100, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            RenderTexture.active = null;
            camera.targetTexture = null;
            System.IO.File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(groundMaterial);
            Object.DestroyImmediate(root);
        }

        private static FigureRig Place(Transform root, Vector3 at, float facing, FigureAction action)
        {
            var go = new GameObject("Figure");
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            var rig = go.AddComponent<FigureRig>();
            rig.Pose = action;
            return rig;
        }
    }
}
