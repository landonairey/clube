using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>Terrain settings (K8–K10, GL44): each generator keeps its own shape values.</summary>
    public class TerrainSettingsTests
    {
        [Test]
        public void SwitchingGenerators_KeepsEachOnesShape()
        {
            var settings = new TerrainSettings { Generator = TerrainGeneratorType.Landscape };
            settings.SurfaceLevel = 20f;
            settings.Amplitude = 110f;

            settings.Generator = TerrainGeneratorType.FractalPerlin2D;
            Assert.AreEqual(110f, settings.Amplitude, "A generator with nothing stored starts from the current values.");
            settings.Amplitude = 13f;
            settings.SurfaceLevel = 28f;

            settings.Generator = TerrainGeneratorType.Landscape;
            Assert.AreEqual(110f, settings.Amplitude);
            Assert.AreEqual(20f, settings.SurfaceLevel);

            settings.Generator = TerrainGeneratorType.FractalPerlin2D;
            Assert.AreEqual(13f, settings.Amplitude);
            Assert.AreEqual(28f, settings.SurfaceLevel);
        }

        [Test]
        public void SetShape_StoresAnotherGeneratorsValues_WithoutSelectingIt()
        {
            var settings = new TerrainSettings { Generator = TerrainGeneratorType.Landscape };
            settings.Amplitude = 110f;
            settings.SetShape(TerrainGeneratorType.Sine, 28f, 4f, 0.05f, 1);

            Assert.AreEqual(TerrainGeneratorType.Landscape, settings.Generator);
            Assert.AreEqual(110f, settings.Amplitude);

            settings.Generator = TerrainGeneratorType.Sine;
            Assert.AreEqual(4f, settings.Amplitude);
            Assert.AreEqual(0.05f, settings.Frequency, 1e-6f);
        }
    }
}
