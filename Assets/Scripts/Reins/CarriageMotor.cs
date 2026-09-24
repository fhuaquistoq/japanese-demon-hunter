using System;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace Reins
{
    /// <summary>Moves the carriage, horses, reins, and tracking rig together without rotating the rider.</summary>
    public sealed class CarriageMotor : MonoBehaviour, ICartSpeedPenaltyReceiver, ICartAccelerationRequester,
        ICartFirstGallopSource
    {
        [SerializeField] private ReinHandle leftRein;
        [SerializeField] private ReinHandle rightRein;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 5f;
        [Tooltip("Speed the carriage already has when the scene starts.")]
        [SerializeField, Min(0f)] private float startingSpeed = 3.5f;
        [Tooltip("Extra speed gained by one full lash of the reins.")]
        [SerializeField, Min(0.1f)] private float accelerationPerStroke = 1.6f;
        [SerializeField, Min(0.1f)] private float brakingPerPull = 0.9f;
        [Tooltip("Speed lost per second when the player stops galloping: this is what brings the carriage to a halt.")]
        [SerializeField, Min(0f)] private float coastingDeceleration = 0.7f;
        [SerializeField, Min(0.1f)] private float laneWidth = 2.8f;
        [SerializeField, Min(0.1f)] private float laneShiftDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float boundarySpeedPenalty = 0.7f;
        [Header("Monster load")]
        [SerializeField, Range(0f, 1f)] private float minimumLoadSpeedMultiplier = 0.3f;
        [Header("Road curvature")]
        [SerializeField] private bool followRoadCurvature = true;
        [SerializeField, Min(0f)] private float maximumYawRate = 25f;
        [Header("Audio de gestos")]
        [SerializeField] private AudioSource gestureAudioSource;
        [Tooltip("Played when both hands complete a reins gesture.")]
        [SerializeField] private AudioClip accelerateClip;
        [Tooltip("Played when the rope is pulled back.")]
        [SerializeField] private AudioClip brakeClip;
        [Tooltip("Played when the rope is pulled sideways.")]
        [SerializeField] private AudioClip laneClip;

        private readonly LaneTransitionModel _laneTransition = new LaneTransitionModel();
        private readonly CarriageStopModel _stopModel = new CarriageStopModel();
        private BilateralReinGestureModel _reinGestures;
        private CartLoadSpeedModel _loadModel;
        private ForestRoad _forestRoad;
        private RoadPathModel _roadPath;
        private Vector3 _roadOriginOffset;
        private float _travelDistance;
        private Vector3 _centerlinePosition;
        private float _speed;
        private int _lane;
        private bool _firstGallopRaised;

        public float Speed => _speed;
        public int Lane => _lane;
        public ReinHandle LeftRein => leftRein;
        public ReinHandle RightRein => rightRein;
        public ReinGestureKind LastCommand { get; private set; }
        public int DetectedGestureCount { get; private set; }
        public event Action FirstGallop;
        public event Action<ReinGestureKind> CommandApplied;
        public event Action ObstacleHit;
        public bool HasGestureAudio => gestureAudioSource != null &&
                                       accelerateClip != null &&
                                       brakeClip != null;

        public float EffectiveSpeed => LoadModel.EffectiveSpeed;
        public float SpeedMultiplier => LoadModel.SpeedMultiplier;
        public float EffectiveMaximumSpeed => LoadModel.EffectiveMaximumSpeed;

        /// <summary>
        /// Created on demand so the load model also works when the component is queried outside
        /// Play Mode (for example by editor validation tooling).
        /// </summary>
        private CartLoadSpeedModel LoadModel => _loadModel ??= new CartLoadSpeedModel(maximumSpeed);

        private void Awake()
        {
            _loadModel = new CartLoadSpeedModel(maximumSpeed);
            _speed = Mathf.Clamp(startingSpeed, 0f, maximumSpeed);
            _loadModel.ReportSpeed(_speed);
            _laneTransition.Begin(0f, _lane, laneWidth, laneShiftDuration);
            _reinGestures = new BilateralReinGestureModel(
                leftRein != null ? leftRein.CreateGestureStateMachine() : new ReinGestureStateMachine());
        }

        private void Start()
        {
            _forestRoad = FindAnyObjectByType<ForestRoad>();
            _centerlinePosition = transform.position - transform.right * _laneTransition.CurrentX;
            if (_forestRoad != null)
            {
                _roadPath = _forestRoad.CreatePathModel();
                _roadOriginOffset = _centerlinePosition -
                    _forestRoad.transform.TransformPoint(_roadPath.GetChunkPosition(0));
            }
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            leftRein?.UpdateGrip(deltaTime);
            rightRein?.UpdateGrip(deltaTime);
            ReinGesture gesture = _reinGestures.Step(
                leftRein != null && leftRein.IsHeldByExpectedHand,
                rightRein != null && rightRein.IsHeldByExpectedHand,
                leftRein != null ? leftRein.Pull : Vector3.zero,
                rightRein != null ? rightRein.Pull : Vector3.zero,
                deltaTime);
            Apply(gesture);

            if (!_stopModel.IsStopped)
            {
                _speed = Mathf.MoveTowards(_speed, 0f, coastingDeceleration * deltaTime);
            }

            _speed = LoadModel.ClampSpeed(_speed);
            LoadModel.ReportSpeed(_speed);

            var previousPosition = transform.position;
            if (followRoadCurvature && _roadPath != null)
            {
                _travelDistance += _speed * deltaTime;
                _roadPath.GetPoseAtDistance(_travelDistance, out var roadPosition, out var heading);
                float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, heading,
                    maximumYawRate * deltaTime);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                _centerlinePosition = _forestRoad.transform.TransformPoint(roadPosition) + _roadOriginOffset;
                transform.position = _centerlinePosition +
                                     transform.right * _laneTransition.Step(deltaTime);
            }
            else
            {
                if (followRoadCurvature) FollowRoadCurvature(deltaTime);
                _centerlinePosition -= transform.forward * (_speed * deltaTime);
                transform.position = _centerlinePosition + transform.right * _laneTransition.Step(deltaTime);
            }

            if (_forestRoad != null && _forestRoad.TryStopOnRock(previousPosition, transform.position))
            {
                StopForObstacle();
            }
        }

        /// <summary>Called by the attached monster load; the carriage slows down while monsters ride it.</summary>
        public void SetMonsterLoadMultiplier(float multiplier)
        {
            LoadModel.SetMonsterLoadMultiplier(Mathf.Clamp(multiplier, minimumLoadSpeedMultiplier, 1f));
            _speed = LoadModel.ClampSpeed(_speed);
            LoadModel.ReportSpeed(_speed);
        }

        /// <summary>Integration point for external systems that request a lash of the reins.</summary>
        public void RequestAcceleration()
        {
            _speed = _stopModel.Accelerate(_speed, accelerationPerStroke, LoadModel.EffectiveMaximumSpeed);
        }

        private void Apply(ReinGesture gesture)
        {
            LastCommand = gesture.Kind;
            if (gesture.Kind != ReinGestureKind.None)
            {
                DetectedGestureCount++;
                PlayGestureAudio(gesture.Kind);
                CommandApplied?.Invoke(gesture.Kind);
            }

            switch (gesture.Kind)
            {
                case ReinGestureKind.Accelerate:
                    _speed = _stopModel.Accelerate(
                        _speed, accelerationPerStroke, LoadModel.EffectiveMaximumSpeed);
                    if (!_firstGallopRaised)
                    {
                        _firstGallopRaised = true;
                        FirstGallop?.Invoke();
                    }
                    break;
                case ReinGestureKind.Brake:
                    _speed = Mathf.Max(0f, _speed - brakingPerPull);
                    break;
                case ReinGestureKind.LanePull:
                    if (ThreeLaneModel.TryShift(_lane, gesture.Direction, out var nextLane))
                    {
                        _lane = nextLane;
                        _laneTransition.Begin(_laneTransition.CurrentX, _lane, laneWidth, laneShiftDuration);
                    }
                    else
                    {
                        _speed = Mathf.Max(0f, _speed - boundarySpeedPenalty);
                    }
                    break;
            }
        }

        private void FollowRoadCurvature(float deltaTime)
        {
            if (_forestRoad == null || !_forestRoad.TryGetRoadFrame(transform.position, out var forward))
            {
                return;
            }

            var targetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, maximumYawRate * deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// Audible confirmation that a gesture was actually detected, which separates "the gesture
        /// was not recognised" from "the carriage did not move".
        /// </summary>
        private void PlayGestureAudio(ReinGestureKind kind)
        {
            if (gestureAudioSource == null)
            {
                return;
            }

            AudioClip clip = kind switch
            {
                ReinGestureKind.Accelerate => accelerateClip,
                ReinGestureKind.Brake => brakeClip,
                ReinGestureKind.LanePull => laneClip,
                _ => null
            };

            if (clip != null)
            {
                gestureAudioSource.PlayOneShot(clip);
            }
        }

        private void StopForObstacle()
        {
            _speed = 0f;
            _stopModel.Stop();
            LoadModel.ReportSpeed(_speed);
            ObstacleHit?.Invoke();
        }
    }
}
