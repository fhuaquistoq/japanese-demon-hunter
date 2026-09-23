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
    /// Phase 3 Play Mode smoke test: proves on a running scene that small monsters attach to the
    /// moving cart, add load, slow it down, can be killed with the prototype sword, release their
    /// load, respond to the whip request and that the giant pursuer closes the gap when the cart
    /// loses speed. It never opens a headset and never moves a camera.
    /// </summary>
    [InitializeOnLoad]
    public static class MonsterSurvivalPlayModeSmoke
    {
        private const string SessionKey = "JapaneseDemonHunter.Phase3.PlayModeSmoke";
        private const string EnteredPlaySessionKey = SessionKey + ".EnteredPlay";
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string ReportPath = "Logs/Phase3PlayModeSmoke.txt";

        private enum Step
        {
            Baseline,
            WaitForAttachment,
            RepeatAttacks,
            LoadAndSpeed,
            SwordCombat,
            Whip,
            GiantClosing,
            Done
        }

        private static readonly List<string> Passed = new List<string>();
        private static readonly List<string> Failed = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        private static Step step = Step.Baseline;
        private static double stepStartedAt;
        private static double playStartedAt;
        private static Vector3 cartStart;
        private static float speedBeforeSword;
        private static float speedBeforeWhip;
        private static int giantDistanceSamples;
        private static float giantDistanceBefore;
        private static GameObject syntheticLoadObject;
        private static AttachedMonsterAttack observedRepeatingAttacker;
        private static int attackCountAtObservationStart;

        static MonsterSurvivalPlayModeSmoke()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                RegisterCallbacks();
            }
            else if (Environment.GetCommandLineArgs().Any(argument => argument == "-survivalPlaySmoke"))
            {
                MonsterBatchGate.RunWhenEditorIsIdle(Run);
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        [MenuItem("Tools/Monsters/Run Phase 3 Play Mode Smoke Test")]
        public static void Run()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                RegisterCallbacks();
                return;
            }

            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(EnteredPlaySessionKey, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= Tick;

            if (EditorApplication.isPlaying)
            {
                BeginObservation();
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(EnteredPlaySessionKey, true);
                BeginObservation();
            }
            else if (state == PlayModeStateChange.EnteredEditMode &&
                     SessionState.GetBool(EnteredPlaySessionKey, false))
            {
                CleanupCallbacks();
            }
        }

        private static void BeginObservation()
        {
            Passed.Clear();
            Failed.Clear();
            Notes.Clear();
            step = Step.Baseline;
            playStartedAt = EditorApplication.timeSinceStartup;
            stepStartedAt = playStartedAt;
            giantDistanceSamples = 0;
            syntheticLoadObject = null;
            observedRepeatingAttacker = null;
            attackCountAtObservationStart = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - stepStartedAt;
            switch (step)
            {
                case Step.Baseline when elapsed >= 0.8d:
                    CaptureBaseline();
                    Advance(Step.WaitForAttachment);
                    break;
                case Step.WaitForAttachment when elapsed >= 1d:
                    if (TryCaptureAttachment())
                    {
                        Advance(Step.RepeatAttacks);
                    }
                    else if (EditorApplication.timeSinceStartup - playStartedAt > 25d)
                    {
                        Failed.Add("No small monster attached to the moving cart within 25 seconds.");
                        Notes.Add("Check attachment point reach, ground surfaces and spawn distances.");
                        Advance(Step.SwordCombat);
                    }

                    break;
                case Step.RepeatAttacks when elapsed >= 0.25d:
                    int repeatedHits = observedRepeatingAttacker != null
                        ? observedRepeatingAttacker.SuccessfulAttackCount - attackCountAtObservationStart
                        : 0;
                    if (repeatedHits >= 2)
                    {
                        Check(true,
                            $"The same attached monster completed {repeatedHits} additional attacks after its cooldowns.",
                            Passed, Failed);
                        Advance(Step.LoadAndSpeed);
                    }
                    else if (elapsed > 7d)
                    {
                        Failed.Add("An attached monster did not repeat two successful attacks within seven seconds.");
                        Advance(Step.LoadAndSpeed);
                    }
                    break;
                case Step.LoadAndSpeed when elapsed >= 0.5d:
                    EvaluateLoadAndSpeed();
                    Advance(Step.SwordCombat);
                    break;
                case Step.SwordCombat when elapsed >= 0.5d:
                    EvaluateSwordCombat();
                    Advance(Step.Whip);
                    break;
                case Step.Whip when elapsed >= 0.5d:
                    speedBeforeWhip = CurrentSpeed();
                    RequestWhip();
                    Advance(Step.GiantClosing);
                    break;
                case Step.GiantClosing when elapsed >= 1.6d:
                    if (EvaluateWhipAndGiant())
                    {
                        Advance(Step.Done);
                    }
                    break;
                case Step.Done:
                    CompleteObservation();
                    break;
            }
        }

        private static void Advance(Step next)
        {
            step = next;
            stepStartedAt = EditorApplication.timeSinceStartup;
        }

        private static void CaptureBaseline()
        {
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            GiantZombieSpawner giantSpawner = UnityEngine.Object.FindAnyObjectByType<GiantZombieSpawner>();

            Check(spawner != null, "MonsterSpawner exists in Play Mode.", Passed, Failed);
            Check(references != null && references.CartTransform != null, "The simulated cart exists.", Passed, Failed);
            if (references != null)
            {
                cartStart = references.CartTransform.position;
            }

            Check(references != null && references.CartMovement != null && references.CartMovement.IsMoving,
                "The cart starts moving on its own.", Passed, Failed);

            if (spawner != null)
            {
                MonsterBase[] active = ActiveMonsters(spawner);
                Check(spawner.SpawnEntries.Count == 5, "The generator exposes all five small monster types.", Passed, Failed);
                Check(active.Any(monster => monster.MovementType == MonsterMovementType.Flying),
                    "A flying small monster spawned around the cart.", Passed, Failed);
                Check(active.Any(monster => monster.MovementType == MonsterMovementType.Ground),
                    "A ground small monster spawned around the cart.", Passed, Failed);
                Check(active.All(monster => monster.IsInitialized), "Every active monster was initialized by the spawner.", Passed, Failed);
                Check(active.Length <= spawner.MaximumActiveMonsters, "The active count respects the configured maximum.", Passed, Failed);
                Notes.Add($"Active monsters at baseline: {active.Length} " +
                          $"({string.Join(", ", active.Select(monster => monster.name).Distinct())}).");
            }

            if (giantSpawner != null && giantSpawner.SpawnedGiant != null && references != null)
            {
                Vector3 toGiant = giantSpawner.SpawnedGiant.transform.position - references.CartTransform.position;
                bool behind = Vector3.Dot(toGiant, references.CartTransform.forward) < 0f;
                Check(behind, "The giant pursuer spawned behind the cart, never ahead or beside it.", Passed, Failed);
                Notes.Add($"Giant distance to cart rear at baseline: {giantSpawner.SpawnedGiant.DistanceToRear:0.0} m.");
            }
            else
            {
                Failed.Add("The giant pursuer did not spawn behind the cart.");
            }
        }

        private static bool TryCaptureAttachment()
        {
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            if (spawner == null || load == null)
            {
                return false;
            }

            MonsterBase attached = ActiveMonsters(spawner)
                .FirstOrDefault(monster => monster.Attachment != null && monster.Attachment.IsAttached);
            if (attached == null)
            {
                return false;
            }

            Check(true, $"A {attached.name} reached the cart and is now attached.", Passed, Failed);
            Check(load.RegisteredMonsterCount >= 1 && load.TotalLoad > 0f,
                $"The attached monster registered cart load ({load.TotalLoad:0.#}).", Passed, Failed);
            observedRepeatingAttacker = attached.GetComponent<AttachedMonsterAttack>();
            attackCountAtObservationStart = observedRepeatingAttacker != null
                ? observedRepeatingAttacker.SuccessfulAttackCount
                : 0;
            Check(observedRepeatingAttacker != null,
                "The attached monster has the repeatable cart-threat attack component.", Passed, Failed);
            return true;
        }

        private static void EvaluateLoadAndSpeed()
        {
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();

            if (references == null || load == null)
            {
                Failed.Add("The cart references or the load registry are missing in Play Mode.");
                return;
            }

            Check(Vector3.Distance(references.CartTransform.position, cartStart) > 3f,
                "The cart keeps advancing while monsters cling to it.", Passed, Failed);
            Check(load.SpeedMultiplier < 1f,
                $"Monster load reduced the cart speed multiplier to {load.SpeedMultiplier:0.00}.", Passed, Failed);
            if (load.RegisteredMonsterCount >= 2)
            {
                Check(true,
                    $"Multiple attached monsters accumulate a perceptible load ({load.RegisteredMonsterCount} monsters, {load.TotalLoad:0.#} units).",
                    Passed, Failed);
            }
            else
            {
                Notes.Add($"This randomized run had one attached monster at the speed sample ({load.TotalLoad:0.#} units); " +
                          "multi-monster accumulation is covered by the deterministic registry verification.");
            }
            SimulatedCartSurvivalController speedController =
                UnityEngine.Object.FindAnyObjectByType<SimulatedCartSurvivalController>();
            float maximumSpeed = speedController != null ? speedController.MaximumSpeed : 6.2f;
            Check(references.CartMovement.Speed <= maximumSpeed * load.SpeedMultiplier + 0.01f && references.CartMovement.Speed >= 0f,
                $"The cart speed respects the load penalty ({references.CartMovement.Speed:0.00}).", Passed, Failed);

            AttachPointReleaseCheck(spawner, load);

            speedBeforeSword = CurrentSpeed();
            Notes.Add($"Speed before sword test: {speedBeforeSword:0.00}, load {load.TotalLoad:0.#}.");
        }

        private static void AttachPointReleaseCheck(MonsterSpawner spawner, CartMonsterLoad load)
        {
            CartAttachmentPoints points = UnityEngine.Object.FindAnyObjectByType<CartAttachmentPoints>();
            if (points == null || spawner == null)
            {
                return;
            }

            int attachedCount = ActiveMonsters(spawner)
                .Count(monster => monster.Attachment != null && monster.Attachment.IsAttached);
            int activeCount = ActiveMonsters(spawner).Length;
            Check(points.OccupiedCount >= attachedCount && points.OccupiedCount <= activeCount,
                $"Attachment occupancy includes {attachedCount} attached monsters plus only valid incoming reservations " +
                $"({points.OccupiedCount}/{activeCount} active).", Passed, Failed);
        }

        private static void EvaluateSwordCombat()
        {
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            SwordDamage sword = UnityEngine.Object.FindAnyObjectByType<SwordDamage>();

            if (spawner == null || load == null || sword == null)
            {
                Failed.Add("The spawner, the cart load or the prototype sword are missing in Play Mode.");
                return;
            }

            MonsterBase victim = ActiveMonsters(spawner)
                .FirstOrDefault(monster => monster.Attachment != null && monster.Attachment.IsAttached &&
                                           monster.GetComponentInChildren<Collider>() != null);
            if (victim == null)
            {
                Failed.Add("No attached monster with a collider was available for the sword test.");
                return;
            }

            Collider victimCollider = victim.GetComponentInChildren<Collider>();
            Vector3 impactPoint = victimCollider.bounds.center;
            int loadBefore = load.RegisteredMonsterCount;
            float totalLoadBefore = load.TotalLoad;

            sword.BeginAttackWindow();
            int firstSweep = sword.Sweep(impactPoint, impactPoint);
            int secondSweep = sword.Sweep(impactPoint, impactPoint);
            sword.EndAttackWindow();

            Check(firstSweep >= 1, "The sword impact zone reached and damaged an attached monster.", Passed, Failed);
            Check(secondSweep == 0 && sword.UniqueHitsThisWindow == firstSweep,
                "A single attack window damages each reached monster only once, even when several overlap.", Passed, Failed);
            Check(victim.IsDead, "The monster died once the sword applied enough damage.", Passed, Failed);
            Check(victim.GetComponent<Collider>() == null || !victim.GetComponent<Collider>().enabled,
                "Dying disabled the monster body collider.", Passed, Failed);
            Check(load.RegisteredMonsterCount < loadBefore && load.TotalLoad < totalLoadBefore,
                $"The dead monster released its load immediately ({totalLoadBefore:0.#} -> {load.TotalLoad:0.#}).",
                Passed, Failed);
            Check(victim.Attachment == null || !victim.Attachment.IsAttached,
                "The dead monster released its attachment point.", Passed, Failed);

            ClearAttachedMonsters(spawner, load);
        }

        private static void ClearAttachedMonsters(MonsterSpawner spawner, CartMonsterLoad load)
        {
            int cleared = 0;
            foreach (MonsterBase monster in ActiveMonsters(spawner)
                         .Where(candidate => candidate.Attachment != null && candidate.Attachment.IsAttached)
                         .ToArray())
            {
                monster.Kill();
                cleared++;
            }

            Notes.Add($"Cleared {cleared} remaining attached monsters before the whip test; load is now {load.TotalLoad:0.#}.");
        }

        private static void RequestWhip()
        {
            SimulatedCartSurvivalController speed = UnityEngine.Object.FindAnyObjectByType<SimulatedCartSurvivalController>();
            if (speed == null)
            {
                Failed.Add("The provisional whip controller is missing in Play Mode.");
                return;
            }

            bool requested = false;
            void Handler() => requested = true;
            speed.AccelerationRequested += Handler;
            speed.RequestAcceleration();
            speed.AccelerationRequested -= Handler;
            Check(requested, "The whip action raises the acceleration request event.", Passed, Failed);
        }

        private static bool EvaluateWhipAndGiant()
        {
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            GiantZombieSpawner giantSpawner = UnityEngine.Object.FindAnyObjectByType<GiantZombieSpawner>();

            if (giantDistanceSamples == 0)
            {
                float afterWhip = CurrentSpeed();
                Check(afterWhip > speedBeforeWhip + 0.01f,
                    $"The cart accelerated after the whip request ({speedBeforeWhip:0.00} -> {afterWhip:0.00}).", Passed, Failed);
                SimulatedCartSurvivalController speedController =
                    UnityEngine.Object.FindAnyObjectByType<SimulatedCartSurvivalController>();
                float maximumSpeed = speedController != null ? speedController.MaximumSpeed : 6.2f;
                Check(load == null || afterWhip <= maximumSpeed * load.SpeedMultiplier + 0.02f,
                    "The acceleration respected the remaining load limit.", Passed, Failed);
            }

            if (giantSpawner == null || giantSpawner.SpawnedGiant == null || references == null)
            {
                Failed.Add("The giant pursuer was missing, so the closing-distance check could not run.");
                return true;
            }

            GiantZombieController giant = giantSpawner.SpawnedGiant;
            if (giant.HasCaughtCart)
            {
                Check(true, "The heavily slowed cart allowed the giant to catch its rear point.", Passed, Failed);
                return true;
            }

            if (giantDistanceSamples == 0)
            {
                ForcesSlowCart(load);
                giantDistanceBefore = giant.DistanceToRear;
                giantDistanceSamples = 1;
                stepStartedAt = EditorApplication.timeSinceStartup;
                Notes.Add($"Forced a heavy load so the closing behaviour is observable; giant distance {giantDistanceBefore:0.0} m.");
                return false;
            }

            float now = giant.DistanceToRear;
            Check(now < giantDistanceBefore - 0.5f,
                $"A slowed cart let the giant close the gap ({giantDistanceBefore:0.0} m -> {now:0.0} m).", Passed, Failed);
            Check(giant.IsChasing || giant.HasCaughtCart, "The giant kept pursuing the cart.", Passed, Failed);
            return true;
        }

        private static void ForcesSlowCart(CartMonsterLoad load)
        {
            if (load == null || syntheticLoadObject != null)
            {
                return;
            }

            syntheticLoadObject = new GameObject("SyntheticHeavyLoad");
            MonsterAttachment synthetic = syntheticLoadObject.AddComponent<MonsterAttachment>();
            load.Register(synthetic, 400f);
            Notes.Add($"Synthetic load registered; multiplier now {load.SpeedMultiplier:0.00}.");
        }

        private static void CompleteObservation()
        {
            EditorApplication.update -= Tick;
            ReleaseSyntheticLoad();

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Phase 3 Play Mode smoke test: {Passed.Count} passed, {Failed.Count} failed");
            foreach (string item in Passed) report.AppendLine($"PASS: {item}");
            foreach (string item in Failed) report.AppendLine($"FAIL: {item}");
            foreach (string item in Notes) report.AppendLine($"NOTE: {item}");
            Directory.CreateDirectory(GetLogDirectory());
            File.WriteAllText(Path.Combine(GetLogDirectory(), Path.GetFileName(ReportPath)), report.ToString());
            Debug.Log(report.ToString());

            SessionState.SetInt(SessionKey + ".ExitCode", Failed.Count == 0 ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }

        private static void ReleaseSyntheticLoad()
        {
            if (syntheticLoadObject == null)
            {
                return;
            }

            CartMonsterLoad load = UnityEngine.Object.FindAnyObjectByType<CartMonsterLoad>();
            MonsterAttachment synthetic = syntheticLoadObject.GetComponent<MonsterAttachment>();
            if (load != null && synthetic != null)
            {
                load.Unregister(synthetic);
            }

            UnityEngine.Object.Destroy(syntheticLoadObject);
            syntheticLoadObject = null;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            SessionState.SetBool(SessionKey, false);
            SessionState.SetBool(EnteredPlaySessionKey, false);
            int exitCode = SessionState.GetInt(SessionKey + ".ExitCode", 1);
            SessionState.EraseInt(SessionKey + ".ExitCode");
            if (Application.isBatchMode ||
                Environment.GetCommandLineArgs().Any(argument => argument == "-survivalPlaySmoke"))
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static MonsterBase[] ActiveMonsters(MonsterSpawner spawner)
        {
            return spawner.ActiveMonsters.Where(monster => monster != null).ToArray();
        }

        private static float CurrentSpeed()
        {
            SimulatedCartSurvivalController speed = UnityEngine.Object.FindAnyObjectByType<SimulatedCartSurvivalController>();
            return speed != null ? speed.CommandedSpeed : 0f;
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
