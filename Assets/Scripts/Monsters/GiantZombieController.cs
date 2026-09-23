using System;
using System.Linq;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class GiantZombieController : MonoBehaviour
    {
        [SerializeField] private Transform cartTransform;
        [SerializeField] private Transform cartRearReachPoint;
        [SerializeField] private MonsterAnimationController animationController;
        [SerializeField, Min(0f)] private float chaseSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float catchRange = 1.8f;
        [SerializeField, Min(0.1f)] private float rotationSharpness = 6f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 12f;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.8f;
        [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 1.8f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private bool stopWhenCaught = true;
        [SerializeField, Range(0.5f, 2f)] private float locomotionAnimationSpeed = 1.15f;

        private bool chasing;
        private bool caughtNotificationSent;

        public bool IsChasing => chasing;
        public bool HasCaughtCart => caughtNotificationSent;
        public float ChaseSpeed => chaseSpeed;
        public Transform CartTransform => cartTransform;
        public Transform CartRearReachPoint => cartRearReachPoint;
        public bool IsConfigured => cartTransform != null && cartRearReachPoint != null;
        public float DistanceToRear => cartRearReachPoint != null
            ? Vector3.Distance(transform.position, cartRearReachPoint.position)
            : float.PositiveInfinity;

        public event Action<GiantZombieController> GiantCaughtCart;

        private void Update()
        {
            TickChase(Time.deltaTime);
        }

        public void Initialize(Transform configuredCart, Transform configuredRearPoint)
        {
            cartTransform = configuredCart;
            cartRearReachPoint = configuredRearPoint;
            caughtNotificationSent = false;
            chasing = cartTransform != null && cartRearReachPoint != null;
            animationController?.PlayLocomotion(locomotionAnimationSpeed);
            SnapToGround();
        }

        public void TickChase(float deltaTime)
        {
            if (!chasing || cartTransform == null || cartRearReachPoint == null || deltaTime <= 0f)
            {
                return;
            }

            if (DistanceToRear <= catchRange)
            {
                if (!caughtNotificationSent)
                {
                    caughtNotificationSent = true;
                    Debug.Log("GiantCaughtCart: the giant reached the cart rear test point.", this);
                    GiantCaughtCart?.Invoke(this);
                }

                if (stopWhenCaught)
                {
                    chasing = false;
                }
                return;
            }

            Vector3 direction = cartRearReachPoint.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            direction.Normalize();
            direction = ResolveDirection(direction);
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector3 candidate = transform.position + direction * (chaseSpeed * deltaTime);
            if (!TryFindGround(candidate, out RaycastHit groundHit))
            {
                return;
            }

            candidate.y = groundHit.point.y;
            transform.position = candidate;
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, 1f - Mathf.Exp(-rotationSharpness * deltaTime));
        }

        public void Configure(
            Transform configuredCart,
            Transform configuredRearPoint,
            MonsterAnimationController configuredAnimation,
            float configuredSpeed,
            float configuredCatchRange,
            LayerMask configuredGroundMask,
            LayerMask configuredObstacleMask,
            bool configuredStopWhenCaught = false,
            float configuredAnimationSpeed = 1.15f)
        {
            cartTransform = configuredCart;
            cartRearReachPoint = configuredRearPoint;
            animationController = configuredAnimation;
            chaseSpeed = Mathf.Max(0f, configuredSpeed);
            catchRange = Mathf.Max(0.1f, configuredCatchRange);
            groundMask = configuredGroundMask;
            obstacleMask = configuredObstacleMask;
            stopWhenCaught = configuredStopWhenCaught;
            locomotionAnimationSpeed = Mathf.Clamp(configuredAnimationSpeed, 0.5f, 2f);
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

        private Vector3 ResolveDirection(Vector3 desired)
        {
            Vector3 origin = transform.position + Vector3.up * obstacleProbeRadius;
            if (!IsBlocked(origin, desired)) return desired;
            Vector3 left = Quaternion.Euler(0f, -50f, 0f) * desired;
            if (!IsBlocked(origin, left)) return left;
            Vector3 right = Quaternion.Euler(0f, 50f, 0f) * desired;
            return !IsBlocked(origin, right) ? right : Vector3.zero;
        }

        private bool IsBlocked(Vector3 origin, Vector3 direction)
        {
            return Physics.SphereCastAll(origin, obstacleProbeRadius, direction, obstacleProbeDistance,
                    obstacleMask, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.collider != null &&
                            !hit.transform.IsChildOf(transform) &&
                            hit.collider.GetComponentInParent<MonsterGroundSurface>() == null);
        }

        private bool TryFindGround(Vector3 position, out RaycastHit hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * groundProbeHeight, Vector3.down,
                groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider != null && candidate.collider.GetComponentInParent<MonsterGroundSurface>() != null)
                {
                    hit = candidate;
                    return true;
                }
            }

            hit = default;
            return false;
        }
    }
}
