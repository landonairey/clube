using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The meteor event (GL736, revamped by GL869–GL871): a burning stone (<see cref="MeteorFireball"/>) comes slowly down
    /// out of the far sky along a <see cref="MeteorPlan"/>, with its flyby sound as it nears (<see cref="MeteorSounds"/>);
    /// where it lands the impact sounds, the crash site is cut (<see cref="MeteorCrashSite"/>: gouge, crater, ore ball and
    /// scattered pieces) once the chunks it touches are loaded, and the crater sizzles a while (<see cref="MeteorSizzle"/>).
    /// <see cref="Call"/> brings one down at a place; the debug menu calls it at the player. Nothing yet calls it by
    /// chance (WE1 proper), so it is only ever called on purpose.
    /// </summary>
    public class MeteorEvent : MonoBehaviour
    {
        private WorldView worldView;
        private MeteorPlan plan;
        private float age;
        private MeteorFireball fireball;
        private bool flybyStarted;
        private bool cutPending;

        /// <summary>True while a meteor is falling.</summary>
        public bool IsFalling => fireball != null;

        /// <summary>Strikes counted since the world loaded (tests and the lab).</summary>
        public int Landed { get; private set; }

        /// <summary>The meteor event of the open scene, made on first use; null when the scene has no world.</summary>
        public static MeteorEvent Find()
        {
            var found = FindAnyObjectByType<MeteorEvent>();
            if (found != null)
            {
                return found;
            }
            var view = FindAnyObjectByType<WorldView>();
            if (view == null)
            {
                return null;
            }
            found = new GameObject("Meteor Event").AddComponent<MeteorEvent>();
            found.worldView = view;
            return found;
        }

        /// <summary>
        /// Brings a meteor down on a spot: the surface under (x, z), searched from a little over <paramref name="target"/>.
        /// False when no terrain is loaded there or one is already falling or landing.
        /// </summary>
        public bool Call(Vector3 target)
        {
            if (fireball != null || cutPending || worldView == null || !worldView.IsReady
                || !GroundProbe.TryGround(worldView, target.x, target.z, target.y, out float ground))
            {
                return false;
            }
            plan = new MeteorPlan(new Vector3(target.x, ground, target.z), Random.value * 360f);
            age = 0f;
            flybyStarted = false;
            fireball = MeteorFireball.Make(transform, plan.Start);
            return true;
        }

        private void Update()
        {
            if (cutPending && MeteorCrashSite.IsReady(worldView, plan))
            {
                cutPending = false;
                MeteorCrashSite.Cut(worldView, plan);
                Landed++;
            }
            if (fireball == null)
            {
                return;
            }
            age += Time.deltaTime;
            float t = age / MeteorPlan.FallSeconds;
            fireball.transform.position = plan.At(t);
            MeteorSounds sounds = MeteorSounds.Load();
            // The flyby is timed to end as the meteor lands (or starts at once when it is longer than the fall).
            if (!flybyStarted && sounds != null && MeteorPlan.FallSeconds - age <= sounds.FlybySeconds)
            {
                flybyStarted = true;
                sounds.PlayFlyby(fireball.gameObject);
            }
            if (t >= 1f)
            {
                Land(sounds);
            }
        }

        private void Land(MeteorSounds sounds)
        {
            fireball.BurnOut();
            fireball = null;
            if (sounds != null)
            {
                sounds.PlayImpact(transform, plan.Impact);
            }
            MeteorSizzle.Make(transform, plan.Impact + plan.Ball.Offset + Vector3.up * MeteorPlan.BallRadius, MeteorPlan.CraterRadius * 0.6f);
            // The edits wait for every chunk they touch (A14; a chunk loaded later would miss its part).
            cutPending = true;
        }
    }
}
