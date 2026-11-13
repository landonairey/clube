using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the town's game objects together (GL400, GL403, GL405, GL414, GL419, GL421, GL424) under its root, from its
    /// <see cref="TownPlan"/>: the gate, each shop's sign, sleeping roll and counter display, the props of the keepers'
    /// chores (the game's own anvil and furnace by the smithy), a sign and everlasting torches for each mine shaft, and a
    /// keeper per shop. The wall, the shops, the shafts, the lumber shop's trees and woodpile and the prospectors' ore
    /// samples are terrain, written by <see cref="TownSite"/>; everything here stands at the height of the ground or floor
    /// under it.
    /// </summary>
    public static class TownBuilder
    {
        /// <summary>What a town is built from.</summary>
        public struct Parts
        {
            public IReadOnlyList<ShopSpecialty> Shops;
            public PriceList Prices;
            public Material Material;
            public DayNightCycle Clock;
            public ShopHours Hours;
            public TownContext Town;

            /// <summary>The Clube/World Text material sign words are drawn with; the font's own when null.</summary>
            public Material TextMaterial;

            /// <summary>The ore group names for the shaft signs, in the plan's group order (tin, copper, silver, gold).</summary>
            public IReadOnlyList<string> OreNames;

            /// <summary>The game's anvil, furnace, torch and sleeping roll prefabs (GL419, GL421, GL417).</summary>
            public GameObject AnvilPrefab;
            public GameObject FurnacePrefab;
            public GameObject TorchPrefab;
            public GameObject SleepingRollPrefab;

            /// <summary>Coins each keeper has to buy with each day (GL426).</summary>
            public float DailyCoins;

            /// <summary>The settlement's name (GL470), on the sign outside its gate.</summary>
            public string Name;

            /// <summary>What the player has done here (GL471: the keepers' dispositions); may be null.</summary>
            public TradingPost Post;

            /// <summary>The wooden crate (a storage chest) the secret cave's valuables are hidden in (GL475).</summary>
            public GameObject CratePrefab;
        }

        /// <summary>Builds the town's objects under its root.</summary>
        public static List<Shopkeeper> Build(Parts parts)
        {
            var keepers = new List<Shopkeeper>();
            BuildGate(parts);
            BuildNameSign(parts);
            foreach (ShopSpecialty shop in parts.Shops)
            {
                if (shop != null)
                {
                    keepers.Add(BuildShop(shop, parts));
                }
            }
            BuildChoreProps(parts);
            BuildShafts(parts);
            BuildCave(parts);
            return keepers;
        }

        // A model at a point of the world's local metres, turned about y in the world's frame.
        private static GameObject Model(string name, Transform parent, Mesh mesh, Material material, Vector3 local, float yaw, TownContext town)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(town.ToScene(local), town.SceneRotation(yaw));
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return go;
        }

        // A copy of a prefab's look at a point of the world's local metres.
        private static GameObject Copied(GameObject prefab, string name, Transform parent, Vector3 local, float yaw, TownContext town, bool colliders)
        {
            GameObject copy = ModelCopy.Copy(prefab, parent, name, colliders);
            copy.transform.SetPositionAndRotation(town.ToScene(local), town.SceneRotation(yaw));
            return copy;
        }

        // A point of the town's frame at a height, in the world's local metres.
        private static Vector3 At(TownPlan plan, Vector2 town, float height)
        {
            Vector2 world = plan.ToWorld(town);
            return new Vector3(world.x, height, world.y);
        }

        // On the land as generated, where the town keeps its shape (the square, GL413).
        private static Vector3 OnGround(TownPlan plan, Vector2 town)
        {
            return At(plan, town, plan.GroundAt(town));
        }

        private static void Box(GameObject on, Vector3 centre, Vector3 size)
        {
            var box = on.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        private static void BuildGate(Parts parts)
        {
            TownPlan plan = parts.Town.Plan;
            float middle = (TownLayout.OuterHalf + TownLayout.InnerHalf) * 0.5f;
            GameObject gate = Model("Gate", parts.Town.Root, null, null, At(plan, new Vector2(0f, -middle), plan.GateGround), plan.Yaw, parts.Town);
            gate.AddComponent<TownGate>().Build(parts.Material);
        }

        // The wooden sign outside the gate (GL470): the trading post's name, beside the way in, facing whoever comes up to it.
        private static void BuildNameSign(Parts parts)
        {
            if (string.IsNullOrEmpty(parts.Name))
            {
                return;
            }
            TownPlan plan = parts.Town.Plan;
            var at = new Vector2(TownLayout.GateWidth * 0.5f + 1.6f, -TownLayout.OuterHalf - 1.4f);
            GameObject sign = Model("Trading post sign", parts.Town.Root, TownMeshes.SignPost(1.9f, 0.62f), parts.Material, OnGround(plan, at), plan.Yaw + 180f, parts.Town);
            Text(sign.transform, $"{parts.Name}\nTrading Post", new Vector3(0f, 1.62f, 0.03f), parts.TextMaterial, 0.03f);
            Box(sign, new Vector3(0f, 1.1f, 0f), new Vector3(1.9f, 2.2f, 0.12f));
        }

        private static Shopkeeper BuildShop(ShopSpecialty shop, Parts parts)
        {
            TownContext town = parts.Town;
            TownPlan plan = town.Plan;
            TownLayout.ShopPlot plot = TownLayout.PlotOf(shop.Kind);
            float floor = plan.FloorHeight(shop.Kind);
            float yaw = plan.Yaw + plot.Yaw;
            var root = new GameObject(shop.ShopName).transform;
            root.SetParent(town.Root, false);
            root.SetPositionAndRotation(town.ToScene(At(plan, plot.Centre, floor)), town.SceneRotation(yaw));

            FitShop(root, shop, parts.Material, parts.TextMaterial, parts.SleepingRollPrefab, plan.Seed);

            var keeperObject = new GameObject($"{shop.ShopName} keeper");
            keeperObject.transform.SetParent(town.Root, false);
            Shopkeeper keeper = keeperObject.AddComponent<Shopkeeper>();
            keeper.Setup(shop, parts.Prices, parts.Hours, parts.Clock, town, parts.Material, plan.Seed + (int)shop.Kind * 977, parts.DailyCoins,
                parts.Post != null ? parts.Post.DispositionOf(shop.Kind) : null);
            return keeper;
        }

        /// <summary>
        /// A shop's fittings under its root (at the middle of its floor, its front out of +z): the sign under the lintel, the
        /// keeper's sleeping roll and the wares on the counter. The town's shops and a planned settlement's (GL613) share it.
        /// </summary>
        public static void FitShop(Transform root, ShopSpecialty shop, Material material, Material textMaterial, GameObject sleepingRollPrefab, int seed)
        {
            float d = TownLayout.ShopDepth * 0.5f;
            // The sign hangs under the lintel over the open front.
            GameObject sign = Local("Sign", root, TownMeshes.SignBoard(shop.Colour), material, new Vector3(0f, TownLayout.ShopHeight - 0.45f, d + 0.06f));
            Text(sign.transform, shop.ShopName, new Vector3(0f, 0.2f, 0.045f), textMaterial, 0.04f);
            // The game's own sleeping roll, lying along the back wall on the floor (GL417).
            if (sleepingRollPrefab != null)
            {
                GameObject roll = ModelCopy.Copy(sleepingRollPrefab, root, "Sleeping roll");
                roll.transform.localPosition = new Vector3(TownLayout.BedCentre.x, 0f, TownLayout.BedCentre.y);
                roll.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            Display(root, shop, material, seed);
        }

        // A model in a shop's (or any parent's) own frame.
        private static GameObject Local(string name, Transform parent, Mesh mesh, Material material, Vector3 at, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return go;
        }

        // Words on a sign, facing out of its front (+z), at a fixed size (GL420: measuring the words to fit read 0 before
        // the text mesh was built, so long names ran off the board).
        internal static void Text(Transform parent, string text, Vector3 at, Material material, float size)
        {
            var go = new GameObject("Sign text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.22f, 0.14f, 0.08f);
            if (material != null)
            {
                material.mainTexture = font.material.mainTexture;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : font.material;
        }

        // The items' own models on the counter (GL403, GL424), spread along it and turned a little each. The prospectors'
        // counter shows real ore samples instead, written into the terrain by TownSite.
        private static void Display(Transform shopRoot, ShopSpecialty shop, Material material, int seed)
        {
            if (shop.Kind == ShopKind.Prospector)
            {
                return;
            }
            IReadOnlyList<ItemDefinition> items = shop.Displayed;
            int count = Mathf.Min(items.Count, 5);
            var random = new System.Random(seed + (int)shop.Kind * 31);
            float half = TownLayout.CounterLength * 0.5f - 0.3f;
            for (int i = 0; i < count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }
                float x = count == 1 ? 0f : Mathf.Lerp(-half, half, i / (float)(count - 1));
                float z = TownLayout.CounterZ - 0.25f + ((float)random.NextDouble() - 0.5f) * 0.1f;
                float yaw = ((float)random.NextDouble() - 0.5f) * 40f;
                GameObject model = DisplayModel(items[i], shopRoot, material);
                model.transform.localPosition += new Vector3(x, TownLayout.CounterHeight, z);
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            }
        }

        // An item as it is in the game: a tool's or bucket's own held model lying on its side, a placeable's own prefab made
        // small (crate, furnace, sleeping roll, torch), anything else its small display model (ingots, buns, bricks).
        private static GameObject DisplayModel(ItemDefinition item, Transform parent, Material material)
        {
            string name = $"Display {item.DisplayName}";
            switch (item)
            {
                case ToolDefinition tool:
                {
                    // Standing on its handle's end, leaning back a little, so it shows over the counter's edge.
                    Mesh mesh = HeldMesh(tool);
                    GameObject go = Local(name, parent, mesh, material, new Vector3(0f, -mesh.bounds.min.y, 0f));
                    go.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    return go;
                }
                case BucketDefinition _:
                    return Local(name, parent, HeldMesh(item), material, new Vector3(0f, 0.36f, 0f));
                case TorchDefinition torch when torch.Prefab != null:
                {
                    GameObject go = ModelCopy.Copy(torch.Prefab, parent, name);
                    go.transform.localPosition = new Vector3(0.35f, 0.04f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    return go;
                }
                case PlaceableDefinition placeable when placeable.Prefab != null:
                    return ModelCopy.Copy(placeable.Prefab, parent, name, false, 0.5f);
                default:
                    return Local(name, parent, DisplayItemMeshes.For(item), material, Vector3.zero);
            }
        }

        private static readonly Dictionary<ItemDefinition, Mesh> HeldMeshes = new Dictionary<ItemDefinition, Mesh>();

        private static Mesh HeldMesh(ItemDefinition item)
        {
            if (!HeldMeshes.TryGetValue(item, out Mesh mesh) || mesh == null)
            {
                mesh = item is BucketDefinition ? HeldToolMeshes.BuildBucket(false) : HeldToolMeshes.Build(item as ToolDefinition, false);
                HeldMeshes[item] = mesh;
            }
            return mesh;
        }

        /// <summary>The blacksmith's disposition that opens the town's anvil and furnace to the player (GL472).</summary>
        public const int SmithyDisposition = 25;

        private static string BlacksmithName(Parts parts)
        {
            foreach (ShopSpecialty shop in parts.Shops)
            {
                if (shop != null && shop.Kind == ShopKind.Blacksmith)
                {
                    return shop.KeeperName;
                }
            }
            return "the blacksmith";
        }

        private static void BuildChoreProps(Parts parts)
        {
            TownContext town = parts.Town;
            TownPlan plan = town.Plan;
            var props = new GameObject("Props").transform;
            props.SetParent(town.Root, false);
            Material m = parts.Material;
            float yaw = plan.Yaw;

            // The game's own furnace and anvil in the yard beside the smithy (GL419): their looks until the blacksmith's
            // disposition reaches 25, then the real stations (GL472).
            Disposition smith = parts.Post != null ? parts.Post.DispositionOf(ShopKind.Blacksmith) : new Disposition();
            string keeper = BlacksmithName(parts);
            GameObject furnace = Copied(parts.FurnacePrefab, "Furnace", props, OnGround(plan, TownLayout.Furnace), yaw + 180f, town, true);
            furnace.AddComponent<TownStation>().Setup(parts.FurnacePrefab, smith, SmithyDisposition, keeper, "furnace");
            GameObject anvil = Copied(parts.AnvilPrefab, "Anvil", props, OnGround(plan, TownLayout.Anvil), yaw + 90f, town, true);
            anvil.AddComponent<TownStation>().Setup(parts.AnvilPrefab, smith, SmithyDisposition, keeper, "anvil");

            // The lumber keeper's seat by the woodpile (the woodpile and the trees are wood in the terrain, GL423).
            Model("Seat", props, TownMeshes.Stump(), m, OnGround(plan, TownLayout.Seat), yaw, town);

            GameObject bench = Model("Bench", props, TownMeshes.Bench(), m, OnGround(plan, TownLayout.Bench), yaw, town);
            Box(bench, new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.44f, 1.6f));

            Color[] ores =
            {
                DisplayItemMeshes.ColourOf("cassiterite"), DisplayItemMeshes.ColourOf("chalcopyrite"),
                DisplayItemMeshes.ColourOf("malachite"), DisplayItemMeshes.ColourOf("silver"), DisplayItemMeshes.ColourOf("gold"),
            };
            GameObject display = Model("Ore display", props, TownMeshes.DisplayTable(ores, plan.Seed), m, OnGround(plan, TownLayout.DisplayTable), yaw, town);
            Box(display, new Vector3(0f, 0.43f, 0f), new Vector3(0.8f, 0.86f, 2.2f));
        }

        // A signpost at each shaft's head naming its ore, and the game's torches (everlasting, GL421) standing at every other
        // turn of the stair and at the face (GL414).
        private static void BuildShafts(Parts parts)
        {
            TownContext town = parts.Town;
            TownPlan plan = town.Plan;
            var shafts = new GameObject("Shafts").transform;
            shafts.SetParent(town.Root, false);
            Mesh post = TownMeshes.SignPost();
            foreach (TownShaft shaft in plan.Shafts)
            {
                string name = parts.OreNames != null && shaft.Group < parts.OreNames.Count ? parts.OreNames[shaft.Group] : "Ore";
                var root = new GameObject($"{name} mine").transform;
                root.SetParent(shafts, false);
                // The post stands just outside the middle of the stairwell's near side, its board facing out.
                // An open pit's post stands just past its rim (GL474), in daylight: no torches.
                float signOffset = shaft.IsPit ? shaft.PitRadius + 0.6f : TownStructures.SpiralHalf + TownStructures.ShaftWidth * 0.5f + 0.35f;
                Vector2 corner = shaft.Head + new Vector2(0f, -signOffset);
                GameObject sign = Model("Sign", root, post, parts.Material, OnGround(plan, corner), plan.Yaw + 180f, town);
                Text(sign.transform, shaft.IsPit ? $"{name} pit" : $"{name} mine", new Vector3(0f, 1.62f, 0.03f), parts.TextMaterial, 0.03f);
                if (parts.TorchPrefab == null || shaft.IsPit)
                {
                    continue;
                }
                Vector2 head = plan.ToWorld(shaft.Head);
                List<Vector3> path = shaft.Path;
                for (int i = 1; i < path.Count; i++)
                {
                    bool face = i == path.Count - 1;
                    if (i % 2 == 0 && !face)
                    {
                        continue;
                    }
                    // Against the outside of the turn, out of the way; at the face, beside it.
                    Vector3 at = path[i];
                    Vector2 outward = new Vector2(at.x - head.x, at.z - head.y);
                    if (face)
                    {
                        Vector3 along = path[i] - path[i - 1];
                        outward = new Vector2(-along.z, along.x);
                    }
                    outward = outward.sqrMagnitude > 1e-4f ? outward.normalized * 0.4f : Vector2.zero;
                    Torch(parts, root, at + new Vector3(outward.x, 0f, outward.y));
                }
            }
        }

        // How many crates the cave holds, and how many kinds of valuables go in each (GL475).
        private const int CaveCrates = 3;
        private const int ValuablesPerCrate = 3;

        // The secret cave (GL475; its room and tunnel are terrain, written by TownSite): crates of valuables from the shops
        // against the far wall, one everlasting torch, and the thief between them and the way in.
        private static void BuildCave(Parts parts)
        {
            TownContext town = parts.Town;
            TownCave cave = town.Plan.Cave;
            if (cave == null)
            {
                return;
            }
            var root = new GameObject("Secret cave").transform;
            root.SetParent(town.Root, false);
            Vector3 room = cave.Room;
            var outward = new Vector3(cave.Outward.x, 0f, cave.Outward.y);
            var across = new Vector3(-outward.z, 0f, outward.x);
            float facingOut = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;

            List<ItemPrice> valuables = Valuables(parts);
            var random = new System.Random(town.Plan.Seed + 4751);
            for (int i = 0; i < CaveCrates && parts.CratePrefab != null; i++)
            {
                // Spread round the far side of the room, each turned to face its middle.
                float angle = (i - (CaveCrates - 1) * 0.5f) * 45f;
                Vector3 towardsWall = Quaternion.Euler(0f, angle, 0f) * -outward;
                Vector3 at = room + towardsWall * (TownCave.RoomRadius - 0.75f);
                GameObject crate = Object.Instantiate(parts.CratePrefab, root);
                crate.name = $"Cave crate {i + 1}";
                crate.transform.SetPositionAndRotation(town.ToScene(at), town.SceneRotation(Mathf.Atan2(-towardsWall.x, -towardsWall.z) * Mathf.Rad2Deg));
                if (crate.TryGetComponent(out PlacedObject placed))
                {
                    placed.Fixed = true;
                    placed.Item = null;
                }
                if (!crate.TryGetComponent(out StorageChest chest))
                {
                    continue;
                }
                chest.MadeOf(1f);
                for (int k = 0; k < ValuablesPerCrate && valuables.Count > 0; k++)
                {
                    ItemPrice valuable = valuables[(i * ValuablesPerCrate + k) % valuables.Count];
                    chest.Storage.Add(valuable.Item, valuable.Lot * (1 + random.Next(3)));
                }
                parts.Post?.RegisterCrate(i, chest);
            }

            if (parts.TorchPrefab != null)
            {
                Torch(parts, root, room + across * (TownCave.RoomRadius - 0.5f));
            }

            var thiefObject = new GameObject("Thief");
            thiefObject.transform.SetParent(root, false);
            thiefObject.transform.SetPositionAndRotation(town.ToScene(room + outward * 0.4f - across * 0.6f), town.SceneRotation(facingOut));
            thiefObject.AddComponent<FigureRig>().Build(FigurePreset.Thief, parts.Material, town.Plan.Seed + 475);
            thiefObject.AddComponent<TownThief>();
        }

        // Goods worth hiding: not tools, buckets, torches, drinks or things to place (ingots, ore, bricks and the like).
        internal static bool IsValuable(ItemDefinition item)
        {
            return item != null && !(item is ToolDefinition) && !(item is BucketDefinition) && !(item is TorchDefinition)
                && !(item is ConsumableDefinition) && !(item is PlaceableDefinition);
        }

        // The dearest goods the shops sell, one of each kind, dearest first (GL475: what the thief took).
        private static List<ItemPrice> Valuables(Parts parts)
        {
            var best = new Dictionary<ItemDefinition, ItemPrice>();
            foreach (ShopSpecialty shop in parts.Shops)
            {
                if (shop == null)
                {
                    continue;
                }
                foreach (ItemPrice offer in shop.Offers(parts.Prices))
                {
                    if (IsValuable(offer.Item) && (!best.TryGetValue(offer.Item, out ItemPrice had) || had.Coins < offer.Coins))
                    {
                        best[offer.Item] = offer;
                    }
                }
            }
            var list = new List<ItemPrice>(best.Values);
            list.Sort((a, b) => (b.Coins / (float)b.Lot).CompareTo(a.Coins / (float)a.Lot));
            if (list.Count > CaveCrates * ValuablesPerCrate)
            {
                list.RemoveRange(CaveCrates * ValuablesPerCrate, list.Count - CaveCrates * ValuablesPerCrate);
            }
            return list;
        }

        // One of the game's own torches, standing, that never burns out and can't be picked up (GL421).
        private static void Torch(Parts parts, Transform parent, Vector3 local)
        {
            GameObject torch = Object.Instantiate(parts.TorchPrefab, parent);
            torch.name = "Everlasting torch";
            torch.transform.SetPositionAndRotation(parts.Town.ToScene(local), parts.Town.SceneRotation(0f));
            if (torch.TryGetComponent(out PlacedTorch burning))
            {
                burning.Everlasting = true;
            }
            if (torch.TryGetComponent(out PlacedObject placed))
            {
                placed.Fixed = true;
            }
        }
    }
}
