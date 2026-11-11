using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Other Mining Corp hands at the tutorial's mountain wall (GL531, GL548, GL549, GL551, GL552, GL556). Each owns a stretch
    /// of the west-to-east wall and works it like the player does: it walks to a spot in front of the face, turns to the wall
    /// and swings the company pick, each landing a real strike (<see cref="SphereStrike"/> targets through
    /// <see cref="ToolStrike.Hit"/> with the same sounds and debris as the player's hit), then walks on to the next spot. After
    /// a sweep of the stretch it digs a step deeper, with no limit, so the wall is worked ever further in. They dig only in
    /// work hours; at <see cref="CompanyWork.QuotaHour"/> they walk to the crew lead, hand in, and go to their bunks in the
    /// warehouse's dorm hall, and wake in the morning to walk back. Everything is map space (under the map transform).
    /// </summary>
    public class WorkerCrew : MonoBehaviour
    {
        // Approach: the walk from the yard to the post by way of the lane in front of the face (GL580).
        private enum Phase { ToSpot, Work, ToLead, HandIn, ToBed, Sleep, ToWork, Approach }

        private const float WakeHour = CompanyWork.WorkStartHour - 1f;
        private const float WalkSpeed = 1.8f;
        private const float StepAlong = 1.1f;
        private const float StepDeeper = 0.5f;
        private const float StandOff = 1.2f;
        private const int SwingsPerSpot = 9;
        private const int ClearAfter = 3;
        private const float HandInSeconds = 2.5f;
        private const float TurnSpeed = 240f;
        private static readonly float[] StrikeHeights = { 0.9f, 1.7f, 2.5f };
        // A standing worker's body, as balls at these heights, for checking that a post is clear of rock (GL580).
        private static readonly float[] BodyHeights = { 0.5f, 1.2f, 1.7f };
        private const float BodyRadius = 0.3f;
        private const float GroundCastUp = 0.6f;
        private const float GroundCastDown = 30f;
        private const float StepUpSpeed = 3f;
        private const float Gravity = 20f;
        private const float MaxFall = 25f;
        // GL586: a digger with no strike landing on rock for this long re-plans.
        private const float StallSeconds = 8f;
        private const int ReplanLookAhead = 12;

        private sealed class Worker
        {
            public int Index;
            public KeeperFigure Figure;
            public AudioSource Audio;
            public StrikeDamage Damage = new StrikeDamage();
            public Phase Phase;
            public float From;
            public float To;
            public float Along;
            public int Pass;
            public int Swings;
            public int Clear;
            public float LastBeat;
            public float Wait;
            public Vector3 Queue;
            public Vector3[] Route;
            public int RouteAt;
            public float Fall;
            public StallTimer Stall;
        }

        private Worker[] workers;
        private Transform map;
        private WorldView world;
        private DayNightCycle dayNight;
        private float lookupAt;
        private PlayerToolUser tools;
        private ToolDefinition pick;
        private HitDebris debris;
        private PlayerToolSounds sounds;

        private readonly List<Vector3Int> targets = new List<Vector3Int>();
        private readonly List<ItemDefinition> collected = new List<ItemDefinition>();
        private readonly List<MinedSample> minedFrom = new List<MinedSample>();

        /// <summary>Total strikes the crew has landed that reached ground: for checking in tests and the Editor.</summary>
        public int Strikes { get; private set; }

        /// <summary>Deepest pass any worker has dug (each is half a metre in): for checking.</summary>
        public int DeepestPass
        {
            get
            {
                int deepest = 0;
                if (workers != null)
                {
                    foreach (Worker w in workers)
                    {
                        deepest = Mathf.Max(deepest, w.Pass);
                    }
                }
                return deepest;
            }
        }

        /// <summary>Puts the workers at the foot of the north wall and starts their day.</summary>
        /// <param name="tools">The player's tools, whose strike settings, sounds and debris the workers borrow; may be null.</param>
        /// <param name="pickDefinition">The company pick the workers swing; may be null (they then dig with nothing and are silent).</param>
        public static WorkerCrew Build(Transform map, Material material, WorldView world, PlayerToolUser tools = null, ToolDefinition pickDefinition = null)
        {
            var crew = new GameObject("Mining crew");
            crew.transform.SetParent(map, false);
            var result = crew.AddComponent<WorkerCrew>();
            result.map = map;
            result.world = world;
            result.tools = tools;
            result.pick = pickDefinition;
            if (tools != null)
            {
                result.debris = tools.GetComponent<HitDebris>();
                result.sounds = tools.GetComponent<PlayerToolSounds>();
            }
            Rect f = CompanyWork.Fields;
            int count = CompanyWarehouse.WorkerBedCount;
            float stretch = f.width / count;
            result.workers = new Worker[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Worker " + (i + 1));
                go.transform.SetParent(crew.transform, false);
                var figure = go.AddComponent<KeeperFigure>();
                figure.BuildWorker(material, 600 + i * 7);
                var audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 3f;
                audio.maxDistance = 45f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                var w = new Worker
                {
                    Index = i,
                    Figure = figure,
                    Audio = audio,
                    From = f.xMin + i * stretch + 0.6f,
                    To = f.xMin + (i + 1) * stretch - 0.6f,
                    Queue = CompanyWarehouse.LeadSpot + new Vector3((i - (count - 1) * 0.5f) * 0.8f, 0f, 1.6f),
                };
                w.Along = w.From;
                w.LastBeat = 0f;
                result.workers[i] = w;
                // GL577: the day starts in bed; they get up and walk to work on the usual schedule.
                w.Phase = Phase.Sleep;
                go.transform.localPosition = CompanyWarehouse.WorkerBedSpot(i) - Vector3.up * 0.55f;
                go.transform.localRotation = Quaternion.identity;
                figure.Pose = KeeperPose.Lie;
                figure.LieAlong(true, map.TransformPoint(CompanyWarehouse.WorkerBedSpot(i)), map.TransformDirection(Vector3.forward));
            }
            return result;
        }

        // Where the face meets the open ground: the lane workers walk along to reach their post, or leave by, so they never
        // cut through the mountain (GL580). Same x as the post: the trench is dug there.
        private static Vector3 LaneOf(Worker w)
        {
            return new Vector3(w.Along, CompanyWork.Ground, CompanyWork.Fields.yMax - StandOff);
        }

        private static void StartApproach(Worker w)
        {
            StartRoute(w, LaneOf(w), PostOf(w));
            w.Phase = Phase.Approach;
        }

        // True when a worker standing at a post (this spot, this pass) would be inside terrain (GL580).
        private bool BodyBlocked(float along, int pass)
        {
            if (world == null || world.World == null)
            {
                return false;
            }
            var probe = new Worker { Along = along, Pass = pass };
            Vector3 post = PostOf(probe);
            foreach (float up in BodyHeights)
            {
                SphereStrike.FindTargets(world.World, post + Vector3.up * up, BodyRadius, targets);
                if (targets.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static float Depth(Worker w) => w.Pass * StepDeeper;

        private static float FaceZ(Worker w) => CompanyWork.Fields.yMax + 0.9f + Depth(w);

        // Where a worker stands to dig: in front of the face, following the hole in.
        private static Vector3 PostOf(Worker w)
        {
            float depth = Depth(w);
            return new Vector3(w.Along, CompanyWork.Ground + Mathf.Min(depth, 1f) * 0.3f, FaceZ(w) - StandOff);
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
            float hour = dayNight != null ? dayNight.ClockHour : 12f;
            bool outAndAbout = hour >= WakeHour && hour < CompanyWork.QuotaHour;
            bool digging = CompanyWork.IsWorkHour(hour);
            float dt = Time.deltaTime;
            for (int i = 0; i < workers.Length; i++)
            {
                Tick(workers[i], outAndAbout, digging, dt);
                Ground(workers[i], dt);
            }
        }

        // GL582: workers stand on the live terrain, whatever the player digs: a cast down against the chunk colliders every frame.
        // They drop (with gravity) when the ground under them is dug away, and step up onto rising ground. Where no collider is loaded
        // (far from the player) they keep their height. A NavMesh over voxels the player edits all day would be rebuilt around every dig
        // and the colliders only exist near the player, so this is the cheap, robust choice for now.
        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        private void Ground(Worker w, float dt)
        {
            if (w.Phase == Phase.Sleep)
            {
                w.Fall = 0f;
                return;
            }
            Transform t = w.Figure.transform;
            Vector3 local = t.localPosition;
            Vector3 origin = map.TransformPoint(local + Vector3.up * GroundCastUp);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, GroundCastUp + GroundCastDown, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float groundY = 0f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.distance < best && hit.collider.GetComponent<ChunkCollider>() != null)
                {
                    best = hit.distance;
                    groundY = map.InverseTransformPoint(hit.point).y;
                }
            }
            if (best == float.MaxValue)
            {
                w.Fall = 0f;
                return;
            }
            if (groundY >= local.y)
            {
                local.y = Mathf.Min(groundY, local.y + StepUpSpeed * dt);
                w.Fall = 0f;
            }
            else
            {
                w.Fall = Mathf.Min(w.Fall + Gravity * dt, MaxFall);
                local.y = Mathf.Max(groundY, local.y - w.Fall * dt);
            }
            t.localPosition = local;
        }

        private void Tick(Worker w, bool upAndAbout, bool digging, float dt)
        {
            Transform t = w.Figure.transform;
            if (upAndAbout)
            {
                if (w.Phase == Phase.Sleep)
                {
                    w.Figure.LieAlong(false, Vector3.zero, Vector3.forward);
                    StartRoute(w, CompanyWarehouse.WorkerBedSide(w.Index), CompanyWarehouse.DoorInside, CompanyWarehouse.DoorOutside);
                    w.Phase = Phase.ToWork;
                }
                else if (w.Phase == Phase.ToLead || w.Phase == Phase.HandIn || w.Phase == Phase.ToBed)
                {
                    StartApproach(w);
                }
            }
            else if (w.Phase == Phase.Work || w.Phase == Phase.ToSpot || w.Phase == Phase.ToWork || w.Phase == Phase.Approach)
            {
                // Out by the lane, not through the rock, then across the yard to the lead.
                StartRoute(w, LaneOf(w), w.Queue);
                w.Phase = Phase.ToLead;
            }

            switch (w.Phase)
            {
                case Phase.ToWork:
                    if (WalkRoute(w, t, dt))
                    {
                        StartApproach(w);
                    }
                    break;
                case Phase.Approach:
                    if (WalkRoute(w, t, dt))
                    {
                        w.Phase = Phase.Work;
                        w.Swings = 0;
                        w.Clear = 0;
                        w.LastBeat = w.Figure.MineBeat;
                        w.Stall.Reset();
                    }
                    break;
                case Phase.ToSpot:
                    if (WalkTo(w, t, PostOf(w), dt))
                    {
                        w.Phase = Phase.Work;
                        w.Swings = 0;
                        w.Clear = 0;
                        w.LastBeat = w.Figure.MineBeat;
                        w.Stall.Reset();
                    }
                    break;
                case Phase.Work:
                    Dig(w, t, digging, dt);
                    break;
                case Phase.ToLead:
                    if (WalkRoute(w, t, dt))
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
                        StartRoute(w, CompanyWarehouse.DoorOutside, CompanyWarehouse.DoorInside, CompanyWarehouse.WorkerBedSide(w.Index),
                            CompanyWarehouse.WorkerBedSpot(w.Index) - Vector3.up * 0.55f);
                        w.Phase = Phase.ToBed;
                    }
                    break;
                case Phase.ToBed:
                    if (WalkRoute(w, t, dt))
                    {
                        w.Phase = Phase.Sleep;
                        t.localRotation = Quaternion.identity;
                        w.Figure.Pose = KeeperPose.Lie;
                        w.Figure.LieAlong(true, map.TransformPoint(CompanyWarehouse.WorkerBedSpot(w.Index)), map.TransformDirection(Vector3.forward));
                    }
                    break;
            }
        }

        // At a spot: face the wall, swing; the pick lands once a beat, and after a few landings, or ground that's clear, on to the next spot.
        private void Dig(Worker w, Transform t, bool digging, float dt)
        {
            if (!digging)
            {
                w.Figure.Pose = KeeperPose.Stand;
                return;
            }
            w.Figure.Pose = KeeperPose.Mine;
            if (w.Stall.Tick(dt, StallSeconds))
            {
                Replan(w);
                return;
            }
            t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.identity, TurnSpeed * dt);
            float beat = w.Figure.MineBeat;
            bool landed = (w.LastBeat < KeeperFigure.MineImpact && beat >= KeeperFigure.MineImpact);
            w.LastBeat = beat;
            if (!landed)
            {
                return;
            }
            Strike(w);
            w.Swings++;
            if (w.Clear >= ClearAfter || w.Swings >= SwingsPerSpot)
            {
                NextSpot(w);
            }
        }

        // One swing's strike: a ball of the pick's reach at the face at one of three heights, through the player's strike code.
        private void Strike(Worker w)
        {
            if (world == null || world.World == null)
            {
                return;
            }
            World data = world.World;
            float height = StrikeHeights[w.Swings % StrikeHeights.Length];
            var centre = new Vector3(w.Along, CompanyWork.Ground + height, FaceZ(w));
            float radius = (tools != null ? tools.Collecting.ToolRadius : 0.6f) * (pick != null ? pick.ReachScale : 1f);
            SphereStrike.FindTargets(data, centre, radius, targets);
            if (targets.Count == 0)
            {
                w.Clear++;
                return;
            }
            w.Clear = 0;
            w.Stall.Reset();
            Strikes++;
            MaterialRegistry materials = world.Config.Materials;
            float iso = world.Config.IsoLevel;
            VoxelMaterial hit = ToolStrike.AimedMaterial(data, data.Grid.WorldToVoxel(centre), iso, materials);
            collected.Clear();
            minedFrom.Clear();
            StrikeResult result = pick != null
                ? ToolStrike.Hit(data, targets, pick, materials, w.Damage, collected, iso, minedFrom)
                : new StrikeResult(targets.Count, 0, 0);
            Vector3 at = map.TransformPoint(centre);
            if (sounds != null)
            {
                sounds.StrikeSound(pick, hit, result, w.Audio);
            }
            if (debris != null)
            {
                Vector3 outward = (map.TransformDirection(Vector3.back) + Vector3.up * 0.3f).normalized;
                debris.StrikeAt(result, hit, materials, at, outward);
            }
        }

        // On to the next spot along the stretch; after the last, a sweep is done and the next goes a step deeper.
        private void NextSpot(Worker w)
        {
            float along = w.Along + StepAlong;
            int pass = w.Pass;
            if (along > w.To)
            {
                along = w.From;
                pass++;
            }
            // GL580: never step to a post that is inside rock (hard stone the picks haven't cleared yet): dig on here instead.
            if (BodyBlocked(along, pass))
            {
                w.Swings = 0;
                w.Clear = 0;
                return;
            }
            w.Along = along;
            w.Pass = pass;
            w.Figure.Pose = KeeperPose.Walk;
            w.Phase = Phase.ToSpot;
        }

        // GL586: eight seconds of digging with nothing struck (the face out of reach after a drop, or the next post blocked by
        // rock the swings can't reach, so NextSpot kept digging on here) -> pick another post, looking ahead for a clear one,
        // and walk out by the lane to it. Never stays silently stuck.
        private void Replan(Worker w)
        {
            float along = w.Along;
            int pass = w.Pass;
            for (int i = 0; i <= ReplanLookAhead; i++)
            {
                along += StepAlong;
                if (along > w.To)
                {
                    along = w.From;
                    pass++;
                }
                if (i == ReplanLookAhead || !BodyBlocked(along, pass))
                {
                    break;
                }
            }
            w.Along = along;
            w.Pass = pass;
            w.Fall = 0f;
            w.Figure.Pose = KeeperPose.Walk;
            StartApproach(w);
        }

        private static void StartRoute(Worker w, params Vector3[] points)
        {
            w.Route = points;
            w.RouteAt = 0;
        }

        // Walks the route a leg at a time; true on arrival at its last point.
        private static bool WalkRoute(Worker w, Transform t, float dt)
        {
            if (w.Route == null || w.RouteAt >= w.Route.Length)
            {
                return true;
            }
            if (WalkTo(w, t, w.Route[w.RouteAt], dt))
            {
                w.RouteAt++;
            }
            return w.RouteAt >= w.Route.Length;
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
