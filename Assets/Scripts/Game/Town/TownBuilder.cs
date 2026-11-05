using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the town's game objects together (GL400, GL403, GL405, GL414) under its root, from its <see cref="TownPlan"/>:
    /// the gate, each shop's sign, sleeping roll and counter display, the props of the keepers' chores, a sign and lanterns
    /// for each mine shaft, and a keeper per shop. The wall, the shops and the shafts themselves are terrain, written by
    /// <see cref="TownSite"/>; everything here stands at the height of the ground or floor under it.
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
        }

        /// <summary>Builds the town's objects under its root.</summary>
        public static List<Shopkeeper> Build(Parts parts)
        {
            var keepers = new List<Shopkeeper>();
            BuildGate(parts);
            foreach (ShopSpecialty shop in parts.Shops)
            {
                if (shop != null)
                {
                    keepers.Add(BuildShop(shop, parts));
                }
            }
            BuildChoreProps(parts);
            BuildShafts(parts);
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

            float d = TownLayout.ShopDepth * 0.5f;
            // The sign hangs under the lintel over the open front.
            GameObject sign = Local("Sign", root, TownMeshes.SignBoard(shop.Colour), parts.Material, new Vector3(0f, TownLayout.ShopHeight - 0.45f, d + 0.06f));
            Text(sign.transform, shop.ShopName, new Vector3(0f, 0.2f, 0.045f), parts.TextMaterial);
            Local("Sleeping roll", root, TownMeshes.SleepingRoll(), parts.Material, new Vector3(TownLayout.BedCentre.x, 0f, TownLayout.BedCentre.y));
            Display(root, shop, parts);

            var keeperObject = new GameObject($"{shop.ShopName} keeper");
            keeperObject.transform.SetParent(town.Root, false);
            Shopkeeper keeper = keeperObject.AddComponent<Shopkeeper>();
            keeper.Setup(shop, parts.Prices, parts.Hours, parts.Clock, town, parts.Material, plan.Seed + (int)shop.Kind * 977);
            return keeper;
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

        // Words on a sign, facing out of its front (+z).
        private static void Text(Transform parent, string text, Vector3 at, Material material, float width = 2.1f)
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
            mesh.characterSize = 0.04f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.22f, 0.14f, 0.08f);
            if (material != null)
            {
                material.mainTexture = font.material.mainTexture;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : font.material;
            float measured = go.GetComponent<MeshRenderer>().bounds.size.x;
            if (measured > width)
            {
                go.transform.localScale = Vector3.one * (width / measured);
            }
        }

        // Example wares on the counter (GL403), spread along it and turned a little each.
        private static void Display(Transform shopRoot, ShopSpecialty shop, Parts parts)
        {
            IReadOnlyList<ItemDefinition> items = shop.Displayed;
            int count = Mathf.Min(items.Count, 5);
            var random = new System.Random(parts.Town.Plan.Seed + (int)shop.Kind * 31);
            float half = TownLayout.CounterLength * 0.5f - 0.25f;
            for (int i = 0; i < count; i++)
            {
                Mesh mesh = DisplayItemMeshes.For(items[i]);
                if (mesh == null)
                {
                    continue;
                }
                float x = count == 1 ? 0f : Mathf.Lerp(-half, half, i / (float)(count - 1));
                float z = TownLayout.CounterZ - 0.25f + ((float)random.NextDouble() - 0.5f) * 0.15f;
                float yaw = ((float)random.NextDouble() - 0.5f) * 50f;
                Local($"Display {items[i].DisplayName}", shopRoot, mesh, parts.Material, new Vector3(x, TownLayout.CounterHeight, z), yaw);
            }
        }

        private static void BuildChoreProps(Parts parts)
        {
            TownContext town = parts.Town;
            TownPlan plan = town.Plan;
            var props = new GameObject("Props").transform;
            props.SetParent(town.Root, false);
            Material m = parts.Material;
            float yaw = plan.Yaw;

            GameObject furnace = Model("Furnace", props, TownMeshes.Furnace(), m, OnGround(plan, TownLayout.Furnace), yaw + 180f, town);
            Box(furnace, new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 2.2f, 1.5f));
            GameObject anvil = Model("Anvil", props, TownMeshes.Anvil(), m, OnGround(plan, TownLayout.Anvil), yaw, town);
            Box(anvil, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.45f));

            GameObject pile = Model("Woodpile", props, TownMeshes.Woodpile(plan.Seed), m, OnGround(plan, TownLayout.Woodpile), yaw, town);
            Box(pile, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 2.3f));
            Model("Seat", props, TownMeshes.Stump(), m, OnGround(plan, TownLayout.Seat), yaw, town);
            for (int i = 0; i < TownLayout.Trees.Length; i++)
            {
                GameObject tree = Model("Tree", props, TownMeshes.Tree(plan.Seed + i * 13), m, OnGround(plan, TownLayout.Trees[i]), yaw + i * 70f, town);
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, 2f, 0f);
                trunk.height = 4f;
                trunk.radius = 0.25f;
            }

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

        // A signpost at each shaft's head naming its ore, and lanterns down the stairs and at the face (GL414).
        private static void BuildShafts(Parts parts)
        {
            TownContext town = parts.Town;
            TownPlan plan = town.Plan;
            var shafts = new GameObject("Shafts").transform;
            shafts.SetParent(town.Root, false);
            Mesh post = TownMeshes.SignPost();
            Mesh lantern = TownMeshes.Lantern();
            foreach (TownShaft shaft in plan.Shafts)
            {
                string name = parts.OreNames != null && shaft.Group < parts.OreNames.Count ? parts.OreNames[shaft.Group] : "Ore";
                var root = new GameObject($"{name} mine").transform;
                root.SetParent(shafts, false);
                // The post stands at the stairwell's corner, its board facing the way down.
                Vector2 corner = shaft.Head + new Vector2(-TownLayout.ShaftHeadRadius + 0.2f, -TownLayout.ShaftHeadRadius + 0.2f);
                GameObject sign = Model("Sign", root, post, parts.Material, OnGround(plan, corner), plan.Yaw + 45f, town);
                Text(sign.transform, $"{name} mine", new Vector3(0f, 1.62f, 0.03f), parts.TextMaterial, 0.95f);
                // Lanterns hung over every other turn of the stair and at the face, each with a small warm light.
                List<Vector3> path = shaft.Path;
                for (int i = 1; i < path.Count; i++)
                {
                    if (i % 2 == 1 || i == path.Count - 1)
                    {
                        Vector3 at = path[i] + Vector3.up * (TownStructures.ShaftHeight - 0.35f);
                        GameObject hung = Model("Lantern", root, lantern, parts.Material, at, 0f, town);
                        var light = new GameObject("Light").AddComponent<Light>();
                        light.transform.SetParent(hung.transform, false);
                        light.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                        light.type = LightType.Point;
                        light.range = 6f;
                        light.intensity = 1.6f;
                        light.color = new Color(1f, 0.72f, 0.4f);
                        light.shadows = LightShadows.None;
                    }
                }
            }
        }
    }
}
