using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Real physics for the held weapon. Let go gently and it drops onto the carriage deck; release
    /// it while the hand is moving fast and it is thrown as a physical projectile that keeps dealing
    /// damage in flight and then falls to the road, left behind by the carriage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrabbableWeapon : MonoBehaviour
    {
        [SerializeField] private HandGrabInteractable interactable;
        [SerializeField] private Rigidbody body;
        [SerializeField] private SwordDamage damage;
        [Tooltip("Motion of the carriage, ignored while the weapon is carried; cleared in flight so the throw itself counts as a hit.")]
        [SerializeField] private Transform velocityReference;

        [Header("Fisica")]
        [Tooltip("Hand speed at release above which the weapon is thrown instead of dropped.")]
        [SerializeField, Min(0f)] private float throwSpeed = 2.5f;
        [Tooltip("Downward acceleration when the weapon is simply let go.")]
        [SerializeField, Min(0f)] private float fallAcceleration = 9.81f;
        [Tooltip("Height, in carriage space, where a dropped weapon comes to rest (the deck).")]
        [SerializeField] private float deckLocalHeight = 0.74f;
        [SerializeField, Min(0f)] private float throwSpin = 8f;
        [Tooltip("How far above the deck the hand must be for a dropped weapon to be considered airborne.")]
        [SerializeField, Min(0f)] private float minimumDropHeight = 0.05f;

        private readonly List<IHand> hands = new List<IHand>();
        private Vector3 previousPosition;
        private Vector3 handVelocity;
        private float fallSpeed;
        private bool held;
        private bool thrown;

        public bool IsHeld => held;
        public bool IsThrown => thrown;
        public float ReleaseSpeed => handVelocity.magnitude;
        public float ThrowSpeedThreshold => throwSpeed;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (interactable == null)
            {
                interactable = GetComponentInChildren<HandGrabInteractable>();
            }

            if (damage == null)
            {
                damage = GetComponentInChildren<SwordDamage>();
            }

            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            RestoreCarriedState();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            TrackHandVelocity(deltaTime);
            bool isGrabbed = IsGrabbedByAnyHand();

            if (isGrabbed && !held)
            {
                PickUp();
            }
            else if (!isGrabbed && held)
            {
                Release();
            }

            if (held)
            {
                fallSpeed = 0f;
            }
            else if (!thrown)
            {
                TickDroppedFall(deltaTime);
            }
        }

        /// <summary>Let go without speed: the weapon drops onto the deck and stays aboard.</summary>
        private void TickDroppedFall(float deltaTime)
        {
            Vector3 localPosition = transform.localPosition;
            if (localPosition.y <= deckLocalHeight + minimumDropHeight)
            {
                fallSpeed = 0f;
                return;
            }

            fallSpeed = Mathf.Min(fallSpeed + fallAcceleration * deltaTime, fallAcceleration);
            localPosition.y = Mathf.Max(deckLocalHeight, localPosition.y - fallSpeed * deltaTime);
            transform.localPosition = localPosition;
        }

        private void PickUp()
        {
            held = true;
            thrown = false;
            fallSpeed = 0f;

            // Back aboard the carriage and inert until the SDK moves it with the hand.
            if (transform.parent != velocityReference && velocityReference != null)
            {
                transform.SetParent(velocityReference, true);
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (damage != null)
            {
                damage.ConfigureVelocityReference(velocityReference);
            }
        }

        private void Release()
        {
            held = false;
            if (handVelocity.magnitude >= throwSpeed)
            {
                Throw();
                return;
            }

            RestoreCarriedState();
        }

        /// <summary>Leaves the carriage as a real projectile: gravity, spin and damage while flying.</summary>
        private void Throw()
        {
            thrown = true;
            fallSpeed = 0f;

            transform.SetParent(null, true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = handVelocity;
                body.angularVelocity = Random.insideUnitSphere * throwSpin;
            }

            if (damage != null)
            {
                // In flight the weapon's own speed is what counts as a hit.
                damage.ConfigureVelocityReference(null);
            }
        }

        private void RestoreCarriedState()
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (damage != null)
            {
                damage.ConfigureVelocityReference(velocityReference);
            }

            thrown = false;
        }

        private void TrackHandVelocity(float deltaTime)
        {
            Vector3 current = transform.position;
            Vector3 instantaneous = (current - previousPosition) / deltaTime;
            previousPosition = current;
            if (held)
            {
                handVelocity = Vector3.Lerp(handVelocity, instantaneous, 0.35f);
            }
            else if (handVelocity != Vector3.zero && instantaneous == Vector3.zero)
            {
                handVelocity = Vector3.zero;
            }
        }

        private bool IsGrabbedByAnyHand()
        {
            if (interactable == null)
            {
                return false;
            }

            hands.Clear();
            foreach (var interactor in interactable.SelectingInteractors)
            {
                if (interactor != null && interactor.Hand != null)
                {
                    hands.Add(interactor.Hand);
                }
            }

            return hands.Count > 0;
        }

        public void Configure(
            HandGrabInteractable configuredInteractable,
            Rigidbody configuredBody,
            SwordDamage configuredDamage,
            Transform configuredVelocityReference,
            float configuredDeckHeight)
        {
            interactable = configuredInteractable;
            body = configuredBody;
            damage = configuredDamage;
            velocityReference = configuredVelocityReference;
            deckLocalHeight = configuredDeckHeight;
        }
    }
}
