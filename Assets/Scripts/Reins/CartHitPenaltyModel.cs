using UnityEngine;

namespace Reins
{
    /// <summary>
    /// Decaying slowdown applied when a monster hits the player: it lowers the carriage's maximum
    /// speed and its acceleration, and recovers on its own. Pure, so it can be tested in EditMode.
    /// </summary>
    public sealed class CartHitPenaltyModel
    {
        private readonly float maximumSpeedLoss;
        private readonly float accelerationLoss;

        public CartHitPenaltyModel(float configuredMaximumSpeedLoss, float configuredAccelerationLoss)
        {
            maximumSpeedLoss = Mathf.Clamp01(configuredMaximumSpeedLoss);
            accelerationLoss = Mathf.Clamp01(configuredAccelerationLoss);
        }

        /// <summary>Accumulated penalty in 0..1: 0 is untouched, 1 is the maximum slowdown.</summary>
        public float Penalty { get; private set; }

        /// <summary>Factor applied to the carriage's maximum speed (1 = no penalty).</summary>
        public float MaximumSpeedFactor => 1f - Penalty * maximumSpeedLoss;

        /// <summary>Factor applied to the carriage's acceleration (1 = no penalty).</summary>
        public float AccelerationFactor => 1f - Penalty * accelerationLoss;

        public void Apply(float perHit)
        {
            Penalty = Mathf.Clamp01(Penalty + Mathf.Max(0f, perHit));
        }

        public void Tick(float deltaTime, float recoverySeconds)
        {
            if (Penalty <= 0f)
            {
                return;
            }

            Penalty = Mathf.Max(0f, Penalty - Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, recoverySeconds));
        }

        public void Clear()
        {
            Penalty = 0f;
        }
    }
}
