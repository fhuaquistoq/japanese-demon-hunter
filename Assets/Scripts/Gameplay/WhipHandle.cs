using System.Collections.Generic;
using JapaneseDemonHunter.Prototype;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// One whip handle. It reads the hand that holds it, runs <see cref="WhipLashModel"/> and asks
    /// the carriage for acceleration through <see cref="ICartAccelerationRequester"/>. It never
    /// writes the carriage speed itself, never moves the hands, the camera or the XR Origin.
    /// <para>
    /// Two instances live on the carriage: one for the left hand and one for the right. They are
    /// independent, so alternating strokes with both hands accelerates faster than using one.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WhipHandle : MonoBehaviour
    {
        [Header("Asignación de mano")]
        [Tooltip("Only this hand may drive the handle. Left handle -> Left, right handle -> Right.")]
        [SerializeField] private Handedness expectedHand = Handedness.Left;
        [Tooltip("Turn off to let either hand drive this handle while the grasp is being tuned.")]
        [SerializeField] private bool requireExpectedHand = true;

        [Header("Integración con la carreta")]
        [Tooltip("Any component implementing ICartAccelerationRequester (the CarriageMotor on VehicleRoot).")]
        [SerializeField] private MonoBehaviour accelerationRequester;
        [Tooltip("What the whip rides on: its motion is ignored when measuring the stroke (VehicleRoot).")]
        [SerializeField] private Transform velocityReference;

        [Header("Referencias")]
        [Tooltip("Measured point at the end of the handle. Falls back to this transform.")]
        [SerializeField] private Transform tip;
        [SerializeField] private HandGrabInteractable interactable;
        [SerializeField] private LineRenderer rope;
        [SerializeField] private AudioSource lashAudioSource;
        [SerializeField] private AudioClip lashClip;

        [Header("Detección del latigazo")]
        [Tooltip("Speed of the stroke relative to the carriage needed to be a lash (m/s).")]
        [SerializeField, Min(0f)] private float minimumLashSpeed = 2.2f;
        [Tooltip("Downward component of the stroke needed to be a lash (m/s).")]
        [SerializeField, Min(0f)] private float minimumDownwardSpeed = 0.6f;
        [Tooltip("Rotation speed (deg/s) that also counts as a lash when the handle moves downwards. 0 disables it.")]
        [SerializeField, Min(0f)] private float angularSpeedThreshold = 260f;
        [Tooltip("Minimum time between two accepted lashes.")]
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [Tooltip("How long the handle must stay slow before another lash is accepted.")]
        [SerializeField, Min(0f)] private float rearmTime = 0.15f;
        [Tooltip("Speed under which the handle counts as slowed down (m/s).")]
        [SerializeField, Min(0f)] private float rearmSpeed = 0.5f;

        [Header("Cuerda (visual)")]
        [SerializeField] private float ropeLength = 1.05f;
        [SerializeField, Min(2)] private int ropeSegments = 5;
        [SerializeField, Min(0f)] private float swayAmount = 0.035f;
        [SerializeField, Min(0f)] private float swayFrequency = 2.4f;
        [SerializeField, Min(0f)] private float crackSwayAmount = 0.16f;
        [SerializeField, Min(0.01f)] private float crackDecay = 2.5f;

        [Header("Reposo del mango")]
        [Tooltip("When nobody holds it, the handle drifts back to where it started instead of being lost.")]
        [SerializeField] private bool returnToRestWhenReleased = true;
        [SerializeField, Min(0.01f)] private float returnToRestSpeed = 2.5f;

        [Header("Prueba de escritorio")]
        [Tooltip("Optional keyboard stroke so the feature can be tested without a Quest (Q = left, E = right).")]
        [SerializeField] private bool enableDesktopInput = true;
        [SerializeField] private Key desktopKey = Key.Q;

        [Header("Diagnóstico")]
        [Tooltip("Logs every accepted lash and warns when a grabbed handle cannot be driven.")]
        [SerializeField] private bool logDiagnostics;

        private readonly List<IHand> hands = new List<IHand>();

        private WhipLashModel lash;
        private Rigidbody body;
        private Vector3 previousTipPosition;
        private Vector3 previousReferencePosition;
        private Quaternion previousRotation;
        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private float crack;
        private bool wasHeld;
        private bool diagnosticLogged;
        private bool requesterWarningLogged;
        private int lashCount;

        /// <summary>True while the expected hand is holding the handle with valid tracking.</summary>
        public bool IsHeld { get; private set; }

        public Handedness ExpectedHand => expectedHand;
        public bool HasRequester => accelerationRequester is ICartAccelerationRequester;
        public MonoBehaviour AccelerationRequester => accelerationRequester;
        public Transform VelocityReference => velocityReference;
        public int LashCount => lashCount;
        public bool IsReady => Lash != null && Lash.IsReady;
        public bool IsArmed => Lash != null && Lash.IsArmed;

        /// <summary>Built on demand so the component can be inspected outside Play Mode.</summary>
        private WhipLashModel Lash => lash ??= CreateModel();

        private void Awake()
        {
            if (interactable == null)
            {
                interactable = GetComponentInChildren<HandGrabInteractable>();
            }

            if (tip == null)
            {
                tip = transform;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            restLocalPosition = transform.localPosition;
            restLocalRotation = transform.localRotation;
            lash = CreateModel();
            ResetMotionBaseline();
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var tipPosition = TipPosition;
            var worldVelocity = (tipPosition - previousTipPosition) / deltaTime;
            var referenceVelocity = velocityReference != null
                ? (velocityReference.position - previousReferencePosition) / deltaTime
                : Vector3.zero;
            var angularSpeed = Quaternion.Angle(previousRotation, transform.rotation) / deltaTime;

            previousTipPosition = tipPosition;
            previousReferencePosition = velocityReference != null
                ? velocityReference.position
                : Vector3.zero;
            previousRotation = transform.rotation;

            var held = IsHeldByExpectedHand();
            IsHeld = held;

            if (held && !wasHeld)
            {
                // Measure from the moment the whip is taken, and skip the model for that single frame
                // so the handle snapping into the hand is never read as a stroke.
                wasHeld = true;
                ResetMotionBaseline();
                return;
            }

            wasHeld = held;

            var sample = new WhipLashSample
            {
                WorldVelocity = worldVelocity,
                ReferenceVelocity = referenceVelocity,
                AngularSpeedDegrees = angularSpeed,
                IsHeld = held
            };

            if (Lash.Step(in sample, deltaTime))
            {
                FireLash();
            }

            if (enableDesktopInput && Keyboard.current != null &&
                Keyboard.current[desktopKey].wasPressedThisFrame && Lash.ConsumeReady())
            {
                FireLash();
            }

            if (!held)
            {
                ReturnToRest(deltaTime);
            }
        }

        private void LateUpdate()
        {
            UpdateRope(Time.deltaTime);
        }

        /// <summary>Asks the carriage for one stroke worth of acceleration. The only place speed changes.</summary>
        private void FireLash()
        {
            lashCount++;
            crack = 1f;

            if (accelerationRequester is ICartAccelerationRequester requester)
            {
                requester.RequestAcceleration();
            }
            else
            {
                WarnMissingRequesterOnce();
            }

            if (lashAudioSource != null && lashClip != null)
            {
                lashAudioSource.PlayOneShot(lashClip);
            }

            if (logDiagnostics)
            {
                Debug.Log($"{name}: lash {lashCount} accepted, acceleration requested.", this);
            }
        }

        private bool IsHeldByExpectedHand()
        {
            if (interactable == null)
            {
                return false;
            }

            hands.Clear();
            var touched = false;

            foreach (var interactor in interactable.SelectingInteractors)
            {
                if (interactor == null || interactor.Hand == null)
                {
                    continue;
                }

                touched = true;
                hands.Add(interactor.Hand);
            }

            for (var i = 0; i < hands.Count; i++)
            {
                IHand hand = hands[i];
                if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid)
                {
                    continue;
                }

                if (!requireExpectedHand || hand.Handedness == expectedHand)
                {
                    diagnosticLogged = false;
                    return true;
                }
            }

            if (touched)
            {
                LogDiagnosticOnce();
            }

            return false;
        }

        /// <summary>
        /// Says out loud why a grabbed whip is not accelerating: it separates "the wrong hand or bad
        /// tracking" from "the stroke was not fast enough".
        /// </summary>
        private void LogDiagnosticOnce()
        {
            if (diagnosticLogged || !logDiagnostics)
            {
                return;
            }

            diagnosticLogged = true;
            Debug.LogWarning(
                $"{name}: this handle is grabbed but cannot be driven. It expects the " +
                $"{expectedHand} hand with valid tracking (tracking is the usual cause).", this);
        }

        private void WarnMissingRequesterOnce()
        {
            if (requesterWarningLogged)
            {
                return;
            }

            requesterWarningLogged = true;
            Debug.LogWarning(
                $"{name}: no ICartAccelerationRequester is wired, so lashes do not move the " +
                "carriage. Assign the CarriageMotor of VehicleRoot.", this);
        }

        /// <summary>Falls back to the handle itself when the caller did not place the whole assembly.</summary>
        private void ReturnToRest(float deltaTime)
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (!returnToRestWhenReleased)
            {
                return;
            }

            var step = returnToRestSpeed * deltaTime;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, restLocalPosition, step);
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, restLocalRotation, 180f * deltaTime);
        }

        /// <summary>Cheap rope: a handful of local points with a small sway, no physics simulation.</summary>
        private void UpdateRope(float deltaTime)
        {
            crack = Mathf.Max(0f, crack - crackDecay * Mathf.Max(0f, deltaTime));

            if (rope == null)
            {
                return;
            }

            var segments = Mathf.Max(2, ropeSegments);
            rope.positionCount = segments;

            var origin = tip != null && tip != transform
                ? transform.InverseTransformPoint(tip.position)
                : Vector3.zero;
            var hang = new Vector3(0f, -1f, -0.55f).normalized;

            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)(segments - 1);
                var sway = Mathf.Sin(Time.time * swayFrequency + t * Mathf.PI) *
                           (swayAmount + crack * crackSwayAmount) * t;
                rope.SetPosition(i, origin + hang * (ropeLength * t) + new Vector3(sway, 0f, sway * 0.5f));
            }
        }

        private void ResetMotionBaseline()
        {
            previousTipPosition = TipPosition;
            previousReferencePosition = velocityReference != null ? velocityReference.position : Vector3.zero;
            previousRotation = transform.rotation;
        }

        private WhipLashModel CreateModel()
        {
            return new WhipLashModel(
                minimumLashSpeed, minimumDownwardSpeed, angularSpeedThreshold,
                cooldown, rearmTime, rearmSpeed);
        }

        private Vector3 TipPosition => tip != null ? tip.position : transform.position;

        [ContextMenu("Request Acceleration (Test)")]
        private void RequestAccelerationForTesting()
        {
            FireLash();
        }
    }
}
