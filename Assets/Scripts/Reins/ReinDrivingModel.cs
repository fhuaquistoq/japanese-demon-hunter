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
            float liftThreshold = 0.18f,
            float dropThreshold = 0.16f,
            float brakeThreshold = 0.22f,
            float laneThreshold = 0.24f,
            float rearmRadius = 0.12f,
            float cooldownSeconds = 0.55f,
            float liftWindowSeconds = 0.8f,
            float minimumDropSpeed = 0.45f)
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
            if (pull.sqrMagnitude <= _rearmRadius * _rearmRadius)
            {
                _armed = true;
            }

            _liftAge += deltaTime;
            var downwardSpeed = deltaTime > 0f ? (_previousY - pull.y) / deltaTime : 0f;
            _previousY = pull.y;

            if (_lifted && _liftAge > _liftWindowSeconds)
            {
                _lifted = false;
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

    public static class ReinHandOwnership
    {
        public static bool CanDrive(
            Handedness expected, Handedness actual, bool selected, bool connected, bool trackedDataValid)
        {
            return selected && connected && trackedDataValid && expected == actual;
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
