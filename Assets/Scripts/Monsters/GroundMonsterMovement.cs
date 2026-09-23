using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class GroundMonsterMovement : MonsterMovement
    {
        [Header("Grounding and avoidance")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 6f;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.4f;
        [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 1.2f;
        [SerializeField, Min(0f)] private float maximumGroundStepHeight = 0.35f;

        private Vector3 velocity;
        private float patrolPhase;

        public override MonsterMovementType MovementType => MonsterMovementType.Ground;
        public override Vector3 Velocity => velocity;
        public float MaximumGroundStepHeight => maximumGroundStepHeight;

        public override void Initialize(Vector3 patrolCenter)
        {
            base.Initialize(patrolCenter);
            patrolPhase = Random.Range(0f, Mathf.PI * 2f);
            SnapToGround();
        }

        public override void TickPatrol(float patrolRadius, float deltaTime)
        {
            patrolPhase += deltaTime * 0.25f;
            Vector3 destination = PatrolCenter + new Vector3(
                Mathf.Cos(patrolPhase) * patrolRadius,
                0f,
                Mathf.Sin(patrolPhase) * patrolRadius);
            TickMoveTowards(destination, patrolSpeed, deltaTime);
        }

        public override void TickMoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0025f)
            {
                Stop();
                return;
            }

            direction.Normalize();
            if (!TryResolveDirection(direction, out Vector3 resolvedDirection))
            {
                Stop();
                return;
            }

            Vector3 candidate = transform.position + resolvedDirection * (Mathf.Max(0f, speed) * deltaTime);
            if (!TryFindGround(candidate, out RaycastHit groundHit))
            {
                Stop();
                return;
            }

            // Only marked road/terrain is valid ground, and even a marked surface may not be
            // climbed as an abrupt step. This keeps terrestrial monsters beside the cart instead
            // of letting collision geometry lift them onto its upper platform.
            if (groundHit.point.y - transform.position.y > maximumGroundStepHeight)
            {
                Stop();
                return;
            }

            candidate.y = groundHit.point.y;
            velocity = (candidate - transform.position) / Mathf.Max(deltaTime, 0.0001f);
            transform.position = candidate;
            RotateTowards(resolvedDirection, deltaTime, false);
        }

        public override void Stop()
        {
            velocity = Vector3.zero;
        }

        public void ConfigureGrounding(
            LayerMask configuredGroundMask,
            LayerMask configuredObstacleMask,
            float configuredMaximumStepHeight = 0.35f)
        {
            groundMask = configuredGroundMask;
            obstacleMask = configuredObstacleMask;
            maximumGroundStepHeight = Mathf.Max(0f, configuredMaximumStepHeight);
        }

        public bool SnapToGround()
        {
            if (!TryFindGround(transform.position, out RaycastHit hit))
            {
                return false;
            }

            Vector3 position = transform.position;
            position.y = hit.point.y;
            transform.position = position;
            return true;
        }

        private bool TryFindGround(Vector3 position, out RaycastHit hit)
        {
            Vector3 origin = position + Vector3.up * groundProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider != null &&
                    !candidate.collider.transform.IsChildOf(transform) &&
                    candidate.collider.GetComponentInParent<MonsterGroundSurface>() != null)
                {
                    hit = candidate;
                    return true;
                }
            }

            hit = default;
            return false;
        }

        private bool TryResolveDirection(Vector3 desiredDirection, out Vector3 resolvedDirection)
        {
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(obstacleProbeRadius, 0.5f);
            if (!IsBlocked(origin, desiredDirection))
            {
                resolvedDirection = desiredDirection;
                return true;
            }

            Vector3 left = Quaternion.Euler(0f, -55f, 0f) * desiredDirection;
            Vector3 right = Quaternion.Euler(0f, 55f, 0f) * desiredDirection;
            if (!IsBlocked(origin, left))
            {
                resolvedDirection = left;
                return true;
            }

            if (!IsBlocked(origin, right))
            {
                resolvedDirection = right;
                return true;
            }

            resolvedDirection = Vector3.zero;
            return false;
        }

        private bool IsBlocked(Vector3 origin, Vector3 direction)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                obstacleProbeRadius,
                direction,
                obstacleProbeDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (!hit.transform.IsChildOf(transform) &&
                    hit.collider.GetComponentInParent<MonsterGroundSurface>() == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
