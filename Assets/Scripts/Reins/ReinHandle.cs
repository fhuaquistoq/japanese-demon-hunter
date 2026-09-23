using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Reins
{
    /// <summary>
    /// One rein: the rope pin the player pulls, plus every grab zone along that side of the rope.
    /// There is no handle: the zones are invisible grab volumes spread along the rope so it can be
    /// taken at any point. The stroke is measured from the instant the rope is grabbed, which is
    /// what makes the gesture work no matter where the hands were when the player took hold.
    /// </summary>
    public sealed class ReinHandle : MonoBehaviour
    {
        [SerializeField] private Handedness expectedHand;
        [Tooltip("Leave off so either hand can pull any part of the rope. Turn on to force the expected hand only.")]
        [SerializeField] private bool requireExpectedHand;
        [Tooltip("Where the pin settles when nobody is holding the rope.")]
        [SerializeField] private Vector3 restLocalPosition;
        [Tooltip("Every grab volume along this side of the rope. The player may take the rope at any of them.")]
        [SerializeField] private List<HandGrabInteractable> grabPoints = new List<HandGrabInteractable>();
        [SerializeField] private LineRenderer tether;
        [SerializeField] private Transform horseHead;

        [Header("Caída de la soga")]
        [Tooltip("Downward acceleration once the hand lets go, so the rope drops and rests instead of snapping back.")]
        [SerializeField, Min(0f)] private float fallAcceleration = 9.81f;
        [SerializeField, Min(0f)] private float maximumFallSpeed = 3.5f;

        [Header("Gestos (ajustar aqui)")]
        [Tooltip("Vertical displacement needed to consider the rope lifted.")]
        [SerializeField, Min(0f)] private float liftThreshold = 0.10f;
        [Tooltip("Vertical drop, measured from the highest point of the lift, that fires a gallop.")]
        [SerializeField, Min(0f)] private float dropThreshold = 0.10f;
        [Tooltip("Backward pull that brakes.")]
        [SerializeField, Min(0f)] private float brakeThreshold = 0.15f;
        [Tooltip("Sideways pull that changes lane.")]
        [SerializeField, Min(0f)] private float laneThreshold = 0.18f;
        [Tooltip("How close to the neutral point the rope must come back before another stroke is allowed.")]
        [SerializeField, Min(0f)] private float rearmRadius = 0.12f;
        [SerializeField, Min(0f)] private float gestureCooldown = 0.35f;
        [Tooltip("How long a raised rope stays armed waiting for the downward stroke.")]
        [SerializeField, Min(0f)] private float liftWindow = 4f;
        [Tooltip("Minimum downward speed of the hand for a fast stroke to count.")]
        [SerializeField, Min(0f)] private float minimumDropSpeed = 0.30f;
        [Tooltip("Warn once in the console when the rope is grabbed but hand tracking cannot drive it.")]
        [SerializeField] private bool logGrabDiagnostics = true;

        private readonly List<GrabZone> _zones = new List<GrabZone>();
        private ReinGestureStateMachine _gestures;
        private Vector3 _baselineLocalPosition;
        private float _fallSpeed;
        private bool _wasHeld;
        private bool _diagnosticLogged;

        public int ExpectedLaneHand => expectedHand == Handedness.Left ? -1 : 1;
        public Transform GripTransform => transform;
        public Vector3 RestLocalPosition => restLocalPosition;
        public int GrabPointCount
        {
            get
            {
                EnsureZones();
                return _zones.Count;
            }
        }

        public bool IsHeld { get; private set; }
        public bool IsBeingTouched { get; private set; }
        public Vector3 Pull => transform.localPosition - _baselineLocalPosition;

        /// <summary>Built on demand so the component also works when queried outside Play Mode.</summary>
        private ReinGestureStateMachine Gestures => _gestures ??= new ReinGestureStateMachine(
            liftThreshold, dropThreshold, brakeThreshold, laneThreshold,
            rearmRadius, gestureCooldown, liftWindow, minimumDropSpeed);

        private sealed class GrabZone
        {
            public HandGrabInteractable interactable;
            public Transform transform;
            public Vector3 homeLocalPosition;
        }

        private void Awake()
        {
            _gestures = new ReinGestureStateMachine(
                liftThreshold, dropThreshold, brakeThreshold, laneThreshold,
                rearmRadius, gestureCooldown, liftWindow, minimumDropSpeed);
            EnsureZones();
        }

        private void EnsureZones()
        {
            if (_zones.Count > 0 || grabPoints.Count == 0)
            {
                return;
            }

            foreach (HandGrabInteractable point in grabPoints)
            {
                if (point == null)
                {
                    continue;
                }

                _zones.Add(new GrabZone
                {
                    interactable = point,
                    transform = point.transform,
                    homeLocalPosition = point.transform.localPosition
                });
            }
        }

        public ReinGesture ReadGesture(float deltaTime)
        {
            EnsureZones();
            IsHeld = false;
            IsBeingTouched = false;
            Transform heldBy = null;

            for (var i = 0; i < _zones.Count; i++)
            {
                GrabZone zone = _zones[i];
                if (zone.interactable == null)
                {
                    continue;
                }

                foreach (var interactor in zone.interactable.SelectingInteractors)
                {
                    IsBeingTouched = true;
                    IHand hand = interactor.Hand;
                    if (hand != null && CanDrive(hand))
                    {
                        IsHeld = true;
                        heldBy = zone.transform;
                    }
                }
            }

            if (IsHeld && heldBy != null)
            {
                if (!_wasHeld)
                {
                    // The stroke is measured from the moment the rope is taken, so any grab height works.
                    _baselineLocalPosition = transform.localPosition;
                }

                transform.position = heldBy.position;
                _fallSpeed = 0f;
                _wasHeld = true;
                _diagnosticLogged = false;
                return Gestures.Step(true, transform.localPosition - _baselineLocalPosition, deltaTime);
            }

            if (IsBeingTouched)
            {
                // Held by an unexpected hand or without valid tracking: keep the rope still, do not drive.
                _fallSpeed = 0f;
                _wasHeld = true;
                LogDiagnosticOnce();
                return Gestures.Step(false, Vector3.zero, deltaTime);
            }

            _wasHeld = false;
            DropTowardsRest(deltaTime);
            ReleaseZones();
            return Gestures.Step(false, Vector3.zero, deltaTime);
        }

        private bool CanDrive(IHand hand)
        {
            return requireExpectedHand
                ? ReinHandOwnership.CanDrive(
                    expectedHand, hand.Handedness, true, hand.IsConnected, hand.IsTrackedDataValid)
                : ReinHandOwnership.CanDriveWithEitherHand(true, hand.IsConnected, hand.IsTrackedDataValid);
        }

        /// <summary>
        /// Says out loud why a grabbed rope is not producing commands: it saves a lot of guessing
        /// while the gesture is being tuned.
        /// </summary>
        private void LogDiagnosticOnce()
        {
            if (_diagnosticLogged || !logGrabDiagnostics)
            {
                return;
            }

            _diagnosticLogged = true;
            Debug.LogWarning(
                $"{name}: the rope is grabbed but the hand cannot drive it. " +
                "Hand tracking is disconnected or the tracking data is invalid (this is the usual " +
                "reason for gestures not firing). Gesture thresholds are not the problem here.", this);
        }

        /// <summary>
        /// The released rope falls to its resting height and stays there, keeping the horizontal
        /// position where the hand let go, so it can be whipped again immediately.
        /// </summary>
        private void DropTowardsRest(float deltaTime)
        {
            _fallSpeed = Mathf.Min(maximumFallSpeed, _fallSpeed + fallAcceleration * deltaTime);
            var localPosition = transform.localPosition;
            if (localPosition.y > restLocalPosition.y)
            {
                localPosition.y = Mathf.Max(restLocalPosition.y, localPosition.y - _fallSpeed * deltaTime);
                transform.localPosition = localPosition;
                return;
            }

            _fallSpeed = 0f;
        }

        /// <summary>Returns the invisible grab volumes to their homes so the rope stays easy to catch.</summary>
        private void ReleaseZones()
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                GrabZone zone = _zones[i];
                if (zone.transform == null)
                {
                    continue;
                }

                if (zone.transform.localPosition != zone.homeLocalPosition)
                {
                    zone.transform.localPosition = zone.homeLocalPosition;
                }
            }
        }

        private void LateUpdate()
        {
            if (tether == null || horseHead == null)
            {
                return;
            }

            tether.SetPosition(0, horseHead.position);
            tether.SetPosition(1, transform.position);
        }
    }
}
