using System;
using System.IO;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>Small Play Mode smoke run with real camera renders; never invokes a Player build.</summary>
    public static class PlayableSmokeCapture
    {
        private const string ScenePath = "Assets/Scenes/JapanDemonHunter.unity";
        private static int stage;
        private static int readyFrame;
        private static float waitUntil;
        private static bool running;
        private const string PendingKey = "JDH.PlayableSmokeCapture.Pending";

        [InitializeOnLoadMethod]
        private static void RestoreAfterReload()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            running = true;
            stage = 0;
            readyFrame = Time.frameCount + 20;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        [MenuItem("Tools/Game/Capture Playable Smoke")]
        public static void Run()
        {
            if (running) return;
            if (EditorApplication.isPlaying)
            {
                running = true;
                stage = 0;
                readyFrame = Time.frameCount + 20;
                EditorApplication.update += Step;
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            running = true;
            stage = 0;
            readyFrame = 20;
            SessionState.SetBool(PendingKey, true);
            EditorApplication.update += Step;
            EditorApplication.isPlaying = true;
        }

        public static void RunFromCommandLine() => Run();

        private static void Step()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (stage == 0)
                {
                    if (Time.frameCount < readyFrame) return;
                    var session = UnityEngine.Object.FindAnyObjectByType<GameSessionController>();
                    if (session == null || session.Result != GameRunResult.Playing)
                        throw new InvalidOperationException("La partida no inició correctamente.");
                    Capture("01-inicio.png");
                    ValidateMonsterFall();
                    var death = UnityEngine.Object.FindAnyObjectByType<DeathScreenEffect>();
                    if (death == null) throw new InvalidOperationException("No hay efecto de derrota.");
                    death.TriggerDefeat();
                    stage = 1;
                    waitUntil = Time.realtimeSinceStartup + 2f;
                }
                else if (stage == 1)
                {
                    if (Time.realtimeSinceStartup < waitUntil) return;
                    var session = UnityEngine.Object.FindAnyObjectByType<GameSessionController>();
                    if (session == null || session.Result != GameRunResult.Defeat)
                        throw new InvalidOperationException("El gigante no activó GAME OVER.");
                    Capture("02-game-over.png");
                    SceneManager.LoadScene(ScenePath);
                    stage = 2;
                    readyFrame = Time.frameCount + 20;
                }
                else if (stage == 2)
                {
                    if (Time.frameCount < readyFrame) return;
                    var victory = UnityEngine.Object.FindAnyObjectByType<LevelVictoryController>();
                    if (victory == null) throw new InvalidOperationException("No hay condición de victoria.");
                    victory.Complete();
                    stage = 3;
                    readyFrame = Time.frameCount + 2;
                }
                else if (stage == 3)
                {
                    if (Time.frameCount < readyFrame) return;
                    var session = UnityEngine.Object.FindAnyObjectByType<GameSessionController>();
                    if (session == null || session.Result != GameRunResult.Victory)
                        throw new InvalidOperationException("La llegada no activó VICTORIA.");
                    Capture("03-victoria.png");
                    Finish(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void ValidateMonsterFall()
        {
            var spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            if (spawner == null || spawner.SpawnEntries.Count == 0 ||
                !spawner.TrySpawn(spawner.SpawnEntries[0]))
                throw new InvalidOperationException("No se pudo crear un monstruo para probar su caída.");
            MonsterBase monster = null;
            foreach (MonsterBase active in spawner.ActiveMonsters)
                if (active != null && active.GetComponent<MonsterDeathPhysics>() != null)
                    monster = active;
            var damageable = monster != null ? monster.GetComponent<MonsterDamageable>() : null;
            if (damageable == null || !damageable.ApplyDamage(damageable.MaximumHealth, null))
                throw new InvalidOperationException("El monstruo no recibió el golpe final.");
            var body = monster.GetComponent<Rigidbody>();
            var collision = monster.GetComponent<Collider>();
            if (body == null || body.isKinematic || collision == null || !collision.enabled)
                throw new InvalidOperationException("La muerte no activó la caída física.");
            Debug.Log("Caída física del monstruo validada en Play Mode.");
        }

        private static void Capture(string fileName)
        {
            var death = UnityEngine.Object.FindAnyObjectByType<DeathScreenEffect>();
            Camera eye = death != null ? death.GetComponent<Camera>() : Camera.main;
            var motor = UnityEngine.Object.FindAnyObjectByType<Reins.CarriageMotor>();
            if (eye == null || motor == null)
                throw new InvalidOperationException("No hay cámara XR o carreta para la captura.");
            string folder = Path.Combine(Application.dataPath, "TestCaptures");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture oldActive = RenderTexture.active;
            // The batch XR simulator can place its virtual eye under the wagon. Render from a
            // separate test camera; neither the player's camera nor the XR Origin is moved.
            var cameraObject = new GameObject("TemporarySmokeCamera", typeof(Camera));
            Camera captureCamera = cameraObject.GetComponent<Camera>();
            captureCamera.CopyFrom(eye);
            captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
            captureCamera.fieldOfView = 76f;
            captureCamera.nearClipPlane = 0.05f;
            captureCamera.targetTexture = target;
            Vector3 cameraPosition = motor.transform.TransformPoint(new Vector3(0f, 2.05f, 0.9f));
            Vector3 lookAt = motor.transform.TransformPoint(new Vector3(0f, 2.05f, -4f));
            captureCamera.transform.position = cameraPosition;
            captureCamera.transform.rotation = Quaternion.LookRotation(lookAt - cameraPosition);
            GameObject tint = null;
            if (death != null && death.Alpha > 0.001f && death.OverlayRenderer != null)
            {
                tint = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tint.name = "TemporarySmokeTint";
                tint.transform.SetParent(captureCamera.transform, false);
                tint.transform.localPosition = Vector3.forward;
                float height = 2f * Mathf.Tan(captureCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                tint.transform.localScale = new Vector3(height * 1280f / 720f, height, 1f);
                Renderer tintRenderer = tint.GetComponent<Renderer>();
                tintRenderer.sharedMaterial = death.OverlayRenderer.sharedMaterial;
                var tintProperties = new MaterialPropertyBlock();
                death.OverlayRenderer.GetPropertyBlock(tintProperties);
                tintRenderer.SetPropertyBlock(tintProperties);
            }
            Texture2D texture = null;
            try
            {
                captureCamera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("Captura de prueba guardada: " + path);
            }
            finally
            {
                RenderTexture.active = oldActive;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (tint != null) UnityEngine.Object.DestroyImmediate(tint);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Finish(int exitCode)
        {
            running = false;
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= Step;
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }
    }
}
