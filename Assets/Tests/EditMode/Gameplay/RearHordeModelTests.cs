using JapaneseDemonHunter.Monsters;
using NUnit.Framework;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class RearHordeModelTests
    {
        [Test]
        public void AThirdOfTheConfiguredHordeRunsAndTheRestNeverDo()
        {
            var model = new RearHordeModel(0.34f, 1.5f);

            int fastRunners = model.FastRunnerCount(6);

            Assert.AreEqual(2, fastRunners, "Six monsters with a 0.34 fraction means two runners.");
            Assert.AreEqual(1.5f, model.SpeedMultiplierFor(0, fastRunners), 0.0001f);
            Assert.AreEqual(1.5f, model.SpeedMultiplierFor(1, fastRunners), 0.0001f);
            Assert.AreEqual(1f, model.SpeedMultiplierFor(2, fastRunners), 0.0001f,
                "Only the first fastRunners members of the horde may run.");
        }

        [Test]
        public void AHordeOfOneStillHasASingleRunner()
        {
            var model = new RearHordeModel(0.34f, 1.5f);

            Assert.AreEqual(1, model.FastRunnerCount(1));
            Assert.AreEqual(0, model.FastRunnerCount(0));
        }

        [Test]
        public void MissingCountFillsTheHordeButNeverExceedsTheActiveLimit()
        {
            var model = new RearHordeModel(0.34f, 1.5f);

            Assert.AreEqual(6, model.MissingCount(0, 6, 0, 8));
            Assert.AreEqual(2, model.MissingCount(4, 6, 4, 8));
            Assert.AreEqual(0, model.MissingCount(6, 6, 6, 8));
            Assert.AreEqual(0, model.MissingCount(0, 6, 8, 8),
                "A full scene cannot spawn the horde beyond the active monster limit.");
            Assert.AreEqual(1, model.MissingCount(0, 6, 7, 8),
                "One free slot still lets the horde place a single missing member.");
            Assert.AreEqual(0, model.MissingCount(0, 0, 0, 8),
                "A disabled horde never spawns.");
        }

        [Test]
        public void SpeedMultiplierScalesTheBasePaceWithoutInvertingIt()
        {
            Assert.AreEqual(10.2f, RearHordeModel.ApplySpeedMultiplier(6.8f, 1.5f), 0.0001f);
            Assert.AreEqual(0f, RearHordeModel.ApplySpeedMultiplier(0f, 1.5f), 0.0001f);
            Assert.AreEqual(6.8f, RearHordeModel.ApplySpeedMultiplier(6.8f, 0f), 0.0001f,
                "A zero or negative multiplier must not invert or zero the pace.");
        }
    }
}
