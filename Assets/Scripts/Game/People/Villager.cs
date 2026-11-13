using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// One resident of a settlement in the world (GL615): the body of a <see cref="Resident"/>. Each frame it reads the
    /// duty its <see cref="Occupation"/> gives for the day of the week and the hour, goes where that duty is done (bed,
    /// home, its shop's yard, the streets, or out where the duty's work is: <see cref="IDutyWork"/>) by the settlement's
    /// lanes (<see cref="SettlementWalks"/>), and holds the duty's pose there, striking with its tool when the work says.
    /// Where it is follows from the clock, so nothing of it is saved; what it does to the terrain is saved with the world.
    /// </summary>
    public class Villager : MonoBehaviour, IClosableInteractable
    {
        private const float WalkSpeed = 1.4f, WorkSpeed = 2.1f;
        private const float SeekSeconds = 1.5f, GroundSeconds = 0.15f, Arrive = 0.2f;

        private static readonly string[] Names =
        {
            "Aldo", "Bera", "Corin", "Dessa", "Edric", "Fenna", "Garth", "Hilde", "Ivo", "Jora", "Kell", "Lena", "Marn", "Nessa", "Odo", "Petra",
            "Quill", "Rhea", "Stan", "Tilda", "Ulf", "Vera", "Wim", "Ysolde",
        };

        private readonly List<Vector2> path = new List<Vector2>();
        private readonly List<Vector2> leg = new List<Vector2>();
        private FigureRig figure;
        private SettlementWalks walks;
        private IDutyWork work;
        private Vector3 at;
        private Vector2 pathEnd;
        private bool hasPath;
        private bool hasJob;
        private Vector3 stand, face;
        private float seekTimer, strokeTimer, groundTimer, waitTimer;
        private float groundHeight;
        private Vector2 strollTo;
        private bool strolling;
        private Mesh toolMesh;
        private bool toolShown;
        private float heading;

        public SettlementPeople People { get; private set; }

        public SettlementSite Site { get; private set; }

        public Resident Resident { get; private set; }

        /// <summary>What the resident is doing now.</summary>
        public Duty Duty { get; private set; }

        /// <summary>The tool of the duty being done; null with none.</summary>
        public ToolDefinition Tool { get; private set; }

        /// <summary>Damage carried between the resident's blows, as the player's is.</summary>
        public StrikeDamage Damage { get; } = new StrikeDamage();

        public System.Random Random { get; private set; }

        /// <summary>Where the resident stands, in the world's own metres.</summary>
        public Vector3 At => at;

        /// <summary>Things got by the day's work so far (wood, stone, hides, fleeces).</summary>
        public int Carried { get; private set; }

        public string Name { get; private set; }

        private SettlementBuilding Home => Site.Buildings[Resident.Home];

        /// <summary>Sets the resident up where its duty of the moment is done; called once, before the first frame.</summary>
        public void Setup(SettlementPeople people, SettlementSite site, SettlementWalks walks, Resident resident, Material material)
        {
            People = people;
            Site = site;
            Resident = resident;
            this.walks = walks;
            Random = new System.Random(resident.Seed);
            Name = Names[(resident.Seed & 0x7fffffff) % Names.Length];
            figure = gameObject.AddComponent<FigureRig>();
            figure.Build(resident.Occupation.Look, material, resident.Seed);
            figure.Hold(null);
            Begin(people.DutyNow(resident.Occupation));
            Vector2 start = Duty.Place == DutyPlace.Bed ? BedSide() : Duty.Place == DutyPlace.Home ? HomeSpot() : YardSpot();
            at = new Vector3(start.x, site.HeightAt(start.x, start.y), start.y);
            groundHeight = at.y;
            heading = Home.Yaw;
            Place();
        }

        /// <summary>Adds to what the resident has got by its work.</summary>
        public void Carry(int count)
        {
            Carried += Mathf.Max(0, count);
        }

        /// <summary>
        /// True where work may change the ground: outside the settlement (by <paramref name="margin"/> metres) and off the
        /// plan's roads.
        /// </summary>
        public bool MayWorkAt(Vector2 spot, float margin)
        {
            Rect town = Site.Bounds;
            bool inTown = spot.x > town.xMin - margin && spot.x < town.xMax + margin && spot.y > town.yMin - margin && spot.y < town.yMax + margin;
            return !inTown && !People.Plan.IsOnRoad(spot, margin);
        }

        // IClosableInteractable: someone to look at, with nothing to trade.

        public bool IsAvailable => false;

        public string Prompt => Duty == null ? Name
            : $"{Name} the {Resident.Occupation.Name}, {Duty.Name}" + (Carried > 0 ? $" ({Carried} got today)" : string.Empty);

        public void Interact(PlayerInteractor by)
        {
        }

        private void Begin(Duty duty)
        {
            if (duty.Id == Core.Duty.Sleep)
            {
                Carried = 0;
            }
            Duty = duty;
            work = DutyWorks.Make(duty.Work);
            Tool = People.ToolOf(duty.Tool);
            toolMesh = Tool != null ? People.ToolMesh(Tool) : null;
            hasJob = false;
            hasPath = false;
            strolling = false;
            seekTimer = 0f;
            strokeTimer = duty.StrokeSeconds;
        }

        private void Update()
        {
            if (People == null || !People.IsRunning)
            {
                return;
            }
            Duty now = People.DutyNow(Resident.Occupation);
            if (now != Duty)
            {
                Begin(now);
            }
            bool arrived;
            bool lying = false;
            bool working = false;
            Vector2 look = default;
            if (Duty.Place == DutyPlace.Bed)
            {
                arrived = Walk(BedSide(), true, WalkSpeed);
                lying = arrived;
            }
            else if (Duty.Place == DutyPlace.Home)
            {
                arrived = Walk(HomeSpot(), true, WalkSpeed);
            }
            else if (Duty.Place == DutyPlace.Street)
            {
                arrived = Stroll();
            }
            else if (Duty.Place == DutyPlace.Field && work != null && Seek())
            {
                // Workers on the same job stand round it, not in one another.
                float turn = Resident.Index * 2.4f;
                arrived = Walk(new Vector2(stand.x + Mathf.Cos(turn) * 0.45f, stand.z + Mathf.Sin(turn) * 0.45f), false, WorkSpeed);
                working = arrived;
                look = new Vector2(face.x - at.x, face.z - at.z);
            }
            else
            {
                arrived = Walk(YardSpot(), false, WalkSpeed);
                look = TownLayout.Turn(new Vector2(0f, -1f), Site.Buildings[Resident.Shop].Yaw);
            }
            if (working)
            {
                strokeTimer -= Time.deltaTime;
                if (strokeTimer <= 0f)
                {
                    strokeTimer = Duty.StrokeSeconds;
                    work.Stroke(this);
                    seekTimer = 0f;
                }
            }
            if (arrived && look.sqrMagnitude > 1e-4f)
            {
                heading = Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg;
            }
            bool atPlace = arrived && (Duty.Place != DutyPlace.Field || working);
            figure.Pose = lying ? FigureAction.Lie : !arrived ? FigureAction.Walk : atPlace ? Duty.Pose : FigureAction.Stand;
            ShowTool(Duty.Place == DutyPlace.Field && toolMesh != null);
            Place();
            SettlementBuilding home = Home;
            Vector2 bed = home.ToWorld(BedLocal());
            Vector2 along = TownLayout.Turn(Vector2.right, home.Yaw);
            figure.LieAlong(lying, People.ToWorld(new Vector3(bed.x, at.y, bed.y)), People.View.transform.TransformDirection(new Vector3(along.x, 0f, along.y)));
        }

        private bool Seek()
        {
            seekTimer -= Time.deltaTime;
            if (seekTimer <= 0f)
            {
                seekTimer = SeekSeconds;
                hasJob = work.Seek(this, out stand, out face);
            }
            return hasJob;
        }

        // A walk round the streets: to one place, a pause, then another.
        private bool Stroll()
        {
            if (!strolling)
            {
                strolling = true;
                strollTo = walks.Nodes.Count > 0 ? walks.Nodes[Random.Next(walks.Nodes.Count)] : YardSpot();
                waitTimer = 4f + (float)Random.NextDouble() * 10f;
            }
            bool arrived = Walk(strollTo, false, WalkSpeed * 0.8f);
            if (arrived)
            {
                waitTimer -= Time.deltaTime;
                strolling = waitTimer > 0f;
            }
            return arrived;
        }

        // Goes towards a place, by the lanes and through the home's door; true once there.
        private bool Walk(Vector2 to, bool indoors, float speed)
        {
            var here = new Vector2(at.x, at.z);
            float moved = hasPath ? (to - pathEnd).sqrMagnitude : float.MaxValue;
            if (moved > 1e-4f && moved < 1.5f * 1.5f)
            {
                // The place shifted a little (the next voxel of a pit, an animal's step): the way there just ends elsewhere.
                pathEnd = to;
                if (path.Count > 0)
                {
                    path[path.Count - 1] = to;
                }
                else
                {
                    path.Add(to);
                }
            }
            else if (moved > 1e-4f)
            {
                hasPath = true;
                pathEnd = to;
                path.Clear();
                Vector2 from = here;
                if (IsIndoors(here))
                {
                    if (indoors)
                    {
                        path.Add(to);
                        from = to;
                    }
                    else
                    {
                        path.Add(DoorInside());
                        path.Add(DoorFront());
                        from = DoorFront();
                    }
                }
                if (from != to)
                {
                    walks.Route(from, indoors ? DoorFront() : to, leg);
                    path.AddRange(leg);
                    if (indoors)
                    {
                        path.Add(DoorInside());
                        path.Add(to);
                    }
                }
            }
            float step = speed * Time.deltaTime;
            while (path.Count > 0 && step > 0f)
            {
                Vector2 way = path[0] - here;
                float distance = way.magnitude;
                if (distance <= Mathf.Max(step, 0.01f))
                {
                    here = path[0];
                    step -= distance;
                    path.RemoveAt(0);
                    continue;
                }
                here += way / distance * step;
                heading = Mathf.Atan2(way.x, way.y) * Mathf.Rad2Deg;
                step = 0f;
            }
            at.x = here.x;
            at.z = here.y;
            figure.Speed = speed;
            Ground(IsIndoors(here));
            return path.Count == 0 && (here - to).sqrMagnitude <= Arrive * Arrive;
        }

        // Keeps the feet on the ground: the terrain under them (so a pit or a tunnel's floor carries them), else the site's own height.
        private void Ground(bool indoors)
        {
            groundTimer -= Time.deltaTime;
            if (groundTimer <= 0f)
            {
                groundTimer = GroundSeconds;
                if (!People.TryGround(at.x, at.z, at.y + 0.6f, out groundHeight))
                {
                    groundHeight = Site.Bounds.Contains(new Vector2(at.x, at.z)) ? Site.HeightAt(at.x, at.z) + (indoors ? TownLayout.FloorHeight : 0f)
                        : People.Ground(at.x, at.z);
                }
            }
            at.y = Mathf.MoveTowards(at.y, groundHeight, 4f * Time.deltaTime);
        }

        private void Place()
        {
            transform.localPosition = at;
            transform.localRotation = Quaternion.Euler(0f, heading, 0f);
        }

        private void ShowTool(bool show)
        {
            if (show != toolShown)
            {
                toolShown = show;
                figure.Hold(show ? toolMesh : null);
            }
        }

        private bool IsIndoors(Vector2 point)
        {
            SettlementBuilding home = Home;
            Vector2 local = TownLayout.Turn(point - home.Centre, -home.Yaw);
            return Mathf.Abs(local.x) < TownLayout.ShopWidth * 0.5f && Mathf.Abs(local.y) < TownLayout.ShopDepth * 0.5f;
        }

        // The home's door: a house's is in its front wall; a shop (where there are no houses) is entered as its keeper does.
        private float DoorX => Home.Kind == SettlementBuildingKind.House ? SettlementStructures.DoorX : TownLayout.InsideDoor.x;

        private Vector2 DoorInside()
        {
            return Home.ToWorld(new Vector2(DoorX, TownLayout.ShopDepth * 0.5f - 0.9f));
        }

        private Vector2 DoorFront()
        {
            return Home.ToWorld(new Vector2(DoorX, TownLayout.ShopDepth * 0.5f + 1.2f));
        }

        private Vector2 HomeSpot()
        {
            return Home.ToWorld(new Vector2(-1.6f + 1.1f * (Resident.Index % 3), 0.3f));
        }

        private Vector2 BedLocal()
        {
            return new Vector2(-0.6f, -1.5f + 1.1f * (Resident.Index % 2));
        }

        private Vector2 BedSide()
        {
            return Home.ToWorld(BedLocal() + new Vector2(0f, 0.1f));
        }

        private Vector2 YardSpot()
        {
            return walks.Front(Resident.Shop, -2.2f + 1.1f * (Resident.Index % 5), 1.2f + 0.9f * (Resident.Index / 5 % 2));
        }
    }
}
