using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The crash site cooling (GL871): sparks spitting up from the crater and steam rising off it, dying down over
    /// <see cref="Seconds"/> and then removed.
    /// </summary>
    public class MeteorSizzle : MonoBehaviour
    {
        /// <summary>How long the site sizzles, seconds.</summary>
        public const float Seconds = 90f;

        private ParticleSystem sparks;
        private ParticleSystem steam;
        private float age;
        private float sparkRate, steamRate;

        /// <summary>Starts sizzling at a point.</summary>
        public static MeteorSizzle Make(Transform parent, Vector3 at, float radius)
        {
            var go = new GameObject("Meteor sizzle");
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            var made = go.AddComponent<MeteorSizzle>();
            made.sparks = MakeSparks(go.transform, radius);
            made.steam = MakeSteam(go.transform, radius);
            made.sparkRate = made.sparks.emission.rateOverTime.constant;
            made.steamRate = made.steam.emission.rateOverTime.constant;
            return made;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float left = 1f - age / Seconds;
            if (left <= 0f)
            {
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 6f);
                enabled = false;
                return;
            }
            ParticleSystem.EmissionModule e = sparks.emission;
            e.rateOverTime = sparkRate * left * left;
            ParticleSystem.EmissionModule s = steam.emission;
            s.rateOverTime = steamRate * Mathf.Sqrt(left);
        }

        private static ParticleSystem MakeSparks(Transform parent, float radius)
        {
            ParticleSystem p = NewSystem(parent, "Sparks");
            ParticleSystem.MainModule main = p.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 400;
            ParticleSystem.EmissionModule emission = p.emission;
            emission.rateOverTime = 70f;
            ParticleSystem.ShapeModule shape = p.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule colour = p.colorOverLifetime;
            colour.enabled = true;
            colour.color = MeteorFx.Fade(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.25f, 0.05f));
            p.Play();
            return p;
        }

        private static ParticleSystem MakeSteam(Transform parent, float radius)
        {
            ParticleSystem p = NewSystem(parent, "Steam");
            ParticleSystem.MainModule main = p.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new Color(0.55f, 0.53f, 0.5f, 0.35f);
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = p.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = p.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule size = p.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            ParticleSystem.ColorOverLifetimeModule colour = p.colorOverLifetime;
            colour.enabled = true;
            colour.color = MeteorFx.Fade(Color.white, Color.white, 0.8f);
            p.Play();
            return p;
        }

        private static ParticleSystem NewSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MeteorFx.Glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ParticleSystem.MainModule main = p.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return p;
        }
    }
}
