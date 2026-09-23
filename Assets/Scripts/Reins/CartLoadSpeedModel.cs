using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace Reins
{
    /// <summary>
    /// Pure speed-limit model. It translates the monster load multiplier pushed by
    /// <c>CartMonsterLoad</c> (through <see cref="ICartSpeedPenaltyReceiver"/>) into the maximum
    /// speed the carriage motor may reach, without referencing the monster assembly.
    /// </summary>
    public sealed class CartLoadSpeedModel : ICartSpeedPenaltyReceiver
    {
        private readonly float maximumSpeed;
        private float speedMultiplier = 1f;
        private float effectiveSpeed;

        public CartLoadSpeedModel(float configuredMaximumSpeed)
        {
            maximumSpeed = Mathf.Max(0f, configuredMaximumSpeed);
        }

        public float MaximumSpeed => maximumSpeed;

        /// <summary>Multiplier requested by the attached monster load (1 = no penalty).</summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>Speed actually reached by the carriage; used by the giant zombie trigger.</summary>
        public float EffectiveSpeed => effectiveSpeed;

        public float EffectiveMaximumSpeed => maximumSpeed * speedMultiplier;

        /// <summary>Loading can reduce the current command; unloading deliberately does not restore it.</summary>
        public void SetMonsterLoadMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp01(multiplier);
        }

        public void ReportSpeed(float speed)
        {
            effectiveSpeed = Mathf.Max(0f, speed);
        }

        public float ClampSpeed(float speed)
        {
            return Mathf.Clamp(speed, 0f, EffectiveMaximumSpeed);
        }

        public float Accelerate(float speed, float amount)
        {
            return Mathf.Min(EffectiveMaximumSpeed, Mathf.Max(0f, speed) + Mathf.Max(0f, amount));
        }
    }
}
