using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>Landscape with branching gullies and ridges eroded into its mountains (P12); see <see cref="ErodedLandscapeHeight"/>.</summary>
    public sealed class ErodedLandscapeGenerator : HeightfieldGenerator<ErodedLandscapeHeight>
    {
        public ErodedLandscapeGenerator(ErodedLandscapeHeight field)
            : base(field)
        {
            Water = field.Water;
        }

        /// <summary>The same land with rivers and lakes cut into it.</summary>
        public ErodedLandscapeGenerator WithWater(WaterMap water)
        {
            return new ErodedLandscapeGenerator(Field.WithWater(water));
        }

        public override WaterMap Water { get; }

        public override bool HasValleys => true;

        public override JobHandle ScheduleValleys(NativeArray<float2> points, NativeArray<float> valleys, JobHandle dependsOn = default)
        {
            return new PointValleysJob { Field = Field, Points = points, Valleys = valleys }.ScheduleParallel(points.Length, 64, dependsOn);
        }

        /// <summary>The biggest gullies' ridge map at points (<see cref="ErodedLandscapeHeight.Valley"/>).</summary>
        [BurstCompile]
        private struct PointValleysJob : IJobFor
        {
            public ErodedLandscapeHeight Field;
            [ReadOnly] public NativeArray<float2> Points;
            [WriteOnly] public NativeArray<float> Valleys;

            public void Execute(int index)
            {
                float2 p = Points[index];
                Valleys[index] = Field.Valley(p.x, p.y);
            }
        }
    }
}
