using Oculus.Interaction.Input;
using UnityEngine;

namespace Reins
{
    public enum ReinGestureKind
    {
        None,
        Accelerate,
        Brake,
        LanePull
    }

    public struct ReinGesture
    {
        public ReinGestureKind Kind;
        public int Direction;

        public ReinGesture(ReinGestureKind kind, int direction = 0)
        {
            Kind = kind;
            Direction = direction;
        }
    }

    /// <summary>Interprets one held, tracked rein's pull relative to its resting position.</summary>
    public sealed class ReinGestureStateMachine
    {
        private readonly float _liftThreshold;
        private readonly float _dropThreshold;
        private readonly float _brakeThreshold;
        private readonly float _laneThreshold;
        private readonly float _rearmRadius;
        private readonly float _cooldownSeconds;
        private readonly float _liftWindowSeconds;
        private readonly float _minimumDropSpeed;

        private float _cooldown;
        private float _liftAge;
        private float _liftPeak;
        private float _previousY;
        private bool _lifted;
        private bool _armed = true;

        public ReinGestureStateMachine(
            float liftThreshold = 0.12f,
            float dropThreshold = 0.12f,
            float brakeThreshold = 0.16f,
            float laneThreshold = 0.18f,
            float rearmRadius = 0.12f,
            float cooldownSeconds = 0.4f,
            float liftWindowSeconds = 3f,
            float minimumDropSpeed = 0.35f)
        {
            _liftThreshold = liftThreshold;
            _dropThreshold = dropThreshold;
            _brakeThreshold = brakeThreshold;
            _laneThreshold = laneThreshold;
            _rearmRadius = rearmRadius;
            _cooldownSeconds = cooldownSeconds;
            _liftWindowSeconds = liftWindowSeconds;
            _minimumDropSpeed = minimumDropSpeed;
        }

        public ReinGesture Step(bool heldByTrackedHand, Vector3 pull, float deltaTime)
        {
            if (!heldByTrackedHand)
            {
                Reset();
                return new ReinGesture(ReinGestureKind.None);
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            // The rein becomes ready again once it is back in its neutral zone, or simply lowered
            // back down. Without the height test a fresh lash would be impossible whenever the rope
            // hangs away from its resting point.
            if (pull.sqrMagnitude <= _rearmRadius * _rearmRadius || pull.y <= _liftThreshold * 0.5f)
            {
                _armed = true;
            }

            _liftAge += deltaTime;
            var downwardSpeed = deltaTime > 0f ? (_previousY - pull.y) / deltaTime : 0f;
            _previousY = pull.y;

            if (_lifted)
            {
                // Keep following the top of the stroke: a player who keeps raising the hand must not
                // have their later yank measured from the first frame that crossed the threshold.
                _liftPeak = Mathf.Max(_liftPeak, pull.y);
                if (_liftAge > _liftWindowSeconds)
                {
                    _lifted = false;
                }
            }

            if (_lifted && _liftAge <= _liftWindowSeconds &&
                _liftPeak - pull.y >= _dropThreshold && downwardSpeed >= _minimumDropSpeed)
            {
                return Fire(ReinGestureKind.Accelerate, 0);
            }

            if (_armed && _cooldown <= 0f)
            {
                if (pull.z >= _brakeThreshold)
                {
                    return Fire(ReinGestureKind.Brake, 0);
                }

                if (Mathf.Abs(pull.x) >= _laneThreshold)
                {
                    return Fire(ReinGestureKind.LanePull, pull.x < 0f ? -1 : 1);
                }
            }

            if (!_lifted && _armed && _cooldown <= 0f && pull.y >= _liftThreshold)
            {
                _lifted = true;
                _liftAge = 0f;
                _liftPeak = pull.y;
            }

            return new ReinGesture(ReinGestureKind.None);
        }

        private ReinGesture Fire(ReinGestureKind kind, int direction)
        {
            _armed = false;
            _lifted = false;
            _cooldown = _cooldownSeconds;
            return new ReinGesture(kind, direction);
        }

        private void Reset()
        {
            _cooldown = 0f;
            _liftAge = 0f;
            _liftPeak = 0f;
            _previousY = 0f;
            _lifted = false;
            _armed = true;
        }
    }

    /// <summary>Interprets one shared gesture made while both reins are held by their assigned hands.</summary>
    public sealed class BilateralReinGestureModel
    {
        private readonly ReinGestureStateMachine _gestures;
        private Vector3 _leftPullBaseline;
        private Vector3 _rightPullBaseline;
        private bool _wasBilateralGrip;

        public BilateralReinGestureModel(ReinGestureStateMachine gestures)
        {
            _gestures = gestures ?? new ReinGestureStateMachine();
        }

        public ReinGesture Step(
            bool leftHeldByExpectedHand,
            bool rightHeldByExpectedHand,
            Vector3 leftPull,
            Vector3 rightPull,
            float deltaTime)
        {
            if (!leftHeldByExpectedHand || !rightHeldByExpectedHand)
            {
                _wasBilateralGrip = false;
                return _gestures.Step(false, Vector3.zero, deltaTime);
            }

            if (!_wasBilateralGrip)
            {
                _leftPullBaseline = leftPull;
                _rightPullBaseline = rightPull;
                _wasBilateralGrip = true;
            }

            Vector3 sharedPull = ((leftPull - _leftPullBaseline) + (rightPull - _rightPullBaseline)) * 0.5f;
            return _gestures.Step(true, sharedPull, deltaTime);
        }
    }

    public static class ReinHandOwnership
    {
        public static bool CanDrive(
            Handedness expected, Handedness actual, bool selected, bool connected, bool trackedDataValid)
        {
            return selected && connected && trackedDataValid && expected == actual;
        }

        /// <summary>
        /// Permissive variant: either hand may pull any part of the rope. Useful while the grasp
        /// gesture is being tuned, because the player never has to match a specific side.
        /// </summary>
        public static bool CanDriveWithEitherHand(bool selected, bool connected, bool trackedDataValid)
        {
            return selected && connected && trackedDataValid;
        }

        public static bool ShouldReturnToRest(bool anyInteractorSelected)
        {
            return !anyInteractorSelected;
        }
    }

    public static class ReinCommandArbitration
    {
        /// <summary>Chooses at most one command per frame; ties are resolved in favor of the left rein.</summary>
        public static ReinGesture Select(ReinGesture left, ReinGesture right)
        {
            var leftPriority = Priority(left.Kind);
            var rightPriority = Priority(right.Kind);
            return rightPriority > leftPriority ? right : left;
        }

        private static int Priority(ReinGestureKind kind)
        {
            switch (kind)
            {
                case ReinGestureKind.Brake:
                    return 3;
                case ReinGestureKind.LanePull:
                    return 2;
                case ReinGestureKind.Accelerate:
                    return 1;
                default:
                    return 0;
            }
        }
    }

    public sealed class LaneTransitionModel
    {
        private float _startX;
        private float _targetX;
        private float _duration = 1f;
        private float _elapsed;

        public float CurrentX { get; private set; }
        public bool IsMoving => _elapsed < _duration;

        public void Begin(float actualX, int targetLane, float laneWidth, float duration)
        {
            _startX = actualX;
            _targetX = targetLane * laneWidth;
            _duration = Mathf.Max(0.001f, duration);
            _elapsed = 0f;
            CurrentX = actualX;
        }

        public float Step(float deltaTime)
        {
            _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, deltaTime));
            var t = _elapsed / _duration;
            var eased = t * t * (3f - 2f * t);
            CurrentX = Mathf.LerpUnclamped(_startX, _targetX, eased);
            return CurrentX;
        }
    }

    public static class ObstacleSchedule
    {
        // Bit positions correspond to lanes -1, 0, +1. The pattern blocks one or two lanes only.
        public static int BlockedLaneMask(int groupIndex)
        {
            switch (Mathf.Abs(groupIndex % 6))
            {
                case 0: return 0b001;
                case 1: return 0b110;
                case 2: return 0b010;
                case 3: return 0b101;
                case 4: return 0b100;
                default: return 0b011;
            }
        }

        public static bool IsLaneBlocked(int mask, int lane)
        {
            return lane >= -1 && lane <= 1 && (mask & (1 << (lane + 1))) != 0;
        }
    }

    public sealed class CarriageStopModel
    {
        public bool IsStopped { get; private set; }

        public void Stop()
        {
            IsStopped = true;
        }

        public float Accelerate(float speed, float amount, float maximum)
        {
            if (IsStopped)
            {
                IsStopped = false;
            }

            return Mathf.Min(maximum, speed + amount);
        }
    }

    public static class ObstacleCollisionModel
    {
        public static bool TrySweep(float previousZ, float currentZ, float previousX, float currentX,
            float rockZ, float rockX, float halfDepth, float halfWidth, ref bool consumed)
        {
            if (consumed || previousZ < rockZ - halfDepth || currentZ > rockZ + halfDepth || previousZ < currentZ)
            {
                return false;
            }

            var crossingZ = Mathf.Clamp(rockZ, currentZ, previousZ);
            var denominator = previousZ - currentZ;
            var t = denominator > 0f ? (previousZ - crossingZ) / denominator : 0f;
            var crossingX = Mathf.Lerp(previousX, currentX, t);
            if (Mathf.Abs(crossingX - rockX) > halfWidth)
            {
                return false;
            }

            consumed = true;
            return true;
        }
    }

    public static class ThreeLaneModel
    {
        public static bool TryShift(int currentLane, int direction, out int nextLane)
        {
            nextLane = currentLane;
            if (direction == 0 || currentLane < -1 || currentLane > 1)
            {
                return false;
            }

            nextLane += direction < 0 ? -1 : 1;
            if (nextLane < -1 || nextLane > 1)
            {
                nextLane = currentLane;
                return false;
            }

            return true;
        }
    }
}
