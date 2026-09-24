using JapaneseDemonHunter.Prototype;
using NUnit.Framework;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    /// <summary>
    /// Checks the whip integration contract on the real carriage motor: every stroke goes through
    /// <see cref="ICartAccelerationRequester"/>, the monster load keeps limiting the ceiling and the
    /// speed can never go past it or below zero.
    /// </summary>
    public sealed class WhipAccelerationIntegrationTests
    {
        private GameObject owner;
        private CarriageMotor motor;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("CarriageMotorUnderTest");
            motor = owner.AddComponent<CarriageMotor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TheCarriageMotorIsTheAccelerationContractTheWhipsUse()
        {
            Assert.IsInstanceOf<ICartAccelerationRequester>(motor);
            Assert.IsInstanceOf<ICartSpeedPenaltyReceiver>(motor);
        }

        [Test]
        public void AnUnloadedCarriageStartsWithANeutralMultiplier()
        {
            Assert.AreEqual(1f, motor.SpeedMultiplier, 0.0001f);
            Assert.Greater(motor.EffectiveMaximumSpeed, 0f);
        }

        [Test]
        public void EachLashAddsSpeedAndNeverPassesTheCeiling()
        {
            var before = motor.Speed;

            ((ICartAccelerationRequester)motor).RequestAcceleration();

            Assert.That(motor.Speed, Is.GreaterThan(before), "One stroke must add speed.");
            Assert.That(motor.Speed, Is.LessThanOrEqualTo(motor.EffectiveMaximumSpeed + 0.0001f));

            for (var stroke = 0; stroke < 20; stroke++)
            {
                motor.RequestAcceleration();
                Assert.That(motor.Speed, Is.LessThanOrEqualTo(motor.EffectiveMaximumSpeed + 0.0001f));
            }

            Assert.AreEqual(motor.EffectiveMaximumSpeed, motor.Speed, 0.0001f);
        }

        [Test]
        public void MonsterLoadLowersTheCeilingAndStrokesCannotPassIt()
        {
            motor.SetMonsterLoadMultiplier(0.4f);

            Assert.AreEqual(0.4f, motor.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(5f * 0.4f, motor.EffectiveMaximumSpeed, 0.0001f);

            for (var stroke = 0; stroke < 20; stroke++)
            {
                motor.RequestAcceleration();
            }

            Assert.AreEqual(motor.EffectiveMaximumSpeed, motor.Speed, 0.0001f);
            Assert.That(motor.Speed, Is.LessThan(3f), "The loaded cart stays well below its free speed.");
        }

        [Test]
        public void KillingTheMonstersRaisesTheCeilingAndStrokesUseItAgain()
        {
            motor.SetMonsterLoadMultiplier(0.4f);
            for (var stroke = 0; stroke < 20; stroke++)
            {
                motor.RequestAcceleration();
            }

            var loadedSpeed = motor.Speed;

            // Every attached monster gone.
            motor.SetMonsterLoadMultiplier(1f);

            Assert.AreEqual(1f, motor.SpeedMultiplier, 0.0001f);
            Assert.Greater(motor.EffectiveMaximumSpeed, loadedSpeed);

            motor.RequestAcceleration();

            Assert.That(motor.Speed, Is.GreaterThan(loadedSpeed), "The freed cart can be whipped faster again.");
        }

        [Test]
        public void TheSpeedIsNeverNegative()
        {
            motor.SetMonsterLoadMultiplier(0.3f);
            for (var stroke = 0; stroke < 30; stroke++)
            {
                motor.RequestAcceleration();
            }

            Assert.That(motor.Speed, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
