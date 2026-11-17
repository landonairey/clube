using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A settlement's light (GL716: "permanent torches lit ... large bonfires ... oil burning lanterns"): its model, a
    /// warm point light that flickers, and for a torch or bonfire a flame and smoke. It never burns out and can't be taken.
    /// Lit from dusk to dawn: its light and flame go out while the day is bright, so a city full of lanterns costs nothing by day.
    /// </summary>
    public sealed class SettlementLamp : MonoBehaviour
    {
        private static readonly Color Warm = new Color(1f, 0.66f, 0.32f);
        private static readonly Color OilLight = new Color(1f, 0.8f, 0.5f);

        private Light lamp;
        private FlameCluster flame;
        private GameObject lit;
        private DayNightCycle clock;
        private float intensity;
        private float seed;

        /// <summary>Lights in daylight brighter than this are put out.</summary>
        public const float DarkEnough = 0.6f;

        /// <summary>Builds the light under this object, which stands at its foot.</summary>
        public void Setup(SettlementLightKind kind, Material material, Material flameMaterial, Material smokeMaterial, DayNightCycle dayNight)
        {
            clock = dayNight;
            intensity = kind.Intensity;
            seed = Random.value * 100f;
            Mesh mesh = SettlementLightMeshes.For(kind.Id);
            if (mesh != null)
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            bool lantern = kind.Id == "lantern";
            Vector3 at = lantern ? SettlementLightMeshes.LanternGlass : new Vector3(0f, kind.FlameHeight, 0f);
            lit = new GameObject("Lit");
            lit.transform.SetParent(transform, false);
            lit.transform.localPosition = at;
            lamp = lit.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = lantern ? OilLight : Warm;
            lamp.range = kind.Range;
            lamp.intensity = intensity;
            lamp.shadows = LightShadows.None;
            if (!lantern && flameMaterial != null)
            {
                // A bonfire's flame is a man's height; a torch's a hand's.
                float size = Mathf.Clamp(kind.Size * 0.6f, 0.3f, 1.4f);
                flame = new FlameCluster(lit.transform, new Vector3(0f, kind.Size > 1f ? -0.55f : -0.12f, 0f), size, flameMaterial);
                if (smokeMaterial != null)
                {
                    TorchFlame.MakeSmoke(lit.transform, new Vector3(0f, size * 0.8f, 0f), smokeMaterial, kind.Size > 1f ? 4f : 1f);
                }
            }
            Update();
        }

        private void Update()
        {
            if (lit == null)
            {
                return;
            }
            bool dark = clock == null || clock.Daylight < DarkEnough;
            if (lit.activeSelf != dark)
            {
                lit.SetActive(dark);
            }
            if (!dark)
            {
                return;
            }
            float t = Time.time * 9f + seed;
            lamp.intensity = intensity * (0.8f + 0.15f * Mathf.PerlinNoise(t, seed) + 0.05f * Mathf.Sin(t * 2.3f));
            flame?.Animate(1f);
        }
    }
}
