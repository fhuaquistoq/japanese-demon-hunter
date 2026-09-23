using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JapaneseDemonHunter.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Monsters.Editor
{
    /// <summary>
    /// Phase 3 deterministic verification. Every check runs without a headset and without entering
    /// play mode, so the cart load maths, attachment reservations, the provisional speed controller
    /// and the giant pursuit can all be validated in a batch editor launch.
    /// </summary>
    [InitializeOnLoad]
    public static class MonsterSurvivalVerification
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string ReportPath = "Logs/Phase3DeterministicVerification.txt";

        static MonsterSurvivalVerification()
        {
            if (Environment.GetCommandLineArgs().Any(argument => argument == "-survivalVerify"))
            {
                MonsterBatchGate.RunWhenEditorIsIdle(RunBootstrapped);
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        [MenuItem("Tools/Monsters/Run Phase 3 Deterministic Verification")]
        public static void Run()
        {
            List<string> passed = new List<string>();
            List<string> failed = new List<string>();

            VerifySceneConfiguration(passed, failed);
            VerifyAttachmentPointCapacity(passed, failed);
            VerifyAttachmentRegistersLoadOnce(passed, failed);
            VerifyLoadNeverGoesNegative(passed, failed);
            VerifySpeedPenaltyAndWhip(passed, failed);
            VerifyAttachedMonsterRepeatsAttack(passed, failed);
            VerifyGroundMonsterCannotClimbCart(passed, failed);
            VerifyGiantClosesDistanceWithoutTeleporting(passed, failed);

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Phase 3 deterministic verification: {passed.Count} passed, {failed.Count} failed");
            foreach (string item in passed) report.AppendLine($"PASS: {item}");
            foreach (string item in failed) report.AppendLine($"FAIL: {item}");
            Directory.CreateDirectory(GetLogDirectory());
            File.WriteAllText(Path.Combine(GetLogDirectory(), Path.GetFileName(ReportPath)), report.ToString());
            Debug.Log(report.ToString());

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (failed.Count > 0)
            {
                throw new InvalidOperationException(report.ToString());
            }
        }

        private static void RunBootstrapped()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- scene level

        private static void VerifySceneConfiguration(ICollection<string> passed, ICollection<string> failed)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CartAttachmentPoints points = UnityEngine.Object.FindAnyObjectByType<CartAttachmentPoints>();
            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            GiantZombieSpawner giantSpawner = UnityEngine.Object.FindAnyObjectByType<GiantZombieSpawner>();
            SwordDamage sword = UnityEngine.Object.FindAnyObjectByType<SwordDamage>();
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();

            Check(references != null && references.IsConfigured,
                "Phase 1 cart, hunter and candle references remain intact.", passed, failed);
            Check(points != null && points.Points.Count >= 6 && points.TotalCapacity >= 8,
                $"Attachment points are distributed around the cart ({points?.Points.Count ?? 0} points, capacity {points?.TotalCapacity ?? 0}).",
                passed, failed);
            Check(load != null && load.GetComponent<SimulatedCartSurvivalController>() != null,
                "CartMonsterLoad drives the provisional speed controller instead of each monster changing speed.", passed, failed);
            Check(load != null && Mathf.Approximately(load.ReferenceMaximumLoad, 45f) &&
                  Mathf.Approximately(load.MinimumSpeedMultiplier, 0.22f),
                "The prototype load balance produces a clearly noticeable slowdown with several attached monsters.",
                passed, failed);
            Check(sword != null && sword.GetComponent<PrototypeSwordController>() != null,
                "The prototype sword exposes SwordDamage with a desktop driver.", passed, failed);
            Check(spawner != null && spawner.SpawnEntries.Count == 5,
                "A single generator registers all five small monster types.", passed, failed);
            Check(giantSpawner != null && giantSpawner.SpawnedGiant == null,
                "The giant pursuer is administered separately from the small monster generator.", passed, failed);
            Check(giantSpawner != null && giantSpawner.GiantPrefab != null &&
                  giantSpawner.CartTransform == references?.CartTransform &&
                  giantSpawner.CartRearReachPoint != null &&
                  Mathf.Approximately(giantSpawner.InitialDistance, 12.5f),
                "The giant spawner keeps valid prefab/cart/rear references and starts 12.5 m behind the cart.",
                passed, failed);

            if (points != null)
            {
                MonsterAttachmentPoint[] groundPoints = points.Points
                    .Where(point => point != null && point.Accepts(MonsterAttachmentKind.Ground))
                    .ToArray();
                bool groundPointsStayOutside = groundPoints.Length == 5 && groundPoints.All(point =>
                    point.transform.localPosition.y <= 0.2f &&
                    (Mathf.Abs(point.transform.localPosition.x) >= 1.8f ||
                     point.transform.localPosition.z <= -2.8f));
                Check(groundPointsStayOutside,
                    "Ground monsters can reserve only five low side/rear points outside the cart platform.",
                    passed, failed);
                Check(!groundPoints.Any(point => point.name.Contains("Top") || point.name == "Attach_FrontCenter"),
                    "No top or front-center attachment point accepts the terrestrial zombie.", passed, failed);
            }

            if (spawner == null)
            {
                return;
            }

            int flyingCount = 0;
            int groundCount = 0;
            foreach (MonsterSpawnEntry entry in spawner.SpawnEntries)
            {
                if (entry.prefab == null)
                {
                    failed.Add("A spawn entry has no prefab.");
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(entry.prefab));
                MonsterAttachment attachment = prefab != null ? prefab.GetComponent<MonsterAttachment>() : null;
                MonsterDamageable damageable = prefab != null ? prefab.GetComponent<MonsterDamageable>() : null;
                AttachedMonsterAttack cartAttack = prefab != null ? prefab.GetComponent<AttachedMonsterAttack>() : null;
                MonsterBase monster = prefab != null ? prefab.GetComponent<MonsterBase>() : null;

                if (entry.movementType == MonsterMovementType.Flying) flyingCount++;
                else groundCount++;

                Check(monster != null && attachment != null && damageable != null && cartAttack != null,
                    $"{entry.prefab.name} shares the reusable attachment, damage and cart-attack components.", passed, failed);
                Check(attachment != null && attachment.AttachmentKind == (entry.movementType == MonsterMovementType.Flying
                          ? MonsterAttachmentKind.Flying
                          : MonsterAttachmentKind.Ground),
                    $"{entry.prefab.name} requests {entry.movementType} attachment points.", passed, failed);
                Check(Mathf.Approximately(entry.attachmentLoad, attachment != null ? attachment.LoadContribution : -1f),
                    $"{entry.prefab.name} declares its cart load once in the generator ({entry.attachmentLoad:0.#}).", passed, failed);
                Check(prefab != null && prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0 &&
                      prefab.GetComponentInChildren<Animator>(true) != null,
                    $"{entry.prefab.name} uses an imported skinned model with its own Animator.", passed, failed);
                Check(cartAttack != null && Mathf.Approximately(cartAttack.ImpactDelay, 0.28f) &&
                      Mathf.Approximately(cartAttack.Cooldown, 1.35f) &&
                      Mathf.Approximately(cartAttack.AttackAnimationSpeed, 1.15f),
                    $"{entry.prefab.name} uses the quicker configurable attack timing (0.28/1.35 s, 1.15x).",
                    passed, failed);
            }

            Check(flyingCount == 2 && groundCount == 3,
                "The generator mixes two flying and three ground small monsters.", passed, failed);

            GameObject giant = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Monsters/GiantZombie/Prefabs/GiantZombie.prefab");
            Check(giant != null && giant.GetComponent<GiantZombieController>() != null,
                "The giant pursuer prefab has its own chase controller.", passed, failed);
            Check(giant != null && giant.GetComponent<MonsterBase>() == null &&
                  giant.GetComponent<MonsterAttachment>() == null &&
                  giant.GetComponent<MonsterDamageable>() == null,
                "The giant cannot attach to the cart, add load or be killed by the common sword.", passed, failed);
            Check(!spawner.SpawnEntries.Any(entry => entry.prefab != null && entry.prefab == giant),
                "The giant never appears in the random small monster generator.", passed, failed);
            GiantZombieController giantController = giant != null ? giant.GetComponent<GiantZombieController>() : null;
            Check(giantController != null && Mathf.Approximately(giantController.ChaseSpeed, 5.6f),
                "The giant uses the faster configurable 5.6 chase speed.",
                passed, failed);

            Animator giantAnimator = giant != null ? giant.GetComponentInChildren<Animator>(true) : null;
            Renderer[] giantRenderers = giant != null ? giant.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            Check(giantAnimator != null && giantAnimator.runtimeAnimatorController != null && !giantAnimator.applyRootMotion,
                "The giant has an Animator Controller and code-driven movement without root motion.", passed, failed);
            Check(giantRenderers.Length > 0 && giantRenderers.All(renderer => renderer.enabled) &&
                  giantRenderers.SelectMany(renderer => renderer.sharedMaterials).All(material =>
                      material != null && material.shader != null && material.shader.isSupported),
                "The giant prefab has enabled renderers and valid supported materials.", passed, failed);

            if (references != null && references.CartTransform != null)
            {
                Transform cartBody = references.CartTransform.Find("CartBody");
                Check(cartBody != null && cartBody.GetComponentInParent<MonsterGroundSurface>() == null,
                    "The cart body is not tagged as ground, so terrestrial monsters cannot snap onto its roof.",
                    passed, failed);
            }

            Physics.SyncTransforms();
            foreach (MonsterSpawnEntry entry in spawner.SpawnEntries)
            {
                if (entry.prefab == null)
                {
                    continue;
                }

                bool valid = spawner.TryFindSpawnPosition(entry, out Vector3 position);
                bool reasonableHeight = valid &&
                                        (entry.movementType == MonsterMovementType.Flying
                                            ? position.y > 2f
                                            : Mathf.Abs(position.y) < 0.5f);
                Check(valid && reasonableHeight,
                    $"{entry.prefab.name} has a valid {entry.movementType} spawn position around the moving cart.", passed, failed);
            }
        }

        // ---------------------------------------------------------------- attachment points

        private static void VerifyAttachmentPointCapacity(ICollection<string> passed, ICollection<string> failed)
        {
            GameObject registryObject = new GameObject("PointCapacityRegistry");
            GameObject first = new GameObject("OccupantA");
            GameObject second = new GameObject("OccupantB");
            try
            {
                MonsterAttachmentPoint point = registryObject.AddComponent<MonsterAttachmentPoint>();
                point.Configure(MonsterAttachmentKind.Ground, 1, Vector3.forward);

                MonsterAttachment occupantA = first.AddComponent<MonsterAttachment>();
                occupantA.Configure(MonsterAttachmentKind.Ground, 10f, 1f, 0f, Vector3.zero);
                MonsterAttachment occupantB = second.AddComponent<MonsterAttachment>();
                occupantB.Configure(MonsterAttachmentKind.Ground, 10f, 1f, 0f, Vector3.zero);

                bool firstReserved = point.TryReserve(occupantA);
                bool secondRejected = !point.TryReserve(occupantB);
                bool countIsOne = point.OccupiedCount == 1;

                point.Release(occupantA);
                bool secondAcceptedAfterRelease = point.TryReserve(occupantB);
                bool releasedFully = point.OccupiedCount == 1;
                point.Release(occupantB);

                Check(firstReserved && secondRejected && countIsOne,
                    "A single-capacity attachment point never hosts two monsters at once.", passed, failed);
                Check(secondAcceptedAfterRelease && releasedFully && point.OccupiedCount == 0,
                    "Releasing a monster frees its attachment point for the next one.", passed, failed);

                GameObject flyingObject = new GameObject("FlyingOccupant");
                try
                {
                    MonsterAttachment flying = flyingObject.AddComponent<MonsterAttachment>();
                    flying.Configure(MonsterAttachmentKind.Flying, 5f, 1f, 0f, Vector3.zero);
                    Check(!point.TryReserve(flying),
                        "A ground-only attachment point refuses a flying monster.", passed, failed);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(flyingObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registryObject);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        // ---------------------------------------------------------------- load accounting

        private static void VerifyAttachmentRegistersLoadOnce(ICollection<string> passed, ICollection<string> failed)
        {
            CartFixture fixture = CartFixture.Create();
            GameObject pointObject = new GameObject("AttachmentPoint");
            GameObject registryObject = new GameObject("AttachmentRegistry");
            GameObject monsterObject = new GameObject("Monster");
            try
            {
                MonsterAttachmentPoint point = pointObject.AddComponent<MonsterAttachmentPoint>();
                point.Configure(MonsterAttachmentKind.Ground, 1, Vector3.forward);
                CartAttachmentPoints registry = registryObject.AddComponent<CartAttachmentPoints>();
                registry.Configure(new[] { point });

                MonsterBase owner = monsterObject.AddComponent<MonsterBase>();
                MonsterAttachment attachment = monsterObject.AddComponent<MonsterAttachment>();
                attachment.Configure(MonsterAttachmentKind.Ground, 15f, 1.3f, 0f, Vector3.zero);
                attachment.Initialize(owner, registry, fixture.Load);
                monsterObject.transform.position = pointObject.transform.position;

                Physics.SyncTransforms();

                bool reserved = attachment.TrySelectPoint();
                bool withinReach = attachment.IsWithinReach();
                bool attached = attachment.TickAttach(0f);
                float firstLoad = fixture.Load.TotalLoad;
                int firstCount = fixture.Load.RegisteredMonsterCount;

                attachment.TickAttach(0f);
                attachment.TickAttach(0f);
                bool noDoubleCounting = Mathf.Approximately(fixture.Load.TotalLoad, firstLoad) &&
                                        fixture.Load.RegisteredMonsterCount == firstCount;

                Check(reserved && withinReach && attached && attachment.IsAttached,
                    "A monster reserves a point, reaches it and switches to Attached.", passed, failed);
                Check(firstCount == 1 && Mathf.Approximately(firstLoad, 15f),
                    "Attaching registers the monster load exactly once.", passed, failed);
                Check(noDoubleCounting && firstCount == 1,
                    "Re-ticking an attached monster never adds its load twice.", passed, failed);
                Check(Mathf.Approximately(fixture.Load.SpeedMultiplier, 0.75f),
                    "Total load 15 of a 60 reference produces a 0.75 speed multiplier.", passed, failed);

                attachment.Detach();
                Check(fixture.Load.RegisteredMonsterCount == 0 && Mathf.Approximately(fixture.Load.TotalLoad, 0f),
                    "Detaching removes the load contribution exactly once.", passed, failed);
                Check(point.OccupiedCount == 0, "Detaching releases the attachment point.", passed, failed);

                attachment.TrySelectPoint();
                attachment.TickAttach(0f);
                Check(fixture.Load.RegisteredMonsterCount == 1 && Mathf.Approximately(fixture.Load.TotalLoad, 15f),
                    "Re-attaching after a detach registers the load again without residue.", passed, failed);

                attachment.Detach();
                UnityEngine.Object.DestroyImmediate(monsterObject);
                Check(fixture.Load.RegisteredMonsterCount == 0 && Mathf.Approximately(fixture.Load.TotalLoad, 0f),
                    "Destroying an attached monster leaves no residual load behind.", passed, failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pointObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
                if (monsterObject != null) UnityEngine.Object.DestroyImmediate(monsterObject);
                fixture.Dispose();
            }
        }

        private static void VerifyLoadNeverGoesNegative(ICollection<string> passed, ICollection<string> failed)
        {
            CartFixture fixture = CartFixture.Create();
            GameObject monsterObject = new GameObject("Monster");
            try
            {
                MonsterAttachment attachment = monsterObject.AddComponent<MonsterAttachment>();
                attachment.Configure(MonsterAttachmentKind.Ground, 20f, 1f, 0f, Vector3.zero);

                bool duplicateRejected = !fixture.Load.Unregister(attachment);
                bool emptyClearSafe = fixture.Load.TotalLoad == 0f;
                fixture.Load.ClearAll();
                bool staysZero = Mathf.Approximately(fixture.Load.TotalLoad, 0f);

                fixture.Load.Register(attachment, 20f);
                Check(fixture.Load.TotalLoad == 20f, "Registering a monster adds its load.", passed, failed);
                bool secondRegisterRejected = !fixture.Load.Register(attachment, 20f);
                Check(secondRegisterRejected && fixture.Load.TotalLoad == 20f,
                    "The same monster instance can never be registered twice.", passed, failed);

                fixture.Load.Unregister(attachment);
                bool releasedOnce = fixture.Load.TotalLoad == 0f;
                fixture.Load.Unregister(attachment);
                bool stillZero = fixture.Load.TotalLoad == 0f;

                Check(duplicateRejected && emptyClearSafe && staysZero,
                    "Unregistering an unknown monster and clearing an empty registry stay at zero load.", passed, failed);
                Check(releasedOnce && stillZero,
                    "A monster death removes its load once and never drives the total negative.", passed, failed);

                GameObject monster2 = new GameObject("Monster2");
                GameObject monster3 = new GameObject("Monster3");
                try
                {
                    MonsterAttachment a = monster2.AddComponent<MonsterAttachment>();
                    MonsterAttachment b = monster3.AddComponent<MonsterAttachment>();
                    fixture.Load.Register(a, 10f);
                    fixture.Load.Register(b, 20f);
                    Check(Mathf.Approximately(fixture.Load.TotalLoad, 30f) && fixture.Load.RegisteredMonsterCount == 2,
                        "Two monsters accumulate their loads.", passed, failed);
                    Check(Mathf.Approximately(fixture.Load.SpeedMultiplier, 0.5f),
                        "Accumulated load recalculates the speed multiplier.", passed, failed);

                    fixture.Load.ClearAll();
                    fixture.Load.Register(a, 400f);
                    Check(Mathf.Approximately(fixture.Load.TotalLoad, 400f) &&
                          Mathf.Approximately(fixture.Load.SpeedMultiplier, 0.3f),
                        "The speed multiplier never drops below the configured minimum.", passed, failed);

                    fixture.Load.ClearAll();
                    Check(fixture.Load.SpeedMultiplier >= 0.3f && fixture.Load.SpeedMultiplier <= 1f &&
                          Mathf.Approximately(fixture.Load.TotalLoad, 0f),
                        "Clearing every monster restores the full speed multiplier without negative load.", passed, failed);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(monster2);
                    UnityEngine.Object.DestroyImmediate(monster3);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterObject);
                fixture.Dispose();
            }
        }

        private static void VerifySpeedPenaltyAndWhip(ICollection<string> passed, ICollection<string> failed)
        {
            CartFixture fixture = CartFixture.Create();
            GameObject monsterObject = new GameObject("Monster");
            try
            {
                MonsterAttachment attachment = monsterObject.AddComponent<MonsterAttachment>();

                float maximumSpeed = fixture.Speed.MaximumSpeed;
                fixture.Load.Register(attachment, 60f);
                for (int step = 0; step < 240; step++)
                {
                    fixture.Speed.TickSpeed(1f / 60f);
                }

                float loadedSpeed = fixture.Movement.Speed;
                bool cappedByLoad = loadedSpeed > 0f && loadedSpeed <= maximumSpeed * fixture.Load.SpeedMultiplier + 0.001f;
                Check(cappedByLoad,
                    $"Heavy load caps the commanded speed ({loadedSpeed:0.00} <= {maximumSpeed * fixture.Load.SpeedMultiplier:0.00}).",
                    passed, failed);

                fixture.Load.Unregister(attachment);
                for (int step = 0; step < 240; step++)
                {
                    fixture.Speed.TickSpeed(1f / 60f);
                }

                float afterUnload = fixture.Movement.Speed;
                Check(afterUnload <= loadedSpeed + 0.001f,
                    "Removing monsters only relieves the penalty; it never awards a free speed boost.", passed, failed);

                fixture.Speed.RequestAcceleration();
                for (int step = 0; step < 240; step++)
                {
                    fixture.Speed.TickSpeed(1f / 60f);
                }

                float afterWhip = fixture.Movement.Speed;
                Check(afterWhip > afterUnload + 0.01f,
                    $"The whip request accelerates the cart ({afterUnload:0.00} -> {afterWhip:0.00}).", passed, failed);
                Check(afterWhip <= maximumSpeed * fixture.Load.SpeedMultiplier + 0.001f && afterWhip >= 0f,
                    "Acceleration still respects the maximum speed and the remaining load limit.", passed, failed);
                Check(fixture.Movement.Speed >= 0f, "Commanded speed is never negative.", passed, failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterObject);
                fixture.Dispose();
            }
        }

        private static void VerifyAttachedMonsterRepeatsAttack(
            ICollection<string> passed,
            ICollection<string> failed)
        {
            GameObject pointObject = new GameObject("RepeatAttackPoint");
            GameObject registryObject = new GameObject("RepeatAttackRegistry");
            GameObject monsterObject = new GameObject("RepeatAttacker");
            GameObject hunterObject = new GameObject("RepeatAttackHunter");
            try
            {
                MonsterAttachmentPoint point = pointObject.AddComponent<MonsterAttachmentPoint>();
                point.Configure(MonsterAttachmentKind.Ground, 1, Vector3.forward);
                CartAttachmentPoints registry = registryObject.AddComponent<CartAttachmentPoints>();
                registry.Configure(new[] { point });

                MonsterBase owner = monsterObject.AddComponent<MonsterBase>();
                MonsterAttachment attachment = monsterObject.AddComponent<MonsterAttachment>();
                attachment.Configure(MonsterAttachmentKind.Ground, 10f, 1f, 0f, Vector3.zero);
                attachment.Initialize(owner, registry, null);
                monsterObject.transform.position = pointObject.transform.position;
                attachment.TrySelectPoint();
                attachment.TickAttach(0f);

                PrototypeHunterMonsterTarget hunter = hunterObject.AddComponent<PrototypeHunterMonsterTarget>();
                hunterObject.transform.position = new Vector3(0f, 0f, 1f);
                hunter.Configure(hunterObject.transform, hunterObject.transform);

                AttachedMonsterAttack repeatedAttack = monsterObject.AddComponent<AttachedMonsterAttack>();
                repeatedAttack.Configure(5.5f, 0.28f, 1.35f, 0, 1.15f);
                repeatedAttack.Initialize(owner, attachment, hunter, null);
                int hunterHits = 0;
                hunter.SimulatedHit += _ => hunterHits++;

                repeatedAttack.TickAttack(0f);
                repeatedAttack.TickAttack(0.29f);
                repeatedAttack.TickAttack(1f);
                bool respectedCooldown = hunterHits == 1;
                repeatedAttack.TickAttack(1.65f);
                repeatedAttack.TickAttack(1.94f);

                Check(respectedCooldown && hunterHits == 2 &&
                      repeatedAttack.ResolvedAttackCount == 2 && repeatedAttack.SuccessfulAttackCount == 2,
                    "An attached monster repeatedly attacks after each cooldown instead of stopping after its first hit.",
                    passed, failed);
            }
            finally
            {
                MonsterAttachment attachment = monsterObject != null
                    ? monsterObject.GetComponent<MonsterAttachment>()
                    : null;
                attachment?.Detach();
                UnityEngine.Object.DestroyImmediate(pointObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
                UnityEngine.Object.DestroyImmediate(monsterObject);
                UnityEngine.Object.DestroyImmediate(hunterObject);
            }
        }

        private static void VerifyGroundMonsterCannotClimbCart(
            ICollection<string> passed,
            ICollection<string> failed)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            GameObject cartObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject zombie = new GameObject("GroundZombieClimbVerification");
            try
            {
                road.transform.localScale = new Vector3(4f, 1f, 4f);
                road.AddComponent<MonsterGroundSurface>();
                cartObstacle.transform.position = new Vector3(0f, 0.5f, 2f);
                cartObstacle.transform.localScale = new Vector3(3f, 1f, 5f);
                zombie.transform.position = new Vector3(0f, 0f, -1.5f);

                GroundMonsterMovement movement = zombie.AddComponent<GroundMonsterMovement>();
                movement.ConfigureGrounding(~0, ~0, 0.35f);
                movement.Initialize(zombie.transform.position);
                Physics.SyncTransforms();

                float highestY = zombie.transform.position.y;
                for (int step = 0; step < 180; step++)
                {
                    movement.TickMoveTowards(new Vector3(0f, 1f, 5f), 3f, 1f / 60f);
                    highestY = Mathf.Max(highestY, zombie.transform.position.y);
                    Physics.SyncTransforms();
                }

                Check(highestY <= 0.36f && Mathf.Approximately(movement.MaximumGroundStepHeight, 0.35f),
                    "A terrestrial monster remains on marked road and cannot step up onto cart-height geometry.",
                    passed, failed);
                Check(zombie.transform.position.z < cartObstacle.transform.position.z + 2.6f,
                    "The ground movement obstacle probe prevents the zombie from crossing through the cart collider.",
                    passed, failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(road);
                UnityEngine.Object.DestroyImmediate(cartObstacle);
                UnityEngine.Object.DestroyImmediate(zombie);
            }
        }

        // ---------------------------------------------------------------- giant pursuit

        private static void VerifyGiantClosesDistanceWithoutTeleporting(ICollection<string> passed, ICollection<string> failed)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            GameObject cart = new GameObject("Cart");
            GameObject rear = new GameObject("RearReachPoint");
            GameObject giantObject = new GameObject("Giant");
            try
            {
                ground.name = "VerificationGround";
                ground.transform.localScale = new Vector3(20f, 1f, 20f);
                ground.AddComponent<MonsterGroundSurface>();

                rear.transform.SetParent(cart.transform, false);
                rear.transform.localPosition = new Vector3(0f, 1f, -3.2f);
                giantObject.transform.position = new Vector3(0f, 0f, -20f);

                GiantZombieController giant = giantObject.AddComponent<GiantZombieController>();
                giant.Configure(cart.transform, rear.transform, null, 5.6f, 2.6f, ~0, ~0,
                    false, 1.15f);

                int caughtEvents = 0;
                giant.GiantCaughtCart += _ => caughtEvents++;
                giant.Initialize(cart.transform, rear.transform);
                Physics.SyncTransforms();

                bool chasing = giant.IsChasing;
                float initialDistance = giant.DistanceToRear;
                float previousDistance = initialDistance;
                bool teleported = false;
                const float step = 0.02f;
                int steps = 0;
                while (!giant.HasCaughtCart && steps < 4000)
                {
                    giant.TickChase(step);
                    Physics.SyncTransforms();
                    float distance = giant.DistanceToRear;
                    if (distance < previousDistance - 10f * step - 0.01f)
                    {
                        teleported = true;
                    }

                    previousDistance = distance;
                    steps++;
                }

                Check(chasing && !teleported,
                    "The giant pursues the cart rear continuously and never teleports to catch up.", passed, failed);
                Check(giant.HasCaughtCart && caughtEvents == 1 && giant.DistanceToRear <= 2.6f + 0.001f,
                    "The giant reports a single GiantCaughtCart event when it reaches the cart rear.", passed, failed);
                Check(initialDistance > 15f,
                    $"The giant starts far behind the cart rear ({initialDistance:0.0} m) and closes the gap by moving.", passed, failed);

                Vector3 positionAtCatch = giant.transform.position;
                cart.transform.position += Vector3.forward * 6f;
                Physics.SyncTransforms();
                giant.TickChase(0.1f);
                Check(giant.IsChasing && Vector3.Distance(positionAtCatch, giant.transform.position) > 0.01f,
                    "After reaching the rear, the giant resumes movement when the moving cart opens the gap.",
                    passed, failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ground);
                UnityEngine.Object.DestroyImmediate(cart);
                UnityEngine.Object.DestroyImmediate(rear);
                UnityEngine.Object.DestroyImmediate(giantObject);
            }
        }

        // ---------------------------------------------------------------- helpers

        private sealed class CartFixture : IDisposable
        {
            public GameObject Cart;
            public SimulatedCartMovement Movement;
            public SimulatedCartSurvivalController Speed;
            public CartMonsterLoad Load;

            public static CartFixture Create()
            {
                CartFixture fixture = new CartFixture();
                fixture.Cart = new GameObject("VerificationCart");
                fixture.Movement = fixture.Cart.AddComponent<SimulatedCartMovement>();
                fixture.Speed = fixture.Cart.AddComponent<SimulatedCartSurvivalController>();
                fixture.Speed.Configure(fixture.Movement, 5f, 6f, 1.25f, 2.5f);
                fixture.Load = fixture.Cart.AddComponent<CartMonsterLoad>();
                fixture.Load.Configure(60f, 0.3f, fixture.Speed);
                return fixture;
            }

            public void Dispose()
            {
                if (Cart != null)
                {
                    UnityEngine.Object.DestroyImmediate(Cart);
                }
            }
        }

        private static void Check(
            bool condition,
            string description,
            ICollection<string> passed,
            ICollection<string> failed)
        {
            (condition ? passed : failed).Add(description);
        }

        private static string GetLogDirectory()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
        }
    }
}
