using NUnit.Framework;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    /// <summary>
    /// Deterministic tests of the whip stroke detector. No headset and no Play Mode: the model only
    /// receives velocities, so the carriage ride, slow hands and repeated strokes can all be checked
    /// exactly.
    /// </summary>
    public sealed class WhipLashModelTests
    {
        private static readonly Vector3 CarriageForward = new Vector3(0f, 0f, -5f);
        private static readonly Vector3 FastDownward = new Vector3(0f, -3f, 0f);

        [Test]
        public void SlowMovementIsNotALash()
        {
            var model = new WhipLashModel();

            Assert.IsFalse(model.Step(Vector3.zero, Vector3.zero, 0f, true, 0.016f));
            // Below the downward threshold.
            Assert.IsFalse(model.Step(new Vector3(0f, -0.4f, 0f), Vector3.zero, 0f, true, 0.016f));
            // Fast, but sideways and upwards: a stroke has to point down.
            Assert.IsFalse(model.Step(new Vector3(4f, 0.5f, 0f), Vector3.zero, 0f, true, 0.016f));
            Assert.AreEqual(0, model.LashCount);
        }

        [Test]
        public void FastDownwardMovementIsALash()
        {
            var model = new WhipLashModel();

            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));
            Assert.AreEqual(1, model.LashCount);
        }

        [Test]
        public void RidingTheCarriageIsNeverALash()
        {
            var model = new WhipLashModel();

            // The whip travels with the carriage and nobody moves it: relative speed is zero.
            Assert.IsFalse(model.Step(CarriageForward, CarriageForward, 0f, true, 0.016f));
            // The whip stays still in the world while the carriage runs away underneath it.
            Assert.IsFalse(model.Step(Vector3.zero, CarriageForward, 0f, true, 0.016f));
            // A faster carriage must not change that.
            Assert.IsFalse(model.Step(CarriageForward * 4f, CarriageForward * 4f, 0f, true, 0.016f));
            Assert.AreEqual(0, model.LashCount);
        }

        [Test]
        public void TheStrokeIsMeasuredAgainstTheCarriage()
        {
            var model = new WhipLashModel();

            // A hand that is fast in the world but barely moving on the carriage is not a stroke.
            var worldUp = CarriageForward + new Vector3(0f, 0.2f, 0f);
            Assert.IsFalse(model.Step(worldUp, CarriageForward, 0f, true, 0.016f));

            // The same downward stroke counts no matter how fast the carriage travels.
            Assert.IsTrue(model.Step(CarriageForward + FastDownward, CarriageForward, 0f, true, 0.016f));
        }

        [Test]
        public void AnUnheldWhipNeverAccelerates()
        {
            var model = new WhipLashModel();

            Assert.IsFalse(model.Step(FastDownward, Vector3.zero, 0f, false, 0.016f));
            Assert.AreEqual(0, model.LashCount);
            Assert.IsTrue(model.IsReady, "A released whip is ready to be whipped again.");
        }

        [Test]
        public void OneSustainedStrokeProducesExactlyOneLash()
        {
            var model = new WhipLashModel();

            var lashes = 0;
            for (var frame = 0; frame < 120; frame++)
            {
                if (model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f))
                {
                    lashes++;
                }
            }

            Assert.AreEqual(1, lashes, "A single movement of the hand cannot accelerate twice.");
        }

        [Test]
        public void CooldownBlocksAnImmediateSecondLash()
        {
            var model = new WhipLashModel();

            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));
            Assert.IsFalse(model.Step(new Vector3(0f, -3f, 0f), Vector3.zero, 0f, true, 0.001f));
            Assert.IsFalse(model.Step(new Vector3(0f, -3f, 0f), Vector3.zero, 0f, true, 0.05f));
            Assert.AreEqual(1, model.LashCount);
        }

        [Test]
        public void SlowingDownRearmsTheNextLash()
        {
            var model = new WhipLashModel();

            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));

            // The hand stops: re-arm needs both the slow time and the cooldown.
            for (var frame = 0; frame < 20; frame++)
            {
                model.Step(Vector3.zero, Vector3.zero, 0f, true, 0.05f);
            }

            Assert.IsTrue(model.IsArmed);
            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));
            Assert.AreEqual(2, model.LashCount);
        }

        [Test]
        public void FastMovementKeepsTheWhipDisarmedUntilItSlowsDown()
        {
            var model = new WhipLashModel();

            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));

            // Keep whipping without slowing down: the cooldown expires but the whip never re-arms.
            for (var frame = 0; frame < 60; frame++)
            {
                Assert.IsFalse(model.Step(FastDownward, Vector3.zero, 0f, true, 0.05f));
            }

            Assert.IsFalse(model.IsArmed);
        }

        [Test]
        public void RaisingTheWhipDoesNotDisarmTheFollowingStroke()
        {
            var model = new WhipLashModel();

            // Lift first (upwards), then strike down: the raise must not consume the stroke.
            Assert.IsFalse(model.Step(new Vector3(0f, 2.5f, 0f), Vector3.zero, 0f, true, 0.05f));
            Assert.IsTrue(model.Step(FastDownward, Vector3.zero, 0f, true, 0.05f));
        }

        [Test]
        public void RotationCanPowerALashThatPointsDownwards()
        {
            var model = new WhipLashModel();

            // A wrist snap: little travel but a fast spin while the handle drops.
            Assert.IsTrue(model.Step(new Vector3(0f, -0.8f, 0f), Vector3.zero, 320f, true, 0.016f));
        }

        [Test]
        public void RotationWithUpwardMotionIsNotALash()
        {
            var model = new WhipLashModel();

            Assert.IsFalse(model.Step(new Vector3(0f, 1.5f, 0f), Vector3.zero, 400f, true, 0.016f));
        }

        [Test]
        public void RotationCanBeDisabled()
        {
            var model = new WhipLashModel(angularSpeedThreshold: 0f);

            Assert.IsFalse(model.Step(new Vector3(0f, -0.8f, 0f), Vector3.zero, 400f, true, 0.016f));
        }

        [Test]
        public void TheTwoHandlesAreIndependent()
        {
            var left = new WhipLashModel();
            var right = new WhipLashModel();

            Assert.IsTrue(left.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));

            // The left handle is on cooldown; the right one is untouched and can answer immediately.
            Assert.AreEqual(1, left.LashCount);
            Assert.AreEqual(0, right.LashCount);
            Assert.IsTrue(right.IsReady);
            Assert.IsTrue(right.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));

            // And the strokes may be interleaved.
            for (var frame = 0; frame < 20; frame++)
            {
                left.Step(Vector3.zero, Vector3.zero, 0f, true, 0.05f);
                right.Step(Vector3.zero, Vector3.zero, 0f, true, 0.05f);
            }

            Assert.IsTrue(left.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));
            Assert.IsTrue(right.Step(FastDownward, Vector3.zero, 0f, true, 0.016f));
            Assert.AreEqual(2, left.LashCount);
            Assert.AreEqual(2, right.LashCount);
        }

        [Test]
        public void DesktopKeyUsesTheSameCooldownAndCannotBeSpammed()
        {
            var model = new WhipLashModel();

            // The handle is never held with a keyboard, so every frame re-arms it; only the cooldown gates it.
            model.Step(Vector3.zero, Vector3.zero, 0f, false, 0.016f);
            Assert.IsTrue(model.ConsumeReady());
            Assert.IsFalse(model.ConsumeReady(), "One key press cannot accelerate twice.");

            model.Step(Vector3.zero, Vector3.zero, 0f, false, 0.4f);
            Assert.IsTrue(model.ConsumeReady(), "After the cooldown another press is accepted.");
            Assert.AreEqual(2, model.LashCount);
        }

        [Test]
        public void ThresholdsAreConfigurable()
        {
            var model = new WhipLashModel(
                minimumLashSpeed: 0.5f,
                minimumDownwardSpeed: 0.1f,
                angularSpeedThreshold: 0f,
                cooldown: 0.1f,
                rearmTime: 0.05f);

            Assert.AreEqual(0.5f, model.MinimumLashSpeed, 0.0001f);
            Assert.IsTrue(model.Step(new Vector3(0f, -0.6f, 0f), Vector3.zero, 0f, true, 0.016f),
                "A gentler threshold makes a gentler stroke count.");
        }

        [Test]
        public void ResetClearsTheCooldownAndTheCount()
        {
            var model = new WhipLashModel();
            model.Step(FastDownward, Vector3.zero, 0f, true, 0.016f);

            model.Reset();

            Assert.AreEqual(0, model.LashCount);
            Assert.IsTrue(model.IsReady);
        }
    }
}
