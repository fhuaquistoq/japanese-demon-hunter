using System;
using System.Reflection;
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
        public void CarriageLoadFloorZeroCanStallButPointThreePreservesOnePointFiveSpeed()
        {
            GameObject zeroFloorObject = new GameObject("ZeroLoadFloorMotor");
            GameObject safeFloorObject = new GameObject("SafeLoadFloorMotor");
            try
            {
                CarriageMotor zeroFloorMotor = zeroFloorObject.AddComponent<CarriageMotor>();
                CarriageMotor safeFloorMotor = safeFloorObject.AddComponent<CarriageMotor>();
                SetPrivateField(zeroFloorMotor, "minimumLoadSpeedMultiplier", 0f);
                SetPrivateField(safeFloorMotor, "minimumLoadSpeedMultiplier", 0.3f);

                zeroFloorMotor.SetMonsterLoadMultiplier(0f);
                safeFloorMotor.SetMonsterLoadMultiplier(0f);

                Assert.AreEqual(0f, zeroFloorMotor.SpeedMultiplier, 0.0001f);
                Assert.AreEqual(0f, zeroFloorMotor.EffectiveMaximumSpeed, 0.0001f);
                Assert.AreEqual(0.3f, safeFloorMotor.SpeedMultiplier, 0.0001f);
                Assert.AreEqual(1.5f, safeFloorMotor.EffectiveMaximumSpeed, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(zeroFloorObject);
                UnityEngine.Object.DestroyImmediate(safeFloorObject);
            }
        }

        [Test]
        public void GiantRequiresTwoPointFiveContinuousSecondsAtOrBelowOnePointFourSpeed()
        {
            Type spawnerType = Type.GetType(
                "JapaneseDemonHunter.Monsters.GiantZombieSpawner, JapaneseDemonHunter.Monsters");
            Assert.That(spawnerType, Is.Not.Null);

            GameObject cart = new GameObject("GiantThresholdTestCart");
            GameObject spawnerObject = new GameObject("GiantThresholdTestSpawner");
            try
            {
                Component spawner = spawnerObject.AddComponent(spawnerType);
                GiantTriggerTestSpeedReceiver receiver = spawnerObject.AddComponent<GiantTriggerTestSpeedReceiver>();
                SetPrivateField(spawner, "cartTransform", cart.transform);
                spawnerType.GetMethod("ConfigureSpeedTrigger").Invoke(spawner,
                    new object[] { true, 1.4f, 2.5f, receiver, 0f });
                MethodInfo tick = spawnerType.GetMethod("HasStayedSlowLongEnough",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(tick, Is.Not.Null);

                receiver.CurrentSpeed = 1.41f;
                Assert.IsFalse((bool)tick.Invoke(spawner, new object[] { 2f }));
                receiver.CurrentSpeed = 1.4f;
                Assert.IsFalse((bool)tick.Invoke(spawner, new object[] { 2.49f }));
                Assert.IsTrue((bool)tick.Invoke(spawner, new object[] { 0.01f }));

                receiver.CurrentSpeed = 1.41f;
                Assert.IsFalse((bool)tick.Invoke(spawner, new object[] { 0.1f }));
                receiver.CurrentSpeed = 1.4f;
                Assert.IsFalse((bool)tick.Invoke(spawner, new object[] { 2.49f }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(spawnerObject);
                UnityEngine.Object.DestroyImmediate(cart);
            }
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

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(instance, value);
        }
    }

    internal sealed class GiantTriggerTestSpeedReceiver : MonoBehaviour, ICartSpeedPenaltyReceiver
    {
        public float CurrentSpeed { get; set; }
        public float EffectiveSpeed => CurrentSpeed;
        public float SpeedMultiplier { get; private set; } = 1f;

        public void SetMonsterLoadMultiplier(float multiplier)
        {
            SpeedMultiplier = Mathf.Clamp01(multiplier);
        }
    }
}
