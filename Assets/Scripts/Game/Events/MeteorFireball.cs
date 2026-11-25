using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The falling meteor as seen (GL869): a grey stone core wrapped in a burning ball of fire, with a long fiery tail of
    /// trail and embers. Its glow keeps a least size on screen, so it reads as a bright streak far off in the sky.
    /// </summary>
    public class MeteorFireball : MonoBehaviour
    {
        /// <summary>Core and fire size, metres.</summary>
        private const float CoreSize = 3.5f, FireSize = 9f;

        /// <summary>The glow is never smaller on screen than this fraction of the distance (about 0.8 degrees).</summary>
        private const float LeastAngularSize = 0.014f;

        private TrailRenderer trail;
        private ParticleSystem fire;
        private ParticleSystem embers;

        /// <summary>Builds a fireball at a point.</summary>
        public static MeteorFireball Make(Transform parent, Vector3 at)
        {
            var root = new GameObject("Meteor");
            root.transform.SetParent(parent, true);
            root.transform.position = at;
            var made = root.AddComponent<MeteorFireball>();
            made.Build();
            return made;
        }

        private void Build()
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(transform, false);
            core.transform.localScale = new Vector3(1f, 0.85f, 1.1f) * CoreSize;
            core.GetComponent<Renderer>().sharedMaterial = MeteorFx.Stone;

            fire = MakeFire(transform);
            embers = MakeEmbers(transform);
            trail = MakeTrail(gameObject);
        }

        private void LateUpdate()
        {
            Camera eyes = Camera.main;
            if (eyes == null)
            {
                return;
            }
            float distance = Vector3.Distance(eyes.transform.position, transform.position);
            // Far off, the whole fireball grows so it stays a visible streak; near, it is its real size.
            float scale = Mathf.Max(1f, distance * LeastAngularSize / FireSize);
            transform.localScale = Vector3.one * scale;
            trail.widthMultiplier = FireSize * 0.8f * scale;
            // The meteor must be drawn even past the terrain's draw range.
            if (eyes.farClipPlane < distance + 100f)
            {
                eyes.farClipPlane = distance + 100f;
            }
        }

        /// <summary>Lets the fire and tail die away where they are, then removes everything.</summary>
        public void BurnOut()
        {
            fire.transform.SetParent(null, true);
            embers.transform.SetParent(null, true);
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(fire.gameObject, 3f);
            Destroy(embers.gameObject, 5f);
            Destroy(gameObject);
        }

        // The burning ball: big soft blobs, white-yellow at the middle, orange to red as they fade.
        private static ParticleSystem MakeFire(Transform parent)
        {
            ParticleSystem p = NewSystem(parent, "Fire", ParticleSystemSimulationSpace.Local);
            ParticleSystem.MainModule main = p.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(FireSize * 0.6f, FireSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 120;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = p.emission;
            emission.rateOverTime = 160f;
            ParticleSystem.ShapeModule shape = p.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = CoreSize * 0.3f;
            ParticleSystem.ColorOverLifetimeModule colour = p.colorOverLifetime;
            colour.enabled = true;
            colour.color = MeteorFx.Fade(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.3f, 0.05f), 0.9f);
            p.Play();
            return p;
        }

        // The tail's embers: left behind in world space, drifting and fading over a few seconds.
        private static ParticleSystem MakeEmbers(Transform parent)
        {
            ParticleSystem p = NewSystem(parent, "Embers", ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = p.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(FireSize * 0.35f, FireSize * 0.8f);
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = p.emission;
            emission.rateOverTime = 140f;
            ParticleSystem.ShapeModule shape = p.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = CoreSize * 0.5f;
            ParticleSystem.ColorOverLifetimeModule colour = p.colorOverLifetime;
            colour.enabled = true;
            colour.color = MeteorFx.Fade(new Color(1f, 0.6f, 0.15f), new Color(0.35f, 0.08f, 0.02f), 0.75f);
            ParticleSystem.SizeOverLifetimeModule size = p.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            p.Play();
            return p;
        }

        // The long streak: a trail that tapers and reddens to nothing.
        private static TrailRenderer MakeTrail(GameObject on)
        {
            var trail = on.AddComponent<TrailRenderer>();
            trail.sharedMaterial = MeteorFx.Glow;
            trail.time = 5f;
            trail.minVertexDistance = 4f;
            trail.widthMultiplier = FireSize * 0.8f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            trail.colorGradient = MeteorFx.Fade(new Color(1f, 0.85f, 0.45f), new Color(0.8f, 0.15f, 0.03f), 0.85f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        private static ParticleSystem NewSystem(Transform parent, string name, ParticleSystemSimulationSpace space)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MeteorFx.Glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ParticleSystem.MainModule main = p.main;
            main.loop = true;
            main.simulationSpace = space;
            return p;
        }
    }
}
