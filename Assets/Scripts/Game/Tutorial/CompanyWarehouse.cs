using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The Mining Corp's warehouse on the tutorial map (GL514), built in code across the work fields' open south end
    /// (<see cref="CompanyWork.Warehouse"/>). Only two rooms open: the dorm hall, through the back door, with the player's
    /// bunk (a bed to sleep in), and off it the crew lead's room, with a crate of valuables at the end of his bed. The
    /// company stores behind them stay locked. Crates of supplies stand round the yard. Every crate is the company's:
    /// taking from one is theft (<see cref="CompanyCrate"/>).
    /// </summary>
    public class CompanyWarehouse : MonoBehaviour
    {
        private const float WallHeight = 4.5f;
        private const float Thick = 0.3f;
        private const float DoorWidth = 1.6f;
        private const float DoorHeight = 2.4f;

        private static readonly Color Plank = new Color(0.55f, 0.43f, 0.3f);
        private static readonly Color DarkPlank = new Color(0.36f, 0.27f, 0.18f);
        private static readonly Color Floor = new Color(0.47f, 0.4f, 0.33f);
        private static readonly Color Roof = new Color(0.33f, 0.35f, 0.38f);
        private static readonly Color Blanket = new Color(0.28f, 0.36f, 0.5f);
        private static readonly Color Linen = new Color(0.85f, 0.83f, 0.78f);

        private LowPolyMeshBuilder mesh;
        private Material material;

        /// <summary>Where the player starts (map space): by the back door, facing the crew lead.</summary>
        public static readonly Vector3 Spawn = new Vector3(44f, CompanyWork.Ground, 18.2f);

        /// <summary>Where the crew lead stands (map space), out in the work fields.</summary>
        public static readonly Vector3 LeadSpot = new Vector3(44f, CompanyWork.Ground, 20.6f);

        /// <summary>Where the ore cart stands (map space), near the mountain's foot.</summary>
        public static readonly Vector3 CartSpot = new Vector3(52f, CompanyWork.Ground, 38f);

        /// <summary>The player's bunk.</summary>
        public SleepingRoll Bunk { get; private set; }

        /// <summary>
        /// Builds the warehouse, the beds and the crates under a parent placed like the world (map space), filling the yard's
        /// crates from one list and the crew lead's crate from another.
        /// </summary>
        public static CompanyWarehouse Build(Transform map, Material material, MiningCorpTutorial tutorial, ItemDefinition[] supplies, ItemDefinition[] valuables)
        {
            var go = new GameObject("Company warehouse");
            go.transform.SetParent(map, false);
            CompanyWarehouse house = go.AddComponent<CompanyWarehouse>();
            house.material = material;
            house.mesh = new LowPolyMeshBuilder();
            house.BuildShell();
            house.BuildRooms();
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = house.mesh.Make("Company warehouse");
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            house.Bunk = house.Bed(new Vector3(34f, 0f, 9.4f), 0f, true);
            foreach (float x in new[] { 37f, 40f, 43f, 46f, 54f })
            {
                house.Bed(new Vector3(x, 0f, 9.4f), 0f, false);
            }
            house.Bed(new Vector3(66.4f, 0f, 14.2f), 90f, false);
            house.Crate(new Vector3(64.8f, 0f, 14.2f), 90f, tutorial, valuables, "The crew lead's crate");
            house.Lamp(new Vector3(44f, WallHeight - 0.6f, 12f));
            house.Lamp(new Vector3(62f, WallHeight - 0.6f, 12f));

            Vector3[] yard = { new Vector3(33.4f, 0f, 19f), new Vector3(66.6f, 0f, 19.2f), new Vector3(31.4f, 0f, 38f), new Vector3(68.6f, 0f, 30f) };
            for (int i = 0; i < yard.Length; i++)
            {
                ItemDefinition item = supplies != null && supplies.Length > 0 ? supplies[i % supplies.Length] : null;
                house.Crate(yard[i], 20f * i, tutorial, item != null ? new[] { item } : null, "A supply crate");
            }
            return house;
        }

        private static float G => CompanyWork.Ground;

        /// <summary>
        /// Turns the ground under the warehouse to a material without grass (dirt), so no grass grows up through its
        /// floor. Map space, through the world's edit path.
        /// </summary>
        public static void ClearGround(WorldView world)
        {
            VoxelMaterial dirt = world.Config.Materials != null ? world.Config.Materials.Find("Dirt") : null;
            if (dirt == null || world.World == null)
            {
                return;
            }
            WorldGrid grid = world.World.Grid;
            Rect r = CompanyWork.Warehouse;
            Vector3Int min = grid.WorldToNearestSample(new Vector3(r.xMin, G - 0.75f, r.yMin));
            Vector3Int max = grid.WorldToNearestSample(new Vector3(r.xMax, G + 0.75f, r.yMax));
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        world.World.SetMaterial(new Vector3Int(x, y, z), dirt.Id);
                    }
                }
            }
        }

        // Floor, outer walls (a back door into the dorm hall, big shut doors on the front) and the roof.
        private void BuildShell()
        {
            Rect r = CompanyWork.Warehouse;
            Solid(new Vector3(r.center.x, G + 0.05f, r.center.y), new Vector3(r.width, 0.1f, r.height), Floor);
            WallX(r.xMin, r.xMax, r.yMax, 44f, false);
            WallX(r.xMin, r.xMax, r.yMin, float.NaN, false);
            WallZ(r.yMin, r.yMax, r.xMin, float.NaN);
            WallZ(r.yMin, r.yMax, r.xMax, float.NaN);
            // The front's loading doors, painted on.
            mesh.Box(new Vector3(r.center.x, G + 1.8f, r.yMin - Thick * 0.6f), new Vector3(6f, 3.6f, 0.1f), Quaternion.identity, DarkPlank);
            // The roof, with its gable ends.
            float rise = 3f;
            var eaves = new Vector3(r.center.x, G + WallHeight, r.center.y);
            mesh.GableRoof(eaves, r.width + 1f, r.height + 1f, rise, 0.25f, Roof);
            foreach (float x in new[] { r.xMin, r.xMax })
            {
                var a = new Vector3(x, G + WallHeight, r.yMin);
                var b = new Vector3(x, G + WallHeight, r.yMax);
                var top = new Vector3(x, G + WallHeight + rise, r.center.y);
                var inside = new Vector3(r.center.x, G + WallHeight, r.center.y);
                mesh.Triangle(a, b, top, inside, Plank);
            }
        }

        // The dorm hall (north), the crew lead's room (north-east) and the locked stores (south).
        private void BuildRooms()
        {
            Rect r = CompanyWork.Warehouse;
            const float hallSouth = 8f;
            const float leadWest = 56f;
            WallX(r.xMin, r.xMax, hallSouth, 50f, true);
            WallZ(hallSouth, r.yMax, leadWest, 12f);
        }

        // A wall along x at z, from x0 to x1, with a door centred at doorX (NaN for none), shut and locked when asked.
        private void WallX(float x0, float x1, float z, float doorX, bool locked)
        {
            if (float.IsNaN(doorX))
            {
                Solid(new Vector3((x0 + x1) * 0.5f, G + WallHeight * 0.5f, z), new Vector3(x1 - x0 + Thick, WallHeight, Thick), Plank);
                return;
            }
            float a = doorX - DoorWidth * 0.5f;
            float b = doorX + DoorWidth * 0.5f;
            Solid(new Vector3((x0 + a) * 0.5f, G + WallHeight * 0.5f, z), new Vector3(a - x0, WallHeight, Thick), Plank);
            Solid(new Vector3((b + x1) * 0.5f, G + WallHeight * 0.5f, z), new Vector3(x1 - b, WallHeight, Thick), Plank);
            Solid(new Vector3(doorX, G + (WallHeight + DoorHeight) * 0.5f, z), new Vector3(DoorWidth, WallHeight - DoorHeight, Thick), Plank);
            if (locked)
            {
                Door(new Vector3(doorX, G + DoorHeight * 0.5f, z), new Vector3(DoorWidth, DoorHeight, Thick * 0.6f));
            }
        }

        // A wall along z at x, from z0 to z1, with an open doorway centred at doorZ (NaN for none).
        private void WallZ(float z0, float z1, float x, float doorZ)
        {
            if (float.IsNaN(doorZ))
            {
                Solid(new Vector3(x, G + WallHeight * 0.5f, (z0 + z1) * 0.5f), new Vector3(Thick, WallHeight, z1 - z0 + Thick), Plank);
                return;
            }
            float a = doorZ - DoorWidth * 0.5f;
            float b = doorZ + DoorWidth * 0.5f;
            Solid(new Vector3(x, G + WallHeight * 0.5f, (z0 + a) * 0.5f), new Vector3(Thick, WallHeight, a - z0), Plank);
            Solid(new Vector3(x, G + WallHeight * 0.5f, (b + z1) * 0.5f), new Vector3(Thick, WallHeight, z1 - b), Plank);
            Solid(new Vector3(x, G + (WallHeight + DoorHeight) * 0.5f, doorZ), new Vector3(Thick, WallHeight - DoorHeight, DoorWidth), Plank);
        }

        // A box drawn into the warehouse's mesh with a collider of its own.
        private void Solid(Vector3 centre, Vector3 size, Color color)
        {
            mesh.Box(centre, size, Quaternion.identity, color);
            var box = new GameObject("Wall").AddComponent<BoxCollider>();
            box.transform.SetParent(transform, false);
            box.transform.localPosition = centre;
            box.size = size;
        }

        // A shut door that only says it's locked.
        private void Door(Vector3 centre, Vector3 size)
        {
            mesh.Box(centre, size, Quaternion.identity, DarkPlank);
            var door = new GameObject("Locked door");
            door.transform.SetParent(transform, false);
            door.transform.localPosition = centre;
            door.AddComponent<BoxCollider>().size = size;
            door.AddComponent<LockedDoor>();
        }

        // A bed centred at a point, its head towards +z turned by yaw; the player's own can be slept in.
        private SleepingRoll Bed(Vector3 centre, float yaw, bool own)
        {
            var bed = new LowPolyMeshBuilder();
            bed.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.95f, 0.3f, 2f), Quaternion.identity, DarkPlank);
            bed.Box(new Vector3(0f, 0.45f, 0.1f), new Vector3(0.85f, 0.12f, 1.75f), Quaternion.identity, own ? new Color(0.55f, 0.3f, 0.25f) : Blanket);
            bed.Box(new Vector3(0f, 0.55f, 0.75f), new Vector3(0.6f, 0.12f, 0.35f), Quaternion.identity, Linen);
            bed.Box(new Vector3(0f, 0.55f, 1f), new Vector3(0.95f, 0.8f, 0.08f), Quaternion.identity, DarkPlank);
            var go = new GameObject(own ? "Your bunk" : "Bunk");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(centre.x, G + 0.1f, centre.z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = bed.Make("Bunk");
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.35f, 0.1f);
            box.size = new Vector3(0.95f, 0.7f, 2f);
            if (!own)
            {
                return null;
            }
            SleepingRoll roll = go.AddComponent<SleepingRoll>();
            roll.FitsGround = false;
            roll.Label = "Sleep in your bunk";
            return roll;
        }

        private void Crate(Vector3 at, float yaw, MiningCorpTutorial tutorial, ItemDefinition[] loot, string title)
        {
            var crate = new LowPolyMeshBuilder();
            crate.Box(new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 0.7f), Quaternion.identity, Plank);
            crate.Box(new Vector3(0f, 0.72f, 0f), new Vector3(1.04f, 0.06f, 0.74f), Quaternion.identity, DarkPlank);
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                crate.Box(new Vector3(x, 0.35f, 0f), new Vector3(0.08f, 0.72f, 0.74f), Quaternion.identity, DarkPlank);
            }
            var go = new GameObject(title);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(at.x, G + 0.1f, at.z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = crate.Make("Crate");
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.38f, 0f);
            box.size = new Vector3(1.04f, 0.76f, 0.74f);
            CompanyCrate company = go.AddComponent<CompanyCrate>();
            company.Tutorial = tutorial;
            company.Loot = loot ?? new ItemDefinition[0];
            company.Title = title;
        }

        private void Lamp(Vector3 at)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(at.x, G + at.y, at.z);
            Light lamp = go.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 10f;
            lamp.intensity = 1.6f;
            lamp.color = new Color(1f, 0.85f, 0.6f);
            lamp.shadows = LightShadows.None;
        }
    }
}
