using System;
using System.Linq;
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
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 12f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 30f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.1f)] private float retryInterval = 1f;
        [SerializeField, Min(0f)] private float groundClearance = 0.02f;

        private GiantZombieController spawnedGiant;
        private float nextSpawnAttempt;

        public GiantZombieController SpawnedGiant => spawnedGiant;
        public bool HasSpawnedGiant => spawnedGiant != null;
        public GameObject GiantPrefab => giantPrefab;
        public Transform CartTransform => cartTransform;
        public Transform CartRearReachPoint => cartRearReachPoint;
        public float InitialDistance => initialDistance;
        public float RetryInterval => retryInterval;

        private void Start()
        {
            TrySpawnGiant();
        }

        private void Update()
        {
            if (spawnedGiant == null && Time.time >= nextSpawnAttempt)
            {
                TrySpawnGiant();
            }
        }

        public bool TrySpawnGiant()
        {
            if (spawnedGiant != null || giantPrefab == null || cartTransform == null || cartRearReachPoint == null)
            {
                nextSpawnAttempt = Time.time + retryInterval;
                return false;
            }

            Vector3 candidate = cartTransform.position - cartTransform.forward * initialDistance;
            if (!TryFindSpawnGround(candidate, out Vector3 spawnPoint))
            {
                Debug.LogWarning("Giant zombie spawn skipped because no marked ground was found behind the cart.", this);
                nextSpawnAttempt = Time.time + retryInterval;
                return false;
            }

            Quaternion facingCart = Quaternion.LookRotation(cartTransform.forward, Vector3.up);
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

            spawnedGiant.Initialize(cartTransform, cartRearReachPoint);
            foreach (Renderer renderer in spawnedGiant.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
            }
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
