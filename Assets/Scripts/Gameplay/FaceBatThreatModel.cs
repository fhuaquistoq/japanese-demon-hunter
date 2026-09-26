using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    public enum FaceBatThreatResult
    {
        None,
        HandCleared,
        TimedOut
    }

    /// <summary>Deterministic rules for one bat staged in the player's near field.</summary>
    public sealed class FaceBatThreatModel
    {
        public static Vector3 GetStagingPosition(
            Vector3 eyePosition, Vector3 eyeForward, Vector3 eyeRight,
            int laneIndex, float laneOffset, float standOffDistance)
        {
            Vector3 forward = eyeForward.sqrMagnitude > 0.0001f ? eyeForward.normalized : Vector3.forward;
            Vector3 right = eyeRight.sqrMagnitude > 0.0001f ? eyeRight.normalized : Vector3.right;
            return eyePosition + forward * Mathf.Max(0f, standOffDistance) +
                   right * (Mathf.Clamp(laneIndex, -1, 1) * Mathf.Max(0f, laneOffset));
        }

        public static bool IsWithinStandOff(Vector3 position, Vector3 stagingPosition, float arrivalDistance)
        {
            return (position - stagingPosition).sqrMagnitude <= Mathf.Max(0f, arrivalDistance) *
                   Mathf.Max(0f, arrivalDistance);
        }
        private readonly float clearDistance;
        private readonly float minimumWaveSpeed;
        private readonly float timeout;
        private float elapsed;
        private Vector3 previousLeftHandPosition;
        private Vector3 previousRightHandPosition;
        private bool hasPreviousLeftHandPosition;
        private bool hasPreviousRightHandPosition;

        public FaceBatThreatModel(float clearDistance, float minimumWaveSpeed, float timeout)
        {
            this.clearDistance = Mathf.Max(0f, clearDistance);
            this.minimumWaveSpeed = Mathf.Max(0f, minimumWaveSpeed);
            this.timeout = Mathf.Max(0f, timeout);
        }

        public bool IsCleared { get; private set; }
        public float Elapsed => elapsed;
        public FaceBatThreatResult Result { get; private set; }

        /// <summary>Advances independent left/right wave histories; invalid tracking only resets its own history.</summary>
        public FaceBatThreatResult Step(
            Vector3 leftPosition, bool leftValid,
            Vector3 rightPosition, bool rightValid,
            float deltaTime)
        {
            if (IsCleared) return Result;
            elapsed += Mathf.Max(0f, deltaTime);
            if (elapsed >= timeout)
            {
                IsCleared = true;
                return Result = FaceBatThreatResult.TimedOut;
            }

            bool leftWave = StepHand(leftPosition, leftValid, ref previousLeftHandPosition,
                ref hasPreviousLeftHandPosition, deltaTime);
            bool rightWave = StepHand(rightPosition, rightValid, ref previousRightHandPosition,
                ref hasPreviousRightHandPosition, deltaTime);
            if (leftWave || rightWave)
            {
                IsCleared = true;
                Result = FaceBatThreatResult.HandCleared;
            }

            return Result;
        }

        private bool StepHand(Vector3 position, bool trackingValid, ref Vector3 previousPosition,
            ref bool hasPreviousPosition, float deltaTime)
        {
            if (!trackingValid)
            {
                hasPreviousPosition = false;
                return false;
            }

            bool wave = false;
            if (hasPreviousPosition && clearDistance > 0f && minimumWaveSpeed > 0f)
            {
                float speed = Vector3.Distance(position, previousPosition) / Mathf.Max(deltaTime, 0.0001f);
                wave = position.z > 0f && position.magnitude <= clearDistance && speed >= minimumWaveSpeed;
            }

            previousPosition = position;
            hasPreviousPosition = true;
            return wave;
        }
    }
}
