using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using JapaneseDemonHunter.Monsters;

namespace JapaneseDemonHunter.MonstersEditor
{
    /// <summary>
    /// Builds the prefabs for the newly imported zombie and giant zombie models, using the animation
    /// takes that actually ship inside each fbx instead of assuming clip names. It only creates the
    /// assets that are missing and never overwrites a prefab that already exists, so running it again
    /// is safe and never discards Inspector tuning.
    /// </summary>
    public static class ImportedZombieSetup
    {
        private const string ZombieModelPath = "Assets/Art/Zombie/Zombie.fbx";
        private const string GiantModelPath = "Assets/Art/Zombie/Gigant_Zombie.fbx";
        private const string ZombiePrefabPath =
            "Assets/Art/Monsters/Zombie/Prefabs/ZombieHordeDemon.prefab";
        private const string GiantPrefabPath =
            "Assets/Art/Monsters/GiantZombie/Prefabs/GiantHordeZombie.prefab";
        private const string ZombieControllerPath = "Assets/Art/Monsters/Zombie/Animations/ZombieHorde.controller";
        private const string GiantControllerPath =
            "Assets/Art/Monsters/GiantZombie/Animations/GiantHorde.controller";

        private const float ZombieTargetHeight = 1.95f;
        private const float GiantTargetHeight = 4.4f;
        private const float GroundChaseSpeed = 6.8f;
        private const float GroundApproachSpeed = 6.4f;
        private const float GiantChaseSpeed = 2.4f;
        private const float AttachmentLoad = 15f;
        private const float AttackRange = 1.75f;
        private const float GiantCatchRange = 2.6f;
        private const float MaximumAttackDistance = 1.55f;
        private const float MaximumChaseDistance = 70f;
        private const float DetectionRadius = 45f;

        [MenuItem("Tools/Monsters/Setup Imported Zombie Prefabs")]
        public static void SetupImportedZombiePrefabs()
        {
            MonsterBase zombie = GetOrCreateZombie();
            GameObject giant = GetOrCreateGiant();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Imported zombie prefabs ready: {zombie.name} and {giant.name}. " +
                "Existing prefabs and scene wiring were not modified.");
        }

        [MenuItem("Tools/Monsters/Setup Imported Zombie Prefabs", true)]
        private static bool ValidateSetupImportedZombiePrefabs()
        {
            return true;
        }

        private static MonsterBase GetOrCreateZombie()
        {
            MonsterBase existing = AssetDatabase.LoadAssetAtPath<MonsterBase>(ZombiePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            AnimationClip locomotion = RequireClip(ZombieModelPath, "ZombieWalk");
            AnimationClip run = RequireClip(ZombieModelPath, "ZombieRun");
            AnimationClip attack = RequireClip(ZombieModelPath, "ZombieBite");
            AnimationClip crawl = RequireClip(ZombieModelPath, "ZombieCrawl");
            // Declared before the prefab is built: reimporting the fbx invalidates anything already
            // instantiated from it, so the takes must be looped first and the clips reloaded after.
            DeclareLoopingTake(ZombieModelPath, locomotion, true);
            DeclareLoopingTake(ZombieModelPath, crawl, true);
            locomotion = RequireClip(ZombieModelPath, "ZombieWalk");
            attack = RequireClip(ZombieModelPath, "ZombieBite");
            crawl = RequireClip(ZombieModelPath, "ZombieCrawl");
            AnimatorController controller = GetOrCreateController(
                ZombieControllerPath, locomotion, run, attack, crawl);
            return BuildZombiePrefab(controller, locomotion, attack, crawl);
        }

        private static GameObject GetOrCreateGiant()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GiantPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            AnimationClip locomotion = RequireClip(GiantModelPath, "Crawl");
            AnimationClip attack = RequireClip(GiantModelPath, "Run_Attack");
            AnimationClip death = RequireClip(GiantModelPath, "Death");
            DeclareLoopingTake(GiantModelPath, locomotion, true);
            locomotion = RequireClip(GiantModelPath, "Crawl");
            AnimatorController controller = GetOrCreateController(
                GiantControllerPath, locomotion, locomotion, attack, death);
            return BuildGiantPrefab(controller, locomotion, death);
        }

        private static MonsterBase BuildZombiePrefab(
            AnimatorController controller, AnimationClip locomotion, AnimationClip attack, AnimationClip crawl)
        {
            GameObject model = RequireModel(ZombieModelPath);
            GameObject root = new GameObject("ZombieHordeDemon");
            try
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                Animator animator = AttachScaledModel(model, visual.transform, "Zombie_Model", ZombieTargetHeight);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                Transform attackPoint = CreateChild(root.transform, "AttackPoint", new Vector3(0f, 1.05f, 0.7f));
                CreateChild(root.transform, "DetectionPoint", new Vector3(0f, 1.15f, 0f));
                CapsuleCollider body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.95f, 0f);
                body.radius = 0.45f;
                body.height = 1.95f;
                body.direction = 1;

                GroundMonsterMovement movement = root.AddComponent<GroundMonsterMovement>();
                movement.ConfigureSpeeds(GroundChaseSpeed, GroundChaseSpeed, GroundApproachSpeed);
                movement.ConfigureGrounding(~0, ~0);

                MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>();
                selector.Strategy = MonsterTargetStrategy.PrioritizeHunter;

                MonsterAttack attackBehaviour = root.AddComponent<MonsterAttack>();
                attackBehaviour.Configure(attackPoint, AttackRange,
                    Mathf.Clamp(attack.length * 0.45f, 0.25f, 1.1f), 2.25f, ~0);

                MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
                animation.Configure(animator, "Locomotion", "Attack", "Death");

                MonsterAttachment attachment = root.AddComponent<MonsterAttachment>();
                attachment.Configure(MonsterAttachmentKind.Ground, AttachmentLoad, 1.3f, 0.3f, Vector3.zero);
                root.AddComponent<MonsterDamageable>();
                root.AddComponent<AttachedMonsterAttack>();

                MonsterBase monster = GetOrAddMonster(root);
                RequireWired(movement, selector, attackBehaviour, animation, body, animator, attackPoint);
                monster.Configure(movement, selector, attackBehaviour, animation, body,
                    DetectionRadius, AttackRange, MaximumChaseDistance, 2.2f);
                return SavePrefab(root, ZombiePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildGiantPrefab(
            AnimatorController controller, AnimationClip locomotion, AnimationClip death)
        {
            GameObject model = RequireModel(GiantModelPath);
            GameObject root = new GameObject("GiantHordeZombie");
            try
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                Animator animator = AttachScaledModel(model, visual.transform, "Giant_Model", GiantTargetHeight);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                CreateChild(root.transform, "CatchProbe", new Vector3(0f, 1.4f, 0.9f));
                CapsuleCollider body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 2.2f, 0f);
                body.radius = 0.9f;
                body.height = 4.4f;
                body.direction = 1;

                MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
                animation.Configure(animator, "Locomotion", "Attack", "Death");
                GiantZombieController giant = GetOrAddGiantController(root);
                giant.Configure(null, null, animation, GiantChaseSpeed, GiantCatchRange, ~0, ~0, false, 1f);

                _ = death;
                return SaveGiantPrefab(root, GiantPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The imported fbx root already carries a MonsterBase, so adding a second one fails and leaves
        /// the prefab without a usable controller. The existing one is reused instead.
        /// </summary>
        private static MonsterBase GetOrAddMonster(GameObject root)
        {
            MonsterBase existing = root.GetComponent<MonsterBase>();
            if (existing != null)
            {
                return existing;
            }

            return root.AddComponent<MonsterBase>();
        }

        /// <summary>The giant fbx root already carries this controller, so the existing one is reused.</summary>
        private static GiantZombieController GetOrAddGiantController(GameObject root)
        {
            GiantZombieController existing = root.GetComponent<GiantZombieController>();
            return existing != null ? existing : root.AddComponent<GiantZombieController>();
        }

        private static GameObject SaveGiantPrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null)
            {
                throw new InvalidOperationException($"The prefab could not be saved at {path}.");
            }

            return saved;
        }

        private static void RequireWired(
            MonsterMovement movement,
            MonsterTargetSelector selector,
            MonsterAttack attack,
            MonsterAnimationController animation,
            Collider body,
            Animator animator,
            Transform attackPoint)
        {
            string missing = null;
            if (movement == null) missing = "movement";
            else if (selector == null) missing = "targetSelector";
            else if (attack == null) missing = "attack";
            else if (animation == null) missing = "animationController";
            else if (body == null) missing = "bodyCollider";
            else if (animator == null) missing = "animator";
            else if (attackPoint == null) missing = "attackPoint";
            if (missing != null)
            {
                throw new InvalidOperationException(
                    $"The imported zombie prefab could not be built: '{missing}' is missing.");
            }
        }

        private static GameObject RequireModel(string path)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                throw new FileNotFoundException("The imported model could not be loaded.", path);
            }

            return model;
        }

        private static Animator AttachScaledModel(
            GameObject model, Transform parent, string name, float targetHeight)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Unity could not instantiate the imported model.");
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = Vector3.one;
            instance.transform.localPosition = Vector3.zero;
            FitHeight(instance, targetHeight);
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            return animator;
        }

        /// <summary>
        /// Scales the model to the height gameplay expects, measured from its own bounds: the imported
        /// zombie mesh is a few centimetres tall, so a fixed scale would make it invisible.
        /// </summary>
        private static void FitHeight(GameObject instance, float targetHeight)
        {
            Bounds bounds = GetBounds(instance);
            if (bounds.size.y <= 0.0001f)
            {
                throw new InvalidOperationException($"{instance.name} has no usable renderer bounds.");
            }

            instance.transform.localScale *= targetHeight / bounds.size.y;
        }

        private static Bounds GetBounds(GameObject instance)
        {
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static MonsterBase SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            MonsterBase prefab = saved != null ? saved.GetComponent<MonsterBase>() : null;
            if (prefab == null)
            {
                throw new InvalidOperationException($"The prefab could not be saved at {path}.");
            }

            return prefab;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
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
                throw new InvalidOperationException(
                    $"No animation containing '{clipNameFragment}' was found in {modelPath}.");
            }

            return clip;
        }

        private static AnimatorController GetOrCreateController(
            string controllerPath,
            AnimationClip locomotion,
            AnimationClip fast,
            AnimationClip attack,
            AnimationClip death)
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(controllerPath));
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = stateMachine.AddState("Locomotion");
            locomotionState.motion = locomotion;
            stateMachine.defaultState = locomotionState;
            AnimatorState fastState = stateMachine.AddState("LocomotionFast");
            fastState.motion = fast;
            AnimatorState attackState = stateMachine.AddState("Attack");
            attackState.motion = attack;
            AnimatorState deathState = stateMachine.AddState("Death");
            deathState.motion = death;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Marks a locomotion take as looping in the model's own import settings. A looped clip is what
        /// keeps the walk and crawl going instead of freezing on their last frame.
        /// </summary>
        private static void DeclareLoopingTake(string modelPath, AnimationClip clip, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] takes = importer.clipAnimations != null &&
                importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0)
            {
                return;
            }

            bool changed = false;
            var updated = (ModelImporterClipAnimation[])takes.Clone();
            for (int index = 0; index < updated.Length; index++)
            {
                if (updated[index].name != clip.name)
                {
                    continue;
                }

                if (updated[index].loopTime == loop)
                {
                    return;
                }

                updated[index].loopTime = loop;
                updated[index].loopPose = true;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            importer.clipAnimations = updated;
            importer.SaveAndReimport();
        }
    }
}
