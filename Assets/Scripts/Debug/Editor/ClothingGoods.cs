using System;
using System.Collections.Generic;
using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Makes hides into leather and wool and fibre into cloth, and both into clothes (list inventory prototype, items 5 and
    /// 17-20): <i>Clube → Items → Create clothing goods</i> writes the items (tanned hides, leather, yarn, string, cloth,
    /// rags, the cloth garments and the leather gear), paints the garments' icons, writes the recipes (one per product: the
    /// hides and wools are swap variants of one recipe each), offers them at the campfire and the workbench, and prices
    /// them at the merchant. Rags aren't priced: they're worth nothing. Safe to run again. Afterwards rebuild the item
    /// catalog and bake icons (<see cref="CreateAll"/> does both).
    /// </summary>
    public static class ClothingGoods
    {
        private const string ItemFolder = "Assets/Config/Items";
        private const string RecipeFolder = "Assets/Config/Recipes";
        private const string IconFolder = "Assets/Textures/Items";
        private const string PricesPath = "Assets/Config/Economy/Merchant prices.asset";
        private const string CampfirePrefab = "Assets/Prefabs/Stations/Campfire.prefab";
        private const string BenchPrefab = "Assets/Prefabs/Stations/Workbench.prefab";
        private const int Size = 64;

        // What a garment's icon shows.
        private enum Cut { Shirt, Rags, Cuirass, Pants, Wraps, Boots, Helmet, Gloves, Bracers }

        private struct Good
        {
            public string Id, Name;
            public ItemType Type;
            public ItemIconShape Shape;
            public Color Tint;
            public int MaxStack, Coins;
            public WearSlot Worn;
            public int Points;
            public Cut Cut;
        }

        private static Good Material(string id, string name, ItemIconShape shape, Color tint, int coins)
        {
            return new Good { Id = id, Name = name, Type = ItemType.Organism, Shape = shape, Tint = tint, MaxStack = 64, Coins = coins };
        }

        private static Good Garment(string id, string name, ItemType type, WearSlot worn, int points, Cut cut, Color tint, int coins)
        {
            return new Good { Id = id, Name = name, Type = type, Shape = ItemIconShape.Painted, Tint = tint, MaxStack = 1, Coins = coins, Worn = worn, Points = points, Cut = cut };
        }

        // Rags 0 points, cloth 2, leather 3 (Outfit): a full cloth outfit +6 disposition, full leather +18.
        private static readonly Good[] Goods =
        {
            Material("tanned-bison-hide", "Tanned bison hide", ItemIconShape.Hide, new Color(0.55f, 0.4f, 0.26f), 30),
            Material("tanned-cow-hide", "Tanned cow hide", ItemIconShape.Hide, new Color(0.76f, 0.55f, 0.34f), 18),
            Material("tanned-deer-hide", "Tanned deer hide", ItemIconShape.Hide, new Color(0.88f, 0.72f, 0.5f), 15),
            Material("tanned-elk-hide", "Tanned elk hide", ItemIconShape.Hide, new Color(0.7f, 0.56f, 0.4f), 24),
            Material("leather", "Leather", ItemIconShape.Hide, new Color(0.5f, 0.27f, 0.12f), 12),
            Material("yarn", "Yarn", ItemIconShape.Coil, new Color(0.93f, 0.88f, 0.78f), 6),
            Material("string", "String", ItemIconShape.Coil, new Color(0.72f, 0.78f, 0.5f), 2),
            Material("cloth", "Cloth", ItemIconShape.Boards, new Color(0.84f, 0.8f, 0.68f), 10),
            Garment("rags", "Rags", ItemType.Clothing, WearSlot.Chest, 0, Cut.Rags, new Color(0.48f, 0.43f, 0.36f), 0),
            Garment("shirt", "Shirt", ItemType.Clothing, WearSlot.Chest, 2, Cut.Shirt, new Color(0.62f, 0.7f, 0.78f), 35),
            Garment("pants", "Pants", ItemType.Clothing, WearSlot.Legs, 2, Cut.Pants, new Color(0.38f, 0.44f, 0.58f), 35),
            Garment("foot-wraps", "Foot wraps", ItemType.Clothing, WearSlot.Feet, 2, Cut.Wraps, new Color(0.8f, 0.75f, 0.62f), 12),
            Garment("leather-helmet", "Leather helmet", ItemType.Armor, WearSlot.Head, 3, Cut.Helmet, new Color(0.52f, 0.32f, 0.16f), 30),
            Garment("leather-gloves", "Leather gloves", ItemType.Armor, WearSlot.Hands, 3, Cut.Gloves, new Color(0.6f, 0.38f, 0.2f), 15),
            Garment("leather-cuirass", "Leather cuirass", ItemType.Armor, WearSlot.Chest, 3, Cut.Cuirass, new Color(0.45f, 0.26f, 0.12f), 60),
            Garment("leather-bracers", "Leather bracers", ItemType.Armor, WearSlot.Arms, 3, Cut.Bracers, new Color(0.56f, 0.34f, 0.18f), 15),
            Garment("leather-boots", "Leather boots", ItemType.Armor, WearSlot.Feet, 3, Cut.Boots, new Color(0.4f, 0.24f, 0.12f), 30),
            Garment("leather-leggings", "Leather leggings", ItemType.Armor, WearSlot.Legs, 3, Cut.Pants, new Color(0.48f, 0.3f, 0.15f), 45),
        };

        /// <summary>Makes everything, then rebuilds the item catalog and bakes the modelled icons.</summary>
        [MenuItem("Clube/Items/Create clothing goods")]
        public static void CreateAll()
        {
            Create();
            ItemCatalogBuilder.RebuildAll();
            ItemIconBaker.Bake(false);
        }

        /// <summary>The items, icons, recipes, station lists and prices; returns the recipes written.</summary>
        public static List<Recipe> Create()
        {
            foreach (Good good in Goods)
            {
                WriteItem(good);
            }
            Price();

            ItemDefinition I(string name) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{name}.asset");
            ItemDefinition[] hides = { I("Bison hide"), I("Cow hide"), I("Deer hide"), I("Elk hide") };
            ItemDefinition[] tanned = { I("Tanned bison hide"), I("Tanned cow hide"), I("Tanned deer hide"), I("Tanned elk hide") };
            ItemDefinition[] wools = { I("Sheep wool"), I("Goat wool"), I("Yak wool"), I("Alpaca wool") };

            var campfire = new List<Recipe>
            {
                // Item 17: each hide tans over the fire into its own tanned hide.
                WriteRecipe("Tan hide (campfire)", StationKind.Campfire, RecipeWork.Time, 20f, tanned[0], 1, 3f, (hides[0], 1), Swaps(hides, tanned)),
            };
            var bench = new List<Recipe>
            {
                // Item 17: any tanned hide cuts into leather at the workbench.
                WriteRecipe("Cut leather", StationKind.Worktop, RecipeWork.Strikes, 3f, I("Leather"), 2, 3f, (tanned[0], 1), Swaps(tanned, null)),
                // Item 18: any wool spins into yarn, fibre into string; either weaves into cloth.
                WriteRecipe("Spin yarn", StationKind.Worktop, RecipeWork.Strikes, 2f, I("Yarn"), 1, 2f, (wools[0], 2), Swaps(wools, null)),
                WriteRecipe("Spin string", StationKind.Worktop, RecipeWork.Strikes, 2f, I("String"), 1, 2f, (I("Plant fibre"), 3), null),
                WriteRecipe("Weave cloth", StationKind.Worktop, RecipeWork.Strikes, 4f, I("Cloth"), 1, 3f, (I("Yarn"), 4), Swaps(new[] { I("Yarn"), I("String") }, null)),
                WriteRecipe("Sew shirt", StationKind.Worktop, RecipeWork.Strikes, 4f, I("Shirt"), 1, 4f, (I("Cloth"), 3), null),
                WriteRecipe("Sew pants", StationKind.Worktop, RecipeWork.Strikes, 4f, I("Pants"), 1, 4f, (I("Cloth"), 3), null),
                WriteRecipe("Sew foot wraps", StationKind.Worktop, RecipeWork.Strikes, 2f, I("Foot wraps"), 1, 2f, (I("Cloth"), 1), null),
                // Item 19: the leather gear.
                WriteRecipe("Make leather helmet", StationKind.Worktop, RecipeWork.Strikes, 4f, I("Leather helmet"), 1, 4f, (I("Leather"), 2), null),
                WriteRecipe("Make leather gloves", StationKind.Worktop, RecipeWork.Strikes, 3f, I("Leather gloves"), 1, 3f, (I("Leather"), 1), null),
                WriteRecipe("Make leather cuirass", StationKind.Worktop, RecipeWork.Strikes, 6f, I("Leather cuirass"), 1, 6f, (I("Leather"), 4), null),
                WriteRecipe("Make leather bracers", StationKind.Worktop, RecipeWork.Strikes, 3f, I("Leather bracers"), 1, 3f, (I("Leather"), 1), null),
                WriteRecipe("Make leather boots", StationKind.Worktop, RecipeWork.Strikes, 4f, I("Leather boots"), 1, 4f, (I("Leather"), 2), null),
                WriteRecipe("Make leather leggings", StationKind.Worktop, RecipeWork.Strikes, 5f, I("Leather leggings"), 1, 5f, (I("Leather"), 3), null),
            };
            Offer(CampfirePrefab, campfire);
            Offer(BenchPrefab, bench);
            AssetDatabase.SaveAssets();
            var all = new List<Recipe>(campfire);
            all.AddRange(bench);
            UnityEngine.Debug.Log($"Clothing goods: {Goods.Length} items, {all.Count} recipes. Add the rags to each scene's player and the recipes to its journal.");
            return all;
        }

        // The swap list of a recipe whose first input is items[0]: the rest, each making outputs[i] (null: the same output).
        private static (ItemDefinition[] For, ItemDefinition[] Outputs) Swaps(ItemDefinition[] items, ItemDefinition[] outputs)
        {
            return (AfterFirst(items), outputs != null ? AfterFirst(outputs) : null);
        }

        private static ItemDefinition[] AfterFirst(ItemDefinition[] items)
        {
            var rest = new ItemDefinition[items.Length - 1];
            Array.Copy(items, 1, rest, 0, rest.Length);
            return rest;
        }

        private static void WriteItem(Good good)
        {
            string path = $"{ItemFolder}/{good.Name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                AssetDatabase.CreateAsset(ItemDefinition.Create(good.Id, good.Name, good.MaxStack), path);
                item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            }
            var serialized = new SerializedObject(item);
            serialized.FindProperty("itemType").intValue = (int)good.Type;
            serialized.FindProperty("maxStack").intValue = good.MaxStack;
            serialized.FindProperty("iconShape").intValue = (int)good.Shape;
            serialized.FindProperty("iconTint").colorValue = good.Tint;
            serialized.FindProperty("wornOn").intValue = (int)good.Worn;
            serialized.FindProperty("outfitPoints").intValue = good.Points;
            if (good.Shape == ItemIconShape.Painted)
            {
                serialized.FindProperty("icon").objectReferenceValue = WriteIcon(good);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Recipe WriteRecipe(string name, StationKind station, RecipeWork work, float amount, ItemDefinition output, int count, float experience,
            (ItemDefinition Item, int Count) input, (ItemDefinition[] For, ItemDefinition[] Outputs)? swaps)
        {
            string path = $"{RecipeFolder}/{name}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
            if (recipe == null)
            {
                AssetDatabase.CreateAsset(Recipe.Create(station, new ItemAmount { Item = output, Count = count }, work, amount), path);
                recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
            }
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("station").intValue = (int)station;
            serialized.FindProperty("work").intValue = (int)work;
            serialized.FindProperty("amount").floatValue = amount;
            serialized.FindProperty("output.Item").objectReferenceValue = output;
            serialized.FindProperty("output.Count").intValue = count;
            SerializedProperty inputs = serialized.FindProperty("inputs");
            inputs.arraySize = 1;
            inputs.GetArrayElementAtIndex(0).FindPropertyRelative("Item").objectReferenceValue = input.Item;
            inputs.GetArrayElementAtIndex(0).FindPropertyRelative("Count").intValue = input.Count;
            serialized.FindProperty("experienceSkill").intValue = (int)SkillType.Crafting;
            serialized.FindProperty("experience").floatValue = experience;
            serialized.FindProperty("swapInput").objectReferenceValue = swaps.HasValue ? input.Item : null;
            SetList(serialized.FindProperty("swapFor"), swaps?.For);
            SetList(serialized.FindProperty("swapOutputs"), swaps?.Outputs);
            serialized.FindProperty("swapQualityFactors").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        private static void SetList(SerializedProperty list, ItemDefinition[] items)
        {
            list.arraySize = items?.Length ?? 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        // Adds the recipes a station prefab doesn't list yet.
        private static void Offer(string prefab, List<Recipe> recipes)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var serialized = new SerializedObject(root.GetComponent("StationObject"));
                SerializedProperty list = serialized.FindProperty("recipes");
                foreach (Recipe recipe in recipes)
                {
                    if (!Contains(list, recipe))
                    {
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>True when an object list holds <paramref name="value"/>.</summary>
        public static bool Contains(SerializedProperty list, Object value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Price()
        {
            var prices = AssetDatabase.LoadAssetAtPath<PriceList>(PricesPath);
            var priced = new SerializedObject(prices);
            SerializedProperty rows = priced.FindProperty("prices");
            foreach (Good good in Goods)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{good.Name}.asset");
                if (good.Coins <= 0 || prices.TryGetLot(item, out _, out _))
                {
                    continue;
                }
                rows.arraySize++;
                SerializedProperty row = rows.GetArrayElementAtIndex(rows.arraySize - 1);
                row.FindPropertyRelative("Item").objectReferenceValue = item;
                row.FindPropertyRelative("Coins").intValue = good.Coins;
                row.FindPropertyRelative("Units").intValue = 1;
                row.FindPropertyRelative("ByVolume").boolValue = false;
            }
            priced.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Icons: a flat garment seen from the front, lit from the upper left, outlined ----

        private static Texture2D WriteIcon(Good good)
        {
            Func<Vector2, bool> mask = MaskOf(good.Cut);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[Size * Size]);
            Color dark = Color.Lerp(good.Tint, Color.black, 0.45f), light = Color.Lerp(good.Tint, Color.white, 0.3f);
            var random = new System.Random(good.Id.GetHashCode());
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    if (!mask(p))
                    {
                        continue;
                    }
                    float shade = Mathf.Clamp01(0.55f + 0.35f * (p.y - p.x) * 0.5f);
                    Color color = shade > 0.5f ? Color.Lerp(good.Tint, light, (shade - 0.5f) * 2f) : Color.Lerp(dark, good.Tint, shade * 2f);
                    // A weave or grain, and the rags' grime.
                    color *= 0.92f + 0.12f * (float)random.NextDouble() - (good.Cut == Cut.Rags && random.NextDouble() < 0.12 ? 0.15f : 0f);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }
            Outline(texture, Color.Lerp(dark, Color.black, 0.4f));
            texture.Apply();

            string path = $"{IconFolder}/{good.Name.ToLowerInvariant()}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Each cut's shape in -1..1, y up.
        private static Func<Vector2, bool> MaskOf(Cut cut)
        {
            switch (cut)
            {
                case Cut.Shirt:
                    return p => Torso(p, 0.85f) && !Neck(p);
                case Cut.Rags:
                    // Torn: a ragged hem and sleeve ends, a hole or two.
                    return p => Torso(p, 0.78f) && !Neck(p) && p.y > -0.8f + 0.12f * Mathf.Abs(Mathf.Sin(p.x * 11f))
                        && Mathf.Abs(p.x) < 0.78f - 0.08f * Mathf.Abs(Mathf.Sin(p.y * 13f)) && (p - new Vector2(0.15f, -0.3f)).sqrMagnitude > 0.012f;
                case Cut.Cuirass:
                    return p => Torso(p, 0.52f) && !Neck(p) && !(Mathf.Abs(p.x) > 0.36f && p.y > 0.3f && Mathf.Abs(p.x) < 0.5f && p.y < 0.45f);
                case Cut.Pants:
                    return p => (Mathf.Abs(p.x) < 0.5f && p.y > 0.45f && p.y < 0.75f)
                        || (p.y > -0.85f && p.y <= 0.45f && Mathf.Abs(p.x) > 0.05f + Mathf.Max(0f, 0.45f - p.y) * 0.03f && Mathf.Abs(p.x) < 0.5f - (0.45f - p.y) * 0.08f);
                case Cut.Wraps:
                    return p => Boot(p, 0.2f) && !(Mathf.Abs(Mathf.Sin((p.x + p.y) * 12f)) < 0.12f && p.y > -0.3f);
                case Cut.Boots:
                    return p => Boot(p, 0.75f);
                case Cut.Helmet:
                    return p => (p.y > -0.2f && (p.x / 0.62f) * (p.x / 0.62f) + ((p.y + 0.15f) / 0.75f) * ((p.y + 0.15f) / 0.75f) < 1f)
                        || (p.y > -0.4f && p.y <= -0.2f && Mathf.Abs(p.x) < 0.78f);
                case Cut.Gloves:
                    return p => (Mathf.Abs(p.x + 0.05f) < 0.38f && p.y > -0.8f && p.y < 0.25f)
                        || (p.y >= 0.25f && p.y < 0.75f - Mathf.Abs(p.x + 0.05f) * 0.3f && p.x > -0.43f && p.x < 0.33f && Mathf.Repeat(p.x + 0.43f, 0.19f) < 0.15f)
                        || (p.x >= 0.33f && p.x < 0.7f && p.y > -0.25f + (p.x - 0.33f) * 0.6f && p.y < 0.05f + (p.x - 0.33f) * 0.6f);
                default: // Bracers: two laced arm guards.
                    return p => (Mathf.Abs(Mathf.Abs(p.x) - 0.42f) < 0.24f - (0.6f - p.y) * 0.04f && Mathf.Abs(p.y) < 0.7f)
                        && !(Mathf.Abs(Mathf.Abs(p.x) - 0.42f) < 0.03f && Mathf.Repeat(p.y, 0.25f) < 0.06f);
            }
        }

        private static bool Torso(Vector2 p, float sleeves)
        {
            bool body = Mathf.Abs(p.x) < 0.45f && p.y > -0.8f && p.y < 0.6f;
            bool arms = Mathf.Abs(p.x) < sleeves && p.y < 0.6f && p.y > 0.6f - 0.45f - (Mathf.Abs(p.x) - 0.45f) * 0.6f;
            return body || arms;
        }

        private static bool Neck(Vector2 p)
        {
            return p.x * p.x + (p.y - 0.65f) * (p.y - 0.65f) < 0.05f;
        }

        // A boot from the side, toe to the right: the shaft up to <paramref name="top"/>, the foot along the bottom.
        private static bool Boot(Vector2 p, float top)
        {
            bool shaft = p.x > -0.5f && p.x < 0.1f && p.y > -0.4f && p.y < top;
            bool foot = p.x > -0.5f && p.y > -0.8f && p.y <= -0.3f && (p.x < 0.5f || (p.x - 0.5f) * (p.x - 0.5f) + (p.y + 0.55f) * (p.y + 0.55f) < 0.07f);
            return shaft || foot;
        }

        private static void Outline(Texture2D texture, Color color)
        {
            Color[] pixels = texture.GetPixels();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (pixels[y * Size + x].a < 0.5f)
                    {
                        continue;
                    }
                    bool edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1
                        || pixels[y * Size + x - 1].a < 0.5f || pixels[y * Size + x + 1].a < 0.5f
                        || pixels[(y - 1) * Size + x].a < 0.5f || pixels[(y + 1) * Size + x].a < 0.5f;
                    if (edge)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }
    }
}
