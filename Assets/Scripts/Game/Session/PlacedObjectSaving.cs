using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the player built, in a save (GL205): each <see cref="PlacedObject"/>'s item, place, quality and
    /// contents, a station's slots, and the samples a structure (campfire, furnace) stands in. Loading places
    /// each again from its item's prefab, as <see cref="PlayerBuilder"/> does, without taking the item from
    /// the inventory. A station's work under way (a recipe's progress, fuel burning) starts over.
    /// </summary>
    public static class PlacedObjectSaving
    {
        public static List<PlacedSave> Capture()
        {
            var saves = new List<PlacedSave>();
            foreach (PlacedObject placed in Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None))
            {
                if (placed.Item == null)
                {
                    continue;
                }
                var save = new PlacedSave
                {
                    Item = placed.Item.Id,
                    Position = placed.transform.position,
                    Rotation = placed.transform.rotation,
                    Quality = placed.Quality,
                    Contents = StackSaving.Capture(placed.Contents),
                };
                if (placed.TryGetComponent(out StationObject station))
                {
                    save.Input = StackSaving.Capture(station.Station.Input);
                    save.Output = StackSaving.Capture(station.Station.Output);
                    if (station.Station.Fuel != null)
                    {
                        save.Fuel = StackSaving.Capture(station.Station.Fuel);
                    }
                }
                if (placed.TryGetComponent(out TerrainStructure structure))
                {
                    if (!structure.IsStanding)
                    {
                        // Mined into: it's ground now, and the object goes with the next frame.
                        continue;
                    }
                    save.Stamped.AddRange(structure.Stamped);
                }
                saves.Add(save);
            }
            return saves;
        }

        /// <summary>Places every saved object again. Returns how many came back.</summary>
        public static int Restore(IEnumerable<PlacedSave> saves, ItemLookup items, MaterialRegistry materials)
        {
            int restored = 0;
            foreach (PlacedSave save in saves)
            {
                if (!(items.Find(save.Item) is PlaceableDefinition item) || item.Prefab == null)
                {
                    continue;
                }
                GameObject placed = Object.Instantiate(item.Prefab, save.Position, save.Rotation);
                placed.name = item.DisplayName;
                PlacedObject placedObject = placed.TryGetComponent(out PlacedObject own) ? own : placed.AddComponent<PlacedObject>();
                placedObject.Item = item;
                placedObject.Contents = StackSaving.Restore(save.Contents, materials);
                placedObject.Quality = save.Quality;
                if (placed.TryGetComponent(out StationObject station))
                {
                    station.MadeOf(save.Quality);
                    StackSaving.Restore(station.Station.Input, save.Input, items.Find, materials);
                    StackSaving.Restore(station.Station.Output, save.Output, items.Find, materials);
                    if (station.Station.Fuel != null)
                    {
                        StackSaving.Restore(station.Station.Fuel, save.Fuel, items.Find, materials);
                    }
                }
                if (placed.TryGetComponent(out TerrainStructure structure))
                {
                    structure.Restore(save.Stamped);
                }
                restored++;
            }
            return restored;
        }
    }
}
