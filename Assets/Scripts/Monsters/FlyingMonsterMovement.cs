using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class FlyingMonsterMovement : MonsterMovement
    {
        [Header("Flight")]
        [SerializeField, Min(0f)] private float minimumHeight = 1.5f;
        [SerializeField, Min(0f)] private float maximumHeight = 7f;
        [SerializeField, Min(0.01f)] private float acceleration = 8f;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.45f;
        [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 2f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private Vector3 velocity;
        private float patrolPhase;

        public override MonsterMovementType MovementType => MonsterMovementType.Flying;
        public override Vector3 Velocity => velocity;

        public override void Initialize(Vector3 patrolCenter)
        {
            base.Initialize(patrolCenter);
            patrolPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        public override void TickPatrol(float patrolRadius, float deltaTime)
        {
            patrolPhase += deltaTime * 0.45f;
            float verticalRange = Mathf.Max(0f, maximumHeight - minimumHeight);
            float height = minimumHeight + verticalRange * (0.5f + Mathf.Sin(patrolPhase * 0.73f) * 0.25f);
            Vector3 destination = PatrolCenter + new Vector3(
                Mathf.Cos(patrolPhase) * patrolRadius,
                height,
                Mathf.Sin(patrolPhase) * patrolRadius);
            TickMoveTowards(destination, patrolSpeed, deltaTime);
        }

        public override void TickMoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            float minimumWorldHeight = PatrolCenter.y + minimumHeight;
            float maximumWorldHeight = PatrolCenter.y + Mathf.Max(minimumHeight, maximumHeight);
            destination.y = Mathf.Clamp(destination.y, minimumWorldHeight, maximumWorldHeight);

            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude < 0.0025f)
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * deltaTime);
                return;
            }

            direction.Normalize();
            direction = ApplyObstacleAvoidance(direction);
            Vector3 desiredVelocity = direction * Mathf.Max(0f, speed);
            velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * deltaTime);
            transform.position += velocity * deltaTime;
            RotateTowards(velocity, deltaTime, true);
        }

        public override void Stop()
        {
            velocity = Vector3.zero;
        }

        public void ConfigureFlight(float configuredMinimumHeight, float configuredMaximumHeight, LayerMask configuredObstacleMask)
        {
            minimumHeight = Mathf.Max(0f, configuredMinimumHeight);
            maximumHeight = Mathf.Max(minimumHeight, configuredMaximumHeight);
            obstacleMask = configuredObstacleMask;
        }

        private Vector3 ApplyObstacleAvoidance(Vector3 direction)
        {
            Vector3 origin = transform.position;
            if (!Physics.SphereCast(
                    origin,
                    obstacleProbeRadius,
                    direction,
                    out RaycastHit hit,
                    obstacleProbeDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return direction;
            }

            Vector3 lateral = Vector3.Cross(hit.normal, Vector3.up);
            if (lateral.sqrMagnitude < 0.01f)
            {
                lateral = transform.right;
            }

            float side = Vector3.Dot(lateral, transform.right) >= 0f ? 1f : -1f;
            Vector3 avoidance = lateral.normalized * side + Vector3.up * 0.65f;
            return Vector3.Slerp(direction, avoidance.normalized, 0.75f).normalized;
        }
    }
}
