using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JapaneseDemonHunter.Prototype;

namespace JapaneseDemonHunter.Monsters
{
    public enum MonsterSpawnDirection
    {
        Rear,
        FrontLane
    }

    [Serializable]
    public sealed class MonsterSpawnEntry
    {
        public GameObject prefab;
        public MonsterMovementType movementType;
        public MonsterSpawnDirection spawnDirection = MonsterSpawnDirection.Rear;
        public bool isFaceThreat;
        [Min(0f)] public float frontLaneForwardRadius = 12f;
        [Min(0f)] public float frontLaneFlyingAltitude = 4f;
        [Min(0f)] public float weight = 1f;
        [Min(0f)] public float minimumRadius;
        [Min(0f)] public float maximumRadius;
        [Min(0f)] public float minimumFlyingHeight;
        [Min(0f)] public float maximumFlyingHeight;
        [Min(0f)] public float attachmentLoad;
        [Tooltip("Speed multiplier for the few members of the horde that are fast enough to reach the cart.")]
        [Min(0.1f)] public float speedMultiplier = 1f;

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
        [SerializeField] private Transform rearReachPoint;
        [SerializeField] private Transform hunterTransform;
        [SerializeField] private MonsterTargetRegistry targetRegistry;
        [SerializeField] private CartAttachmentPoints attachmentPoints;
        [SerializeField] private CartMonsterLoad cartLoad;
        [SerializeField] private PrototypeHunterMonsterTarget hunterTarget;
        [SerializeField] private List<MonsterSpawnEntry> spawnEntries = new List<MonsterSpawnEntry>();

        [Header("Spawn timing")]
        [SerializeField] private bool spawnOneOfEachOnStart;
        [SerializeField] private bool waitForFirstGallop = true;
        [SerializeField, Min(0.1f)] private float spawnInterval = 11f;
        [Tooltip("Seconds before the first spawn when the scene must start empty.")]
        [SerializeField, Min(0f)] private float initialSpawnDelay = 35f;
        [Header("Horda permanente")]
        [Tooltip("Rear monsters that must always be alive behind the cart, so the ride is never empty.")]
        [SerializeField, Min(0)] private int minimumHordeSize;
        [Tooltip("Only this many of them chase; the rest keep a slower pace and never catch the cart.")]
        [SerializeField, Range(0f, 1f)] private float fastHordeFraction = 0.34f;
        [SerializeField, Min(0.1f)] private float fastHordeSpeedMultiplier = 1.5f;
        [SerializeField, Min(0.1f)] private float hordeRefillInterval = 0.5f;
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
        [SerializeField, Range(5f, 80f)] private float rearSpawnHalfAngle = 48f;
        [SerializeField, Min(0.1f)] private float frontLaneSpacing = 2.8f;

        private readonly HashSet<MonsterBase> activeMonsters = new HashSet<MonsterBase>();
        private readonly List<MonsterBase> rearMonsters = new List<MonsterBase>();
        private RearHordeModel hordeModel;
        private float nextHordeRefillTime;
        private float nextSpawnTime;
        private ICartFirstGallopSource firstGallopSource;
        private bool waitingForFirstGallop;
        private int reservedFaceThreatSlots;

        public Transform CartTransform => cartTransform;
        public int ActiveMonsterCount => activeMonsters.Count;
        public int MaximumActiveMonsters => maximumActiveMonsters;
        public bool SpawnsOneOfEachOnStart => spawnOneOfEachOnStart;
        public float InitialSpawnDelay => initialSpawnDelay;
        public float SpawnInterval => spawnInterval;
        public bool WaitsForFirstGallop => waitForFirstGallop;
        public bool IsWaitingForFirstGallop => waitingForFirstGallop;
        public bool HasFirstGallopSource => firstGallopSource != null ||
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(component =>
                component is ICartFirstGallopSource);
        public int AttachedMonsterCount => activeMonsters.Count(monster =>
            monster != null && monster.Attachment != null && monster.Attachment.IsAttached);
        /// <summary>Rear monsters kept alive so a horde is always visible behind the cart.</summary>
        public int RearMonsterCount
        {
            get
            {
                rearMonsters.RemoveAll(monster => monster == null || !monster.gameObject.activeInHierarchy);
                return rearMonsters.Count;
            }
        }
        public int MinimumHordeSize => minimumHordeSize;
        public RearHordeModel HordeModel => hordeModel ??=
            new RearHordeModel(fastHordeFraction, fastHordeSpeedMultiplier);
        public IReadOnlyCollection<MonsterBase> ActiveMonsters => activeMonsters;
        public IReadOnlyList<MonsterSpawnEntry> SpawnEntries => spawnEntries;
        public event Action<MonsterBase> MonsterSpawned;
        public bool IsConfigured => cartTransform != null && targetRegistry != null &&
                                    spawnEntries.Any(entry => entry != null && entry.prefab != null && entry.weight > 0f);

        private void Start()
        {
            ResolveRearReachPoint();
            Physics.SyncTransforms();
            ResolveFirstGallopSource();
            if (waitForFirstGallop && firstGallopSource != null)
            {
                waitingForFirstGallop = true;
                firstGallopSource.FirstGallop += HandleFirstGallop;
                return;
            }

            if (waitForFirstGallop)
            {
                waitingForFirstGallop = true;
                Debug.LogError("No ICartFirstGallopSource was found; monster spawning stays paused until one is connected.", this);
                return;
            }

            BeginSpawnSequence();
        }

        private void OnDestroy()
        {
            if (firstGallopSource != null)
            {
                firstGallopSource.FirstGallop -= HandleFirstGallop;
            }
        }

        private void BeginSpawnSequence()
        {
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
            MaintainHorde();
            if (waitingForFirstGallop || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + spawnInterval;
            if (activeMonsters.Count < maximumActiveMonsters - reservedFaceThreatSlots)
            {
                TrySpawn();
            }
        }

        /// <summary>
        /// Keeps the configured number of rear monsters alive and promotes only a few of them to the
        /// fast pace. The horde fills immediately once the ride starts, which is what makes the
        /// monsters always visible behind the cart instead of trickling in one at a time.
        /// </summary>
        private void MaintainHorde()
        {
            if (minimumHordeSize <= 0 || !IsConfigured)
            {
                return;
            }

            int rearCount = RearMonsterCount;
            if (Time.time < nextHordeRefillTime)
            {
                ApplyHordeSpeeds();
                return;
            }

            int missing = HordeModel.MissingCount(rearCount, minimumHordeSize, activeMonsters.Count,
                maximumActiveMonsters);
            if (missing <= 0)
            {
                ApplyHordeSpeeds();
                return;
            }

            MonsterSpawnEntry rearEntry = SelectRearEntry();
            if (rearEntry == null)
            {
                return;
            }

            for (int spawned = 0; spawned < missing; spawned++)
            {
                if (!TrySpawn(rearEntry))
                {
                    break;
                }
            }

            nextHordeRefillTime = Time.time + hordeRefillInterval;
            ApplyHordeSpeeds();
        }

        private void ApplyHordeSpeeds()
        {
            int fastRunners = HordeModel.FastRunnerCount(rearMonsters.Count);
            for (int index = rearMonsters.Count - 1; index >= 0; index--)
            {
                MonsterBase monster = rearMonsters[index];
                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    rearMonsters.RemoveAt(index);
                    continue;
                }

                float multiplier = HordeModel.SpeedMultiplierFor(index, fastRunners);
                monster.SpeedMultiplier = multiplier;
            }
        }

        private MonsterSpawnEntry SelectRearEntry()
        {
            MonsterSpawnEntry candidate = null;
            foreach (MonsterSpawnEntry entry in spawnEntries)
            {
                if (entry == null || entry.prefab == null ||
                    entry.spawnDirection == MonsterSpawnDirection.FrontLane)
                {
                    continue;
                }

                if (candidate == null || entry.weight > candidate.weight)
                {
                    candidate = entry;
                }
            }

            return candidate;
        }

        public bool TrySpawn(MonsterSpawnEntry requestedEntry = null)
        {
            if (!IsConfigured || activeMonsters.Count >= maximumActiveMonsters)
            {
                return false;
            }

            MonsterSpawnEntry entry = requestedEntry ?? SelectWeightedEntry();
            int frontLaneIndex = 0;
            if (entry == null || entry.prefab == null ||
                !TryFindSpawnPosition(entry, out Vector3 position, out frontLaneIndex))
            {
                return false;
            }

            return SpawnAtPosition(entry, position, frontLaneIndex) != null;
        }

        public bool TrySpawnFaceThreatRound(int count)
        {
            if (!IsConfigured || count < 1 || count > 3 ||
                activeMonsters.Count + count > maximumActiveMonsters)
            {
                return false;
            }

            MonsterSpawnEntry entry = spawnEntries.FirstOrDefault(candidate =>
                candidate != null && candidate.prefab != null && candidate.weight > 0f &&
                candidate.isFaceThreat && candidate.spawnDirection == MonsterSpawnDirection.FrontLane);
            if (entry == null || entry.prefab.GetComponent<MonsterBase>() == null)
            {
                return false;
            }

            Vector3[] positions = new Vector3[count];
            int[] lanes = new int[count];
            for (int index = 0; index < count; index++)
            {
                int lane = count == 1 ? 0 : count == 2 ? (index == 0 ? -1 : 1) : index - 1;
                if (!TryFindFrontLanePosition(entry, out positions[index], out lanes[index], lane))
                {
                    return false;
                }
            }

            for (int index = 0; index < count; index++)
            {
                if (SpawnAtPosition(entry, positions[index], lanes[index]) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private MonsterBase SpawnAtPosition(MonsterSpawnEntry entry, Vector3 position, int frontLaneIndex)
        {
            GameObject instance = Instantiate(entry.prefab, position, Quaternion.identity, transform);
            MonsterBase monster = instance.GetComponent<MonsterBase>();
            if (monster == null)
            {
                Destroy(instance);
                Debug.LogError($"Spawn prefab {entry.prefab.name} has no MonsterBase component.", this);
                return null;
            }

            monster.name = entry.prefab.name;
            monster.BecameInactive += HandleMonsterInactive;
            activeMonsters.Add(monster);
            if (entry.spawnDirection != MonsterSpawnDirection.FrontLane)
            {
                rearMonsters.Add(monster);
                monster.SpeedMultiplier = HordeModel.SpeedMultiplierFor(
                    rearMonsters.Count - 1, HordeModel.FastRunnerCount(rearMonsters.Count));
            }
            if (entry.overrideTargetStrategy)
            {
                MonsterTargetSelector selector = monster.GetComponent<MonsterTargetSelector>();
                if (selector != null)
                {
                    selector.Strategy = entry.targetStrategy;
                }
            }

            monster.ConfigureSpawnDirection(entry.spawnDirection, frontLaneIndex);
            monster.ConfigureFaceThreatSpawn(entry.isFaceThreat);
            monster.ConfigureAttachmentEnabled(entry.spawnDirection != MonsterSpawnDirection.FrontLane);
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
                rearReachPoint = rearReachPoint,
                attachmentPoints = attachmentPoints,
                cartLoad = cartLoad,
                hunterTarget = hunterTarget
            });
            MonsterSpawned?.Invoke(monster);
            return monster;
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
                float angle = UnityEngine.Random.Range(-rearSpawnHalfAngle, rearSpawnHalfAngle);
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
            return TryFindSpawnPosition(entry, out position, out _);
        }

        private bool TryFindSpawnPosition(MonsterSpawnEntry entry, out Vector3 position, out int frontLaneIndex)
        {
            position = default;
            frontLaneIndex = 0;
            if (entry == null || cartTransform == null)
            {
                return false;
            }

            if (entry.spawnDirection == MonsterSpawnDirection.FrontLane)
            {
                return TryFindFrontLanePosition(entry, out position, out frontLaneIndex);
            }

            float entryMinimumRadius = entry.minimumRadius > 0f ? entry.minimumRadius : minimumRadius;
            float entryMaximumRadius = entry.maximumRadius > 0f ? entry.maximumRadius : maximumRadius;
            for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
            {
                float angle = UnityEngine.Random.Range(-rearSpawnHalfAngle, rearSpawnHalfAngle);
                float radius = UnityEngine.Random.Range(entryMinimumRadius, Mathf.Max(entryMinimumRadius, entryMaximumRadius));
                if (TryValidatePositionAtAngle(entry.movementType, angle, radius, out position,
                        entry.minimumFlyingHeight, entry.maximumFlyingHeight))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindFrontLanePosition(MonsterSpawnEntry entry, out Vector3 position, out int lane,
            int? forcedLane = null)
        {
            position = default;
            lane = forcedLane.HasValue ? Mathf.Clamp(forcedLane.Value, -1, 1) : UnityEngine.Random.Range(-1, 2);
            Vector3 forward = Vector3.ProjectOnPlane(-cartTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.back;
            }

            Vector3 candidate = cartTransform.position + forward * Mathf.Max(0f, entry.frontLaneForwardRadius) +
                                cartTransform.right * (lane * frontLaneSpacing);

            if (entry.movementType == MonsterMovementType.Flying)
            {
                candidate.y = cartTransform.position.y + Mathf.Max(0f, entry.frontLaneFlyingAltitude);
            }
            else
            {
                Vector3 rayOrigin = candidate + Vector3.up * groundProbeHeight;
                RaycastHit[] groundHits = Physics.RaycastAll(rayOrigin, Vector3.down, groundProbeDistance,
                    groundMask, QueryTriggerInteraction.Ignore);
                Array.Sort(groundHits, (left, right) => left.distance.CompareTo(right.distance));
                RaycastHit? validGroundHit = groundHits.FirstOrDefault(hit =>
                    hit.collider != null && hit.collider.GetComponentInParent<MonsterGroundSurface>() != null);
                if (!validGroundHit.HasValue || validGroundHit.Value.collider == null)
                {
                    return false;
                }

                candidate = validGroundHit.Value.point;
            }

            if (!IsCandidateClear(candidate, entry.movementType))
            {
                return false;
            }

            position = candidate;
            return true;
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
            ResolveRearReachPoint();
            Vector3 rearDirection = rearReachPoint != null
                ? Vector3.ProjectOnPlane(rearReachPoint.position - cartTransform.position, Vector3.up).normalized
                : cartTransform.forward;
            Vector3 radialDirection = cartTransform.right * Mathf.Sin(angleRadians) +
                                      rearDirection * Mathf.Cos(angleRadians);
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
        public void ConfigureTiming(
            bool configuredSpawnOneOfEachOnStart,
            float configuredInitialDelay,
            float configuredInterval,
            bool configuredWaitForFirstGallop = true)
        {
            spawnOneOfEachOnStart = configuredSpawnOneOfEachOnStart;
            initialSpawnDelay = Mathf.Max(0f, configuredInitialDelay);
            spawnInterval = Mathf.Max(0.1f, configuredInterval);
            waitForFirstGallop = configuredWaitForFirstGallop;
        }

        public void ConfigureFirstGallopSource(MonoBehaviour configuredSource)
        {
            firstGallopSource = configuredSource as ICartFirstGallopSource;
        }

        /// <summary>Configures the permanent rear horde: how many, and how many of them run.</summary>
        public void ConfigureHorde(
            int configuredMinimumSize,
            float configuredFastFraction,
            float configuredFastSpeedMultiplier,
            float configuredRefillInterval = 0.5f)
        {
            minimumHordeSize = Mathf.Max(0, configuredMinimumSize);
            fastHordeFraction = Mathf.Clamp01(configuredFastFraction);
            fastHordeSpeedMultiplier = Mathf.Max(0.1f, configuredFastSpeedMultiplier);
            hordeRefillInterval = Mathf.Max(0.1f, configuredRefillInterval);
            hordeModel = new RearHordeModel(fastHordeFraction, fastHordeSpeedMultiplier);
            maximumActiveMonsters = Mathf.Max(maximumActiveMonsters, minimumHordeSize);
        }

        public void ConfigureFirstGallopWait(bool configuredWait)
        {
            waitForFirstGallop = configuredWait;
        }

        public void ReserveFaceThreatSlots(int slots)
        {
            reservedFaceThreatSlots = Mathf.Clamp(slots, 0, maximumActiveMonsters);
        }

        private void ResolveFirstGallopSource()
        {
            if (firstGallopSource != null)
            {
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            firstGallopSource = behaviours.OfType<ICartFirstGallopSource>().FirstOrDefault();
        }

        private void HandleFirstGallop()
        {
            if (!waitingForFirstGallop)
            {
                return;
            }

            waitingForFirstGallop = false;
            if (firstGallopSource != null)
            {
                firstGallopSource.FirstGallop -= HandleFirstGallop;
                firstGallopSource = null;
            }

            BeginSpawnSequence();
        }

        private void ResolveRearReachPoint()
        {
            if (rearReachPoint != null) return;
            GiantZombieSpawner giant = FindAnyObjectByType<GiantZombieSpawner>();
            if (giant != null) rearReachPoint = giant.CartRearReachPoint;
        }

        private MonsterSpawnEntry SelectWeightedEntry()
        {
            float totalWeight = spawnEntries
                .Where(entry => entry != null && entry.prefab != null &&
                                (reservedFaceThreatSlots == 0 || !entry.isFaceThreat))
                .Sum(entry => Mathf.Max(0f, entry.weight));
            if (totalWeight <= 0f)
            {
                return null;
            }

            float selection = UnityEngine.Random.value * totalWeight;
            foreach (MonsterSpawnEntry entry in spawnEntries)
            {
                if (entry == null || entry.prefab == null ||
                    (reservedFaceThreatSlots > 0 && entry.isFaceThreat))
                {
                    continue;
                }

                selection -= Mathf.Max(0f, entry.weight);
                if (selection <= 0f)
                {
                    return entry;
                }
            }

            return spawnEntries.LastOrDefault(entry => entry != null && entry.prefab != null &&
                                                      (reservedFaceThreatSlots == 0 || !entry.isFaceThreat));
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
            rearMonsters.Remove(monster);
            Destroy(monster.gameObject);
        }

        private void RemoveStaleReferences()
        {
            activeMonsters.RemoveWhere(monster => monster == null || !monster.gameObject.activeInHierarchy);
            rearMonsters.RemoveAll(monster => monster == null || !monster.gameObject.activeInHierarchy);
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
