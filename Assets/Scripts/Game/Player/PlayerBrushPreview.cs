using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shows where and how big the player's next dig or place will be (M8): a translucent
    /// sphere at the <see cref="PlayerDigTool"/>'s target, sized to its brush. Tinted by what
    /// is held: neutral while aiming, red while digging, green while placing, grey where
    /// placing is refused because it would reach into the player.
    /// </summary>
    [RequireComponent(typeof(PlayerDigTool))]
    public class PlayerBrushPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("A transparent material with a _BaseColor (BrushPreview, URP Unlit).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color aimColor = new Color(1f, 1f, 1f, 0.15f);

        [SerializeField]
        private Color digColor = new Color(1f, 0.3f, 0.2f, 0.3f);

        [SerializeField]
        private Color placeColor = new Color(0.3f, 1f, 0.4f, 0.3f);

        [SerializeField]
        private Color blockedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        private PlayerDigTool digTool;
        private GameObject sphere;
        private MeshRenderer sphereRenderer;
        private MaterialPropertyBlock properties;

        private void Awake()
        {
            digTool = GetComponent<PlayerDigTool>();
            properties = new MaterialPropertyBlock();

            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Brush Preview";
            // A preview only: nothing collides with it or casts shadows from it.
            Destroy(sphere.GetComponent<Collider>());
            sphereRenderer = sphere.GetComponent<MeshRenderer>();
            sphereRenderer.sharedMaterial = material;
            sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sphereRenderer.receiveShadows = false;
            sphere.SetActive(false);
        }

        private void OnDisable()
        {
            if (sphere != null)
            {
                sphere.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Destroy(sphere);
        }

        // After the dig tool's Update has found this frame's target.
        private void LateUpdate()
        {
            // While a tool is held, the brush only places, so it only shows while placing.
            bool show = digTool.isActiveAndEnabled && digTool.HasTarget && material != null
                && (!digTool.ToolHeld || digTool.HeldOperation == BrushOperation.Add);
            sphere.SetActive(show);
            if (!show)
            {
                return;
            }

            sphere.transform.SetPositionAndRotation(digTool.Target, Quaternion.identity);
            sphere.transform.localScale = Vector3.one * (digTool.Radius * 2f);
            properties.SetColor(BaseColorId, PickColor());
            sphereRenderer.SetPropertyBlock(properties);
        }

        private Color PickColor()
        {
            switch (digTool.HeldOperation)
            {
                case BrushOperation.Remove:
                    return digColor;
                case BrushOperation.Add:
                    return digTool.PlaceBlocked ? blockedColor : placeColor;
                default:
                    return aimColor;
            }
        }
    }
}
