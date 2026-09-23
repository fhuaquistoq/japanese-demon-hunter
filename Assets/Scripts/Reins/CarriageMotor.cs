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
        [SerializeField, Min(0.1f)] private float laneShiftDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float boundarySpeedPenalty = 0.7f;

        private readonly LaneTransitionModel _laneTransition = new LaneTransitionModel();
        private readonly CarriageStopModel _stopModel = new CarriageStopModel();
        private ForestRoad _forestRoad;
        private float _speed;
        private int _lane;

        public float Speed => _speed;
        public int Lane => _lane;

        private void Awake()
        {
            _laneTransition.Begin(transform.position.x, _lane, laneWidth, laneShiftDuration);
        }

        private void Start()
        {
            _forestRoad = FindObjectOfType<ForestRoad>();
        }

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

            if (!_stopModel.IsStopped)
            {
                _speed = Mathf.MoveTowards(_speed, 0f, coastingDeceleration * deltaTime);
            }

            var previousPosition = transform.position;
            var position = previousPosition;
            position.z -= _speed * deltaTime;
            position.x = _laneTransition.Step(deltaTime);
            transform.position = position;
            if (_forestRoad != null && _forestRoad.TryStopOnRock(previousPosition, position))
            {
                StopForObstacle();
            }
        }

        private void Apply(ReinGesture gesture)
        {
            switch (gesture.Kind)
            {
                case ReinGestureKind.Accelerate:
                    _speed = _stopModel.Accelerate(_speed, accelerationPerStroke, maximumSpeed);
                    break;
                case ReinGestureKind.Brake:
                    _speed = Mathf.Max(0f, _speed - brakingPerPull);
                    break;
                case ReinGestureKind.LanePull:
                    if (ThreeLaneModel.TryShift(_lane, gesture.Direction, out var nextLane))
                    {
                        _lane = nextLane;
                        _laneTransition.Begin(transform.position.x, _lane, laneWidth, laneShiftDuration);
                    }
                    else
                    {
                        _speed = Mathf.Max(0f, _speed - boundarySpeedPenalty);
                    }
                    break;
            }
        }

        private void StopForObstacle()
        {
            _speed = 0f;
            _stopModel.Stop();
        }
    }
}
