using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JapaneseDemonHunter.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Monsters.Editor
{
    [InitializeOnLoad]
    public static class MonsterPhaseTwoVerification
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string BatModelPath = "Assets/Art/Monsters/Bat/Models/Bat.fbx";
        private const string ZombieModelPath = "Assets/Art/Monsters/Zombie/Models/zombo.fbx";
        private const string BatPrefabPath = "Assets/Art/Monsters/Bat/Prefabs/BatDemon.prefab";
        private const string ZombiePrefabPath = "Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab";
        private const string ReportPath = "Logs/Phase2DeterministicVerification.txt";

        static MonsterPhaseTwoVerification()
        {
            if (Application.isBatchMode &&
                Environment.GetCommandLineArgs().Any(argument => argument == "-phase2Verify"))
            {
                RunBootstrappedVerification();
            }
        }

        [MenuItem("Tools/Monsters/Run Phase 2 Deterministic Verification")]
        public static void Run()
        {
            List<string> passed = new List<string>();
            List<string> failed = new List<string>();
            VerifySceneAndSpawner(passed, failed);
            VerifyImportedAssets(passed, failed);
            VerifyTargetReselection(passed, failed);
            VerifyConfirmedAttackAndDeathCancellation(passed, failed);

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Phase 2 deterministic verification: {passed.Count} passed, {failed.Count} failed");
            foreach (string item in passed) report.AppendLine($"PASS: {item}");
            foreach (string item in failed) report.AppendLine($"FAIL: {item}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (failed.Count > 0)
            {
                throw new InvalidOperationException(report.ToString());
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        private static void RunBootstrappedVerification()
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

        private static void VerifySceneAndSpawner(ICollection<string> passed, ICollection<string> failed)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            MonsterTargetRegistry registry = UnityEngine.Object.FindAnyObjectByType<MonsterTargetRegistry>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();

            Check(references != null && references.IsConfigured,
                "Phase 1 cart, hunter, attack point, and four candle references remain intact.", passed, failed);
            Check(registry != null && registry.IsConfigured && registry.Targets.Count == 5,
                "The explicit target registry has one hunter and four candles.", passed, failed);
            Check(spawner != null && spawner.IsConfigured && spawner.SpawnEntries.Count >= 2,
                "The spawner keeps at least the Bat and Zombie entries.", passed, failed);
            Check(spawner != null && spawner.MaximumActiveMonsters == 8,
                "The spawner active-enemy limit is eight.", passed, failed);

            if (spawner == null)
            {
                return;
            }

            Physics.SyncTransforms();
            foreach (float angle in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                Check(spawner.TryValidatePositionAtAngle(MonsterMovementType.Flying, angle, 15f, out Vector3 flying) &&
                      flying.y > 2f,
                    $"Flying spawn is valid at {angle:0} degrees.", passed, failed);
                Check(spawner.TryValidatePositionAtAngle(MonsterMovementType.Ground, angle, 15f, out Vector3 ground) &&
                      Mathf.Abs(ground.y) < 0.2f,
                    $"Ground spawn is valid at {angle:0} degrees.", passed, failed);
            }
        }

        private static void VerifyImportedAssets(ICollection<string> passed, ICollection<string> failed)
        {
            VerifyPrefab(BatModelPath, BatPrefabPath, MonsterMovementType.Flying,
                new[] { "Bat_Flying", "Bat_Attack", "Bat_Death" }, passed, failed);
            VerifyPrefab(ZombieModelPath, ZombiePrefabPath, MonsterMovementType.Ground,
                new[] { "Walk", "Attack", "Die" }, passed, failed);

            Animator batAnimator = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath)
                ?.GetComponentInChildren<Animator>(true);
            Animator zombieAnimator = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath)
                ?.GetComponentInChildren<Animator>(true);
            Check(batAnimator != null && zombieAnimator != null &&
                  batAnimator.runtimeAnimatorController != zombieAnimator.runtimeAnimatorController,
                "Bat and Zombie use separate Animator Controllers.", passed, failed);
        }

        private static void VerifyPrefab(
            string modelPath,
            string prefabPath,
            MonsterMovementType movementType,
            IEnumerable<string> requiredClipFragments,
            ICollection<string> passed,
            ICollection<string> failed)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            MonsterBase monster = prefab != null ? prefab.GetComponent<MonsterBase>() : null;
            Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            AnimatorController controller = animator != null
                ? animator.runtimeAnimatorController as AnimatorController
                : null;
            string[] stateNames = controller != null
                ? controller.layers[0].stateMachine.states.Select(state => state.state.name).ToArray()
                : Array.Empty<string>();
            string[] clipNames = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>().Select(clip => clip.name).ToArray();

            Check(model != null && prefab != null && monster != null,
                $"{Path.GetFileName(prefabPath)} and its real FBX are loadable.", passed, failed);
            Check(prefab != null && prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0,
                $"{Path.GetFileName(prefabPath)} contains a real skinned mesh.", passed, failed);
            Check(monster != null && monster.MovementType == movementType,
                $"{Path.GetFileName(prefabPath)} uses {movementType} movement.", passed, failed);
            Check(controller != null && new[] { "Locomotion", "Attack", "Death" }.All(stateNames.Contains),
                $"{Path.GetFileName(prefabPath)} controller has locomotion, attack, and death states.", passed, failed);
            Check(requiredClipFragments.All(fragment => clipNames.Any(name =>
                    name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)),
                $"{Path.GetFileName(modelPath)} exposes all implemented real clips.", passed, failed);
        }

        private static void VerifyTargetReselection(ICollection<string> passed, ICollection<string> failed)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MonsterTargetRegistry registry = UnityEngine.Object.FindAnyObjectByType<MonsterTargetRegistry>();
            GameObject selectorObject = new GameObject("VerificationSelector");
            try
            {
                MonsterTargetSelector selector = selectorObject.AddComponent<MonsterTargetSelector>();
                selector.Strategy = MonsterTargetStrategy.ClosestLitCandle;
                selector.Initialize(registry);
                IMonsterTarget first = selector.SelectTarget(Vector3.zero, true);
                bool extinguished = first != null && first.TargetType == MonsterTargetType.Candle &&
                                    first.TryReceiveHit(null);
                IMonsterTarget second = selector.SelectTarget(Vector3.zero, true);
                Check(extinguished && second != null && !ReferenceEquals(first, second) && second.IsAvailable,
                    "An extinguished candle is rejected and another lit candle is selected.", passed, failed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(selectorObject);
            }
        }

        private static void VerifyConfirmedAttackAndDeathCancellation(
            ICollection<string> passed,
            ICollection<string> failed)
        {
            GameObject batObject = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath)) as GameObject;
            GameObject zombieObject = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath)) as GameObject;
            GameObject firstObject = CreateCandle("HitCandle", new Vector3(0f, 0f, 1.35f), out PrototypeCandle first);
            GameObject secondObject = CreateCandle("UntargetedCandle", new Vector3(4f, 0f, 1.35f), out PrototypeCandle second);
            GameObject registryObject = new GameObject("VerificationRegistry");

            try
            {
                PrototypeCandleMonsterTarget firstTarget = firstObject.AddComponent<PrototypeCandleMonsterTarget>();
                firstTarget.Configure(first);
                PrototypeCandleMonsterTarget secondTarget = secondObject.AddComponent<PrototypeCandleMonsterTarget>();
                secondTarget.Configure(second);
                MonsterTargetRegistry registry = registryObject.AddComponent<MonsterTargetRegistry>();
                registry.Configure(new MonoBehaviour[] { firstTarget, secondTarget });

                MonsterBase bat = batObject.GetComponent<MonsterBase>();
                bat.Initialize(new MonsterSpawnContext { targetRegistry = registry, patrolCenter = Vector3.zero });
                MonsterAttack batAttack = bat.GetComponent<MonsterAttack>();
                batAttack.Configure(batObject.transform.Find("AttackPoint"), 2.5f, 0f, 2.5f, ~0);
                Physics.SyncTransforms();
                bool began = batAttack.TryBeginAttack(firstTarget);
                MonsterAttackResult result = batAttack.TickAttack();
                Check(began && result == MonsterAttackResult.Hit && !first.IsLit && second.IsLit && !batAttack.IsReady,
                    "A physically confirmed attack extinguishes only its selected candle and starts cooldown.",
                    passed, failed);

                GameObject deathCandleObject = CreateCandle(
                    "DeathCancelCandle", new Vector3(0f, 0f, 1.2f), out PrototypeCandle deathCandle);
                PrototypeCandleMonsterTarget deathTarget = deathCandleObject.AddComponent<PrototypeCandleMonsterTarget>();
                deathTarget.Configure(deathCandle);
                registry.Configure(new MonoBehaviour[] { deathTarget });
                MonsterBase zombie = zombieObject.GetComponent<MonsterBase>();
                zombie.Initialize(new MonsterSpawnContext { targetRegistry = registry, patrolCenter = Vector3.zero });
                MonsterAttack zombieAttack = zombie.GetComponent<MonsterAttack>();
                zombieAttack.Configure(zombieObject.transform.Find("AttackPoint"), 2.5f, 10f, 0f, ~0);
                bool pending = zombieAttack.TryBeginAttack(deathTarget);
                zombie.Kill();
                Check(pending && zombie.IsDead && !zombieAttack.IsAttacking &&
                      zombieAttack.TickAttack() == MonsterAttackResult.Cancelled && deathCandle.IsLit,
                    "Kill() enters Dead and cancels a pending attack before impact.", passed, failed);
                UnityEngine.Object.DestroyImmediate(deathCandleObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(batObject);
                UnityEngine.Object.DestroyImmediate(zombieObject);
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
            }
        }

        private static GameObject CreateCandle(string name, Vector3 position, out PrototypeCandle candle)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.75f, 0f);
            collider.radius = 0.35f;
            GameObject flame = new GameObject("Flame");
            flame.transform.SetParent(root.transform, false);
            Light light = flame.AddComponent<Light>();
            GameObject point = new GameObject("AttackPoint");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            candle = root.AddComponent<PrototypeCandle>();
            candle.ConfigurePrototypeReferences(flame, light, point.transform);
            return root;
        }

        private static void Check(
            bool condition,
            string description,
            ICollection<string> passed,
            ICollection<string> failed)
        {
            (condition ? passed : failed).Add(description);
        }
    }
}
