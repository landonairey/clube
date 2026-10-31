using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL205 (first pass of S1-S3, S6): a save written and read back gives the same world edits and stacks.
    public class SaveTests
    {
        private static readonly Vector3Int ChunkSize = new Vector3Int(4, 4, 4);

        private static World NewWorld()
        {
            return new World(ChunkSize, 0.25f, samples => VoxelStorages.Create(VoxelStorageType.FlatByte, samples));
        }

        private static GameSave RoundTrip(GameSave save)
        {
            using var stream = new MemoryStream();
            GameSaveFile.Write(stream, save);
            stream.Position = 0;
            return GameSaveFile.Read(stream);
        }

        [Test]
        public void EditedChunks_ComeBackAsTheyWere()
        {
            World world = NewWorld();
            world.Load(new Vector3Int(0, 0, 0), null);
            world.Load(new Vector3Int(1, 0, 0), null);
            world.Load(new Vector3Int(0, 1, 0), null);
            // On the border between the first two chunks, so both are edited.
            world.SetDensity(new Vector3Int(4, 1, 2), 0.8f);
            world.SetMaterial(new Vector3Int(4, 1, 2), 3);
            world.SetDensity(new Vector3Int(1, 2, 3), 0.6f);
            world.SetCarved(new Vector3Int(1, 1, 1), true);

            var save = new GameSave();
            save.Chunks = WorldSaving.Capture(world);
            Assert.AreEqual(2, save.Chunks.Count, "Only the edited chunks are saved.");
            GameSave read = RoundTrip(save);

            World loaded = NewWorld();
            Assert.AreEqual(2, WorldSaving.Restore(loaded, read.Chunks));
            Assert.IsTrue(loaded.IsEdited(new Vector3Int(1, 0, 0)));
            loaded.Load(new Vector3Int(0, 0, 0), null);
            loaded.Load(new Vector3Int(1, 0, 0), null);
            Assert.AreEqual(world.GetDensity(new Vector3Int(4, 1, 2)), loaded.GetDensity(new Vector3Int(4, 1, 2)));
            Assert.AreEqual((byte)3, loaded.GetMaterial(new Vector3Int(4, 1, 2)));
            Assert.AreEqual(world.GetDensity(new Vector3Int(1, 2, 3)), loaded.GetDensity(new Vector3Int(1, 2, 3)));
            Assert.AreEqual(0f, loaded.GetDensity(new Vector3Int(2, 2, 2)));
            Assert.IsTrue(loaded.IsCarved(new Vector3Int(1, 1, 1)));
            Assert.IsFalse(loaded.IsCarved(new Vector3Int(2, 1, 1)));
        }

        [Test]
        public void Stacks_KeepTheirContentsPartsAndQuality()
        {
            ItemDefinition clayItem = ItemDefinition.Create("clay", "Clay", 1024);
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay").WithBreaking(1f, clayItem);
            VoxelMaterial stone = VoxelMaterial.Create(0, "Stone");
            MaterialRegistry registry = MaterialRegistry.Create(stone, clay);
            try
            {
                var inventory = new Inventory(4);
                inventory.Add(clayItem, 12, Composition.From(new Content(clay, 0.75f), new Content(stone, 0.25f)), 0, 0.75f, 0.4f);
                inventory.Move(0, 2);

                var save = new GameSave { Player = new PlayerSave { Slots = StackSaving.Capture(inventory), Coins = 12.5f } };
                GameSave read = RoundTrip(save);

                var loaded = new Inventory(4);
                loaded.Add(clayItem, 1);
                var items = new Dictionary<string, ItemDefinition> { { "clay", clayItem } };
                Assert.AreEqual(1, StackSaving.Restore(loaded, read.Player.Slots, id => items.TryGetValue(id, out ItemDefinition item) ? item : null, registry));
                Assert.IsTrue(loaded[0].IsEmpty, "What was there before is replaced.");
                ItemStack stack = loaded[2];
                Assert.AreEqual(clayItem, stack.Item);
                Assert.AreEqual(12, stack.Count);
                Assert.AreEqual(0.4f, stack.Part, 1e-5f);
                Assert.AreEqual(0.75f, stack.Quality, 1e-5f);
                Assert.AreEqual(0.25f, stack.Contents.FractionOf(stone), 1e-5f);
                Assert.AreEqual(12.5f, read.Player.Coins);
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(clayItem);
            }
        }

        // S1: a version 1 save (before names, GL206, GL207) still reads, with empty names.
        [Test]
        public void Version1Save_StillReads()
        {
            var save = new GameSave { Header = new SaveHeader { Mode = "World", Seed = 7, Build = "b" } };
            save.Player = new PlayerSave { Coins = 3f };
            byte[] current;
            using (var stream = new MemoryStream())
            {
                GameSaveFile.Write(stream, save);
                current = stream.ToArray();
            }
            // Version 2 added two strings at the header's end: here both empty, one length byte each, after
            // the magic (8), version (4), "World" (6), seed (4), time (8) and "b" (2).
            const int namesAt = 8 + 4 + 6 + 4 + 8 + 2;
            Assert.AreEqual(0, current[namesAt]);
            Assert.AreEqual(0, current[namesAt + 1]);
            var version1 = new List<byte>(current);
            version1.RemoveRange(namesAt, 2);
            version1[8] = 1;

            using var old = new MemoryStream(version1.ToArray());
            GameSave read = GameSaveFile.Read(old);
            Assert.AreEqual(1, read.Header.Version);
            Assert.AreEqual(7, read.Header.Seed);
            Assert.AreEqual("", read.Header.SaveName);
            Assert.AreEqual(3f, read.Player.Coins);
        }

        // GL206, GL207: the default save name, and the file a name is kept in.
        [Test]
        public void SaveNames_DefaultAndFileName()
        {
            var time = new System.DateTime(2026, 10, 30, 15, 4, 59);
            Assert.AreEqual("Hello World - World - 2026-10-30 15:04", SaveNames.DefaultSaveName("Hello World", "World", time));
            Assert.AreEqual("Hello World - World - 2026-10-30 15:04", SaveNames.DefaultSaveName("  ", "World", time), "No name: the default world name.");
            Assert.AreEqual("Hello World - World - 2026-10-30 15-04.clubesave", SaveNames.FileNameFor("Hello World - World - 2026-10-30 15:04"));
            Assert.AreEqual("a-b-c.clubesave", SaveNames.FileNameFor(" a/b?c. "));
            Assert.AreEqual("Save.clubesave", SaveNames.FileNameFor("???".Replace("?", "")));
            Assert.AreEqual(SaveNames.MaxLength, SaveNames.Clean(new string('x', 100)).Length);
        }

        [Test]
        public void Header_ReadsAlone_AndANewerVersionIsRefused()
        {
            string path = Path.Combine(Application.temporaryCachePath, "save-test.clubesave");
            try
            {
                var save = new GameSave { Header = new SaveHeader { Mode = "World", Seed = 4242, Build = "test", SaveName = "My base", WorldName = "Hello World" } };
                save.Placed.Add(new PlacedSave { Item = "furnace", Position = new Vector3(1f, 2f, 3f), Stamped = { new Vector3Int(4, 8, 4) } });
                GameSaveFile.Write(path, save);

                SaveHeader header = GameSaveFile.ReadHeader(path);
                Assert.AreEqual(SaveHeader.CurrentVersion, header.Version);
                Assert.AreEqual("World", header.Mode);
                Assert.AreEqual(4242, header.Seed);
                Assert.AreEqual("My base", header.SaveName);
                Assert.AreEqual("Hello World", header.WorldName);
                Assert.IsNull(GameSaveFile.Read(path).Player, "Saved before there was a player.");
                Assert.AreEqual(new Vector3Int(4, 8, 4), GameSaveFile.Read(path).Placed[0].Stamped[0]);

                // A save from a newer build: the version just after the magic word.
                byte[] bytes = File.ReadAllBytes(path);
                bytes[8] = (byte)(SaveHeader.CurrentVersion + 1);
                File.WriteAllBytes(path, bytes);
                Assert.IsNull(GameSaveFile.ReadHeader(path));
                Assert.Throws<InvalidDataException>(() => GameSaveFile.Read(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
