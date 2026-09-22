using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Reins
{
    public sealed class ReinHandle : MonoBehaviour
    {
        [SerializeField] private Handedness expectedHand;
        [SerializeField] private Vector3 restLocalPosition;
        [SerializeField] private HandGrabInteractable interactable;
        [SerializeField] private LineRenderer tether;
        [SerializeField] private Transform horseHead;
        [SerializeField, Min(0.1f)] private float returnSpeed = 8f;
        [SerializeField, Min(0f)] private float liftThreshold = 0.18f;
        [SerializeField, Min(0f)] private float dropThreshold = 0.16f;
        [SerializeField, Min(0f)] private float brakeThreshold = 0.22f;
        [SerializeField, Min(0f)] private float laneThreshold = 0.24f;
        [SerializeField, Min(0f)] private float rearmRadius = 0.12f;
        [SerializeField, Min(0f)] private float gestureCooldown = 0.55f;
        [SerializeField, Min(0f)] private float liftWindow = 0.8f;
        [SerializeField, Min(0f)] private float minimumDropSpeed = 0.45f;

        private ReinGestureStateMachine _gestures;

        private void Awake()
        {
            _gestures = new ReinGestureStateMachine(
                liftThreshold, dropThreshold, brakeThreshold, laneThreshold,
                rearmRadius, gestureCooldown, liftWindow, minimumDropSpeed);
        }

        public int ExpectedLaneHand => expectedHand == Handedness.Left ? -1 : 1;

        public ReinGesture ReadGesture(float deltaTime)
        {
            var heldByTrackedHand = IsSelectedByExpectedTrackedHand(out var anyInteractorSelected);
            if (ReinHandOwnership.ShouldReturnToRest(anyInteractorSelected))
            {
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition, restLocalPosition, returnSpeed * deltaTime);
            }

            var pull = transform.localPosition - restLocalPosition;
            return _gestures.Step(heldByTrackedHand, pull, deltaTime);
        }

        private bool IsSelectedByExpectedTrackedHand(out bool anyInteractorSelected)
        {
            anyInteractorSelected = false;
            if (interactable == null)
            {
                return false;
            }

            foreach (var interactor in interactable.SelectingInteractors)
            {
                anyInteractorSelected = true;
                IHand hand = interactor.Hand;
                if (hand != null && ReinHandOwnership.CanDrive(
                    expectedHand, hand.Handedness, true, hand.IsConnected, hand.IsTrackedDataValid))
                {
                    return true;
                }
            }

            return false;
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
