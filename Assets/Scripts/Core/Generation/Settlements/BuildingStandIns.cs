using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The shapes a building's far stand-in is made of (GL718): each a unit mesh, scaled into place.</summary>
    public enum StandInShape
    {
        /// <summary>Board walls over a stone plinth: a unit box, its base at y 0, centred in x and z.</summary>
        Walls,
        /// <summary>Stone walls (<see cref="BuildingDesign.Masonry"/>): the same box in stone.</summary>
        StoneWalls,
        /// <summary>A gable roof: a unit prism, its eaves at y 0 along z ±0.5, its ridge at y 1 along x.</summary>
        Roof,
    }

    /// <summary>One shape of a stand-in, placed in the world's frame (x, height, z).</summary>
    public readonly struct StandInPart
    {
        public StandInPart(StandInShape shape, Matrix4x4 matrix)
        {
            Shape = shape;
            Matrix = matrix;
        }

        public StandInShape Shape { get; }
        public Matrix4x4 Matrix { get; }
    }

    /// <summary>
    /// GL718: the low-poly stand-ins that show a settlement's buildings from far off, made from the same building plan
    /// the voxels are written from (<see cref="SettlementSite.Buildings"/>, W2's <see cref="BuildingDesign"/> rows): a box
    /// for the walls up to the eaves and a gable prism at the roof's pitch, or a flat-topped box to the parapet for a
    /// battlemented building. Not part of the voxels; the far view swaps them for the voxels where the world draws.
    /// </summary>
    public static class BuildingStandIns
    {
        /// <summary>Metres the walls run on below the pad: the coarse terrain under them has no terrace cut.</summary>
        public const float Footing = 2f;

        /// <summary>Adds the parts of every building of a site.</summary>
        public static void Collect(SettlementSite site, float voxel, List<StandInPart> into)
        {
            foreach (SettlementBuilding building in site.Buildings)
            {
                Collect(building, voxel, into);
            }
        }

        /// <summary>Adds one building's parts: walls, then a roof unless it is flat.</summary>
        public static void Collect(SettlementBuilding building, float voxel, List<StandInPart> into)
        {
            BuildingDesign design = building.Design ?? new BuildingDesign();
            int stories = Mathf.Clamp(design.Stories, 1, 3);
            float eaves = TownLayout.FloorHeight + stories * design.StoryHeight;
            float top = building.Pad + (design.Battlements ? SettlementStructures.RoofAt(design, building.Pitch, voxel, 0f) : eaves);
            float bottom = building.Pad - Footing;
            Quaternion turn = Quaternion.Euler(0f, building.Yaw, 0f);
            var centre = new Vector3(building.Centre.x, bottom, building.Centre.y);
            StandInShape walls = design.Masonry ? StandInShape.StoneWalls : StandInShape.Walls;
            into.Add(new StandInPart(walls, Matrix4x4.TRS(centre, turn, new Vector3(building.Width, top - bottom, building.Depth))));
            if (design.Battlements)
            {
                return;
            }
            // The roof reaches its eave past the front and back walls, as the voxel roof does.
            float ridge = SettlementStructures.RoofAt(design, building.Pitch, voxel, 0f);
            float span = (ridge - eaves) / Mathf.Max(building.Pitch, 0.01f) * 2f;
            var eave = new Vector3(building.Centre.x, building.Pad + eaves, building.Centre.y);
            into.Add(new StandInPart(StandInShape.Roof, Matrix4x4.TRS(eave, turn, new Vector3(building.Width + 2f * voxel, ridge - eaves, span))));
        }
    }
}
