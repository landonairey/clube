using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the town together as game objects (GL400, GL403, GL405) under a root standing on the town's levelled ground,
    /// turned so the gate faces the player's spawn: the cobblestone wall with its colliders, the gate, the five shops with
    /// their signs, sleeping rolls and counter displays, the props of the keepers' chores, and a keeper per shop. Positions
    /// come from <see cref="TownLayout"/>, models from <see cref="TownMeshes"/>.
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
            public int Seed;
        }

        /// <summary>Builds the town under <paramref name="root"/>, whose position and turn are the town's.</summary>
        public static List<Shopkeeper> Build(Transform root, Parts parts)
        {
            var keepers = new List<Shopkeeper>();
            BuildWall(root, parts);
            BuildGate(root, parts);
            foreach (ShopSpecialty shop in parts.Shops)
            {
                if (shop != null)
                {
                    keepers.Add(BuildShop(root, shop, parts));
                }
            }
            BuildChoreProps(root, parts);
            return keepers;
        }

        private static GameObject Model(string name, Transform parent, Mesh mesh, Material material, Vector2 at, float yaw, float y = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, y, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void Box(GameObject on, Vector3 centre, Vector3 size)
        {
            var box = on.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        private static void BuildWall(Transform root, Parts parts)
        {
            GameObject wall = Model("Wall", root, TownMeshes.Wall(parts.Seed), parts.Material, Vector2.zero, 0f);
            float h = TownLayout.WallHeight;
            float outer = TownLayout.OuterHalf;
            float inner = TownLayout.InnerHalf;
            float middle = (outer + inner) * 0.5f;
            float t = TownLayout.WallThickness;
            float gate = TownLayout.GateWidth * 0.5f;
            Box(wall, new Vector3(0f, h * 0.5f, middle), new Vector3(outer * 2f, h, t));
            Box(wall, new Vector3(-middle, h * 0.5f, 0f), new Vector3(t, h, inner * 2f));
            Box(wall, new Vector3(middle, h * 0.5f, 0f), new Vector3(t, h, inner * 2f));
            float sideLength = outer - gate;
            Box(wall, new Vector3(-(gate + sideLength * 0.5f), h * 0.5f, -middle), new Vector3(sideLength, h, t));
            Box(wall, new Vector3(gate + sideLength * 0.5f, h * 0.5f, -middle), new Vector3(sideLength, h, t));
        }

        private static void BuildGate(Transform root, Parts parts)
        {
            float middle = (TownLayout.OuterHalf + TownLayout.InnerHalf) * 0.5f;
            var gate = new GameObject("Gate");
            gate.transform.SetParent(root, false);
            gate.transform.localPosition = new Vector3(0f, 0f, -middle);
            gate.AddComponent<TownGate>().Build(parts.Material);
        }

        private static Shopkeeper BuildShop(Transform root, ShopSpecialty shop, Parts parts)
        {
            TownLayout.ShopPlot plot = TownLayout.PlotOf(shop.Kind);
            GameObject building = Model(shop.ShopName, root, TownMeshes.Shop(shop.Colour), parts.Material, plot.Centre, plot.Yaw);
            float w = TownLayout.ShopWidth * 0.5f;
            float d = TownLayout.ShopDepth * 0.5f;
            float h = TownLayout.ShopHeight;
            Box(building, new Vector3(0f, h * 0.5f, -d + 0.1f), new Vector3(w * 2f, h, 0.2f));
            Box(building, new Vector3(-w + 0.1f, h * 0.5f, 0f), new Vector3(0.2f, h, d * 2f));
            Box(building, new Vector3(w - 0.1f, h * 0.5f, 0.35f), new Vector3(0.2f, h, d * 2f - 0.7f));
            Box(building, new Vector3(0f, TownLayout.CounterHeight * 0.5f, TownLayout.CounterZ), new Vector3(TownLayout.CounterLength + 0.2f, TownLayout.CounterHeight, 0.7f));
            Box(building, new Vector3(0f, h + 0.6f, 0f), new Vector3(w * 2f + 0.5f, 0.4f, d * 2f + 0.8f));

            Sign(building.transform, shop.ShopName, new Vector3(0f, h + 0.36f, d + 0.1f));
            Model("Sleeping roll", building.transform, TownMeshes.SleepingRoll(), parts.Material, TownLayout.BedCentre, 0f, 0.1f);
            Display(building.transform, shop, parts);

            var keeperObject = new GameObject($"{shop.ShopName} keeper");
            keeperObject.transform.SetParent(root, false);
            Shopkeeper keeper = keeperObject.AddComponent<Shopkeeper>();
            keeper.Setup(shop, parts.Prices, parts.Hours, parts.Clock, root, parts.Material, parts.Seed + (int)shop.Kind * 977);
            return keeper;
        }

        // The shop's name on its sign board, facing out of the front.
        private static void Sign(Transform building, string text, Vector3 at)
        {
            var go = new GameObject("Sign text");
            go.transform.SetParent(building, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.045f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.22f, 0.14f, 0.08f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            // Long names shrink to fit the board.
            float width = mesh.GetComponent<MeshRenderer>().bounds.size.x;
            if (width > 2.3f)
            {
                go.transform.localScale = Vector3.one * (2.3f / width);
            }
        }

        // Example wares on the counter (GL403), spread along it and turned a little each.
        private static void Display(Transform building, ShopSpecialty shop, Parts parts)
        {
            IReadOnlyList<ItemDefinition> items = shop.Displayed;
            int count = Mathf.Min(items.Count, 5);
            var random = new System.Random(parts.Seed + (int)shop.Kind * 31);
            for (int i = 0; i < count; i++)
            {
                Mesh mesh = DisplayItemMeshes.For(items[i]);
                if (mesh == null)
                {
                    continue;
                }
                float x = count == 1 ? 0f : Mathf.Lerp(-1.9f, 1.9f, i / (float)(count - 1));
                float z = TownLayout.CounterZ + ((float)random.NextDouble() - 0.5f) * 0.25f;
                float yaw = ((float)random.NextDouble() - 0.5f) * 50f;
                Model($"Display {items[i].DisplayName}", building, mesh, parts.Material, new Vector2(x, z), yaw, TownLayout.CounterHeight);
            }
        }

        private static void BuildChoreProps(Transform root, Parts parts)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            Material m = parts.Material;

            GameObject furnace = Model("Furnace", props, TownMeshes.Furnace(), m, TownLayout.Furnace, 180f);
            Box(furnace, new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 2.2f, 1.5f));
            GameObject anvil = Model("Anvil", props, TownMeshes.Anvil(), m, TownLayout.Anvil, 0f);
            Box(anvil, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.45f));

            GameObject pile = Model("Woodpile", props, TownMeshes.Woodpile(parts.Seed), m, TownLayout.Woodpile, 0f);
            Box(pile, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 2.3f));
            Model("Seat", props, TownMeshes.Stump(), m, TownLayout.Seat, 0f);
            for (int i = 0; i < TownLayout.Trees.Length; i++)
            {
                GameObject tree = Model("Tree", props, TownMeshes.Tree(parts.Seed + i * 13), m, TownLayout.Trees[i], i * 70f);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, 2f, 0f);
                trunk.height = 4f;
                trunk.radius = 0.25f;
            }

            GameObject bench = Model("Bench", props, TownMeshes.Bench(), m, TownLayout.Bench, 0f);
            Box(bench, new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.44f, 1.6f));
            Model("Beer table", props, TownMeshes.Table(new Vector2(0.7f, 0.7f), 0.75f), m, TownLayout.BeerTable, 0f);

            Color[] ores =
            {
                DisplayItemMeshes.ColourOf("cassiterite"), DisplayItemMeshes.ColourOf("chalcopyrite"),
                DisplayItemMeshes.ColourOf("malachite"), DisplayItemMeshes.ColourOf("silver"), DisplayItemMeshes.ColourOf("gold"),
            };
            GameObject display = Model("Ore display", props, TownMeshes.DisplayTable(ores, parts.Seed), m, TownLayout.DisplayTable, 0f);
            Box(display, new Vector3(0f, 0.43f, 0f), new Vector3(0.8f, 0.86f, 2.2f));
        }
    }
}
