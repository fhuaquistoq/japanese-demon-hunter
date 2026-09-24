using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Reins
{
    /// <summary>
    /// One end of the shared horse rein loop. The grip follows the tracked wrist from the moment it
    /// is grabbed, so gesture displacement is independent of the player's height or posture.
    /// </summary>
    public sealed class ReinHandle : MonoBehaviour
    {
        [SerializeField] private Handedness expectedHand;
        [Tooltip("When enabled, only this side's hand can hold this grip.")]
        [SerializeField] private bool requireExpectedHand = true;
        [Tooltip("Where the pin settles when nobody is holding the rope.")]
        [SerializeField] private Vector3 restLocalPosition;
        [Tooltip("Hand-grabbable handle(s) attached to this end of the shared rope.")]
        [SerializeField] private List<HandGrabInteractable> grabPoints = new List<HandGrabInteractable>();
        [Tooltip("Legacy single grab point, kept so existing scene data continues to work.")]
        [SerializeField, HideInInspector] private HandGrabInteractable interactable;
        [SerializeField, HideInInspector] private LineRenderer tether;

        [Header("Caída de la soga")]
        [Tooltip("Downward acceleration once the hand lets go, so the rope drops and rests instead of snapping back.")]
        [SerializeField, Min(0f)] private float fallAcceleration = 9.81f;
        [SerializeField, Min(0f)] private float maximumFallSpeed = 3.5f;

        [Header("Gestos compartidos (ajustar en la rienda izquierda)")]
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
        private Vector3 _baselineLocalPosition;
        private Vector3 _wristPositionOffsetLocal;
        private IHand _heldHand;
        private Handedness _holdingHand;
        private float _fallSpeed;
        private bool _wasHeld;
        private bool _diagnosticLogged;

        public int ExpectedLaneHand => expectedHand == Handedness.Left ? -1 : 1;
        public Transform GripTransform => transform;
        public Vector3 RestLocalPosition => restLocalPosition;
        public bool IsHeldByExpectedHand => IsHeld && (!requireExpectedHand || _holdingHand == expectedHand);
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

        private sealed class GrabZone
        {
            public HandGrabInteractable interactable;
            public Transform transform;
            public Vector3 homeLocalPosition;
        }

        private void Awake()
        {
            if (tether != null)
            {
                // These old per-horse lines duplicated the single continuous ClosedReinLoop.
                tether.enabled = false;
            }

            EnsureZones();
        }

        private void EnsureZones()
        {
            if (_zones.Count > 0)
            {
                return;
            }

            AddZone(interactable);
            foreach (HandGrabInteractable point in grabPoints)
            {
                AddZone(point);
            }
        }

        private void AddZone(HandGrabInteractable point)
        {
            if (point == null || _zones.Exists(zone => zone.interactable == point))
            {
                return;
            }

            _zones.Add(new GrabZone
            {
                interactable = point,
                transform = point.transform,
                homeLocalPosition = point.transform.localPosition
            });
        }

        public ReinGestureStateMachine CreateGestureStateMachine()
        {
            return new ReinGestureStateMachine(
                liftThreshold, dropThreshold, brakeThreshold, laneThreshold,
                rearmRadius, gestureCooldown, liftWindow, minimumDropSpeed);
        }

        /// <summary>Updates the grip anchor from tracked hand input without interpreting a gesture.</summary>
        public void UpdateGrip(float deltaTime)
        {
            EnsureZones();
            IsHeld = false;
            IsBeingTouched = false;
            _heldHand = null;
            _holdingHand = default;

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
                        _heldHand = hand;
                        _holdingHand = hand.Handedness;
                    }
                }
            }

            if (IsHeld && _heldHand != null &&
                _heldHand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose))
            {
                if (!_wasHeld)
                {
                    _baselineLocalPosition = transform.localPosition;
                    _wristPositionOffsetLocal = transform.localPosition - ToParentSpace(wristPose.position);
                }

                // Follow tracked wrist translation; the captured offset keeps the grip at its grab point.
                transform.localPosition = ToParentSpace(wristPose.position) + _wristPositionOffsetLocal;
                _fallSpeed = 0f;
                _wasHeld = true;
                _diagnosticLogged = false;
                return;
            }

            if (IsBeingTouched && _heldHand == null)
            {
                // Held by an unexpected hand or without valid tracking: keep the rope still, do not drive.
                _fallSpeed = 0f;
                _wasHeld = false;
                LogDiagnosticOnce();
                return;
            }

            if (IsHeld)
            {
                // Tracking became invalid while selected. Freeze the grip until valid wrist data returns.
                IsHeld = false;
                _fallSpeed = 0f;
                _wasHeld = false;
                LogDiagnosticOnce();
                return;
            }

            _wasHeld = false;
            DropTowardsRest(deltaTime);
            ReleaseZones();
        }

        private bool CanDrive(IHand hand)
        {
            return requireExpectedHand
                ? ReinHandOwnership.CanDrive(
                    expectedHand, hand.Handedness, true, hand.IsConnected, hand.IsTrackedDataValid)
                : ReinHandOwnership.CanDriveWithEitherHand(true, hand.IsConnected, hand.IsTrackedDataValid);
        }

        private Vector3 ToParentSpace(Vector3 worldPosition)
        {
            return transform.parent != null
                ? transform.parent.InverseTransformPoint(worldPosition)
                : worldPosition;
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

                if (zone.transform == transform)
                {
                    continue;
                }

                if (zone.transform.localPosition != zone.homeLocalPosition)
                {
                    zone.transform.localPosition = zone.homeLocalPosition;
                }
            }
        }

    }
}
