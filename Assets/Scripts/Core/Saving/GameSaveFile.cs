using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Reads and writes a <see cref="GameSave"/> (S1, GL205): binary, a short plain header the menu can read on
    /// its own (<see cref="ReadHeader"/>), then the rest compressed (chunks compress well: most samples are
    /// all air or all solid). The header starts with a version number; a reader takes its own version and
    /// older ones, and refuses a save from a newer build.
    /// </summary>
    public static class GameSaveFile
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CLUBESAV");

        /// <summary>Writes the save to a file, through a temporary one so a failed write never spoils an older save.</summary>
        public static void Write(string path, GameSave save)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string temporary = path + ".tmp";
            using (FileStream file = File.Create(temporary))
            {
                Write(file, save);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
        }

        public static GameSave Read(string path)
        {
            using FileStream file = File.OpenRead(path);
            return Read(file);
        }

        /// <summary>Only the header: mode, seed, when and by what it was saved. Null when the file isn't a save.</summary>
        public static SaveHeader ReadHeader(string path)
        {
            try
            {
                using FileStream file = File.OpenRead(path);
                using var reader = new BinaryReader(file, Encoding.UTF8);
                return ReadHeader(reader);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void Write(Stream stream, GameSave save)
        {
            var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteHeader(writer, save.Header);
            writer.Flush();
            using (var zip = new GZipStream(stream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            using (var body = new BinaryWriter(zip, Encoding.UTF8))
            {
                body.Write(save.Days);
                body.Write(save.Player != null);
                if (save.Player != null)
                {
                    WritePlayer(body, save.Player);
                }
                WriteList(body, save.Chunks, WriteChunk);
                WriteList(body, save.Placed, WritePlaced);
                WriteList(body, save.Drying, (w, d) =>
                {
                    Write(w, d.Sample);
                    w.Write(d.Material);
                    w.Write(d.ReadyAt);
                });
                // Version 3 (GL216): the player's quests done, at the end so older bodies read as they were.
                WriteList(body, save.Player?.Quests ?? new List<string>(), (w, q) => w.Write(q ?? ""));
            }
            writer.Dispose();
        }

        public static GameSave Read(Stream stream)
        {
            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var save = new GameSave { Header = ReadHeader(reader) };
            using (var zip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true))
            using (var body = new BinaryReader(zip, Encoding.UTF8))
            {
                save.Days = body.ReadDouble();
                save.Player = body.ReadBoolean() ? ReadPlayer(body) : null;
                save.Chunks = ReadList(body, ReadChunk);
                save.Placed = ReadList(body, ReadPlaced);
                save.Drying = ReadList(body, r => new DryingSave { Sample = ReadVector3Int(r), Material = r.ReadByte(), ReadyAt = r.ReadDouble() });
                if (save.Header.Version >= 3)
                {
                    List<string> quests = ReadList(body, r => r.ReadString());
                    if (save.Player != null)
                    {
                        save.Player.Quests = quests;
                    }
                }
            }
            reader.Dispose();
            return save;
        }

        private static void WriteHeader(BinaryWriter writer, SaveHeader header)
        {
            writer.Write(Magic);
            writer.Write(SaveHeader.CurrentVersion);
            writer.Write(header.Mode ?? "");
            writer.Write(header.Seed);
            writer.Write(header.SavedAt.ToUniversalTime().Ticks);
            writer.Write(header.Build ?? "");
            // Version 2.
            writer.Write(header.SaveName ?? "");
            writer.Write(header.WorldName ?? "");
        }

        private static SaveHeader ReadHeader(BinaryReader reader)
        {
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || Encoding.ASCII.GetString(magic) != Encoding.ASCII.GetString(Magic))
            {
                throw new InvalidDataException("Not a clube save.");
            }
            int version = reader.ReadInt32();
            if (version > SaveHeader.CurrentVersion)
            {
                throw new InvalidDataException($"The save is version {version}, newer than this build reads ({SaveHeader.CurrentVersion}).");
            }
            var header = new SaveHeader
            {
                Version = version,
                Mode = reader.ReadString(),
                Seed = reader.ReadInt32(),
                SavedAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                Build = reader.ReadString(),
            };
            if (version >= 2)
            {
                header.SaveName = reader.ReadString();
                header.WorldName = reader.ReadString();
            }
            return header;
        }

        private static void WritePlayer(BinaryWriter w, PlayerSave player)
        {
            Write(w, player.Position);
            w.Write(player.Yaw);
            w.Write(player.Pitch);
            WriteList(w, player.Slots, WriteStack);
            w.Write(player.HotbarSelected);
            w.Write(player.Coins);
            WriteList(w, player.Skills, (x, s) =>
            {
                x.Write(s.Skill ?? "");
                x.Write(s.Experience);
            });
            WriteList(w, player.Ores, (x, o) =>
            {
                x.Write(o.Material);
                x.Write(o.FirstDepth);
                x.Write(o.LowestGrade);
                x.Write(o.HighestGrade);
                x.Write(o.Samples);
            });
            WriteList(w, player.MadeRecipes, (x, r) => x.Write(r ?? ""));
            WriteList(w, player.Parcels, (x, p) =>
            {
                x.Write(p.x);
                x.Write(p.y);
            });
        }

        private static PlayerSave ReadPlayer(BinaryReader r)
        {
            var player = new PlayerSave
            {
                Position = ReadVector3(r),
                Yaw = r.ReadSingle(),
                Pitch = r.ReadSingle(),
                Slots = ReadList(r, ReadStack),
                HotbarSelected = r.ReadInt32(),
                Coins = r.ReadSingle(),
                Skills = ReadList(r, x => new SkillSave { Skill = x.ReadString(), Experience = x.ReadSingle() }),
                Ores = ReadList(r, x => new OreEntrySave
                {
                    Material = x.ReadByte(),
                    FirstDepth = x.ReadSingle(),
                    LowestGrade = x.ReadSingle(),
                    HighestGrade = x.ReadSingle(),
                    Samples = x.ReadInt32(),
                }),
                MadeRecipes = ReadList(r, x => x.ReadString()),
                Parcels = ReadList(r, x => new Vector2Int(x.ReadInt32(), x.ReadInt32())),
            };
            return player;
        }

        private static void WriteStack(BinaryWriter w, StackSave stack)
        {
            w.Write(stack.Item ?? "");
            if (stack.IsEmpty)
            {
                return;
            }
            w.Write(stack.Count);
            w.Write(stack.Part);
            w.Write(stack.Wear);
            w.Write(stack.Quality);
            WriteContents(w, stack.Contents);
        }

        private static StackSave ReadStack(BinaryReader r)
        {
            var stack = new StackSave { Item = r.ReadString() };
            if (stack.IsEmpty)
            {
                return stack;
            }
            stack.Count = r.ReadInt32();
            stack.Part = r.ReadSingle();
            stack.Wear = r.ReadInt32();
            stack.Quality = r.ReadSingle();
            stack.Contents = ReadContents(r);
            return stack;
        }

        // A count, -1 for none.
        private static void WriteContents(BinaryWriter w, List<ContentSave> contents)
        {
            w.Write(contents != null ? contents.Count : -1);
            if (contents == null)
            {
                return;
            }
            foreach (ContentSave part in contents)
            {
                w.Write(part.Material);
                w.Write(part.Fraction);
            }
        }

        private static List<ContentSave> ReadContents(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count < 0)
            {
                return null;
            }
            var contents = new List<ContentSave>(count);
            for (int i = 0; i < count; i++)
            {
                contents.Add(new ContentSave { Material = r.ReadByte(), Fraction = r.ReadSingle() });
            }
            return contents;
        }

        private static void WritePlaced(BinaryWriter w, PlacedSave placed)
        {
            w.Write(placed.Item ?? "");
            Write(w, placed.Position);
            w.Write(placed.Rotation.x);
            w.Write(placed.Rotation.y);
            w.Write(placed.Rotation.z);
            w.Write(placed.Rotation.w);
            w.Write(placed.Quality);
            WriteContents(w, placed.Contents);
            WriteList(w, placed.Input, WriteStack);
            WriteList(w, placed.Output, WriteStack);
            WriteList(w, placed.Fuel, WriteStack);
            WriteList(w, placed.Stamped, Write);
        }

        private static PlacedSave ReadPlaced(BinaryReader r)
        {
            return new PlacedSave
            {
                Item = r.ReadString(),
                Position = ReadVector3(r),
                Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                Quality = r.ReadSingle(),
                Contents = ReadContents(r),
                Input = ReadList(r, ReadStack),
                Output = ReadList(r, ReadStack),
                Fuel = ReadList(r, ReadStack),
                Stamped = ReadList(r, ReadVector3Int),
            };
        }

        private static void WriteChunk(BinaryWriter w, ChunkSave chunk)
        {
            Write(w, chunk.Coord);
            Write(w, chunk.SampleCount);
            bool bytes = chunk.ByteDensities != null;
            w.Write(bytes);
            if (bytes)
            {
                WriteBytes(w, chunk.ByteDensities);
            }
            else
            {
                w.Write(chunk.FloatDensities.Length);
                foreach (float density in chunk.FloatDensities)
                {
                    w.Write(density);
                }
            }
            w.Write(chunk.UniformMaterial);
            WriteBytes(w, chunk.Materials);
            WriteBytes(w, chunk.Carves);
        }

        private static ChunkSave ReadChunk(BinaryReader r)
        {
            var chunk = new ChunkSave { Coord = ReadVector3Int(r), SampleCount = ReadVector3Int(r) };
            if (r.ReadBoolean())
            {
                chunk.ByteDensities = ReadBytes(r);
            }
            else
            {
                int count = r.ReadInt32();
                chunk.FloatDensities = new float[count];
                for (int i = 0; i < count; i++)
                {
                    chunk.FloatDensities[i] = r.ReadSingle();
                }
            }
            chunk.UniformMaterial = r.ReadByte();
            chunk.Materials = ReadBytes(r);
            chunk.Carves = ReadBytes(r);
            return chunk;
        }

        // A length, -1 for null.
        private static void WriteBytes(BinaryWriter w, byte[] bytes)
        {
            w.Write(bytes != null ? bytes.Length : -1);
            if (bytes != null)
            {
                w.Write(bytes);
            }
        }

        private static byte[] ReadBytes(BinaryReader r)
        {
            int length = r.ReadInt32();
            if (length < 0)
            {
                return null;
            }
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("The save ends early.");
            }
            return bytes;
        }

        private static void WriteList<T>(BinaryWriter w, List<T> items, Action<BinaryWriter, T> write)
        {
            w.Write(items != null ? items.Count : 0);
            if (items == null)
            {
                return;
            }
            foreach (T item in items)
            {
                write(w, item);
            }
        }

        private static List<T> ReadList<T>(BinaryReader r, Func<BinaryReader, T> read)
        {
            int count = r.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("The save is damaged.");
            }
            var items = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(read(r));
            }
            return items;
        }

        private static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        private static void Write(BinaryWriter w, Vector3Int v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        private static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        private static Vector3Int ReadVector3Int(BinaryReader r)
        {
            return new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        }
    }
}
