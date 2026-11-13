using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL612: inventory icons are pictures of low-poly models, chosen by data and framed by arithmetic.</summary>
    public class ItemIconTests
    {
        // ---- the camera's framing ----

        [Test]
        public void Framing_HoldsEveryPoint_CentredWithTheMargin()
        {
            var points = new List<Vector3>
            {
                new Vector3(-0.3f, 0f, 0.2f), new Vector3(0.7f, 0.1f, -0.4f), new Vector3(0.1f, 1.4f, 0f), new Vector3(0f, 0.2f, 0.9f),
            };
            foreach (Quaternion view in new[] { Quaternion.identity, IconFraming.ThreeQuarter, Quaternion.Euler(80f, 200f, 0f) })
            {
                IconFraming framing = IconFraming.Fit(points, view, 0.1f);
                Vector2 min = Vector2.one * 9f, max = Vector2.one * -9f;
                foreach (Vector3 point in points)
                {
                    Vector3 framed = framing.ToFrame(point);
                    min = Vector2.Min(min, framed);
                    max = Vector2.Max(max, framed);
                    Assert.That(framed.z, Is.GreaterThan(framing.NearClip).And.LessThan(framing.FarClip), "in front of the camera, within its clip planes");
                }
                // Centred both ways, and the wider way reaches the margin exactly.
                Assert.That(min.x + max.x, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(min.y + max.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(Mathf.Max(max.x, max.y), Is.EqualTo(0.9f).Within(1e-4f));
                Assert.That(Mathf.Min(max.x, max.y), Is.LessThanOrEqualTo(0.9f + 1e-4f));
            }
        }

        [Test]
        public void Framing_OfABox_FitsItsTurnedCorners()
        {
            var bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 1f, 0.1f));
            // A thin upright box turned onto the diagonal is as wide as it is tall.
            IconFraming framing = IconFraming.Fit(bounds, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -45f)), Quaternion.identity, 0f);
            float across = (1f + 0.1f) / Mathf.Sqrt(2f);
            Assert.That(framing.OrthographicSize, Is.EqualTo(across * 0.5f).Within(1e-4f));
            Assert.That(framing.ToFrame(Quaternion.Euler(0f, 0f, -45f) * new Vector3(0f, 0.5f, 0f)).x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Framing_OfNothing_IsStillACamera()
        {
            IconFraming framing = IconFraming.Fit(new List<Vector3>(), Quaternion.identity);
            Assert.That(framing.OrthographicSize, Is.GreaterThan(0f));
            Assert.That(framing.FarClip, Is.GreaterThan(framing.NearClip));
        }

        // ---- which model an item gets ----

        [Test]
        public void Shape_IsTheAssetsChoice_OrByKindAndMaterial()
        {
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition dirt = ItemDefinition.Create("dirt", "Dirt");
            ItemDefinition crushed = ItemDefinition.Create("crushed-stone", "Crushed stone").WithPurityAs(stone).WithIcon(ItemIconShape.Gravel);
            ItemDefinition pearl = ItemDefinition.Create("pearl", "Pearl");
            ItemDefinition ingot = ItemDefinition.Create("ingot", "Ingot").WithIcon(ItemIconShape.Ingot, Color.red);
            VoxelMaterial rock = VoxelMaterial.Create(1, "Stone", VoxelMaterialCategory.Aggregate);
            VoxelMaterial soil = VoxelMaterial.Create(2, "Dirt", VoxelMaterialCategory.Aggregate);
            SetField(rock, "drop", stone);
            SetField(soil, "drop", dirt);
            SetField(soil, "isSoft", true);
            MaterialRegistry materials = MaterialRegistry.Create(rock, soil);

            Assert.AreEqual(ItemIconShape.Chunk, ItemIconShapes.Resolve(stone, materials), "rock: a chunk, as the loose stones");
            Assert.AreEqual(ItemIconShape.Mound, ItemIconShapes.Resolve(dirt, materials), "soil: a mound");
            Assert.AreEqual(ItemIconShape.Chunk, ItemIconShapes.Resolve(pearl, materials), "a new item of no material still gets a chunk");
            Assert.AreEqual(ItemIconShape.Gravel, ItemIconShapes.Resolve(crushed, materials));
            Assert.AreEqual(ItemIconShape.Ingot, ItemIconShapes.Resolve(ingot, materials));
            Assert.AreEqual(Color.red, ingot.IconTint);
            Assert.AreEqual(ItemIconShape.Model, ItemIconShapes.Resolve(PlaceableDefinition.Create("campfire", "Campfire"), materials));
            Assert.AreEqual(ItemIconShape.Painted, ItemIconShapes.Resolve(null, materials));

            Assert.AreSame(rock, ItemIconShapes.MaterialOf(stone, materials));
            Assert.AreSame(rock, ItemIconShapes.MaterialOf(crushed, materials), "a crushed ore wears its ore's texture");
            Assert.IsNull(ItemIconShapes.MaterialOf(ingot, materials));
        }

        private static void SetField(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (value is bool flag)
            {
                property.boolValue = flag;
            }
            else
            {
                property.objectReferenceValue = (UnityEngine.Object)value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- the models ----

        [Test]
        public void EveryBuiltShape_IsASmallLowPolyModel_StandingOnTheGround()
        {
            foreach (ItemIconShape shape in Enum.GetValues(typeof(ItemIconShape)))
            {
                Mesh mesh = ItemChunkMeshes.Build(shape, new Color(0.5f, 0.4f, 0.3f));
                if (!ItemChunkMeshes.Has(shape))
                {
                    Assert.IsNull(mesh, shape + " has no model here");
                    continue;
                }
                Assert.IsNotNull(mesh, shape.ToString());
                int triangles = mesh.triangles.Length / 3;
                Assert.That(triangles, Is.InRange(10, 800), shape + " is low-poly");
                Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, shape + " is coloured by vertex");
                Assert.That(mesh.bounds.size.x, Is.InRange(0.15f, 1.3f), shape + " is about a unit across");
                Assert.That(mesh.bounds.size.y, Is.InRange(0.01f, 1.3f));
                Assert.That(mesh.bounds.min.y, Is.GreaterThan(-0.2f), shape + " stands on the ground");
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Chunk_IsFaceted_ClosedAndTheSameEveryTime()
        {
            Mesh first = ItemChunkMeshes.Build(ItemIconShape.Chunk, Color.white);
            Mesh second = ItemChunkMeshes.Build(ItemIconShape.Chunk, Color.white);
            Assert.AreEqual(20, first.triangles.Length / 3, "an icosahedron's twenty facets");
            CollectionAssert.AreEqual(first.vertices, second.vertices);
            // Every facet faces out from the middle.
            Vector3[] vertices = first.vertices, normals = first.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(Vector3.Dot(normals[i], vertices[i] - first.bounds.center), Is.GreaterThan(0f));
            }
            // Facets differ in shade, so the shape reads without a light.
            var shades = new HashSet<float>();
            foreach (Color colour in first.colors)
            {
                shades.Add(colour.r);
            }
            Assert.That(shades.Count, Is.GreaterThan(5));
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }

        // ---- the game's items ----

        [Test]
        public void EveryItemInTheCatalog_HasAnIcon_AndFewAreStillPainted()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            var materials = AssetDatabase.LoadAssetAtPath<MaterialRegistry>("Assets/Config/Materials/MaterialRegistry.asset");
            var painted = new List<string>();
            foreach (ItemDefinition item in catalog.Items)
            {
                if (item.Id == "hand")
                {
                    continue;
                }
                Assert.IsNotNull(item.Icon, item.DisplayName + " has an icon");
                if (ItemIconShapes.Resolve(item, materials) == ItemIconShape.Painted)
                {
                    painted.Add(item.DisplayName);
                }
                else
                {
                    Assert.AreEqual(128, item.Icon.width, item.DisplayName + "'s icon is baked from its model");
                }
            }
            Assert.That(painted.Count, Is.LessThanOrEqualTo(4), "painted by hand: " + string.Join(", ", painted));
        }
    }
}
