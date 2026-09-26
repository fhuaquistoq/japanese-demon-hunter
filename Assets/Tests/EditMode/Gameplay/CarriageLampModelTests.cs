using NUnit.Framework;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class CarriageLampModelTests
    {
        [Test]
        public void FlameModulationIsDeterministicAndBoundedByAmplitude()
        {
            float first = CarriageLampModel.FlameFactor(2.5f, 0.2f, 0.2f, 0.04f);
            float repeat = CarriageLampModel.FlameFactor(2.5f, 0.2f, 0.2f, 0.04f);

            Assert.AreEqual(first, repeat);
            Assert.That(first, Is.InRange(0.96f, 1.04f));
            Assert.AreEqual(1f, CarriageLampModel.FlameFactor(12f, 0f, 0f, 0f));
        }

        [Test]
        public void GallopCueIsQuietAtCruiseStartAndBoundedAtMaximumSpeed()
        {
            Assert.AreEqual(1f, CarriageLampModel.GallopFactor(3.4f, 3.4f, 5f, 0.12f));
            Assert.AreEqual(1.06f, CarriageLampModel.GallopFactor(4.2f, 3.4f, 5f, 0.12f), 0.0001f);
            Assert.AreEqual(1.12f, CarriageLampModel.GallopFactor(9f, 3.4f, 5f, 0.12f), 0.0001f);
        }

        [Test]
        public void NegativeInputsCannotProduceNegativeLightScale()
        {
            Assert.AreEqual(1f, CarriageLampModel.FlameFactor(1f, 0f, -4f, -1f));
            Assert.AreEqual(1f, CarriageLampModel.GallopFactor(-1f, 2f, 1f, -2f));
        }
    }
}
