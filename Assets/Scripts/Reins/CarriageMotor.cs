using UnityEngine;

namespace Reins
{
    /// <summary>Moves the carriage, horses, reins, and tracking rig together without rotating the rider.</summary>
    public sealed class CarriageMotor : MonoBehaviour
    {
        [SerializeField] private ReinHandle leftRein;
        [SerializeField] private ReinHandle rightRein;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float accelerationPerStroke = 0.7f;
        [SerializeField, Min(0.1f)] private float brakingPerPull = 0.65f;
        [SerializeField, Min(0f)] private float coastingDeceleration = 0.08f;
        [SerializeField, Min(0.1f)] private float laneWidth = 2.8f;
        [SerializeField, Min(0.1f)] private float laneShiftSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float boundarySpeedPenalty = 0.7f;

        private float _speed;
        private int _lane;

        public float Speed => _speed;
        public int Lane => _lane;

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            var leftGesture = leftRein != null
                ? leftRein.ReadGesture(deltaTime)
                : new ReinGesture(ReinGestureKind.None);
            var rightGesture = rightRein != null
                ? rightRein.ReadGesture(deltaTime)
                : new ReinGesture(ReinGestureKind.None);
            Apply(ReinCommandArbitration.Select(leftGesture, rightGesture));

            _speed = Mathf.MoveTowards(_speed, 0f, coastingDeceleration * deltaTime);
            var position = transform.position;
            position.z -= _speed * deltaTime;
            position.x = Mathf.MoveTowards(position.x, _lane * laneWidth, laneShiftSpeed * deltaTime);
            transform.position = position;
        }

        private void Apply(ReinGesture gesture)
        {
            switch (gesture.Kind)
            {
                case ReinGestureKind.Accelerate:
                    _speed = Mathf.Min(maximumSpeed, _speed + accelerationPerStroke);
                    break;
                case ReinGestureKind.Brake:
                    _speed = Mathf.Max(0f, _speed - brakingPerPull);
                    break;
                case ReinGestureKind.LanePull:
                    if (ThreeLaneModel.TryShift(_lane, gesture.Direction, out var nextLane))
                    {
                        _lane = nextLane;
                    }
                    else
                    {
                        _speed = Mathf.Max(0f, _speed - boundarySpeedPenalty);
                    }
                    break;
            }
        }
    }
}
