using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Other Mining Corp hands at the tutorial's mountain wall (GL531, GL548, GL549). Each works its own stretch of the
    /// west-to-east wall, swinging a pick and taking a few small bites of terrain out through the world's brush every
    /// couple of seconds, sweeping along the stretch and then a little deeper, so over the days the whole wall is worked.
    /// At <see cref="CompanyWork.QuotaHour"/> they walk to the crew lead, hand in the day's stone and go to bed; in the
    /// morning they walk back to the wall. Everything is map space (under the map transform).
    /// </summary>
    public class WorkerCrew : MonoBehaviour
    {
        private enum Phase { Work, ToLead, HandIn, ToBed, Sleep, ToWork }

        private const float StartHour = 6f;
        private const float WalkSpeed = 1.8f;
        private const float BiteInterval = 2.2f;
        private const float BiteRadius = 0.55f;
        private const float StepAlong = 0.45f;
        private const float StepDeeper = 0.5f;
        private const int MaxPasses = 8;
        private const float HandInSeconds = 2.5f;
        private const float BedSpacing = 2.6f;
        private static readonly float[] BiteHeights = { 0.9f, 1.7f, 2.5f };
        private static readonly Vector3 BedStart = new Vector3(52f, CompanyWork.Ground, 19f);

        private sealed class Worker
        {
            public KeeperFigure Figure;
            public Phase Phase;
            public float From;
            public float To;
            public float Along;
            public int Pass;
            public float Timer;
            public float Wait;
            public Vector3 Bed;
            public Vector3 Queue;
        }

        private Worker[] workers;
        private Transform map;
        private WorldView world;
        private DayNightCycle dayNight;
        private float lookupAt;

        /// <summary>Puts the workers at the foot of the north wall and starts their day.</summary>
        public static WorkerCrew Build(Transform map, Material material, WorldView world)
        {
            var crew = new GameObject("Mining crew");
            crew.transform.SetParent(map, false);
            var result = crew.AddComponent<WorkerCrew>();
            result.map = map;
            result.world = world;
            Rect f = CompanyWork.Fields;
            const int count = 6;
            float stretch = f.width / count;
            result.workers = new Worker[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Worker " + (i + 1));
                go.transform.SetParent(crew.transform, false);
                var figure = go.AddComponent<KeeperFigure>();
                figure.Build(ShopKind.Mining, material, 600 + i * 7);
                var w = new Worker
                {
                    Figure = figure,
                    From = f.xMin + i * stretch,
                    To = f.xMin + (i + 1) * stretch,
                    Timer = BiteInterval * i / count,
                    Bed = BedStart + Vector3.right * (BedSpacing * i),
                    Queue = CompanyWarehouse.LeadSpot + new Vector3((i - (count - 1) * 0.5f) * 0.8f, 0f, 1.6f),
                };
                w.Along = w.From + stretch * 0.5f;
                result.workers[i] = w;
                go.transform.localPosition = PostOf(w);
                figure.Pose = KeeperPose.Mine;
            }
            return result;
        }

        private static float Depth(Worker w) => w.Pass * StepDeeper;

        // Where a worker stands to dig: just in front of the face, following the hole in.
        private static Vector3 PostOf(Worker w)
        {
            float depth = Depth(w);
            return new Vector3(w.Along, CompanyWork.Ground + Mathf.Min(depth, 1f) * 0.3f, CompanyWork.Fields.yMax - 0.8f + depth);
        }

        private void Update()
        {
            if (workers == null)
            {
                return;
            }
            if (dayNight == null && Time.time > lookupAt)
            {
                lookupAt = Time.time + 2f;
                dayNight = FindAnyObjectByType<DayNightCycle>();
            }
            bool shift = dayNight == null || (dayNight.ClockHour >= StartHour && dayNight.ClockHour < CompanyWork.QuotaHour);
            float dt = Time.deltaTime;
            for (int i = 0; i < workers.Length; i++)
            {
                Tick(workers[i], shift, dt);
            }
        }

        private void Tick(Worker w, bool shift, float dt)
        {
            Transform t = w.Figure.transform;
            if (shift)
            {
                if (w.Phase == Phase.Sleep)
                {
                    w.Figure.LieAlong(false, Vector3.zero, Vector3.right);
                    w.Phase = Phase.ToWork;
                }
                else if (w.Phase != Phase.Work && w.Phase != Phase.ToWork)
                {
                    w.Phase = Phase.ToWork;
                }
            }
            else if (w.Phase == Phase.Work || w.Phase == Phase.ToWork)
            {
                w.Phase = Phase.ToLead;
            }

            switch (w.Phase)
            {
                case Phase.Work:
                    Dig(w, t, dt);
                    break;
                case Phase.ToWork:
                    if (WalkTo(w, t, PostOf(w), dt))
                    {
                        t.localRotation = Quaternion.identity;
                        w.Figure.Pose = KeeperPose.Mine;
                        w.Phase = Phase.Work;
                    }
                    break;
                case Phase.ToLead:
                    if (WalkTo(w, t, w.Queue, dt))
                    {
                        w.Phase = Phase.HandIn;
                        w.Wait = HandInSeconds;
                        w.Figure.Pose = KeeperPose.Stand;
                        FaceTowards(t, CompanyWarehouse.LeadSpot);
                    }
                    break;
                case Phase.HandIn:
                    w.Wait -= dt;
                    if (w.Wait <= 0f)
                    {
                        w.Phase = Phase.ToBed;
                    }
                    break;
                case Phase.ToBed:
                    if (WalkTo(w, t, w.Bed, dt))
                    {
                        w.Phase = Phase.Sleep;
                        t.localRotation = Quaternion.identity;
                        w.Figure.Pose = KeeperPose.Lie;
                        w.Figure.LieAlong(true, map.TransformPoint(w.Bed), map.TransformDirection(Vector3.right));
                    }
                    break;
            }
        }

        // One bite every couple of seconds: a few small spheres up the face at head height, then a step along the stretch.
        private void Dig(Worker w, Transform t, float dt)
        {
            w.Timer -= dt;
            if (w.Timer > 0f || world == null || world.World == null)
            {
                return;
            }
            w.Timer = BiteInterval * Random.Range(0.8f, 1.2f);
            var brush = new BrushSettings(BiteRadius);
            float faceZ = CompanyWork.Fields.yMax + 0.9f + Depth(w);
            for (int i = 0; i < BiteHeights.Length; i++)
            {
                world.World.ApplyBrush(new Vector3(w.Along, CompanyWork.Ground + BiteHeights[i], faceZ), brush, BrushOperation.Remove);
            }
            w.Along += StepAlong;
            if (w.Along > w.To)
            {
                w.Along = w.From;
                w.Pass = (w.Pass + 1) % MaxPasses;
            }
            t.localPosition = PostOf(w);
        }

        private static bool WalkTo(Worker w, Transform t, Vector3 target, float dt)
        {
            Vector3 at = t.localPosition;
            Vector3 flat = target - at;
            flat.y = 0f;
            float dist = flat.magnitude;
            w.Figure.Pose = KeeperPose.Walk;
            if (dist < 0.1f)
            {
                t.localPosition = target;
                return true;
            }
            t.localRotation = Quaternion.LookRotation(flat / dist);
            Vector3 next = at + flat / dist * Mathf.Min(WalkSpeed * dt, dist);
            next.y = Mathf.Lerp(at.y, target.y, Mathf.Min(1f, WalkSpeed * dt / dist));
            t.localPosition = next;
            return false;
        }

        private static void FaceTowards(Transform t, Vector3 point)
        {
            Vector3 d = point - t.localPosition;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
            {
                t.localRotation = Quaternion.LookRotation(d);
            }
        }
    }
}
