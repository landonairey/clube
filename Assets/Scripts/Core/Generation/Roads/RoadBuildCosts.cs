using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The ways a stretch of road is made (GL737): laid on the ground, graded into it, carried over a gap, or bored through.</summary>
    public enum RoadWork
    {
        /// <summary>Laid on the ground as it lies (the class's own light evening-out is part of it).</summary>
        Path,
        /// <summary>Cut into the ground and built up over it (cut-and-fill), deeper than the class's own bed, so it keeps within its slope.</summary>
        Grade,
        /// <summary>A deck over water or a gap.</summary>
        Bridge,
        /// <summary>A bore through the ground.</summary>
        Tunnel,
    }

    /// <summary>
    /// What building a road costs (GL737), by the voxel of road length each way of making it takes: a plain path 1, grading 2,
    /// a bridge 3, a tunnel 3. The road search prices every stretch by these (<see cref="RoadRefiner"/>), bridges are priced by
    /// them in the network plan (<see cref="RoadPlanner"/>), a tunnel is bored only where it costs less than the road it stands
    /// in for (<see cref="RoadTunnels"/>), and every finished road adds up what it cost (<see cref="PlannedRoad.BuildCost"/>).
    /// So a road takes the longer way round where that is cheapest, and grading, a bridge or a tunnel where they are.
    /// </summary>
    /// <remarks>
    /// The prices are for a voxel of length along the road, not a voxel of material moved: a deep cut costs what a shallow
    /// one does by the metre, but grading only goes <see cref="GradeDepth"/> deep, so a canyon wall cannot be graded away.
    /// </remarks>
    [Serializable]
    public sealed class RoadBuildCosts
    {
        /// <summary>Metres of road per costed voxel (the world's voxel size).</summary>
        public float VoxelSize = 0.25f;
        /// <summary>Price of a voxel of plain path.</summary>
        public float Path = 1f;
        /// <summary>Price of a voxel of graded road (dug or built up).</summary>
        public float Grade = 2f;
        /// <summary>Price of a voxel of bridge.</summary>
        public float Bridge = 3f;
        /// <summary>Price of a voxel of tunnel.</summary>
        public float Tunnel = 3f;
        /// <summary>The deepest grading cuts into the ground or builds up over it, in metres.</summary>
        public float GradeDepth = 3f;
        /// <summary>Metres to each side of a point that grading evens a road's bed over: it smooths rough ground, not a long slope.</summary>
        public float GradeRun = 24f;
        /// <summary>How many times a river bridge's price a metre of bridge over still water costs: a road goes round a lake but for a narrow neck.</summary>
        public float LakePrice = 3f;

        /// <summary>The price of a voxel of road made this way.</summary>
        public float PriceOf(RoadWork work)
        {
            switch (work)
            {
                case RoadWork.Grade: return Grade;
                case RoadWork.Bridge: return Bridge;
                case RoadWork.Tunnel: return Tunnel;
                default: return Path;
            }
        }

        /// <summary>What <paramref name="metres"/> of road made this way cost, in voxel prices.</summary>
        public float Cost(RoadWork work, float metres)
        {
            return metres / Mathf.Max(0.01f, VoxelSize) * PriceOf(work);
        }

        /// <summary>How many metres of plain path cost what a metre made this way does: the road search's own unit is a metre of level path.</summary>
        public float Relative(RoadWork work)
        {
            return PriceOf(work) / Mathf.Max(1e-4f, Path);
        }

        /// <summary>
        /// What a stretch of ground costs a road of a class, in metres of level path: laid as it lies where it is within the
        /// class's limit (<see cref="RoadClassDefinition.StretchCost"/>); graded where the ground evened over
        /// <see cref="GradeRun"/> is within the limit (<paramref name="gradedSlope"/>), at the grading price for a stretch at
        /// the limit, so grading never undercuts a gentle path; and otherwise as it lies, over the limit, which is dear.
        /// </summary>
        /// <param name="slope">Its rise over run as laid.</param>
        /// <param name="gradedSlope">Its rise over run once graded; <see cref="float.MaxValue"/> where it cannot be graded.</param>
        public float StretchCost(RoadClassDefinition definition, float metres, float slope, float gradedSlope)
        {
            if (slope <= definition.MaxSlope)
            {
                return Relative(RoadWork.Path) * definition.StretchCost(metres, slope);
            }
            if (gradedSlope <= definition.MaxSlope)
            {
                return Relative(RoadWork.Grade) * definition.StretchCost(metres, definition.MaxSlope);
            }
            return definition.StretchCost(metres, slope);
        }
    }

    /// <summary>Metres of a road made each way (GL737).</summary>
    [Serializable]
    public struct RoadWorks
    {
        public float Path, Grade, Bridge, Tunnel;

        public float Total => Path + Grade + Bridge + Tunnel;

        public float this[RoadWork work]
        {
            get => work == RoadWork.Grade ? Grade : work == RoadWork.Bridge ? Bridge : work == RoadWork.Tunnel ? Tunnel : Path;
            set
            {
                switch (work)
                {
                    case RoadWork.Grade: Grade = value; break;
                    case RoadWork.Bridge: Bridge = value; break;
                    case RoadWork.Tunnel: Tunnel = value; break;
                    default: Path = value; break;
                }
            }
        }

        /// <summary>What the road costs at these prices.</summary>
        public float CostAt(RoadBuildCosts costs)
        {
            return costs.Cost(RoadWork.Path, Path) + costs.Cost(RoadWork.Grade, Grade) + costs.Cost(RoadWork.Bridge, Bridge) + costs.Cost(RoadWork.Tunnel, Tunnel);
        }
    }
}
