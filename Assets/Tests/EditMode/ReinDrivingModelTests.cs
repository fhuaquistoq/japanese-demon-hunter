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
        public void EitherHandMayPullAnyPartOfTheRopeWhenStrictOwnershipIsOff()
        {
            Assert.IsTrue(ReinHandOwnership.CanDriveWithEitherHand(true, true, true));
            Assert.IsFalse(ReinHandOwnership.CanDriveWithEitherHand(false, true, true),
                "A rope nobody is holding cannot drive.");
            Assert.IsFalse(ReinHandOwnership.CanDriveWithEitherHand(true, false, true),
                "A disconnected hand cannot drive.");
            Assert.IsFalse(ReinHandOwnership.CanDriveWithEitherHand(true, true, false),
                "Invalid tracking data cannot drive.");
        }

        [Test]
        public void SelectedHandleDoesNotReturnToRestUntilEveryInteractorReleasesIt()        {
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

        [Test]
        public void LiftingHoldingAndThenYankingDownStillAccelerates()
        {
            var machine = new ReinGestureStateMachine();

            // Raise the rope and keep the hand up far longer than one lash takes.
            machine.Step(true, new Vector3(0f, 0.15f, 0f), 0.016f);
            machine.Step(true, new Vector3(0f, 0.3f, 0f), 1.5f);

            Assert.AreEqual(ReinGestureKind.Accelerate,
                machine.Step(true, new Vector3(0f, 0.05f, 0f), 0.1f).Kind);
        }

        [TestCase(0.7f)]
        [TestCase(1.6f)]
        public void BilateralGallopWorksFromDifferentGrabHeights(float grabHeight)
        {
            var model = new BilateralReinGestureModel(new ReinGestureStateMachine());
            var leftGrabPosition = new Vector3(-0.3f, grabHeight, 0f);
            var rightGrabPosition = new Vector3(0.3f, grabHeight, 0f);
            var lift = new Vector3(0f, 0.2f, 0f);

            model.Step(true, true, leftGrabPosition, rightGrabPosition, 0.016f);
            Assert.AreEqual(ReinGestureKind.None,
                model.Step(true, true, leftGrabPosition + lift, rightGrabPosition + lift, 0.016f).Kind);
            model.Step(true, true, leftGrabPosition + (2f * lift), rightGrabPosition + (2f * lift), 0.016f);
            Assert.AreEqual(ReinGestureKind.Accelerate,
                model.Step(true, true, leftGrabPosition, rightGrabPosition, 0.016f).Kind);
        }

        [Test]
        public void ReinGesturesRequireBothExpectedHandsToKeepHolding()
        {
            var model = new BilateralReinGestureModel(new ReinGestureStateMachine());
            var lift = new Vector3(0f, 0.2f, 0f);

            Assert.AreEqual(ReinGestureKind.None,
                model.Step(true, false, lift, Vector3.zero, 0.016f).Kind,
                "One hand cannot arm a gallop by itself.");
            Assert.AreEqual(ReinGestureKind.None,
                model.Step(true, true, lift, Vector3.zero, 0.016f).Kind,
                "Movement before the second grip is not counted as the beginning of a two-hand gesture.");
            Assert.AreEqual(ReinGestureKind.None,
                model.Step(true, true, lift * 2f, lift, 0.016f).Kind,
                "Both hands are still lifting.");
            Assert.AreEqual(ReinGestureKind.Accelerate,
                model.Step(true, true, lift, Vector3.zero, 0.016f).Kind,
                "Both hands lower quickly while both expected hands keep holding.");

            Assert.AreEqual(ReinGestureKind.None,
                model.Step(true, false, new Vector3(0f, 0f, 0.3f), Vector3.zero, 0.016f).Kind,
                "A brake pull is ignored if either hand releases its grip.");
        }

        [Test]
        public void PullingTheRopeBackwardsBrakes()
        {
            var machine = new ReinGestureStateMachine();

            Assert.AreEqual(ReinGestureKind.Brake,
                machine.Step(true, new Vector3(0f, 0f, 0.2f), 0.016f).Kind);
        }

        [TestCase(0.25f, 0f, ReinGestureKind.LanePull)]
        [TestCase(0f, 0.25f, ReinGestureKind.Brake)]
        public void HoldingAPullDoesNotRepeatTheCommand(float sideways, float backwards,
            ReinGestureKind expected)
        {
            var machine = new ReinGestureStateMachine();
            var pull = new Vector3(sideways, 0f, backwards);
            Assert.AreEqual(expected, machine.Step(true, pull, 0.016f).Kind);
            for (var i = 0; i < 10; i++)
                Assert.AreEqual(ReinGestureKind.None, machine.Step(true, pull, 0.1f).Kind);
            machine.Step(true, Vector3.zero, 0.016f);
            Assert.AreEqual(expected, machine.Step(true, pull, 0.016f).Kind);
        }

        [Test]
        public void DroppingTheRopeBackDownRearmsTheNextLash()
        {
            var machine = new ReinGestureStateMachine();
            machine.Step(true, new Vector3(0f, 0.2f, 0f), 0.016f);
            Assert.AreEqual(ReinGestureKind.Accelerate,
                machine.Step(true, Vector3.zero, 0.016f).Kind);

            // The rope is let go and falls back to its resting height; that must re-arm the whip.
            machine.Step(true, Vector3.zero, 0.5f);
            machine.Step(true, new Vector3(0f, 0.2f, 0f), 0.5f);
            Assert.AreEqual(ReinGestureKind.Accelerate,
                machine.Step(true, Vector3.zero, 0.05f).Kind);
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

        [Test]
        public void LaneTransitionIsMonotonicAndReachesExactTarget()
        {
            var transition = new LaneTransitionModel();
            transition.Begin(-0.35f, 1, 2.8f, 0.8f);
            var previous = transition.CurrentX;
            for (var i = 0; i < 8; i++)
            {
                var current = transition.Step(0.1f);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                previous = current;
            }

            Assert.AreEqual(2.8f, transition.CurrentX, 0.0001f);
            Assert.IsFalse(transition.IsMoving);
        }

        [Test]
        public void MidShiftRequestStartsFromActualInterpolatedPosition()
        {
            var transition = new LaneTransitionModel();
            transition.Begin(-2.8f, 0, 2.8f, 1f);
            var actualX = transition.Step(0.4f);
            transition.Begin(actualX, 1, 2.8f, 1f);
            Assert.AreEqual(actualX, transition.Step(0f));
            Assert.That(transition.Step(0.5f), Is.GreaterThan(actualX));
        }

        [TestCase(0b001)]
        [TestCase(0b110)]
        [TestCase(0b010)]
        [TestCase(0b101)]
        [TestCase(0b100)]
        [TestCase(0b011)]
        public void EveryObstaclePatternBlocksOneOrTwoLanesAndLeavesAnOpenLane(int mask)
        {
            var blocked = 0;
            for (var lane = -1; lane <= 1; lane++)
            {
                if (ObstacleSchedule.IsLaneBlocked(mask, lane)) blocked++;
            }

            Assert.That(blocked, Is.InRange(1, 2));
            Assert.That(3 - blocked, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void SweptCollisionUsesInterpolatedXAtRockCrossingAndConsumesOnce()
        {
            var consumed = false;
            Assert.IsFalse(ObstacleCollisionModel.TrySweep(1f, -1f, -2f, 2f,
                0f, 1.5f, 0.1f, 0.25f, ref consumed), "At the rock's Z crossing the carriage is at x=0.");
            Assert.IsFalse(consumed);
            Assert.IsTrue(ObstacleCollisionModel.TrySweep(1f, -1f, 1f, 2f,
                0f, 1.5f, 0.1f, 0.25f, ref consumed));
            Assert.IsFalse(ObstacleCollisionModel.TrySweep(-1f, -2f, 2f, 2f,
                0f, 2f, 0.1f, 0.25f, ref consumed));
        }

        [Test]
        public void OneAccelerationClearsStoppedLatchAndAddsSpeed()
        {
            var stopped = new CarriageStopModel();
            stopped.Stop();
            Assert.IsTrue(stopped.IsStopped);
            Assert.AreEqual(0.7f, stopped.Accelerate(0f, 0.7f, 3.2f), 0.0001f);
            Assert.IsFalse(stopped.IsStopped);
            Assert.AreEqual(1.4f, stopped.Accelerate(0.7f, 0.7f, 3.2f), 0.0001f);
        }
    }
}
