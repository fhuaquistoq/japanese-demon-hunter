using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>One frame of whip motion, already measured by <see cref="WhipHandle"/>.</summary>
    public struct WhipLashSample
    {
        /// <summary>World velocity of the whip tip, in m/s.</summary>
        public Vector3 WorldVelocity;

        /// <summary>
        /// World velocity of whatever the whip is riding on (the carriage). It is subtracted so that
        /// simply travelling forward never looks like a lash.
        /// </summary>
        public Vector3 ReferenceVelocity;

        /// <summary>Rotation speed of the handle in degrees per second.</summary>
        public float AngularSpeedDegrees;

        /// <summary>True while the expected hand holds the whip with valid tracking.</summary>
        public bool IsHeld;
    }

    /// <summary>
    /// Deterministic lash detector for one whip handle.
    /// <para>
    /// It is plain C# (no MonoBehaviour) so it can be unit tested without a headset and so the left
    /// and right handles can share exactly the same rules. A lash needs a fast movement measured
    /// <b>relative to the carriage</b> that points downwards, or a fast rotation while the handle
    /// travels downwards. After a lash the detector disarms until the handle slows down again, so a
    /// single stroke can never produce several accelerations.
    /// </para>
    /// </summary>
    public sealed class WhipLashModel
    {
        private readonly float minimumLashSpeed;
        private readonly float minimumDownwardSpeed;
        private readonly float angularSpeedThreshold;
        private readonly float cooldown;
        private readonly float rearmTime;
        private readonly float rearmSpeed;

        private float cooldownRemaining;
        private float slowElapsed;
        private bool armed = true;

        public WhipLashModel(
            float minimumLashSpeed = 2.2f,
            float minimumDownwardSpeed = 0.6f,
            float angularSpeedThreshold = 260f,
            float cooldown = 0.35f,
            float rearmTime = 0.15f,
            float rearmSpeed = 0.5f)
        {
            this.minimumLashSpeed = Mathf.Max(0f, minimumLashSpeed);
            this.minimumDownwardSpeed = Mathf.Max(0f, minimumDownwardSpeed);
            this.angularSpeedThreshold = Mathf.Max(0f, angularSpeedThreshold);
            this.cooldown = Mathf.Max(0f, cooldown);
            this.rearmTime = Mathf.Max(0f, rearmTime);
            this.rearmSpeed = Mathf.Max(0f, rearmSpeed);
            slowElapsed = this.rearmTime;
        }

        public float MinimumLashSpeed => minimumLashSpeed;
        public float MinimumDownwardSpeed => minimumDownwardSpeed;
        public float AngularSpeedThreshold => angularSpeedThreshold;
        public float Cooldown => cooldown;
        public float RearmTime => rearmTime;
        public float RearmSpeed => rearmSpeed;

        /// <summary>True once the handle has slowed down again: a new stroke is allowed.</summary>
        public bool IsArmed => armed;

        /// <summary>True when a lash would be accepted right now.</summary>
        public bool IsReady => armed && cooldownRemaining <= 0f;

        public float CooldownRemaining => cooldownRemaining;

        /// <summary>Accepted lashes since the last <see cref="Reset"/>.</summary>
        public int LashCount { get; private set; }

        /// <summary>Advances the detector. Returns true on the single frame a valid lash is accepted.</summary>
        public bool Step(in WhipLashSample sample, float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);

            if (!sample.IsHeld)
            {
                // Let go: the whip is ready to be whipped again, but the cooldown keeps running so a
                // release-and-regrab cannot be used to spam accelerations.
                armed = true;
                slowElapsed = rearmTime;
                return false;
            }

            var relative = sample.WorldVelocity - sample.ReferenceVelocity;
            var linearSpeed = relative.magnitude;
            var downwardSpeed = -relative.y;

            if (linearSpeed <= rearmSpeed || downwardSpeed <= 0f)
            {
                slowElapsed += deltaTime;
                if (slowElapsed >= rearmTime)
                {
                    armed = true;
                }
            }
            else
            {
                slowElapsed = 0f;
            }

            if (!armed || cooldownRemaining > 0f || downwardSpeed < minimumDownwardSpeed)
            {
                return false;
            }

            var fastEnough = linearSpeed >= minimumLashSpeed;
            var spinningEnough = angularSpeedThreshold > 0f &&
                                 sample.AngularSpeedDegrees >= angularSpeedThreshold;
            if (!fastEnough && !spinningEnough)
            {
                return false;
            }

            return ConsumeReady();
        }

        /// <summary>Convenience overload for callers that hold the values separately.</summary>
        public bool Step(
            Vector3 worldVelocity,
            Vector3 referenceVelocity,
            float angularSpeedDegrees,
            bool isHeld,
            float deltaTime)
        {
            var sample = new WhipLashSample
            {
                WorldVelocity = worldVelocity,
                ReferenceVelocity = referenceVelocity,
                AngularSpeedDegrees = angularSpeedDegrees,
                IsHeld = isHeld
            };
            return Step(in sample, deltaTime);
        }

        /// <summary>
        /// Consumes one lash if the detector is ready. <see cref="WhipHandle"/> uses this for the
        /// optional desktop key so the keyboard goes through exactly the same cooldown and the same
        /// acceleration call as a real stroke.
        /// </summary>
        public bool ConsumeReady()
        {
            if (!IsReady)
            {
                return false;
            }

            cooldownRemaining = cooldown;
            armed = false;
            slowElapsed = 0f;
            LashCount++;
            return true;
        }

        public void Reset()
        {
            cooldownRemaining = 0f;
            slowElapsed = rearmTime;
            armed = true;
            LashCount = 0;
        }
    }
}
