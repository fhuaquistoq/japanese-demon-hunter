using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    public abstract class MonsterMovement : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField, Min(0f)] protected float patrolSpeed = 1.5f;
        [SerializeField, Min(0f)] protected float chaseSpeed = 3.5f;
        [SerializeField, Min(0f)] protected float approachSpeed = 1f;
        [SerializeField, Min(0.01f)] protected float rotationSharpness = 7f;

        protected Vector3 PatrolCenter { get; private set; }

        public abstract MonsterMovementType MovementType { get; }
        public abstract Vector3 Velocity { get; }
        public float PatrolSpeed => patrolSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float ApproachSpeed => approachSpeed;

        public virtual void Initialize(Vector3 patrolCenter)
        {
            PatrolCenter = patrolCenter;
            Stop();
        }

        public void UpdatePatrolCenter(Vector3 patrolCenter)
        {
            PatrolCenter = patrolCenter;
        }

        public abstract void TickPatrol(float patrolRadius, float deltaTime);
        public abstract void TickMoveTowards(Vector3 destination, float speed, float deltaTime);
        public abstract void Stop();

        public virtual void ConfigureSpeeds(float configuredPatrolSpeed, float configuredChaseSpeed, float configuredApproachSpeed)
        {
            patrolSpeed = Mathf.Max(0f, configuredPatrolSpeed);
            chaseSpeed = Mathf.Max(0f, configuredChaseSpeed);
            approachSpeed = Mathf.Max(0f, configuredApproachSpeed);
        }

        protected void RotateTowards(Vector3 direction, float deltaTime, bool allowPitch)
        {
            if (!allowPitch)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }
    }
}
