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
        // GL668: metres two people keep between them, and how fast they step apart when closer.
        private const float Personal = 0.75f, SpreadSpeed = 1.2f;
        private const float SeekSeconds = 1.5f, GroundSeconds = 0.15f;

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
        private Mesh shownMesh;
        private IReadOnlyList<Villager> neighbours;
        private float heading;
        private Inventory sack;
        private float dazeTimer;

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

        /// <summary>What the resident carries that is not its own (GL617: a thief's loot); made on first use.</summary>
        public Inventory Sack => sack ??= new Inventory(8);

        /// <summary>Coins the resident carries (a thief's takings).</summary>
        public Wallet Purse { get; } = new Wallet();

        /// <summary>True while the sack or the purse holds anything.</summary>
        public bool IsLoaded => Purse.Coins > 0f || (sack != null && !Robbery.IsEmpty(sack));

        /// <summary>True while knocked down.</summary>
        public bool IsDazed => dazeTimer > 0f;

        /// <summary>Knocks the resident down for a while (GL617: a thief struck by the player); it does nothing until it gets up.</summary>
        public void Daze(float seconds)
        {
            dazeTimer = Mathf.Max(dazeTimer, seconds);
            hasPath = false;
            hasJob = false;
            seekTimer = 0f;
        }

        private SettlementBuilding Home => Site.Buildings[Resident.Home];

        /// <summary>Sets the resident up where its duty of the moment is done; called once, before the first frame.</summary>
        public void Setup(SettlementPeople people, SettlementSite site, SettlementWalks walks, Resident resident, Material material,
            IReadOnlyList<Villager> neighbours = null)
        {
            this.neighbours = neighbours;
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
            // Work done at one far place (a band by its road): whoever comes on it in the middle of the duty finds them there.
            if (Duty.Place == DutyPlace.Field && work != null && work.TryPost(this, out Vector2 post) && people.StartsAtPost(site, post))
            {
                at = new Vector3(post.x, people.Ground(post.x, post.y), post.y);
            }
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
        /// True where work may change the ground: outside the settlement (by <paramref name="margin"/> metres), off the
        /// plan's roads and off the player's starting parcel and the eight round it (GL731, <see cref="WorkLand"/>).
        /// </summary>
        public bool MayWorkAt(Vector2 spot, float margin)
        {
            return People.Land.MayWork(Site.Bounds, spot, margin);
        }

        // IClosableInteractable: someone to look at, with nothing to trade.

        public bool IsAvailable => false;

        public string Prompt => Duty == null ? Name
            : Resident.Occupation.Outlaw ? $"A {Resident.Occupation.Name}, {(IsDazed ? "knocked down" : Duty.Name)}" + (IsLoaded ? " (carrying loot)" : string.Empty)
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
            using FrameCost.Scope cost = FrameCost.Of("People (Villager)");
            if (People == null || !People.IsRunning)
            {
                return;
            }
            if (dazeTimer > 0f)
            {
                // Flat on the ground where it was struck.
                dazeTimer -= Time.deltaTime;
                figure.Pose = FigureAction.Lie;
                ShowTool(null);
                Ground(false);
                Place();
                figure.LieAlong(dazeTimer > 0f, People.ToWorld(at), transform.forward);
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
                arrived = Walk(new Vector2(stand.x + Mathf.Cos(turn) * 0.9f, stand.z + Mathf.Sin(turn) * 0.9f), false, WorkSpeed * work.Pace);
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
            // GL669: a yard duty with a prop (the sweeper's broom) holds it once there, as field work holds its tool.
            ShowTool(Duty.Place == DutyPlace.Field && toolMesh != null ? toolMesh : atPlace ? FigureProps.For(Duty.Prop) : null);
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
                    if (!indoors && SettlementSite.Beyond(Site.Bounds, from) > 12f && SettlementSite.Beyond(Site.Bounds, to) > 12f
                        && walks.StraightCost(from, to) <= Vector2.Distance(from, to) + 0.01f)
                    {
                        // Out in the country both ends, with no building between (GL668): straight there, the settlement's lanes are no help.
                        leg.Clear();
                        leg.Add(to);
                    }
                    else
                    {
                        walks.Route(from, indoors ? DoorFront() : to, leg);
                    }
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
            bool inside = IsIndoors(here);
            if (!inside)
            {
                here = Spread(here);
            }
            at.x = here.x;
            at.z = here.y;
            figure.Speed = speed;
            Ground(inside);
            // Outdoors, people pressed apart (GL668) count as there within their personal room.
            float arrive = indoors ? 0.2f : Personal;
            return path.Count == 0 && (here - to).sqrMagnitude <= arrive * arrive;
        }

        // GL668: steps away from anyone standing too close, and out of any building but the home (whose door it may be
        // passing through), so people neither stand in one another nor press into walls.
        private Vector2 Spread(Vector2 here)
        {
            if (neighbours != null)
            {
                Vector2 push = Vector2.zero;
                foreach (Villager other in neighbours)
                {
                    if (other == null || other == this || other.IsDazed)
                    {
                        continue;
                    }
                    Vector2 away = here - new Vector2(other.at.x, other.at.z);
                    float distance = away.magnitude;
                    if (distance < Personal)
                    {
                        // Two on one spot part by their seeds, not both the same way.
                        away = distance > 1e-3f ? away / distance : TownLayout.Turn(Vector2.right, (Resident.Seed & 0xff) * 1.41f);
                        push += away * (Personal - distance);
                    }
                }
                if (push.sqrMagnitude > 1e-6f)
                {
                    here += Vector2.ClampMagnitude(push, 1f) * (SpreadSpeed * Time.deltaTime);
                }
            }
            return walks.PushOut(here, 0.25f, Resident.Home);
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

        private void ShowTool(Mesh held)
        {
            if (held != shownMesh || !toolShown)
            {
                toolShown = true;
                shownMesh = held;
                figure.Hold(held);
            }
        }

        private bool IsIndoors(Vector2 point)
        {
            SettlementBuilding home = Home;
            Vector2 local = TownLayout.Turn(point - home.Centre, -home.Yaw);
            return Mathf.Abs(local.x) < home.Width * 0.5f && Mathf.Abs(local.y) < home.Depth * 0.5f;
        }

        // The home's door: a house's is in its front wall; a shop (where there are no houses) is entered as its keeper does.
        private float DoorX => Home.Kind == SettlementBuildingKind.House ? SettlementStructures.DoorX : TownLayout.InsideDoor.x;

        private Vector2 DoorInside()
        {
            return Home.ToWorld(new Vector2(DoorX, Home.Depth * 0.5f - 0.9f));
        }

        private Vector2 DoorFront()
        {
            return Home.ToWorld(new Vector2(DoorX, Home.Depth * 0.5f + 1.2f));
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
            // GL713/GL714: at a fixture of the shop's work yard (an anvil, a spinning wheel) where it has one for the duty.
            SettlementBuilding shop = Site.Buildings[Resident.Shop];
            YardFixture station = shop.Design?.StationFor(Duty?.Id, Resident.Index);
            if (station != null)
            {
                return shop.ToWorld(station.Stand);
            }
            // GL669: never a spot inside a neighbouring building, where its worker would stand pressed against the wall.
            return walks.PushOut(walks.Front(Resident.Shop, -2.2f + 1.1f * (Resident.Index % 5), 1.2f + 0.9f * (Resident.Index / 5 % 2)), 0.4f);
        }
    }
}
