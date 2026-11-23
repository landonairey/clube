using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL804 ("a weird spikey line of stone that cuts across the starting parcel land ... something similar as a ring
    /// around the nearby lake", seed 819369): the bounded map's ground has no thin steps. The erosion steered its gullies by
    /// a slope taken over half a metre, so each crease in the map's shaping became a ridge a metre high and a quarter of a
    /// metre wide, steep enough to be bare stone.
    /// </summary>
    public class TerrainCreaseTests
    {
        // The most the ground may bend over a quarter metre (h(x - v) + h(x + v) - 2 h(x)); the spike line was 2.65 m.
        private const float MostBend = 0.5f;

        [Test]
        public void Seed819369_TheStartingParcelHasNoSpikeLine()
        {
            WorldConfig source = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfig>("Assets/Config/MapGenWorldConfig.asset");
            Assert.IsNotNull(source);
            WorldConfig config = Object.Instantiate(source);
            try
            {
                TerrainSettings terrain = config.Terrain;
                terrain.Generator = TerrainGeneratorType.BoundedMap;
                terrain.Seed = 819369;
                BoundedMapGenerator map = BoundedMapGenerator.CreateDry(terrain);
                // The playtest's spot: voxel (-300, 1245, -3920), 0.25 m voxels.
                const float step = 0.25f;
                float worst = 0f;
                Vector2 at = default;
                for (float z = -1030f; z <= -930f; z += 1f)
                {
                    for (float x = -125f; x <= -25f; x += step)
                    {
                        float bend = Mathf.Abs(map.Height(x - step, z) + map.Height(x + step, z) - 2f * map.Height(x, z));
                        if (bend > worst)
                        {
                            worst = bend;
                            at = new Vector2(x, z);
                        }
                    }
                }
                Assert.Less(worst, MostBend, $"the ground bends {worst:0.00} m over a quarter metre at {at}");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
