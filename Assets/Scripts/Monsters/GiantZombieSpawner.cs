using System;
using System.Linq;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class GiantZombieSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject giantPrefab;
        [SerializeField] private Transform cartTransform;
        [SerializeField] private Transform cartRearReachPoint;
        [SerializeField, Min(1f)] private float initialDistance = 22f;
        [SerializeField, Min(0f)] private float giantChaseSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 12f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 30f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.1f)] private float retryInterval = 1f;
        [SerializeField, Min(0f)] private float groundClearance = 0.02f;

        [Header("Aparición por velocidad (opcional)")]
        [SerializeField] private bool spawnWhenSpeedDrops;
        [SerializeField, Min(0f)] private float speedThreshold = 1.5f;
        [SerializeField, Min(0f)] private float slowSpeedDuration = 2.5f;
        [SerializeField, Min(0f)] private float armDistance = 30f;
        [SerializeField] private MonoBehaviour speedSource;

        private GiantZombieController spawnedGiant;
        private float nextSpawnAttempt;
        private ICartSpeedPenaltyReceiver speedReceiver;
        private float slowElapsed;
        private Vector3 startPosition;
        private bool startCaptured;

        public GiantZombieController SpawnedGiant => spawnedGiant;
        public event Action<GiantZombieController> GiantSpawned;
        public bool HasSpawnedGiant => spawnedGiant != null;
        public GameObject GiantPrefab => giantPrefab;
        public Transform CartTransform => cartTransform;
        public Transform CartRearReachPoint => cartRearReachPoint;
        public float InitialDistance => initialDistance;
        public float RetryInterval => retryInterval;
        public bool SpawnWhenSpeedDrops => spawnWhenSpeedDrops;
        public float SpeedThreshold => speedThreshold;
        public float SlowSpeedDuration => slowSpeedDuration;
        public float ArmDistance => armDistance;

        private void Start()
        {
            if (spawnWhenSpeedDrops)
            {
                speedReceiver = speedSource as ICartSpeedPenaltyReceiver;
                if (cartTransform != null)
                {
                    startPosition = cartTransform.position;
                    startCaptured = true;
                }

                return;
            }

            TrySpawnGiant();
        }

        private void Update()
        {
            if (spawnedGiant != null)
            {
                return;
            }

            if (spawnWhenSpeedDrops && !HasStayedSlowLongEnough(Time.deltaTime))
            {
                return;
            }

            if (Time.time >= nextSpawnAttempt)
            {
                TrySpawnGiant();
            }
        }

        /// <summary>
        /// Optional defeat condition: the giant appears once the carriage has really ridden
        /// (<see cref="armDistance"/>) and then lost speed for a sustained time. Disabled by default
        /// so older scenes keep spawning the giant immediately.
        /// </summary>
        private bool HasStayedSlowLongEnough(float deltaTime)
        {
            if (speedReceiver == null || !IsArmed)
            {
                return false;
            }

            float speed = speedReceiver.EffectiveSpeed;
            if (speed <= speedThreshold)
            {
                slowElapsed += Mathf.Max(0f, deltaTime);
            }
            else
            {
                slowElapsed = 0f;
            }

            return slowElapsed >= slowSpeedDuration;
        }

        /// <summary>
        /// Armed by distance travelled rather than by raw speed: several monsters hanging on the
        /// cart cap the maximum speed below any threshold, and that stall is exactly the defeat case.
        /// </summary>
        public bool IsArmed
        {
            get
            {
                if (!spawnWhenSpeedDrops)
                {
                    return true;
                }

                if (cartTransform == null)
                {
                    return false;
                }

                if (!startCaptured)
                {
                    startPosition = cartTransform.position;
                    startCaptured = true;
                }

                return (cartTransform.position - startPosition).sqrMagnitude >= armDistance * armDistance;
            }
        }

        public bool TrySpawnGiant()
        {
            if (spawnedGiant != null || giantPrefab == null || cartTransform == null || cartRearReachPoint == null)
            {
                nextSpawnAttempt = Time.time + retryInterval;
                return false;
            }

            Vector3 rearDirection = Vector3.ProjectOnPlane(
                cartRearReachPoint.position - cartTransform.position, Vector3.up).normalized;
            if (rearDirection.sqrMagnitude < 0.1f) rearDirection = cartTransform.forward;
            Vector3 candidate = cartTransform.position + rearDirection * initialDistance;
            if (!TryFindSpawnGround(candidate, out Vector3 spawnPoint))
            {
                Debug.LogWarning("Giant zombie spawn skipped because no marked ground was found behind the cart.", this);
                nextSpawnAttempt = Time.time + retryInterval;
                return false;
            }

            Quaternion facingCart = Quaternion.LookRotation(-rearDirection, Vector3.up);
            GameObject instance = Instantiate(
                giantPrefab,
                spawnPoint + Vector3.up * groundClearance,
                facingCart,
                transform);
            spawnedGiant = instance.GetComponent<GiantZombieController>();
            if (spawnedGiant == null)
            {
                Destroy(instance);
                nextSpawnAttempt = Time.time + retryInterval;
                return false;
            }

            spawnedGiant.SetChaseSpeed(giantChaseSpeed);
            spawnedGiant.Initialize(cartTransform, cartRearReachPoint);
            GiantSpawned?.Invoke(spawnedGiant);
            return true;
        }

        public void Configure(
            GameObject configuredPrefab,
            Transform configuredCart,
            Transform configuredRearPoint,
            float configuredInitialDistance,
            LayerMask configuredGroundMask)
        {
            giantPrefab = configuredPrefab;
            cartTransform = configuredCart;
            cartRearReachPoint = configuredRearPoint;
            initialDistance = Mathf.Max(1f, configuredInitialDistance);
            groundMask = configuredGroundMask;
        }

        /// <summary>Wires the optional "appears when the carriage slows down" defeat trigger.</summary>
        public void ConfigureSpeedTrigger(
            bool enabled, float threshold, float duration, MonoBehaviour source, float armingDistance = 30f)
        {
            spawnWhenSpeedDrops = enabled;
            speedThreshold = Mathf.Max(0f, threshold);
            slowSpeedDuration = Mathf.Max(0f, duration);
            armDistance = Mathf.Max(0f, armingDistance);
            speedSource = source;
            speedReceiver = source as ICartSpeedPenaltyReceiver;
            slowElapsed = 0f;
            startCaptured = false;
            if (cartTransform != null)
            {
                startPosition = cartTransform.position;
                startCaptured = true;
            }
        }

        private bool TryFindSpawnGround(Vector3 center, out Vector3 point)
        {
            float[] lateralOffsets = { 0f, -2f, 2f, -4f, 4f };
            foreach (float lateralOffset in lateralOffsets)
            {
                Vector3 candidate = center + cartTransform.right * lateralOffset;
                RaycastHit[] hits = Physics.RaycastAll(candidate + Vector3.up * groundProbeHeight, Vector3.down,
                    groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                RaycastHit ground = hits.FirstOrDefault(hit =>
                    hit.collider != null && hit.collider.GetComponentInParent<MonsterGroundSurface>() != null);
                if (ground.collider != null)
                {
                    point = ground.point;
                    return true;
                }
            }

            point = default;
            return false;
        }
    }
}
