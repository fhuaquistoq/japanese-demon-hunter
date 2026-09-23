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
    [InitializeOnLoad]
    public static class MonsterPlayModeSmokeRunner
    {
        private const string SessionKey = "JapaneseDemonHunter.Phase2.PlayModeSmoke";
        private const string EnteredPlaySessionKey = SessionKey + ".EnteredPlay";
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string ReportPath = "Logs/Phase2PlayModeSmoke.txt";

        private static readonly Dictionary<ulong, Vector3> InitialMonsterPositions = new Dictionary<ulong, Vector3>();
        private static double playStartTime;
        private static Vector3 cartStart;
        private static bool captured;

        static MonsterPlayModeSmokeRunner()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                RegisterCallbacks();
            }
            else if (Environment.GetCommandLineArgs().Any(argument => argument == "-phase2PlaySmoke"))
            {
                Run();
            }
        }

        [MenuItem("Tools/Monsters/Run Phase 2 Play Mode Smoke Test")]
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

        public static void RunFromCommandLine()
        {
            Run();
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
            playStartTime = EditorApplication.timeSinceStartup;
            captured = false;
            InitialMonsterPositions.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - playStartTime;
            if (!captured && elapsed >= 0.65d)
            {
                MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
                PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
                if (spawner != null)
                {
                    foreach (MonsterBase monster in spawner.ActiveMonsters.Where(monster => monster != null))
                    {
                        InitialMonsterPositions[EntityId.ToULong(monster.GetEntityId())] = monster.transform.position;
                    }
                }

                if (references != null)
                {
                    cartStart = references.CartTransform.position;
                }

                captured = true;
            }

            if (elapsed < 3.25d)
            {
                return;
            }

            CompleteObservation();
        }

        private static void CompleteObservation()
        {
            EditorApplication.update -= Tick;
            List<string> passed = new List<string>();
            List<string> failed = new List<string>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();

            Check(spawner != null, "MonsterSpawner exists in Play Mode.", passed, failed);
            Check(references != null && references.CartMovement.IsMoving,
                "The simulated cart is moving in Play Mode.", passed, failed);
            Check(references != null && Vector3.Distance(references.CartTransform.position, cartStart) > 1f,
                "The cart advanced during the observation window.", passed, failed);

            if (spawner != null)
            {
                MonsterBase[] active = spawner.ActiveMonsters.Where(monster => monster != null).ToArray();
                Check(active.Any(monster => monster.MovementType == MonsterMovementType.Flying),
                    "At least one flying BatDemon spawned.", passed, failed);
                Check(active.Any(monster => monster.MovementType == MonsterMovementType.Ground),
                    "At least one ground ZombieDemon spawned.", passed, failed);
                Check(active.All(monster => monster.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0),
                    "Every active enemy uses a real skinned model.", passed, failed);
                Check(active.All(monster => monster.IsInitialized),
                    "Every active enemy was initialized by the spawner.", passed, failed);
                Check(active.All(monster => monster.CurrentTarget != null || monster.UsesCartAttachment),
                    "Every observed enemy can act: it holds a target or the cart attachment component.", passed, failed);
                Check(active.Length <= spawner.MaximumActiveMonsters,
                    "The active count respects the configured maximum.", passed, failed);
                Check(active.Any(monster => InitialMonsterPositions.TryGetValue(
                                                EntityId.ToULong(monster.GetEntityId()), out Vector3 start) &&
                                            Vector3.Distance(monster.transform.position, start) > 0.1f),
                    "At least one enemy moved under FSM control.", passed, failed);
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Phase 2 Play Mode smoke test: {passed.Count} passed, {failed.Count} failed");
            foreach (string item in passed) report.AppendLine($"PASS: {item}");
            foreach (string item in failed) report.AppendLine($"FAIL: {item}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());

            SessionState.SetInt(SessionKey + ".ExitCode", failed.Count == 0 ? 0 : 1);
            EditorApplication.ExitPlaymode();
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
                Environment.GetCommandLineArgs().Any(argument => argument == "-phase2PlaySmoke"))
            {
                EditorApplication.Exit(exitCode);
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
    }
}
