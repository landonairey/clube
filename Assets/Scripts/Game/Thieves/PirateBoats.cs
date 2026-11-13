using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The thieves' boats (GL617: "water-based pirates that go out and intercept sea vessels and/or river boat vessels
    /// for their goods"). <see cref="RaiderRuns"/> gives every thieves' camp with water of its own and a water route in
    /// reach one boat; while its crew's duty of the hour is a sortie (the "pirate" occupation's "prowl" duty, rows like
    /// any other) it sails from its cove to the route and waits there, and a vessel of <see cref="MapBoats"/> that comes
    /// by with goods is held, boarded and its crates emptied into the raider's. The haul is landed in the camp's stash
    /// (<see cref="ThievesCamps"/>) when it comes home. Far from the viewer a raider is only a place on its way; the
    /// boarding happens where the vessels are in the world, near the player.
    /// </summary>
    public class PirateBoats : MonoBehaviour
    {
        // Metres at which a waiting raider goes for a vessel, holds it, and can lay hands on its crates.
        private const float Sight = 80f, HoldWithin = 45f, Alongside = 2.2f;
        private const int MostStrokes = 5;

        [SerializeField]
        private MapSession session;

        [SerializeField]
        private MapBoats boats;

        [SerializeField]
        private ThievesCamps thieves;

        [SerializeField]
        private SettlementPeople people;

        private sealed class Raider
        {
            public RaiderRun Run;
            public Inventory Haul = new Inventory(12);
            public RoadLine Lane;
            public bool LaneTried;
            public float Length;
            public Boat Boat;
            public int Prey = -1;
            public int Strokes;
            public float StrokeTimer;
            public Vector2? Adrift;
            public Vector2 Heading = Vector2.up;
        }

        private readonly List<Raider> raiders = new List<Raider>();
        private readonly List<ItemStack> taken = new List<ItemStack>();
        private Duty prowl;
        private Occupation crew;
        private int version = -1;
        private double lastSeconds;

        /// <summary>Every thieves' boat of the map.</summary>
        public int Count => raiders.Count;

        /// <summary>A raider's run (where it is, what it is about).</summary>
        public RaiderRun RunOf(int raider)
        {
            return raiders[raider].Run;
        }

        /// <summary>A raider's boat, when it is in the world; null otherwise.</summary>
        public Boat BoatOf(int raider)
        {
            return raiders[raider].Boat;
        }

        /// <summary>What a raider has taken and not yet landed.</summary>
        public Inventory HaulOf(int raider)
        {
            return raiders[raider].Haul;
        }

        /// <summary>Where a raider is on the map now (x, z).</summary>
        public Vector2 PositionOf(int raider)
        {
            return raiders[raider].Adrift ?? OnItsWay(raiders[raider]);
        }

        private void Update()
        {
            if (session == null || boats == null || thieves == null || people == null || !session.IsReady || session.Plan == null || !people.IsRunning || boats.Root == null)
            {
                return;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            Camera view = Camera.main;
            if (view == null || crew == null || prowl == null)
            {
                return;
            }
            Vector3 local = people.ToLocal(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            double seconds = boats.Seconds;
            float passed = (float)System.Math.Max(0.0, System.Math.Min(60.0, seconds - lastSeconds));
            lastSeconds = seconds;
            bool sortie = people.DutyNow(crew) == prowl;
            for (int i = 0; i < raiders.Count; i++)
            {
                Raider raider = raiders[i];
                Show(raider, (PositionOf(i) - viewer).magnitude);
                if (raider.Prey >= 0)
                {
                    Board(raider, passed);
                }
                else if (raider.Adrift.HasValue)
                {
                    // Back onto its own way after a boarding.
                    Vector2 back = OnItsWay(raider);
                    raider.Adrift = Steer(raider, raider.Adrift.Value, back, passed);
                    if ((raider.Adrift.Value - back).sqrMagnitude < 1f)
                    {
                        raider.Adrift = null;
                    }
                }
                else
                {
                    float before = raider.Run.Along;
                    if (raider.Run.Step(sortie, raider.Length, passed))
                    {
                        Land(raider);
                    }
                    if (raider.Lane != null && Mathf.Abs(raider.Run.Along - before) > 1e-4f)
                    {
                        raider.Heading = raider.Lane.Direction(raider.Run.Along) * Mathf.Sign(raider.Run.Along - before);
                    }
                    if (sortie && !raider.Run.Loaded && raider.Boat != null)
                    {
                        raider.Prey = FindPrey(raider);
                    }
                }
                if (raider.Boat != null)
                {
                    Vector2 at = PositionOf(i);
                    raider.Boat.Float(at, boats.SurfaceAt(at, raider.Boat.Surface), raider.Heading, raider.Run.Phase != RaiderPhase.Moored && raider.Run.Phase != RaiderPhase.Waiting);
                }
            }
        }

        // Where a raider is on its own way between cove and route: on its lane once that is found, on the straight line before.
        private static Vector2 OnItsWay(Raider it)
        {
            return it.Lane != null ? it.Lane.Flat(it.Run.Along) : Vector2.Lerp(it.Run.Cove, it.Run.Spot, it.Length > 0f ? it.Run.Along / it.Length : 0f);
        }

        // Puts a raider's boat in the world while the viewer is near, on a way found on the real water the first time.
        private void Show(Raider raider, float distance)
        {
            if (raider.Boat == null && distance <= boats.ShowRange && boats.Material != null)
            {
                if (!raider.LaneTried)
                {
                    raider.LaneTried = true;
                    var planned = new List<Vector2> { raider.Run.Cove, raider.Run.Spot };
                    bool river = raider.Run.Class.Kind == WaterRouteKind.River;
                    float corridor = Mathf.Max(river ? 80f : 120f, Vector2.Distance(raider.Run.Cove, raider.Run.Spot) * 0.6f);
                    List<Vector2> lane = WaterLanes.Find(planned, WaterLanes.Floats(session.Map.Water, raider.Run.Class), river ? 2f : 8f, corridor, out _);
                    if (lane != null && lane.Count >= 2 && WaterLanes.Length(lane) > raider.Run.Class.Length * 2f)
                    {
                        float share = raider.Length > 0f ? raider.Run.Along / raider.Length : 0f;
                        raider.Lane = new RoadLine(lane, (x, z) => 0f, 1000f);
                        raider.Length = raider.Lane.Length;
                        raider.Run.Along = share * raider.Length;
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"Pirates: the boat of {session.Plan.Settlements[raider.Run.Camp].Name} has no water to sail from its cove.", this);
                    }
                }
                if (raider.Lane == null)
                {
                    return;
                }
                var body = new GameObject($"{session.Plan.Settlements[raider.Run.Camp].Name}: {raider.Run.Class.Name}");
                body.transform.SetParent(boats.Root, false);
                raider.Boat = body.AddComponent<Boat>();
                raider.Boat.Build(raider.Run.Class, raider.Run.Class.Name, boats.Material, boats.CratePrefab, FigurePreset.Thief, raider.Run.Camp * 131 + 7);
                raider.Boat.Surface = boats.SeaLevel;
                raider.Boat.Describe($"A {raider.Run.Class.Name.ToLowerInvariant()} out of {session.Plan.Settlements[raider.Run.Camp].Name}");
                // Its crates are its haul: what it took can be taken back from them.
                for (int c = 0; c < raider.Boat.Crates.Count; c++)
                {
                    raider.Boat.Crates[c].Hold("Pirates' haul", c == 0 ? raider.Haul : new Inventory(1));
                }
            }
            else if (raider.Boat != null && distance > boats.ShowRange + 60f && raider.Prey < 0)
            {
                Destroy(raider.Boat.gameObject);
                raider.Boat = null;
            }
        }

        // The vessel in the world nearest the raider, within sight, with something in its crates; -1 for none.
        private int FindPrey(Raider raider)
        {
            int prey = -1;
            float nearest = Sight * Sight;
            for (int i = 0; i < boats.Runs.Count; i++)
            {
                Boat vessel = boats.BoatOf(i);
                if (vessel == null || boats.Runs[i].Class.Kind != raider.Run.Class.Kind)
                {
                    continue;
                }
                float distance = (vessel.Flat - raider.Boat.Flat).sqrMagnitude;
                if (distance < nearest && HasGoods(vessel))
                {
                    nearest = distance;
                    prey = i;
                }
            }
            if (prey >= 0)
            {
                raider.Run.Phase = RaiderPhase.Boarding;
                raider.Adrift = raider.Boat.Flat;
                raider.Strokes = 0;
                raider.StrokeTimer = prowl.StrokeSeconds;
            }
            return prey;
        }

        // Runs the prey down, holds it, and empties its crates into the haul, a few stacks at a stroke.
        private void Board(Raider raider, float passed)
        {
            Boat vessel = boats.BoatOf(raider.Prey);
            if (vessel == null || raider.Boat == null)
            {
                EndBoarding(raider);
                return;
            }
            Vector2 beside = vessel.Flat + (raider.Boat.Flat - vessel.Flat).normalized * (vessel.Class.Beam * 0.5f + raider.Run.Class.Beam * 0.5f + 0.8f);
            float distance = (raider.Boat.Flat - vessel.Flat).magnitude;
            if (distance < HoldWithin)
            {
                boats.HoldFor(raider.Prey, 5f);
            }
            raider.Adrift = Steer(raider, raider.Adrift ?? raider.Boat.Flat, beside, passed);
            if ((raider.Adrift.Value - beside).magnitude > Alongside)
            {
                return;
            }
            raider.StrokeTimer -= Time.deltaTime;
            if (raider.StrokeTimer > 0f)
            {
                return;
            }
            raider.StrokeTimer = prowl.StrokeSeconds;
            raider.Strokes++;
            taken.Clear();
            foreach (StorageChest crate in vessel.Crates)
            {
                if (crate != null && !Robbery.IsEmpty(crate.Storage))
                {
                    Robbery.Take(crate.Storage, raider.Haul, prowl.Stacks, new System.Random(raider.Run.Camp * 977 + raider.Strokes + Time.frameCount), taken);
                    break;
                }
            }
            raider.Run.Loaded |= taken.Count > 0;
            if (taken.Count == 0 || raider.Strokes >= MostStrokes || !HasGoods(vessel))
            {
                if (raider.Run.Loaded)
                {
                    ThievesCamp camp = thieves.CampOf(raider.Run.Camp);
                    if (camp != null)
                    {
                        camp.VesselsRobbed++;
                    }
                    thieves.HeardAt(raider.Boat.transform.position, $"Pirates out of {session.Plan.Settlements[raider.Run.Camp].Name} have boarded the {vessel.Name}!");
                }
                EndBoarding(raider);
            }
        }

        private void EndBoarding(Raider raider)
        {
            raider.Prey = -1;
            raider.Run.Phase = RaiderPhase.Home;
        }

        // Moves a boat off its lane towards a place at its own speed; returns where it is now.
        private Vector2 Steer(Raider raider, Vector2 from, Vector2 to, float passed)
        {
            Vector2 way = to - from;
            if (way.sqrMagnitude > 0.04f)
            {
                raider.Heading = way.normalized;
            }
            return Vector2.MoveTowards(from, to, raider.Run.Class.Speed * 1.2f * passed);
        }

        // Home with a haul: into the camp's stash, as the duty's row says.
        private void Land(Raider raider)
        {
            ThievesCamp camp = thieves.CampOf(raider.Run.Camp);
            if (camp != null && prowl.LootTo == Duty.Stash)
            {
                Robbery.TakeAll(raider.Haul, camp.Stash);
            }
            raider.Run.Loaded = false;
        }

        private static bool HasGoods(Boat vessel)
        {
            foreach (StorageChest crate in vessel.Crates)
            {
                if (crate != null && !Robbery.IsEmpty(crate.Storage))
                {
                    return true;
                }
            }
            return false;
        }

        private void StartOver()
        {
            version = session.Version;
            foreach (Raider raider in raiders)
            {
                if (raider.Boat != null)
                {
                    Destroy(raider.Boat.gameObject);
                }
            }
            raiders.Clear();
            crew = Occupation.Find(people.Occupations, Occupation.Pirate);
            prowl = null;
            foreach (Duty duty in people.Duties)
            {
                prowl = prowl == null && duty.Work == Duty.Board ? duty : prowl;
            }
            if (crew == null || prowl == null)
            {
                return;
            }
            foreach (RaiderRun run in RaiderRuns.Plan(session.Plan, boats.Classes, session.Survey.CellSize, prowl.Range))
            {
                raiders.Add(new Raider { Run = run, Length = Vector2.Distance(run.Cove, run.Spot) });
            }
            lastSeconds = boats.Seconds;
        }
    }
}
