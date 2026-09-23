using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [Serializable]
    public sealed class MonsterSpawnEntry
    {
        public GameObject prefab;
        public MonsterMovementType movementType;
        [Min(0f)] public float weight = 1f;
        [Min(0f)] public float minimumRadius;
        [Min(0f)] public float maximumRadius;
        [Min(0f)] public float minimumFlyingHeight;
        [Min(0f)] public float maximumFlyingHeight;
        [Min(0f)] public float attachmentLoad;

        [Header("Comportamiento opcional")]
        [Tooltip("When enabled the spawner overrides the prefab's own target strategy, so the same monster prefab can hunt the player in one scene and candles in another.")]
        public bool overrideTargetStrategy;
        public MonsterTargetStrategy targetStrategy = MonsterTargetStrategy.PrioritizeHunter;
    }

    [DisallowMultipleComponent]
    public sealed class MonsterSpawner : MonoBehaviour
    {
        [Header("Required references")]
        [SerializeField] private Transform cartTransform;
        [SerializeField] private Transform hunterTransform;
        [SerializeField] private MonsterTargetRegistry targetRegistry;
        [SerializeField] private CartAttachmentPoints attachmentPoints;
        [SerializeField] private CartMonsterLoad cartLoad;
        [SerializeField] private PrototypeHunterMonsterTarget hunterTarget;
        [SerializeField] private List<MonsterSpawnEntry> spawnEntries = new List<MonsterSpawnEntry>();

        [Header("Spawn timing")]
        [SerializeField] private bool spawnOneOfEachOnStart = true;
        [SerializeField, Min(0.1f)] private float spawnInterval = 5f;
        [Tooltip("Seconds before the first spawn when the scene must start empty.")]
        [SerializeField, Min(0f)] private float initialSpawnDelay = 20f;
        [SerializeField, Min(1)] private int maximumActiveMonsters = 8;
        [SerializeField, Min(1)] private int maximumPlacementAttempts = 12;

        [Header("Placement")]
        [SerializeField, Min(0f)] private float minimumRadius = 13f;
        [SerializeField, Min(0f)] private float maximumRadius = 24f;
        [SerializeField, Min(0f)] private float minimumFlyingHeight = 3.5f;
        [SerializeField, Min(0f)] private float maximumFlyingHeight = 7f;
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 9f;
        [SerializeField, Min(0.05f)] private float clearanceRadius = 0.85f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 12f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 30f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField, Min(1f)] private float maximumDespawnDistance = 90f;

        private readonly HashSet<MonsterBase> activeMonsters = new HashSet<MonsterBase>();
        private float nextSpawnTime;

        public int ActiveMonsterCount => activeMonsters.Count;
        public int MaximumActiveMonsters => maximumActiveMonsters;
        public bool SpawnsOneOfEachOnStart => spawnOneOfEachOnStart;
        public float InitialSpawnDelay => initialSpawnDelay;
        public float SpawnInterval => spawnInterval;
        public int AttachedMonsterCount => activeMonsters.Count(monster =>
            monster != null && monster.Attachment != null && monster.Attachment.IsAttached);
        public IReadOnlyCollection<MonsterBase> ActiveMonsters => activeMonsters;
        public IReadOnlyList<MonsterSpawnEntry> SpawnEntries => spawnEntries;
        public bool IsConfigured => cartTransform != null && targetRegistry != null &&
                                    spawnEntries.Any(entry => entry != null && entry.prefab != null && entry.weight > 0f);

        private void Start()
        {
            Physics.SyncTransforms();
            if (spawnOneOfEachOnStart)
            {
                foreach (MonsterSpawnEntry entry in spawnEntries)
                {
                    if (activeMonsters.Count >= maximumActiveMonsters)
                    {
                        break;
                    }

                    TrySpawn(entry);
                }
            }

            nextSpawnTime = Time.time + (spawnOneOfEachOnStart ? spawnInterval : initialSpawnDelay);
        }

        private void Update()
        {
            RemoveStaleReferences();
            RetireDistantMonsters();
            if (Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + spawnInterval;
            if (activeMonsters.Count < maximumActiveMonsters)
            {
                TrySpawn();
            }
        }

        public bool TrySpawn(MonsterSpawnEntry requestedEntry = null)
        {
            if (!IsConfigured || activeMonsters.Count >= maximumActiveMonsters)
            {
                return false;
            }

            MonsterSpawnEntry entry = requestedEntry ?? SelectWeightedEntry();
            if (entry == null || entry.prefab == null ||
                !TryFindSpawnPosition(entry, out Vector3 position))
            {
                return false;
            }

            GameObject instance = Instantiate(entry.prefab, position, Quaternion.identity, transform);
            MonsterBase monster = instance.GetComponent<MonsterBase>();
            if (monster == null)
            {
                Destroy(instance);
                Debug.LogError($"Spawn prefab {entry.prefab.name} has no MonsterBase component.", this);
                return false;
            }

            monster.name = entry.prefab.name;
            monster.BecameInactive += HandleMonsterInactive;
            activeMonsters.Add(monster);
            if (entry.overrideTargetStrategy)
            {
                MonsterTargetSelector selector = monster.GetComponent<MonsterTargetSelector>();
                if (selector != null)
                {
                    selector.Strategy = entry.targetStrategy;
                }
            }

            MonsterAttachment spawnedAttachment = monster.GetComponent<MonsterAttachment>();
            if (spawnedAttachment != null && entry.attachmentLoad > 0f)
            {
                spawnedAttachment.SetLoadContribution(entry.attachmentLoad);
            }
            monster.Initialize(new MonsterSpawnContext
            {
                targetRegistry = targetRegistry,
                patrolCenter = cartTransform.position,
                cartTransform = cartTransform,
                attachmentPoints = attachmentPoints,
                cartLoad = cartLoad,
                hunterTarget = hunterTarget
            });
            return true;
        }

        public bool TryFindSpawnPosition(MonsterMovementType movementType, out Vector3 position)
        {
            position = default;
            if (cartTransform == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f);
                float radius = UnityEngine.Random.Range(minimumRadius, Mathf.Max(minimumRadius, maximumRadius));
                if (TryValidatePositionAtAngle(movementType, angle, radius, out position))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryFindSpawnPosition(MonsterSpawnEntry entry, out Vector3 position)
        {
            position = default;
            if (entry == null || cartTransform == null)
            {
                return false;
            }

            float entryMinimumRadius = entry.minimumRadius > 0f ? entry.minimumRadius : minimumRadius;
            float entryMaximumRadius = entry.maximumRadius > 0f ? entry.maximumRadius : maximumRadius;
            for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f);
                float radius = UnityEngine.Random.Range(entryMinimumRadius, Mathf.Max(entryMinimumRadius, entryMaximumRadius));
                if (TryValidatePositionAtAngle(entry.movementType, angle, radius, out position,
                        entry.minimumFlyingHeight, entry.maximumFlyingHeight))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryValidatePositionAtAngle(
            MonsterMovementType movementType,
            float angleDegrees,
            float radius,
            out Vector3 position)
        {
            return TryValidatePositionAtAngle(movementType, angleDegrees, radius, out position, 0f, 0f);
        }

        private bool TryValidatePositionAtAngle(
            MonsterMovementType movementType,
            float angleDegrees,
            float radius,
            out Vector3 position,
            float entryMinimumFlyingHeight,
            float entryMaximumFlyingHeight)
        {
            position = default;
            if (cartTransform == null)
            {
                return false;
            }

            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            Vector3 radialDirection = new Vector3(Mathf.Sin(angleRadians), 0f, Mathf.Cos(angleRadians));
            Vector3 candidate = cartTransform.position + radialDirection * Mathf.Max(0f, radius);

            if (movementType == MonsterMovementType.Flying)
            {
                candidate.y = cartTransform.position.y + UnityEngine.Random.Range(
                    entryMinimumFlyingHeight > 0f ? entryMinimumFlyingHeight : minimumFlyingHeight,
                    Mathf.Max(
                        entryMinimumFlyingHeight > 0f ? entryMinimumFlyingHeight : minimumFlyingHeight,
                        entryMaximumFlyingHeight > 0f ? entryMaximumFlyingHeight : maximumFlyingHeight));
            }
            else
            {
                Vector3 rayOrigin = candidate + Vector3.up * groundProbeHeight;
                RaycastHit[] groundHits = Physics.RaycastAll(
                        rayOrigin,
                        Vector3.down,
                        groundProbeDistance,
                        groundMask,
                        QueryTriggerInteraction.Ignore);
                Array.Sort(groundHits, (left, right) => left.distance.CompareTo(right.distance));
                RaycastHit? validGroundHit = groundHits.FirstOrDefault(hit =>
                    hit.collider != null && hit.collider.GetComponentInParent<MonsterGroundSurface>() != null);
                if (!validGroundHit.HasValue || validGroundHit.Value.collider == null)
                {
                    return false;
                }

                candidate = validGroundHit.Value.point;
            }

            if (!IsCandidateClear(candidate, movementType))
            {
                return false;
            }

            position = candidate;
            return true;
        }

        public void Configure(
            Transform configuredCartTransform,
            Transform configuredHunterTransform,
            MonsterTargetRegistry configuredTargetRegistry,
            IEnumerable<MonsterSpawnEntry> configuredEntries,
            LayerMask configuredGroundMask,
            LayerMask configuredObstacleMask,
            CartAttachmentPoints configuredAttachmentPoints = null,
            CartMonsterLoad configuredCartLoad = null,
            PrototypeHunterMonsterTarget configuredHunterTarget = null)
        {
            cartTransform = configuredCartTransform;
            hunterTransform = configuredHunterTransform;
            targetRegistry = configuredTargetRegistry;
            spawnEntries = configuredEntries != null
                ? configuredEntries.Where(entry => entry != null).ToList()
                : new List<MonsterSpawnEntry>();
            groundMask = configuredGroundMask;
            obstacleMask = configuredObstacleMask;
            attachmentPoints = configuredAttachmentPoints;
            cartLoad = configuredCartLoad;
            hunterTarget = configuredHunterTarget;
        }

        /// <summary>
        /// Controls when monsters start appearing: a scene can begin empty and let the tension build
        /// before the first spawn.
        /// </summary>
        public void ConfigureTiming(bool configuredSpawnOneOfEachOnStart, float configuredInitialDelay, float configuredInterval)
        {
            spawnOneOfEachOnStart = configuredSpawnOneOfEachOnStart;
            initialSpawnDelay = Mathf.Max(0f, configuredInitialDelay);
            spawnInterval = Mathf.Max(0.1f, configuredInterval);
        }

        private MonsterSpawnEntry SelectWeightedEntry()
        {
            float totalWeight = spawnEntries
                .Where(entry => entry != null && entry.prefab != null)
                .Sum(entry => Mathf.Max(0f, entry.weight));
            if (totalWeight <= 0f)
            {
                return null;
            }

            float selection = UnityEngine.Random.value * totalWeight;
            foreach (MonsterSpawnEntry entry in spawnEntries)
            {
                if (entry == null || entry.prefab == null)
                {
                    continue;
                }

                selection -= Mathf.Max(0f, entry.weight);
                if (selection <= 0f)
                {
                    return entry;
                }
            }

            return spawnEntries.LastOrDefault(entry => entry != null && entry.prefab != null);
        }

        private bool IsCandidateClear(Vector3 candidate, MonsterMovementType movementType)
        {
            Vector3 hunterPosition = hunterTransform != null ? hunterTransform.position : cartTransform.position;
            if ((candidate - hunterPosition).sqrMagnitude < minimumPlayerDistance * minimumPlayerDistance)
            {
                return false;
            }

            Vector3 clearanceCenter = candidate + Vector3.up *
                (movementType == MonsterMovementType.Ground ? clearanceRadius + 0.1f : 0f);
            Collider[] overlaps = Physics.OverlapSphere(
                clearanceCenter,
                clearanceRadius,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap == null)
                {
                    continue;
                }

                if (overlap.transform == cartTransform || overlap.transform.IsChildOf(cartTransform))
                {
                    return false;
                }

                if (movementType == MonsterMovementType.Ground &&
                    ((1 << overlap.gameObject.layer) & groundMask.value) != 0 &&
                    overlap.bounds.max.y <= clearanceCenter.y - clearanceRadius + 0.2f)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void HandleMonsterInactive(MonsterBase monster)
        {
            if (monster == null)
            {
                return;
            }

            monster.BecameInactive -= HandleMonsterInactive;
            activeMonsters.Remove(monster);
            Destroy(monster.gameObject);
        }

        private void RemoveStaleReferences()
        {
            activeMonsters.RemoveWhere(monster => monster == null || !monster.gameObject.activeInHierarchy);
        }

        private void RetireDistantMonsters()
        {
            if (cartTransform == null || maximumDespawnDistance <= 0f)
            {
                return;
            }

            float maximumSqrDistance = maximumDespawnDistance * maximumDespawnDistance;
            foreach (MonsterBase monster in activeMonsters.ToArray())
            {
                if (monster == null || (monster.Attachment != null && monster.Attachment.IsAttached))
                {
                    continue;
                }

                if ((monster.transform.position - cartTransform.position).sqrMagnitude > maximumSqrDistance)
                {
                    monster.Retire();
                }
            }
        }
    }
}
