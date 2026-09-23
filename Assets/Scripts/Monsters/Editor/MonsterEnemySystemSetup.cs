using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JapaneseDemonHunter.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Monsters.Editor
{
    [InitializeOnLoad]
    public static class MonsterEnemySystemSetup
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string BatSourcePath = "Assets/Art/Monsters/Bat by Quaternius - hNO9XvjlKa/Bat.fbx";
        private const string ZombieSourcePath = "Assets/Art/Monsters/Zombie by bachosoftdesign - xqEzosAVYX/zombo.fbx";
        private const string BatModelPath = "Assets/Art/Monsters/Bat/Models/Bat.fbx";
        private const string ZombieModelPath = "Assets/Art/Monsters/Zombie/Models/zombo.fbx";
        private const string BatControllerPath = "Assets/Art/Monsters/Bat/Animations/BatDemon.controller";
        private const string ZombieControllerPath = "Assets/Art/Monsters/Zombie/Animations/ZombieDemon.controller";
        private const string BatPrefabPath = "Assets/Art/Monsters/Bat/Prefabs/BatDemon.prefab";
        private const string ZombiePrefabPath = "Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab";

        static MonsterEnemySystemSetup()
        {
            if (Application.isBatchMode &&
                Environment.GetCommandLineArgs().Any(argument => argument == "-phase2Setup"))
            {
                try
                {
                    SetupEnemySystem();
                    EditorApplication.Exit(0);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorApplication.Exit(1);
                }
            }
        }

        [MenuItem("Tools/Monsters/Setup Enemy System")]
        public static void SetupEnemySystem()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException(
                    "The Phase 1 prototype scene is required. Run Tools > Monsters > Create Prototype Scene first.",
                    ScenePath);
            }

            EnsureArtLayout();
            string batPath = MoveModelIfNeeded(BatSourcePath, BatModelPath);
            string zombiePath = MoveModelIfNeeded(ZombieSourcePath, ZombieModelPath);
            ConfigureModelImporter(batPath, "Flying");
            ConfigureModelImporter(zombiePath, "Walk", "Idle");

            AnimationClip batLocomotion = RequireClip(batPath, "Bat_Flying");
            AnimationClip batAttack = RequireClip(batPath, "Bat_Attack");
            AnimationClip batDeath = RequireClip(batPath, "Bat_Death");
            AnimationClip zombieLocomotion = RequireClip(zombiePath, "Walk");
            AnimationClip zombieAttack = RequireClip(zombiePath, "Attack");
            AnimationClip zombieDeath = RequireClip(zombiePath, "Die");

            AnimatorController batController = GetOrCreateController(
                BatControllerPath, batLocomotion, batAttack, batDeath);
            AnimatorController zombieController = GetOrCreateController(
                ZombieControllerPath, zombieLocomotion, zombieAttack, zombieDeath);

            MonsterBase batPrefab = GetOrCreateMonsterPrefab(
                BatPrefabPath, "BatDemon", batPath, batController, MonsterMovementType.Flying,
                batAttack.length, batDeath.length);
            MonsterBase zombiePrefab = GetOrCreateMonsterPrefab(
                ZombiePrefabPath, "ZombieDemon", zombiePath, zombieController, MonsterMovementType.Ground,
                zombieAttack.length, zombieDeath.length);

            // Creating the second prefab may refresh the AssetDatabase and invalidate the first
            // native object handle. Reload both before serializing scene references.
            batPrefab = AssetDatabase.LoadAssetAtPath<MonsterBase>(BatPrefabPath);
            zombiePrefab = AssetDatabase.LoadAssetAtPath<MonsterBase>(ZombiePrefabPath);
            if (batPrefab == null || zombiePrefab == null)
            {
                throw new InvalidOperationException("Unity created the prefabs but could not reload their MonsterBase components.");
            }

            ConfigurePrototypeScene();
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ValidateOpenScene(out string report))
            {
                throw new InvalidOperationException($"Enemy setup validation failed:\n{report}");
            }

            Debug.Log($"Phase 2 enemy system configured successfully.\n{report}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Enemy system ready",
                    "BatDemon and ZombieDemon use the real FBX models. The spawner and five targets are configured in MonstersPrototype.",
                    "OK");
            }
        }

        public static void SetupEnemySystemFromCommandLine()
        {
            SetupEnemySystem();
        }

        [MenuItem("Tools/Monsters/Validate Enemy System")]
        public static void ValidateEnemySystemMenu()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool valid = ValidateOpenScene(out string report);
            Debug.Log($"Phase 2 enemy validation {(valid ? "passed" : "failed")}:\n{report}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(valid ? "Validation passed" : "Validation failed", report, "OK");
            }
        }

        public static void ValidateEnemySystemFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!ValidateOpenScene(out string report))
            {
                throw new InvalidOperationException(report);
            }

            Debug.Log(report);
        }

        private static void EnsureArtLayout()
        {
            EnsureFolder("Assets/Art", "Monsters");
            EnsureMonsterFolders("Bat");
            EnsureMonsterFolders("Zombie");
        }

        private static void EnsureMonsterFolders(string monsterName)
        {
            string root = $"Assets/Art/Monsters/{monsterName}";
            EnsureFolder("Assets/Art/Monsters", monsterName);
            EnsureFolder(root, "Models");
            EnsureFolder(root, "Materials");
            EnsureFolder(root, "Animations");
            EnsureFolder(root, "Prefabs");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string MoveModelIfNeeded(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            {
                return destinationPath;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
            {
                throw new FileNotFoundException($"Required FBX was not found at {sourcePath} or {destinationPath}.");
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Could not organize {sourcePath}: {error}");
            }

            return destinationPath;
        }

        private static void ConfigureModelImporter(string modelPath, params string[] loopingClipFragments)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"No ModelImporter is available for {modelPath}.");
            }

            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                           importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar ||
                           !importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool shouldLoop = loopingClipFragments.Any(fragment =>
                    clip.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
                if (clip.loopTime != shouldLoop)
                {
                    clip.loopTime = shouldLoop;
                    changed = true;
                }
            }

            importer.clipAnimations = clips;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip RequireClip(string modelPath, string clipNameFragment)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    candidate.name.IndexOf(clipNameFragment, StringComparison.OrdinalIgnoreCase) >= 0);
            if (clip == null)
            {
                throw new InvalidOperationException($"Animation containing '{clipNameFragment}' was not found in {modelPath}.");
            }

            return clip;
        }

        private static AnimatorController GetOrCreateController(
            string controllerPath,
            AnimationClip locomotion,
            AnimationClip attack,
            AnimationClip death)
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = stateMachine.AddState("Locomotion");
            locomotionState.motion = locomotion;
            stateMachine.defaultState = locomotionState;
            AnimatorState attackState = stateMachine.AddState("Attack");
            attackState.motion = attack;
            AnimatorState deathState = stateMachine.AddState("Death");
            deathState.motion = death;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static MonsterBase GetOrCreateMonsterPrefab(
            string prefabPath,
            string monsterName,
            string modelPath,
            RuntimeAnimatorController animatorController,
            MonsterMovementType movementType,
            float attackClipLength,
            float deathClipLength)
        {
            MonsterBase existing = AssetDatabase.LoadAssetAtPath<MonsterBase>(prefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                throw new FileNotFoundException("The model asset could not be loaded.", modelPath);
            }

            GameObject root = new GameObject(monsterName);
            try
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (modelInstance == null)
                {
                    throw new InvalidOperationException($"Unity could not instantiate {modelPath}.");
                }

                modelInstance.name = movementType == MonsterMovementType.Flying ? "Bat_Model" : "Zombie_Model";
                modelInstance.transform.SetParent(visual.transform, false);
                if (movementType == MonsterMovementType.Flying)
                {
                    modelInstance.transform.localScale = Vector3.one * 0.38f;
                    modelInstance.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                }
                else
                {
                    modelInstance.transform.localScale = Vector3.one * 0.55f;
                    modelInstance.transform.localPosition = new Vector3(0f, 0.34f, 0f);
                }

                Animator animator = modelInstance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = modelInstance.AddComponent<Animator>();
                }
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;

                GameObject attackPointObject = new GameObject("AttackPoint");
                attackPointObject.transform.SetParent(root.transform, false);
                attackPointObject.transform.localPosition = movementType == MonsterMovementType.Flying
                    ? new Vector3(0f, 0.65f, 0.9f)
                    : new Vector3(0f, 1.15f, 0.75f);

                GameObject detectionPoint = new GameObject("DetectionPoint");
                detectionPoint.transform.SetParent(root.transform, false);
                detectionPoint.transform.localPosition = movementType == MonsterMovementType.Flying
                    ? new Vector3(0f, 0.7f, 0f)
                    : new Vector3(0f, 1.25f, 0f);

                CapsuleCollider bodyCollider = root.AddComponent<CapsuleCollider>();
                if (movementType == MonsterMovementType.Flying)
                {
                    bodyCollider.center = new Vector3(0f, 0.7f, 0f);
                    bodyCollider.height = 1.6f;
                    bodyCollider.radius = 0.7f;
                    bodyCollider.direction = 2;
                }
                else
                {
                    bodyCollider.center = new Vector3(0f, 1.35f, 0f);
                    bodyCollider.height = 2.7f;
                    bodyCollider.radius = 0.55f;
                    bodyCollider.direction = 1;
                }

                MonsterMovement movement;
                if (movementType == MonsterMovementType.Flying)
                {
                    FlyingMonsterMovement flying = root.AddComponent<FlyingMonsterMovement>();
                    flying.ConfigureSpeeds(3f, 7.2f, 6.8f);
                    flying.ConfigureFlight(1.7f, 7.5f, ~0);
                    movement = flying;
                }
                else
                {
                    GroundMonsterMovement ground = root.AddComponent<GroundMonsterMovement>();
                    ground.ConfigureSpeeds(1.8f, 6.8f, 6.4f);
                    ground.ConfigureGrounding(~0, ~0);
                    movement = ground;
                }

                MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>();
                selector.Strategy = movementType == MonsterMovementType.Flying
                    ? MonsterTargetStrategy.ClosestLitCandle
                    : MonsterTargetStrategy.RandomLitCandle;

                MonsterAttack attack = root.AddComponent<MonsterAttack>();
                float attackRange = movementType == MonsterMovementType.Flying ? 2.1f : 1.75f;
                float impactDelay = Mathf.Clamp(attackClipLength * 0.45f, 0.25f, 1.1f);
                attack.Configure(attackPointObject.transform, attackRange, impactDelay, 2.25f, ~0);

                MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
                animation.Configure(animator, "Locomotion", "Attack", "Death");

                MonsterBase monster = root.AddComponent<MonsterBase>();
                monster.Configure(
                    movement,
                    selector,
                    attack,
                    animation,
                    bodyCollider,
                    movementType == MonsterMovementType.Flying ? 55f : 45f,
                    attackRange,
                    movementType == MonsterMovementType.Flying ? 85f : 70f,
                    deathClipLength + 0.2f);

                GameObject savedRoot = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                MonsterBase savedPrefab = savedRoot != null ? savedRoot.GetComponent<MonsterBase>() : null;
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException($"Prefab could not be saved at {prefabPath}.");
                }

                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePrototypeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath);
            GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            if (batPrefab == null || zombiePrefab == null)
            {
                throw new InvalidOperationException("Monster prefab assets could not be reloaded after opening the scene.");
            }

            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            if (references == null || !references.IsConfigured)
            {
                throw new InvalidOperationException("The Phase 1 PrototypeSceneReferences component is missing or incomplete.");
            }

            GameObject road = GameObject.Find("PrototypeRoad");
            if (road != null)
            {
                Vector3 scale = road.transform.localScale;
                road.transform.localScale = new Vector3(Mathf.Max(scale.x, 6f), scale.y, scale.z);
                GetOrAdd<MonsterGroundSurface>(road);
            }

            PrototypeHunterMonsterTarget hunterTarget =
                GetOrAdd<PrototypeHunterMonsterTarget>(references.HunterTransform.gameObject);
            hunterTarget.Configure(references.HunterTransform, references.HunterAttackPoint);

            List<MonoBehaviour> targetComponents = new List<MonoBehaviour> { hunterTarget };
            foreach (PrototypeCandle candle in references.Candles)
            {
                if (candle == null)
                {
                    throw new InvalidOperationException("A Phase 1 candle reference is null.");
                }

                PrototypeCandleMonsterTarget candleTarget = GetOrAdd<PrototypeCandleMonsterTarget>(candle.gameObject);
                candleTarget.Configure(candle);
                targetComponents.Add(candleTarget);
            }

            GameObject systemObject = GameObject.Find("MonsterSystem");
            if (systemObject == null)
            {
                systemObject = new GameObject("MonsterSystem");
            }
            MonsterTargetRegistry registry = GetOrAdd<MonsterTargetRegistry>(systemObject);
            registry.Configure(targetComponents);

            MonsterSpawner spawner = GetOrAdd<MonsterSpawner>(systemObject);
            spawner.Configure(
                references.CartTransform,
                references.HunterTransform,
                registry,
                new[]
                {
                    new MonsterSpawnEntry
                    {
                        prefab = batPrefab,
                        movementType = MonsterMovementType.Flying,
                        weight = 1f
                    },
                    new MonsterSpawnEntry
                    {
                        prefab = zombiePrefab,
                        movementType = MonsterMovementType.Ground,
                        weight = 1f
                    }
                },
                ~0,
                ~0);

            EditorUtility.SetDirty(systemObject);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Unity could not save {ScenePath}.");
            }

            Selection.activeGameObject = systemObject;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void EnsureSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static bool ValidateOpenScene(out string report)
        {
            List<string> failures = new List<string>();
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            MonsterTargetRegistry registry = UnityEngine.Object.FindAnyObjectByType<MonsterTargetRegistry>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();

            Require(references != null && references.IsConfigured, "Phase 1 references remain configured.", failures);
            Require(registry != null && registry.IsConfigured && registry.Targets.Count == 5,
                "Target registry contains hunter plus four candles.", failures);
            Require(spawner != null, "MonsterSpawner exists.", failures);
            if (spawner != null)
            {
                Require(spawner.SpawnEntries.Count >= 2,
                    "Spawner contains at least the two Phase 2 entries (later phases may add more).", failures);
                Require(spawner.IsConfigured, "Spawner references, prefab weights, and target registry are valid.", failures);
            }
            ValidatePrefab(BatPrefabPath, MonsterMovementType.Flying, failures);
            ValidatePrefab(ZombiePrefabPath, MonsterMovementType.Ground, failures);
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(BatModelPath) != null,
                "Organized Bat.fbx exists.", failures);
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ZombieModelPath) != null,
                "Organized zombo.fbx exists.", failures);

            report = failures.Count == 0
                ? "Static configuration checks passed: two real-model prefabs, five explicit targets, and one bounded spawner are assigned."
                : string.Join("\n", failures.Select(failure => $"FAILED: {failure}"));
            return failures.Count == 0;
        }

        private static void ValidatePrefab(
            string prefabPath,
            MonsterMovementType expectedMovementType,
            ICollection<string> failures)
        {
            MonsterBase monster = AssetDatabase.LoadAssetAtPath<MonsterBase>(prefabPath);
            Require(monster != null, $"{prefabPath} exists.", failures);
            if (monster == null)
            {
                return;
            }

            Require(monster.MovementType == expectedMovementType,
                $"{monster.name} has the expected movement type.", failures);
            Require(monster.GetComponent<MonsterAttack>() != null &&
                    monster.GetComponent<MonsterTargetSelector>() != null &&
                    monster.GetComponent<MonsterAnimationController>() != null,
                $"{monster.name} has reusable AI, targeting, attack, and animation components.", failures);
            Require(monster.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0,
                $"{monster.name} contains a real skinned model.", failures);
            Animator animator = monster.GetComponentInChildren<Animator>(true);
            Require(animator != null && animator.runtimeAnimatorController != null,
                $"{monster.name} has its own configured Animator Controller.", failures);
        }

        private static void Require(bool condition, string description, ICollection<string> failures)
        {
            if (!condition)
            {
                failures.Add(description);
            }
        }
    }
}
