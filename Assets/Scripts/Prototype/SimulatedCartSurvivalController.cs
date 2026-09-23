using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JapaneseDemonHunter.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulatedCartMovement))]
    public sealed class SimulatedCartSurvivalController : MonoBehaviour, ICartSpeedPenaltyReceiver, ICartAccelerationRequester
    {
        [SerializeField] private SimulatedCartMovement movement;
        [Header("Provisional speed tuning")]
        [SerializeField, Min(0f)] private float baseSpeed = 5.5f;
        [SerializeField, Min(0f)] private float maximumSpeed = 6f;
        [SerializeField, Min(0.01f)] private float acceleration = 1.25f;
        [SerializeField, Min(0.01f)] private float deceleration = 2.5f;
        [SerializeField, Min(0.05f)] private float accelerationRequestDuration = 1.25f;
        [SerializeField] private bool enableDesktopWhipInput = true;
        [SerializeField] private Key whipKey = Key.LeftShift;

        private float speedMultiplier = 1f;
        private float commandedSpeed;
        private float accelerationUntil;

        public float EffectiveSpeed => movement != null ? movement.EffectiveSpeed : 0f;
        public float SpeedMultiplier => speedMultiplier;
        public float CommandedSpeed => commandedSpeed;
        public float MaximumSpeed => maximumSpeed;

        public event Action AccelerationRequested;
        public event Action<float> EffectiveSpeedChanged;

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<SimulatedCartMovement>();
            }

            maximumSpeed = Mathf.Max(baseSpeed, maximumSpeed);
            commandedSpeed = Mathf.Clamp(baseSpeed, 0f, maximumSpeed);
            ApplySpeedImmediately();
        }

        private void Update()
        {
            if (enableDesktopWhipInput && Keyboard.current != null && Keyboard.current[whipKey].wasPressedThisFrame)
            {
                RequestAcceleration();
            }

            TickSpeed(Time.deltaTime);
        }

        public void SetMonsterLoadMultiplier(float multiplier)
        {
            float previous = speedMultiplier;
            speedMultiplier = Mathf.Clamp01(multiplier);
            if (speedMultiplier < previous)
            {
                // Loading can reduce the current command. Unloading deliberately does not restore it.
                commandedSpeed = Mathf.Min(commandedSpeed, maximumSpeed * speedMultiplier);
            }
        }

        [ContextMenu("Request Acceleration (Prototype)")]
        public void RequestAcceleration()
        {
            accelerationUntil = Mathf.Max(accelerationUntil, Time.time + accelerationRequestDuration);
            AccelerationRequested?.Invoke();
        }

        public void TickSpeed(float deltaTime)
        {
            if (movement == null || deltaTime <= 0f)
            {
                return;
            }

            float loadLimitedMaximum = Mathf.Max(0f, maximumSpeed * speedMultiplier);
            if (Time.time < accelerationUntil)
            {
                commandedSpeed = Mathf.MoveTowards(commandedSpeed, loadLimitedMaximum, acceleration * deltaTime);
            }
            else if (commandedSpeed > loadLimitedMaximum)
            {
                commandedSpeed = Mathf.MoveTowards(commandedSpeed, loadLimitedMaximum, deceleration * deltaTime);
            }

            float previous = movement.Speed;
            movement.Speed = Mathf.Clamp(commandedSpeed, 0f, loadLimitedMaximum);
            if (!Mathf.Approximately(previous, movement.Speed))
            {
                EffectiveSpeedChanged?.Invoke(movement.Speed);
            }
        }

        public void Configure(
            SimulatedCartMovement configuredMovement,
            float configuredBaseSpeed,
            float configuredMaximumSpeed,
            float configuredAcceleration,
            float configuredDeceleration)
        {
            movement = configuredMovement;
            baseSpeed = Mathf.Max(0f, configuredBaseSpeed);
            maximumSpeed = Mathf.Max(baseSpeed, configuredMaximumSpeed);
            acceleration = Mathf.Max(0.01f, configuredAcceleration);
            deceleration = Mathf.Max(0.01f, configuredDeceleration);
            commandedSpeed = baseSpeed;
            ApplySpeedImmediately();
        }

        private void ApplySpeedImmediately()
        {
            if (movement != null)
            {
                movement.Speed = Mathf.Clamp(commandedSpeed, 0f, maximumSpeed * speedMultiplier);
            }
        }
    }
}
