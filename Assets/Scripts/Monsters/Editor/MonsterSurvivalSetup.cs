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
    /// <summary>
    /// Phase 3 integration: builds the six monster prefabs (five cart-clinging enemies plus the
    /// giant pursuer) and wires the survival systems into the prototype scene without replacing
    /// the Phase 1 movement, VR, horse or map work owned by other teammates.
    /// </summary>
    [InitializeOnLoad]
    public static class MonsterSurvivalSetup
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string ReportPath = "Logs/Phase3SurvivalSetup.txt";
        private const string SwordMaterialPath = "Assets/Prototype/Materials/Sword_Steel.mat";

        private const string CartRootName = "SimulatedCart";
        private const string CartBodyName = "CartBody";
        private const string HunterName = "SimulatedHunter";
        private const string SystemName = "MonsterSystem";
        private const string RoadName = "PrototypeRoad";
        private const string AttachmentRootName = "CartAttachmentPoints";
        private const string RearReachPointName = "CartRearReachPoint";

        private static readonly MonsterDefinition[] Definitions =
        {
            new MonsterDefinition
            {
                DisplayName = "BatDemon",
                FolderName = "Bat",
                SourcePath = "Assets/Art/Monsters/Bat by Quaternius - hNO9XvjlKa/Bat.fbx",
                MovementType = MonsterMovementType.Flying,
                AttachmentKind = MonsterAttachmentKind.Flying,
                Load = 5f,
                ModelScale = 0.38f,
                ModelLocalPosition = new Vector3(0f, -0.1f, 0f),
                PatrolSpeed = 3f,
                ChaseSpeed = 7.2f,
                ApproachSpeed = 6.8f,
                DetectionRadius = 55f,
                AttackRange = 2.1f,
                MaximumChaseDistance = 85f,
                AttackCooldown = 2.25f,
                Weight = 1f,
                MinimumRadius = 13f,
                MaximumRadius = 24f,
                MinimumFlyingHeight = 3.5f,
                MaximumFlyingHeight = 7f,
                LocomotionFragments = new[] { "Flying" },
                AttackFragments = new[] { "Attack" },
                DeathFragments = new[] { "Death" }
            },
            new MonsterDefinition
            {
                DisplayName = "DemonMonster",
                FolderName = "Demon",
                SourcePath = "Assets/Art/Monsters/Demon by Quaternius - Mo2ky6vkf8/Demon.fbx",
                MovementType = MonsterMovementType.Ground,
                AttachmentKind = MonsterAttachmentKind.Ground,
                Load = 12f,
                ModelScale = 0.55f,
                ModelLocalPosition = new Vector3(0f, 0.34f, 0f),
                PatrolSpeed = 2f,
                ChaseSpeed = 6.9f,
                ApproachSpeed = 6.5f,
                DetectionRadius = 50f,
                AttackRange = 1.9f,
                MaximumChaseDistance = 75f,
                AttackCooldown = 2.4f,
                Weight = 1f,
                LocomotionFragments = new[] { "Walk", "Run", "Idle" },
                AttackFragments = new[] { "Attack", "Punch", "Claw" },
                DeathFragments = new[] { "Death", "Die" }
            },
            new MonsterDefinition
            {
                DisplayName = "SpiderMonster",
                FolderName = "Spider",
                SourcePath = "Assets/Art/Monsters/Spider by Quaternius - yRYJiAJyiM/Spider.fbx",
                MovementType = MonsterMovementType.Ground,
                AttachmentKind = MonsterAttachmentKind.Ground,
                Load = 8f,
                ModelScale = 0.6f,
                ModelLocalPosition = new Vector3(0f, 0.18f, 0f),
                PatrolSpeed = 2.3f,
                ChaseSpeed = 7.1f,
                ApproachSpeed = 6.7f,
                DetectionRadius = 48f,
                AttackRange = 1.7f,
                MaximumChaseDistance = 75f,
                AttackCooldown = 2f,
                Weight = 1f,
                LocomotionFragments = new[] { "Walk", "Run", "Idle" },
                AttackFragments = new[] { "Attack", "Bite" },
                DeathFragments = new[] { "Death", "Die" }
            },
            new MonsterDefinition
            {
                DisplayName = "GhostMonster",
                FolderName = "Ghost",
                SourcePath = "Assets/Art/Monsters/Ghost by Quaternius - Iip30bDHmu/Ghost.fbx",
                MovementType = MonsterMovementType.Flying,
                AttachmentKind = MonsterAttachmentKind.Flying,
                Load = 5f,
                ModelScale = 0.5f,
                ModelLocalPosition = new Vector3(0f, -0.05f, 0f),
                PatrolSpeed = 2.6f,
                ChaseSpeed = 7f,
                ApproachSpeed = 6.6f,
                DetectionRadius = 52f,
                AttackRange = 2f,
                MaximumChaseDistance = 80f,
                AttackCooldown = 2.6f,
                Weight = 1f,
                MinimumRadius = 14f,
                MaximumRadius = 26f,
                MinimumFlyingHeight = 3f,
                MaximumFlyingHeight = 7.5f,
                LocomotionFragments = new[] { "Float", "Fly", "Flying", "Idle" },
                AttackFragments = new[] { "Attack", "Scream", "Spell" },
                DeathFragments = new[] { "Death", "Die" }
            },
            new MonsterDefinition
            {
                DisplayName = "ZombieDemon",
                FolderName = "Zombie",
                SourcePath = "Assets/Art/Monsters/Zombie by bachosoftdesign - xqEzosAVYX/zombo.fbx",
                MovementType = MonsterMovementType.Ground,
                AttachmentKind = MonsterAttachmentKind.Ground,
                Load = 15f,
                ModelScale = 0.55f,
                ModelLocalPosition = new Vector3(0f, 0.34f, 0f),
                PatrolSpeed = 1.8f,
                ChaseSpeed = 6.8f,
                ApproachSpeed = 6.4f,
                DetectionRadius = 45f,
                AttackRange = 1.75f,
                MaximumChaseDistance = 70f,
                AttackCooldown = 2.25f,
                Weight = 1f,
                LocomotionFragments = new[] { "Walk" },
                AttackFragments = new[] { "Attack" },
                DeathFragments = new[] { "Die" }
            }
        };

        private static readonly GiantDefinition Giant = new GiantDefinition
        {
            DisplayName = "GiantZombie",
            FolderName = "GiantZombie",
            SourcePath = "Assets/Art/Monsters/Zombie by Quaternius - VlXjG0N8Eg/Zombie_Basic.fbx",
            ModelScale = 1.15f,
            ModelLocalPosition = new Vector3(0f, 0.6f, 0f),
            TargetHeight = 4.5f,
            ChaseSpeed = 5.6f,
            CatchRange = 2.6f,
            InitialDistance = 12.5f,
            LocomotionFragments = new[] { "Walk", "Run", "Idle" },
            AttackFragments = new[] { "Attack" },
            DeathFragments = new[] { "Death", "Die" }
        };

        private static readonly StringBuilder Log = new StringBuilder();
        private static readonly List<string> Warnings = new List<string>();

        static MonsterSurvivalSetup()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Any(argument => argument == "-survivalSetup"))
            {
                MonsterBatchGate.RunWhenEditorIsIdle(RunBatchSetup);
            }
        }

        /// <summary>Batch entry point: inspect, build, wire and validate in a single editor launch.</summary>
        public static void RunBatchSetupFromCommandLine()
        {
            RunBatchSetup();
        }

        private static void RunBatchSetup()
        {
            try
            {
                SetupSurvivalSystems();
                bool valid = ValidateOpenScene(out string report);
                Debug.Log($"{Log}\n{report}");
                EditorApplication.Exit(valid ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError($"{Log}\nSETUP FAILED: {exception.Message}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Monsters/Setup Survival Systems")]
        public static void SetupSurvivalSystems()
        {
            Log.Clear();
            Warnings.Clear();

            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException(
                    "The Phase 1 prototype scene is required. Run Tools > Monsters > Create Prototype Scene first.",
                    ScenePath);
            }

            EnsureArtLayout();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Phase A: organize and import every model first. Reading clips or building prefabs from
            // an asset that was moved in this same session returns a stale, empty import.
            Dictionary<string, string> modelPaths = new Dictionary<string, string>();
            foreach (MonsterDefinition definition in Definitions)
            {
                string organizedModel = ResolveModel(definition.SourcePath, definition.FolderName, definition.Flyable);
                modelPaths[definition.FolderName] = organizedModel;
                ConfigureModelImporter(organizedModel, definition.LocomotionFragments);
            }

            string giantModelPath = ResolveModel(Giant.SourcePath, Giant.FolderName, false);
            ConfigureModelImporter(giantModelPath, Giant.LocomotionFragments);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Phase B: build controllers and prefabs from the imported models.
            List<(MonsterDefinition Definition, GameObject Prefab, string Resolution)> smallPrefabs =
                new List<(MonsterDefinition, GameObject, string)>();
            foreach (MonsterDefinition definition in Definitions)
            {
                string modelPath = modelPaths[definition.FolderName];
                try
                {
                    string resolution = ExtractClips(modelPath, definition, out MonsterClips clips);
                    AnimatorController controller = GetOrCreateController(definition.FolderName, clips);
                    GameObject prefab = GetOrCreateSmallMonsterPrefab(definition, modelPath, controller, clips);
                    smallPrefabs.Add((definition, prefab, resolution));
                }
                catch (Exception exception)
                {
                    Warnings.Add(
                        $"{definition.DisplayName} could not be built ({exception.Message}). " +
                        "The remaining monster types continue without it.");
                    smallPrefabs.Add((definition, null, "build failed"));
                }
            }

            GameObject giantPrefab;
            string giantResolution;
            try
            {
                giantResolution = ExtractClips(giantModelPath, Giant, out MonsterClips giantClips);
                AnimatorController giantController = GetOrCreateController(Giant.FolderName, giantClips);
                giantPrefab = GetOrCreateGiantPrefab(giantModelPath, giantController);
            }
            catch (Exception exception)
            {
                Warnings.Add($"The giant pursuer could not be built ({exception.Message}).");
                giantPrefab = null;
                giantResolution = "build failed";
            }

            ConfigureSurvivalScene(smallPrefabs, giantPrefab);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.AppendLine("Small monster spawn/attachment configuration:");
            foreach ((MonsterDefinition definition, GameObject prefab, string resolution) in smallPrefabs)
            {
                Log.AppendLine(
                    $"  - {definition.DisplayName}: prefab={AssetDatabase.GetAssetPath(prefab)} | " +
                    $"load={definition.Load:0.#} | kind={definition.AttachmentKind} | {resolution}");
            }

            Log.AppendLine($"  - Giant: prefab={AssetDatabase.GetAssetPath(giantPrefab)} | speed={Giant.ChaseSpeed:0.#} | {giantResolution}");
            foreach (string warning in Warnings)
            {
                Log.AppendLine($"WARNING: {warning}");
            }

            string written = Log.ToString();
            Directory.CreateDirectory(Path.Combine(GetProjectRoot(), "Logs"));
            File.WriteAllText(Path.Combine(GetProjectRoot(), ReportPath), written);
            Debug.Log(written);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Survival systems ready",
                    "Five cart-clinging monsters, the giant pursuer, the monster-load system, the " +
                    "prototype sword and the whip request are configured in MonstersPrototype.",
                    "OK");
            }
        }

        [MenuItem("Tools/Monsters/Validate Survival Systems")]
        public static void ValidateSurvivalSystems()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool valid = ValidateOpenScene(out string report);
            Debug.Log($"{report}\n{Log}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(valid ? "Survival validation passed" : "Survival validation failed", report, "OK");
            }
        }

        // ---------------------------------------------------------------- art layout

        private static void EnsureArtLayout()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Monsters");
            EnsureFolder("Assets", "Prototype");
            EnsureFolder("Assets/Prototype", "Materials");
            foreach (MonsterDefinition definition in Definitions)
            {
                EnsureMonsterFolders(definition.FolderName);
            }

            EnsureMonsterFolders(Giant.FolderName);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureMonsterFolders(string monsterName)
        {
            string root = $"Assets/Art/Monsters/{monsterName}";
            EnsureFolder("Assets/Art/Monsters", monsterName);
            EnsureFolder(root, "Models");
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

        private static string OrganizedModelPath(string folderName)
        {
            return $"Assets/Art/Monsters/{folderName}/Models";
        }

        /// <summary>
        /// Returns the organized model path, moving the downloaded FBX into place when required.
        /// Falls back to an empty string when only a temporary placeholder can be used.
        /// </summary>
        private static string ResolveModel(string sourcePath, string folderName, bool flyable)
        {
            string targetFolder = OrganizedModelPath(folderName);
            string fileName = Path.GetFileName(sourcePath);
            string destination = $"{targetFolder}/{fileName}";

            if (LoadModelAsset(destination) != null)
            {
                return destination;
            }

            GameObject source = LoadModelAsset(sourcePath);
            if (source != null)
            {
                string error = AssetDatabase.MoveAsset(sourcePath, destination);
                if (string.IsNullOrEmpty(error))
                {
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    return LoadModelAsset(destination) != null ? destination : string.Empty;
                }

                Warnings.Add($"Could not organize {sourcePath}: {error}");
            }

            Warnings.Add(
                $"Model missing for '{folderName}' (looked at {sourcePath} and {destination}). " +
                "A clearly named temporary placeholder is used so the remaining monsters keep working.");
            return string.Empty;
        }

        /// <summary>
        /// Loads a model FBX, forcing a reimport when the file is on disk but the AssetDatabase has
        /// not produced its imported asset yet.
        /// </summary>
        private static GameObject LoadModelAsset(string assetPath)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset != null || !File.Exists(assetPath))
            {
                return asset;
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private static void ConfigureModelImporter(string modelPath, IEnumerable<string> loopingFragments)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                return;
            }

            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer))
            {
                return;
            }

            string[] fragments = loopingFragments.ToArray();
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
                bool shouldLoop = fragments.Any(fragment =>
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

        // ---------------------------------------------------------------- clip extraction

        private sealed class MonsterClips
        {
            public AnimationClip Locomotion;
            public AnimationClip Attack;
            public AnimationClip Death;
        }

        private static string ExtractClips(string modelPath, MonsterDefinition definition, out MonsterClips clips)
        {
            clips = new MonsterClips();
            if (string.IsNullOrEmpty(modelPath))
            {
                return "no model (placeholder visuals)";
            }

            List<AnimationClip> available = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => clip.name, StringComparer.Ordinal)
                .ToList();

            HashSet<AnimationClip> used = new HashSet<AnimationClip>();
            clips.Locomotion = PickClip(available, definition.LocomotionFragments, used);
            clips.Attack = PickClip(available, definition.AttackFragments, used);
            clips.Death = PickClip(available, definition.DeathFragments, used);

            return $"clips: locomotion={Describe(clips.Locomotion)} attack={Describe(clips.Attack)} " +
                   $"death={Describe(clips.Death)} (of {available.Count} available: " +
                   $"{string.Join(", ", available.Select(clip => clip.name))})";
        }

        private static string ExtractClips(string modelPath, GiantDefinition definition, out MonsterClips clips)
        {
            clips = new MonsterClips();
            if (string.IsNullOrEmpty(modelPath))
            {
                return "no model (placeholder visuals)";
            }

            List<AnimationClip> available = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => clip.name, StringComparer.Ordinal)
                .ToList();

            HashSet<AnimationClip> used = new HashSet<AnimationClip>();
            clips.Locomotion = PickClip(available, definition.LocomotionFragments, used);
            clips.Attack = PickClip(available, definition.AttackFragments, used);
            clips.Death = PickClip(available, definition.DeathFragments, used);

            return $"clips: locomotion={Describe(clips.Locomotion)} attack={Describe(clips.Attack)} " +
                   $"death={Describe(clips.Death)} (of {available.Count} available: " +
                   $"{string.Join(", ", available.Select(clip => clip.name))})";
        }

        private static AnimationClip PickClip(
            IEnumerable<AnimationClip> available,
            IEnumerable<string> fragments,
            ISet<AnimationClip> used)
        {
            List<AnimationClip> candidates = available.Where(clip => !used.Contains(clip)).ToList();
            foreach (string fragment in fragments)
            {
                AnimationClip match = candidates.FirstOrDefault(clip =>
                    clip.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    used.Add(match);
                    return match;
                }
            }

            return null;
        }

        private static string Describe(AnimationClip clip)
        {
            return clip != null ? clip.name : "<none>";
        }

        private static AnimatorController GetOrCreateController(string folderName, MonsterClips clips)
        {
            string path = $"Assets/Art/Monsters/{folderName}/Animations/{folderName}.controller";
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                RepairController(existing, clips);
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = clips.Locomotion;
            stateMachine.defaultState = locomotion;
            stateMachine.AddState("Attack").motion = clips.Attack;
            stateMachine.AddState("Death").motion = clips.Death;
            EditorUtility.SetDirty(controller);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? controller;
        }

        /// <summary>
        /// A controller created while a model was still missing keeps empty states. Assign the real
        /// clips as soon as they become available instead of leaving the monster unanimated.
        /// </summary>
        private static void RepairController(AnimatorController controller, MonsterClips clips)
        {
            bool repaired = false;
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state == null || child.state.motion != null)
                {
                    continue;
                }

                if (child.state.name == "Locomotion" && clips.Locomotion != null)
                {
                    child.state.motion = clips.Locomotion;
                    repaired = true;
                }
                else if (child.state.name == "Attack" && clips.Attack != null)
                {
                    child.state.motion = clips.Attack;
                    repaired = true;
                }
                else if (child.state.name == "Death" && clips.Death != null)
                {
                    child.state.motion = clips.Death;
                    repaired = true;
                }
            }

            if (repaired)
            {
                EditorUtility.SetDirty(controller);
            }
        }

        // ---------------------------------------------------------------- prefab building

        private static GameObject GetOrCreateSmallMonsterPrefab(
            MonsterDefinition definition,
            string modelPath,
            AnimatorController controller,
            MonsterClips clips)
        {
            string prefabPath = $"Assets/Art/Monsters/{definition.FolderName}/Prefabs/{definition.DisplayName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null && !string.IsNullOrEmpty(modelPath) &&
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
            {
                // The prefab was built earlier while its model was still missing. Rebuild it with
                // the real imported model instead of leaving placeholder geometry in the scene.
                AssetDatabase.DeleteAsset(prefabPath);
                prefab = null;
            }

            if (prefab == null)
            {
                prefab = CreateSmallMonsterPrefab(definition, prefabPath, modelPath, controller, clips);
            }

            if (prefab == null)
            {
                Warnings.Add($"{definition.DisplayName} prefab could not be created at {prefabPath}.");
                return null;
            }

            ExtendSmallMonsterPrefab(prefabPath, definition);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject CreateSmallMonsterPrefab(
            MonsterDefinition definition,
            string prefabPath,
            string modelPath,
            AnimatorController controller,
            MonsterClips clips)
        {
            bool flying = definition.MovementType == MonsterMovementType.Flying;
            GameObject root = new GameObject(definition.DisplayName);
            try
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                Animator animator = BuildModelVisual(modelPath, visual.transform, definition.ModelScale,
                    definition.ModelLocalPosition, controller, flying ? "Bat_Model" : "Monster_Model");

                GameObject attackPoint = new GameObject("AttackPoint");
                attackPoint.transform.SetParent(root.transform, false);
                attackPoint.transform.localPosition = flying
                    ? new Vector3(0f, 0.65f, 0.9f)
                    : new Vector3(0f, 1.15f, 0.75f);

                GameObject detectionPoint = new GameObject("DetectionPoint");
                detectionPoint.transform.SetParent(root.transform, false);
                detectionPoint.transform.localPosition = flying ? new Vector3(0f, 0.7f, 0f) : new Vector3(0f, 1.25f, 0f);

                CapsuleCollider bodyCollider = root.AddComponent<CapsuleCollider>();
                if (flying)
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
                if (flying)
                {
                    FlyingMonsterMovement flight = root.AddComponent<FlyingMonsterMovement>();
                    flight.ConfigureSpeeds(definition.PatrolSpeed, definition.ChaseSpeed, definition.ApproachSpeed);
                    flight.ConfigureFlight(1.7f, 7.5f, ~0);
                    movement = flight;
                }
                else
                {
                    GroundMonsterMovement ground = root.AddComponent<GroundMonsterMovement>();
                    ground.ConfigureSpeeds(definition.PatrolSpeed, definition.ChaseSpeed, definition.ApproachSpeed);
                    ground.ConfigureGrounding(~0, ~0);
                    movement = ground;
                }

                MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>();
                selector.Strategy = flying ? MonsterTargetStrategy.ClosestLitCandle : MonsterTargetStrategy.RandomLitCandle;

                MonsterAttack attack = root.AddComponent<MonsterAttack>();
                float impactDelay = clips.Attack != null
                    ? Mathf.Clamp(clips.Attack.length * 0.45f, 0.25f, 1.1f)
                    : 0.45f;
                attack.Configure(attackPoint.transform, definition.AttackRange, impactDelay, definition.AttackCooldown, ~0);

                MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
                animation.Configure(animator, "Locomotion", "Attack", "Death");

                MonsterBase monster = root.AddComponent<MonsterBase>();
                monster.Configure(
                    movement,
                    selector,
                    attack,
                    animation,
                    bodyCollider,
                    definition.DetectionRadius,
                    definition.AttackRange,
                    definition.MaximumChaseDistance,
                    (clips.Death != null ? clips.Death.length : 1.2f) + 0.2f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Animator BuildModelVisual(
            string modelPath,
            Transform visualParent,
            float scale,
            Vector3 localPosition,
            AnimatorController controller,
            string modelName)
        {
            GameObject modelInstance = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (modelAsset != null)
                {
                    modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                }
            }

            if (modelInstance == null)
            {
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                modelInstance.name = $"Placeholder_{modelName}";
                Collider placeholderCollider = modelInstance.GetComponent<Collider>();
                if (placeholderCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(placeholderCollider);
                }
            }

            modelInstance.name = modelName;
            modelInstance.transform.SetParent(visualParent, false);
            modelInstance.transform.localScale = Vector3.one * scale;
            modelInstance.transform.localPosition = localPosition;

            Animator animator = modelInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            return animator;
        }

        /// <summary>
        /// Rescales a model so its rendered height matches the requested value and its base sits on
        /// the pivot origin. The giant pursuer is sized relative to the cart this way instead of
        /// relying on the FBX's unknown native scale.
        /// </summary>
        private static void FitModelVisualToHeight(Transform model, float targetHeight)
        {
            if (model == null || targetHeight <= 0f)
            {
                return;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = CalculateRendererBounds(renderers);
            if (bounds.size.y <= 0.0001f)
            {
                return;
            }

            model.localScale *= targetHeight / bounds.size.y;

            Bounds fitted = CalculateRendererBounds(renderers);
            model.localPosition += Vector3.up * (model.parent.position.y - fitted.min.y);
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        /// <summary>Adds the Phase 3 attachment, damage and cart-attack components when absent.</summary>
        private static void ExtendSmallMonsterPrefab(string prefabPath, MonsterDefinition definition)
        {
            GameObject contents;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception exception)
            {
                Warnings.Add($"{prefabPath} could not be opened for extension ({exception.Message}).");
                return;
            }

            try
            {
                bool changed = false;

                if (!contents.TryGetComponent(out MonsterBase monster) || monster == null)
                {
                    throw new InvalidOperationException($"{prefabPath} has no MonsterBase component.");
                }

                MonsterAttachment attachment = contents.GetComponent<MonsterAttachment>();
                if (attachment == null)
                {
                    attachment = contents.AddComponent<MonsterAttachment>();
                    changed = true;
                }
                attachment.Configure(definition.AttachmentKind, definition.Load, 1.3f, 0.3f, Vector3.zero);
                changed = true;

                MonsterDamageable damageable = contents.GetComponent<MonsterDamageable>();
                if (damageable == null)
                {
                    damageable = contents.AddComponent<MonsterDamageable>();
                    changed = true;
                }
                damageable.Configure(definition.Health, true);
                changed = true;

                AttachedMonsterAttack cartAttack = contents.GetComponent<AttachedMonsterAttack>();
                if (cartAttack == null)
                {
                    cartAttack = contents.AddComponent<AttachedMonsterAttack>();
                    changed = true;
                }
                cartAttack.Configure(5.5f, 0.28f, 1.35f, ~0, 1.15f);
                changed = true;

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject GetOrCreateGiantPrefab(string modelPath, AnimatorController controller)
        {
            string prefabPath = $"Assets/Art/Monsters/{Giant.FolderName}/Prefabs/{Giant.DisplayName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && !string.IsNullOrEmpty(modelPath) &&
                existing.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                existing = null;
            }

            if (existing != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    GiantZombieController giant = contents.GetComponent<GiantZombieController>();
                    MonsterAnimationController animation = contents.GetComponent<MonsterAnimationController>();
                    Animator animator = contents.GetComponentInChildren<Animator>(true);
                    if (animator != null)
                    {
                        if (controller != null)
                        {
                            animator.runtimeAnimatorController = controller;
                        }
                        animator.applyRootMotion = false;
                    }

                    foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.enabled = true;
                    }

                    animation?.Configure(animator, "Locomotion", "Attack", "Death");
                    if (giant != null)
                    {
                        giant.Configure(null, null, animation, Giant.ChaseSpeed, Giant.CatchRange, ~0, ~0,
                            false, 1.15f);
                        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return existing;
            }

            GameObject root = new GameObject(Giant.DisplayName);
            try
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                Animator animator = BuildModelVisual(modelPath, visual.transform, Giant.ModelScale,
                    Giant.ModelLocalPosition, controller, "Giant_Model");
                if (visual.transform.childCount > 0)
                {
                    FitModelVisualToHeight(visual.transform.GetChild(0), Giant.TargetHeight);
                }

                GameObject rearProbe = new GameObject("CatchProbe");
                rearProbe.transform.SetParent(root.transform, false);
                rearProbe.transform.localPosition = new Vector3(0f, 1.4f, 0.9f);

                CapsuleCollider bodyCollider = root.AddComponent<CapsuleCollider>();
                bodyCollider.center = new Vector3(0f, 2.2f, 0f);
                bodyCollider.height = 4.4f;
                bodyCollider.radius = 0.9f;
                bodyCollider.direction = 1;

                MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
                animation.Configure(animator, "Locomotion", "Attack", "Death");

                GiantZombieController giant = root.AddComponent<GiantZombieController>();
                giant.Configure(null, null, animation, Giant.ChaseSpeed, Giant.CatchRange, ~0, ~0,
                    false, 1.15f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- scene wiring

        private static void ConfigureSurvivalScene(
            List<(MonsterDefinition Definition, GameObject Prefab, string Resolution)> smallPrefabs,
            GameObject giantPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            if (references == null || !references.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The Phase 1 PrototypeSceneReferences component is missing or incomplete. " +
                    "Run Tools > Monsters > Create Prototype Scene first.");
            }

            Transform cart = references.CartTransform;
            Transform hunter = references.HunterTransform;

            GameObject road = GameObject.Find(RoadName);
            if (road != null)
            {
                GetOrAdd<MonsterGroundSurface>(road);
            }

            CartAttachmentPoints attachmentPoints = BuildAttachmentPoints(cart);
            CartMonsterLoad cartLoad = BuildCartLoad(cart);
            SimulatedCartSurvivalController speedController = BuildSpeedController(cart, cartLoad);
            BuildHunterPrototype(hunter);
            SwordDamage swordDamage = BuildSword(hunter);
            Transform rearReachPoint = BuildRearReachPoint(cart);

            PrototypeHunterMonsterTarget hunterTarget = hunter.GetComponent<PrototypeHunterMonsterTarget>();
            if (hunterTarget == null)
            {
                hunterTarget = hunter.gameObject.AddComponent<PrototypeHunterMonsterTarget>();
            }

            hunterTarget.Configure(hunter, references.HunterAttackPoint);

            GameObject systemObject = GameObject.Find(SystemName);
            if (systemObject == null)
            {
                systemObject = new GameObject(SystemName);
            }

            BuildSpawner(references, systemObject, attachmentPoints, cartLoad, hunterTarget, smallPrefabs);
            GiantZombieSpawner giantSpawner = BuildGiantSpawner(systemObject, cart, rearReachPoint, giantPrefab);
            BuildHud(systemObject, cartLoad, speedController, giantSpawner, hunterTarget);

            if (swordDamage == null)
            {
                Warnings.Add("The prototype sword could not be created; melee combat needs manual review.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Unity could not save {ScenePath}.");
            }

            Selection.activeGameObject = systemObject;
        }

        private static CartAttachmentPoints BuildAttachmentPoints(Transform cart)
        {
            GameObject root = cart.Find(AttachmentRootName)?.gameObject;
            if (root == null)
            {
                root = new GameObject(AttachmentRootName);
                root.transform.SetParent(cart, false);
            }

            CartAttachmentPoints registry = GetOrAdd<CartAttachmentPoints>(root);
            List<MonsterAttachmentPoint> points = new List<MonsterAttachmentPoint>();

            points.Add(BuildPoint(root.transform, "Attach_LeftFront", new Vector3(-1.85f, 0.15f, 1.6f),
                new Vector3(1f, 0f, 0f), MonsterAttachmentKind.Ground, 1));
            points.Add(BuildPoint(root.transform, "Attach_LeftRear", new Vector3(-1.85f, 0.15f, -1.6f),
                new Vector3(1f, 0f, 0f), MonsterAttachmentKind.Ground, 1));
            points.Add(BuildPoint(root.transform, "Attach_RightFront", new Vector3(1.85f, 0.15f, 1.6f),
                new Vector3(-1f, 0f, 0f), MonsterAttachmentKind.Ground, 1));
            points.Add(BuildPoint(root.transform, "Attach_RightRear", new Vector3(1.85f, 0.15f, -1.6f),
                new Vector3(-1f, 0f, 0f), MonsterAttachmentKind.Ground, 1));
            points.Add(BuildPoint(root.transform, "Attach_RearCenter", new Vector3(0f, 0.15f, -2.85f),
                new Vector3(0f, 0f, 1f), MonsterAttachmentKind.Ground, 1));
            // The former front ground point remains available only to flying enemies. Terrestrial
            // monsters therefore have no reservation that can pull them onto/through the platform.
            points.Add(BuildPoint(root.transform, "Attach_FrontCenter", new Vector3(0f, 1.25f, 2.75f),
                new Vector3(0f, 0f, -1f), MonsterAttachmentKind.Flying, 1));
            points.Add(BuildPoint(root.transform, "Attach_TopLeft", new Vector3(-0.55f, 1.7f, 0f),
                new Vector3(0f, 0f, 1f), MonsterAttachmentKind.Flying, 2));
            points.Add(BuildPoint(root.transform, "Attach_TopRight", new Vector3(0.55f, 1.7f, 0f),
                new Vector3(0f, 0f, 1f), MonsterAttachmentKind.Flying, 2));

            registry.Configure(points);
            EditorUtility.SetDirty(registry);
            return registry;
        }

        private static MonsterAttachmentPoint BuildPoint(
            Transform parent,
            string pointName,
            Vector3 localPosition,
            Vector3 localFacing,
            MonsterAttachmentKind kinds,
            int capacity)
        {
            Transform existing = parent.Find(pointName);
            GameObject pointObject = existing != null ? existing.gameObject : new GameObject(pointName);
            if (existing == null)
            {
                pointObject.transform.SetParent(parent, false);
            }

            pointObject.transform.localPosition = localPosition;
            pointObject.transform.localRotation = Quaternion.identity;
            MonsterAttachmentPoint point = GetOrAdd<MonsterAttachmentPoint>(pointObject);
            point.Configure(kinds, capacity, localFacing);
            EditorUtility.SetDirty(point);
            return point;
        }

        private static CartMonsterLoad BuildCartLoad(Transform cart)
        {
            CartMonsterLoad load = GetOrAdd<CartMonsterLoad>(cart.gameObject);
            if (cart.GetComponent<SimulatedCartSurvivalController>() == null && cart.GetComponent<SimulatedCartMovement>() == null)
            {
                Warnings.Add(
                    "The cart has no speed controller and no simulated movement, so monster load cannot reduce any speed.");
            }

            load.Configure(45f, 0.22f, cart.GetComponent<MonoBehaviour>());
            EditorUtility.SetDirty(load);
            return load;
        }

        private static SimulatedCartSurvivalController BuildSpeedController(Transform cart, CartMonsterLoad load)
        {
            SimulatedCartMovement movement = cart.GetComponent<SimulatedCartMovement>();
            if (movement == null)
            {
                Warnings.Add("SimulatedCartMovement is missing; the provisional speed controller was skipped.");
                return null;
            }

            SimulatedCartSurvivalController controller = GetOrAdd<SimulatedCartSurvivalController>(cart.gameObject);
            controller.Configure(movement, 5.2f, 6.2f, 1.1f, 3f);
            load.Configure(45f, 0.22f, controller);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(load);
            return controller;
        }

        private static void BuildHunterPrototype(Transform hunter)
        {
            SimulatedHunterMovement movement = GetOrAdd<SimulatedHunterMovement>(hunter.gameObject);
            movement.Configure(2.2f, new Vector2(1.05f, 1.75f));
            EditorUtility.SetDirty(movement);
        }

        private static SwordDamage BuildSword(Transform hunter)
        {
            Transform existing = hunter.Find("HunterSword");
            GameObject sword = existing != null ? existing.gameObject : new GameObject("HunterSword");
            if (existing == null)
            {
                sword.transform.SetParent(hunter, false);
                sword.transform.localPosition = new Vector3(0.55f, 0.1f, 0.25f);
            }

            Material bladeMaterial = GetOrCreateMaterial("Sword_Steel", new Color(0.62f, 0.66f, 0.72f));

            Transform bladeTransform = sword.transform.Find("Blade");
            GameObject blade = bladeTransform != null ? bladeTransform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (bladeTransform == null)
            {
                blade.name = "Blade";
                blade.transform.SetParent(sword.transform, false);
            }

            blade.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            blade.transform.localRotation = Quaternion.identity;
            blade.transform.localScale = new Vector3(0.07f, 0.07f, 1.1f);
            Renderer bladeRenderer = blade.GetComponent<Renderer>();
            if (bladeRenderer != null && bladeMaterial != null)
            {
                bladeRenderer.sharedMaterial = bladeMaterial;
            }

            BoxCollider bladeCollider = GetOrAdd<BoxCollider>(blade);
            bladeCollider.isTrigger = true;
            bladeCollider.size = Vector3.one;

            Transform tipTransform = sword.transform.Find("BladeTip");
            GameObject tip = tipTransform != null ? tipTransform.gameObject : new GameObject("BladeTip");
            if (tipTransform == null)
            {
                tip.transform.SetParent(sword.transform, false);
            }

            tip.transform.localPosition = new Vector3(0f, 0f, 1.1f);
            tip.transform.localRotation = Quaternion.identity;
            tip.transform.localScale = Vector3.one;

            SwordDamage damage = GetOrAdd<SwordDamage>(sword);
            damage.Configure(tip.transform, 35f, 0.18f, ~0);
            damage.ConfigureVelocityReference(hunter);
            EditorUtility.SetDirty(damage);

            PrototypeSwordController swing = GetOrAdd<PrototypeSwordController>(sword);
            swing.Configure(damage);
            EditorUtility.SetDirty(swing);
            return damage;
        }

        private static Transform BuildRearReachPoint(Transform cart)
        {
            Transform existing = cart.Find(RearReachPointName);
            GameObject point = existing != null ? existing.gameObject : new GameObject(RearReachPointName);
            if (existing == null)
            {
                point.transform.SetParent(cart, false);
            }

            point.transform.localPosition = new Vector3(0f, 1f, -3.2f);
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
            return point.transform;
        }

        private static void BuildSpawner(
            PrototypeSceneReferences references,
            GameObject systemObject,
            CartAttachmentPoints attachmentPoints,
            CartMonsterLoad cartLoad,
            PrototypeHunterMonsterTarget hunterTarget,
            List<(MonsterDefinition Definition, GameObject Prefab, string Resolution)> smallPrefabs)
        {
            MonsterTargetRegistry registry = systemObject.GetComponent<MonsterTargetRegistry>();
            if (registry == null || !registry.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The Phase 2 MonsterTargetRegistry is missing or incomplete. " +
                    "Run Tools > Monsters > Setup Enemy System first; this phase reuses the same targets.");
            }

            MonsterSpawner spawner = GetOrAdd<MonsterSpawner>(systemObject);

            List<MonsterSpawnEntry> entries = smallPrefabs
                .Where(entry => entry.Prefab != null)
                .Select(entry => new MonsterSpawnEntry
                {
                    prefab = entry.Prefab,
                    movementType = entry.Definition.MovementType,
                    weight = entry.Definition.Weight,
                    minimumRadius = entry.Definition.MinimumRadius,
                    maximumRadius = entry.Definition.MaximumRadius,
                    minimumFlyingHeight = entry.Definition.MinimumFlyingHeight,
                    maximumFlyingHeight = entry.Definition.MaximumFlyingHeight,
                    attachmentLoad = entry.Definition.Load
                })
                .ToList();

            spawner.Configure(
                references.CartTransform,
                references.HunterTransform,
                registry,
                entries,
                ~0,
                ~0,
                attachmentPoints,
                cartLoad,
                hunterTarget);

            if (entries.Count < Definitions.Length)
            {
                Warnings.Add(
                    $"Only {entries.Count} of {Definitions.Length} small monster types could be registered in the spawner.");
            }

            EditorUtility.SetDirty(spawner);
        }

        private static GiantZombieSpawner BuildGiantSpawner(
            GameObject systemObject,
            Transform cart,
            Transform rearReachPoint,
            GameObject giantPrefab)
        {
            GiantZombieSpawner spawner = GetOrAdd<GiantZombieSpawner>(systemObject);
            spawner.Configure(giantPrefab, cart, rearReachPoint, Giant.InitialDistance, ~0);
            EditorUtility.SetDirty(spawner);
            return spawner;
        }

        private static void BuildHud(
            GameObject systemObject,
            CartMonsterLoad cartLoad,
            SimulatedCartSurvivalController speedController,
            GiantZombieSpawner giantSpawner,
            PrototypeHunterMonsterTarget hunterTarget)
        {
            SurvivalPrototypeHud hud = GetOrAdd<SurvivalPrototypeHud>(systemObject);
            hud.Configure(cartLoad, speedController, giantSpawner, hunterTarget);
            EditorUtility.SetDirty(hud);
        }

        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"Assets/Prototype/Materials/{materialName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader) { name = materialName, color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
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

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        // ---------------------------------------------------------------- validation

        public static bool ValidateOpenScene(out string report)
        {
            List<string> failures = new List<string>();
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CartMonsterLoad cartLoad = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            CartAttachmentPoints attachmentPoints = UnityEngine.Object.FindAnyObjectByType<CartAttachmentPoints>();
            GiantZombieSpawner giantSpawner = UnityEngine.Object.FindAnyObjectByType<GiantZombieSpawner>();
            SurvivalPrototypeHud hud = UnityEngine.Object.FindAnyObjectByType<SurvivalPrototypeHud>();
            SwordDamage sword = UnityEngine.Object.FindAnyObjectByType<SwordDamage>();

            Require(references != null && references.IsConfigured,
                "Phase 1 cart, hunter, attack point and candle references remain intact.", failures);
            Require(attachmentPoints != null && attachmentPoints.IsConfigured,
                "The cart exposes a configurable attachment point registry.", failures);
            Require(attachmentPoints != null && attachmentPoints.Points.Count >= 6 && attachmentPoints.TotalCapacity >= 8,
                "Attachment points are distributed around the cart with configurable capacity.", failures);
            Require(cartLoad != null, "CartMonsterLoad exists on the cart.", failures);
            Require(cartLoad != null && cartLoad.GetComponent<SimulatedCartSurvivalController>() != null,
                "The provisional cart speed controller implements the load penalty receiver.", failures);
            Require(sword != null && sword.GetComponent<PrototypeSwordController>() != null,
                "A prototype sword exposes SwordDamage for desktop testing.", failures);
            Require(giantSpawner != null && giantSpawner.HasSpawnedGiant == false,
                "The giant pursuer spawner is configured outside the small monster generator.", failures);
            Require(hud != null, "The survival prototype HUD is present.", failures);

            if (spawner != null)
            {
                Require(spawner.SpawnEntries.Count == Definitions.Length,
                    $"The spawner registers all {Definitions.Length} small monster types in one generator.", failures);
                Require(spawner.IsConfigured, "Spawner references and prefab weights are valid.", failures);

                foreach (MonsterSpawnEntry entry in spawner.SpawnEntries)
                {
                    if (entry.prefab == null)
                    {
                        failures.Add("A spawner entry has no prefab.");
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(entry.prefab);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    MonsterAttachment attachment = prefab != null ? prefab.GetComponent<MonsterAttachment>() : null;
                    MonsterDamageable damageable = prefab != null ? prefab.GetComponent<MonsterDamageable>() : null;
                    AttachedMonsterAttack cartAttack = prefab != null ? prefab.GetComponent<AttachedMonsterAttack>() : null;
                    Require(attachment != null && damageable != null && cartAttack != null,
                        $"{entry.prefab.name} can attach to the cart, take sword damage and threaten the hunter.", failures);
                    Require(entry.attachmentLoad > 0f,
                        $"{entry.prefab.name} declares a configurable cart load.", failures);
                }
            }

            GameObject giant = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Art/Monsters/{Giant.FolderName}/Prefabs/{Giant.DisplayName}.prefab");
            Require(giant != null, "The giant pursuer prefab exists.", failures);
            Require(giant != null && giant.GetComponent<GiantZombieController>() != null,
                "The giant prefab has its own chase controller.", failures);
            Require(giant != null && giant.GetComponent<MonsterBase>() == null &&
                    giant.GetComponent<MonsterAttachment>() == null &&
                    giant.GetComponent<MonsterDamageable>() == null,
                "The giant is excluded from the small monster spawner, attachment and sword-load systems.", failures);

            report = failures.Count == 0
                ? $"Survival setup validated: {Definitions.Length} small monsters with shared attachment/load/sword components, " +
                  $"{attachmentPoints?.Points.Count ?? 0} attachment points (capacity {attachmentPoints?.TotalCapacity ?? 0}) and one giant pursuer."
                : string.Join("\n", failures.Select(failure => $"FAILED: {failure}"));
            return failures.Count == 0;
        }

        private static void Require(bool condition, string description, ICollection<string> failures)
        {
            if (!condition)
            {
                failures.Add(description);
            }
        }

        private sealed class MonsterDefinition
        {
            public string DisplayName;
            public string FolderName;
            public string SourcePath;
            public MonsterMovementType MovementType;
            public MonsterAttachmentKind AttachmentKind;
            public float Load;
            public float Health = 30f;
            public float ModelScale = 0.5f;
            public Vector3 ModelLocalPosition;
            public float PatrolSpeed = 1.3f;
            public float ChaseSpeed = 3f;
            public float ApproachSpeed = 0.9f;
            public float DetectionRadius = 45f;
            public float AttackRange = 1.75f;
            public float MaximumChaseDistance = 70f;
            public float AttackCooldown = 2.25f;
            public float Weight = 1f;
            public float WeightFloor;
            public float MinimumRadius = 13f;
            public float MaximumRadius = 24f;
            public float MinimumFlyingHeight = 3.5f;
            public float MaximumFlyingHeight = 7f;
            public string[] LocomotionFragments = Array.Empty<string>();
            public string[] AttackFragments = Array.Empty<string>();
            public string[] DeathFragments = Array.Empty<string>();

            public bool Flyable => MovementType == MonsterMovementType.Flying;
        }

        private sealed class GiantDefinition
        {
            public string DisplayName;
            public string FolderName;
            public string SourcePath;
            public float ModelScale = 1.1f;
            public Vector3 ModelLocalPosition;
            public float TargetHeight = 4.5f;
            public float ChaseSpeed = 4f;
            public float CatchRange = 2.6f;
            public float InitialDistance = 18f;
            public string[] LocomotionFragments = Array.Empty<string>();
            public string[] AttackFragments = Array.Empty<string>();
            public string[] DeathFragments = Array.Empty<string>();
        }
    }
}
