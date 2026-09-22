using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class ReinDrivingModelTests
    {
        [Test]
        public void GestureIsInertWhenReinIsReleasedOrTrackingIsInvalid()
        {
            var machine = new ReinGestureStateMachine();
            Assert.AreEqual(ReinGestureKind.None,
                machine.Step(false, new Vector3(0.4f, 0.5f, 0.5f), 0.016f).Kind);
            Assert.AreEqual(ReinGestureKind.None,
                machine.Step(false, new Vector3(0.4f, 0f, 0f), 0.016f).Kind);
        }

        [TestCase(Handedness.Left, Handedness.Left, true, true, true, true)]
        [TestCase(Handedness.Left, Handedness.Right, true, true, true, false)]
        [TestCase(Handedness.Right, Handedness.Right, true, false, true, false)]
        [TestCase(Handedness.Right, Handedness.Right, true, true, false, false)]
        [TestCase(Handedness.Right, Handedness.Right, false, true, true, false)]
        public void OnlySelectedExpectedHandWithValidTrackingCanDrive(
            Handedness expected, Handedness actual, bool selected, bool connected, bool valid, bool canDrive)
        {
            Assert.AreEqual(canDrive,
                ReinHandOwnership.CanDrive(expected, actual, selected, connected, valid));
        }

        [Test]
        public void SelectedHandleDoesNotReturnToRestUntilEveryInteractorReleasesIt()
        {
            Assert.IsFalse(ReinHandOwnership.ShouldReturnToRest(true));
            Assert.IsTrue(ReinHandOwnership.ShouldReturnToRest(false));
            Assert.IsFalse(ReinHandOwnership.CanDrive(
                Handedness.Left, Handedness.Left, true, true, false));
        }

        [Test]
        public void BrakeTakesPrecedenceOverSimultaneousAcceleration()
        {
            var winner = ReinCommandArbitration.Select(
                new ReinGesture(ReinGestureKind.Accelerate),
                new ReinGesture(ReinGestureKind.Brake));

            Assert.AreEqual(ReinGestureKind.Brake, winner.Kind);
        }

        [Test]
        public void SimultaneousSameDirectionLanePullsProduceOnlyOneAdjacentShift()
        {
            var winner = ReinCommandArbitration.Select(
                new ReinGesture(ReinGestureKind.LanePull, 1),
                new ReinGesture(ReinGestureKind.LanePull, 1));

            Assert.AreEqual(ReinGestureKind.LanePull, winner.Kind);
            Assert.IsTrue(ThreeLaneModel.TryShift(0, winner.Direction, out var nextLane));
            Assert.AreEqual(1, nextLane);
        }

        [Test]
        public void LaneChangeTakesPrecedenceOverAcceleration()
        {
            var winner = ReinCommandArbitration.Select(
                new ReinGesture(ReinGestureKind.Accelerate),
                new ReinGesture(ReinGestureKind.LanePull, -1));

            Assert.AreEqual(ReinGestureKind.LanePull, winner.Kind);
            Assert.AreEqual(-1, winner.Direction);
        }

        [Test]
        public void LiftThenQuickDownProducesOneAccelerationAndRequiresRearm()
        {
            var machine = new ReinGestureStateMachine();
            machine.Step(true, new Vector3(0f, 0.2f, 0f), 0.016f);

            Assert.AreEqual(ReinGestureKind.Accelerate,
                machine.Step(true, Vector3.zero, 0.016f).Kind);
            Assert.AreEqual(ReinGestureKind.None,
                machine.Step(true, new Vector3(0f, 0.2f, 0f), 0.016f).Kind);
        }

        [Test]
        public void SlowDropDoesNotAccelerate()
        {
            var machine = new ReinGestureStateMachine();
            machine.Step(true, new Vector3(0f, 0.2f, 0f), 0.016f);

            Assert.AreEqual(ReinGestureKind.None,
                machine.Step(true, new Vector3(0f, 0.1f, 0f), 0.5f).Kind);
        }

        [TestCase(0, -1, -1)]
        [TestCase(0, 1, 1)]
        [TestCase(-1, 1, 0)]
        [TestCase(1, -1, 0)]
        public void LanePullShiftsExactlyOneAdjacentLane(int current, int direction, int expected)
        {
            Assert.IsTrue(ThreeLaneModel.TryShift(current, direction, out var next));
            Assert.AreEqual(expected, next);
        }

        [TestCase(-1, -1)]
        [TestCase(1, 1)]
        public void OutwardPullAtBoundaryCannotLeaveTheThreeLanes(int current, int direction)
        {
            Assert.IsFalse(ThreeLaneModel.TryShift(current, direction, out var next));
            Assert.AreEqual(current, next);
        }
    }
}
