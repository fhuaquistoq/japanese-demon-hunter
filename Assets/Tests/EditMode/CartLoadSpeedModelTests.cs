using JapaneseDemonHunter.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class CartLoadSpeedModelTests
    {
        [Test]
        public void WithoutLoadTheFullMaximumSpeedIsAvailable()
        {
            var model = new CartLoadSpeedModel(3.2f);

            Assert.AreEqual(1f, model.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(3.2f, model.EffectiveMaximumSpeed, 0.0001f);
            Assert.AreEqual(3.2f, model.ClampSpeed(10f), 0.0001f);
        }

        [Test]
        public void MonsterLoadReducesTheEffectiveMaximumSpeed()
        {
            var model = new CartLoadSpeedModel(4f);

            model.SetMonsterLoadMultiplier(0.5f);

            Assert.AreEqual(0.5f, model.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(2f, model.EffectiveMaximumSpeed, 0.0001f);
            Assert.AreEqual(2f, model.ClampSpeed(3.9f), 0.0001f);
        }

        [Test]
        public void MultipliersAreClampedToTheUnitRange()
        {
            var model = new CartLoadSpeedModel(4f);

            model.SetMonsterLoadMultiplier(5f);
            Assert.AreEqual(1f, model.SpeedMultiplier, 0.0001f);

            model.SetMonsterLoadMultiplier(-2f);
            Assert.AreEqual(0f, model.SpeedMultiplier, 0.0001f);
        }

        [Test]
        public void AccelerationNeverExceedsTheLoadLimitedMaximum()
        {
            var model = new CartLoadSpeedModel(3f);
            model.SetMonsterLoadMultiplier(0.3f);

            Assert.AreEqual(0.9f, model.Accelerate(0.8f, 5f), 0.0001f);
            Assert.AreEqual(0.7f, model.Accelerate(0.5f, 0.2f), 0.0001f);
        }

        [Test]
        public void ReportedSpeedIsExposedForTheGiantTrigger()
        {
            var model = new CartLoadSpeedModel(3f);
            Assert.AreEqual(0f, model.EffectiveSpeed, 0.0001f);

            model.ReportSpeed(2.25f);
            Assert.AreEqual(2.25f, model.EffectiveSpeed, 0.0001f);

            model.ReportSpeed(-4f);
            Assert.AreEqual(0f, model.EffectiveSpeed, 0.0001f);
        }

        [Test]
        public void TheModelSatisfiesTheCartIntegrationContract()
        {
            ICartSpeedPenaltyReceiver receiver = new CartLoadSpeedModel(3f);
            receiver.SetMonsterLoadMultiplier(0.25f);

            Assert.AreEqual(0.25f, receiver.SpeedMultiplier, 0.0001f);

            receiver.SetMonsterLoadMultiplier(1f);
            Assert.AreEqual(1f, receiver.SpeedMultiplier, 0.0001f);
        }
    }
}
