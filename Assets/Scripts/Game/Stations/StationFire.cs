using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A fire station's fire while it works (GL329): when the <see cref="StationObject"/> beside it is smelting (a recipe
    /// running, and burning fuel if it needs fuel), its glow light shines brighter and flickers, and a campfire also shows
    /// the torch's dancing flame (<see cref="FlameCluster"/>) and smoke. Idle, the glow is steady at its set intensity.
    /// </summary>
    [RequireComponent(typeof(StationObject))]
    public sealed class StationFire : MonoBehaviour
    {
        [Tooltip("The station's glow (its Glow child's point light).")]
        [SerializeField]
        private Light glow;

        [Tooltip("How many times brighter the glow is while the station works.")]
        [SerializeField, Range(1f, 4f)]
        private float workingBoost = 1.6f;

        [Tooltip("Flames shown while it works (Embers: glowing); none for a closed furnace.")]
        [SerializeField]
        private Material flameMaterial;

        [Tooltip("Smoke rising while it works (the Drying steam material); none for no smoke.")]
        [SerializeField]
        private Material smokeMaterial;

        [Tooltip("Where the flame stands, above the station's foot.")]
        [SerializeField]
        private Vector3 flameAt = new Vector3(0f, 0.05f, 0f);

        [Tooltip("The flame's height in metres.")]
        [SerializeField, Range(0.1f, 1.5f)]
        private float flameHeight = 0.45f;

        private StationObject station;
        private FlameCluster flame;
        private ParticleSystem smoke;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            station = GetComponent<StationObject>();
            seed = Random.value * 100f;
            if (glow != null)
            {
                baseIntensity = glow.intensity;
            }
            if (flameMaterial != null)
            {
                flame = new FlameCluster(transform, flameAt, flameHeight, flameMaterial) { Shown = false };
            }
            if (smokeMaterial != null)
            {
                smoke = TorchFlame.MakeSmoke(transform, flameAt + Vector3.up * flameHeight * 0.6f, smokeMaterial);
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>True while it smelts: a recipe running, burning fuel if it needs it.</summary>
        public bool IsWorking
        {
            get
            {
                var work = station != null ? station.Station : null;
                return work != null && work.IsBusy && (!work.BurnsFuel || work.IsBurning);
            }
        }

        private void Update()
        {
            bool working = IsWorking;
            if (glow != null)
            {
                float t = Time.time * 9f + seed;
                float flicker = 0.78f + 0.16f * Mathf.PerlinNoise(t, seed) + 0.06f * Mathf.Sin(t * 2.3f) + 0.04f * Mathf.Sin(t * 6.1f);
                glow.intensity = working ? baseIntensity * workingBoost * flicker : baseIntensity;
            }
            if (flame != null)
            {
                flame.Shown = working;
                if (working)
                {
                    flame.Animate(1f);
                }
            }
            if (smoke != null && smoke.isEmitting != working)
            {
                if (working)
                {
                    smoke.Play();
                }
                else
                {
                    smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void OnDestroy()
        {
            flame?.Destroy();
        }
    }
}
