using System;
using System.Collections.Generic;
using System.IO;
using JapaneseDemonHunter.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.PrototypeEditor
{
    public static class MonstersPrototypeSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";
        private const string MaterialFolder = "Assets/Prototype/Materials";

        [MenuItem("Tools/Monsters/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (File.Exists(ScenePath))
            {
                bool shouldOpen = Application.isBatchMode || EditorUtility.DisplayDialog(
                    "Monsters prototype already exists",
                    $"{ScenePath} already exists. It will be opened without being overwritten.",
                    "Open existing scene",
                    "Cancel");

                if (shouldOpen)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    Debug.Log($"Opened existing prototype scene without modifying it: {ScenePath}");
                }

                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets", "Prototype");
            EnsureFolder("Assets/Prototype", "Materials");

            Material roadMaterial = GetOrCreateMaterial("Road_Dark", new Color(0.035f, 0.04f, 0.045f));
            Material cartMaterial = GetOrCreateMaterial("Cart_Wood", new Color(0.24f, 0.105f, 0.035f));
            Material hunterMaterial = GetOrCreateMaterial("Hunter", new Color(0.16f, 0.19f, 0.22f));
            Material candleMaterial = GetOrCreateMaterial("Candle_Wax", new Color(0.72f, 0.64f, 0.45f));
            Material flameMaterial = GetOrCreateMaterial("Candle_Flame", new Color(1f, 0.28f, 0.015f), true);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MonstersPrototype";

            ConfigureEnvironment();
            CreateRoad(roadMaterial);
            CreateDirectionalLight();

            GameObject cartRoot = new GameObject("SimulatedCart");
            cartRoot.transform.position = new Vector3(0f, 0f, -70f);
            SimulatedCartMovement cartMovement = cartRoot.AddComponent<SimulatedCartMovement>();
            PrototypeSceneReferences references = cartRoot.AddComponent<PrototypeSceneReferences>();

            GameObject cartBody = CreatePrimitive(
                PrimitiveType.Cube,
                "CartBody",
                cartRoot.transform,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(3f, 1f, 5f),
                cartMaterial);

            GameObject hunter = CreatePrimitive(
                PrimitiveType.Sphere,
                "SimulatedHunter",
                cartRoot.transform,
                new Vector3(0f, 1.65f, 0f),
                Vector3.one * 1.1f,
                hunterMaterial);

            GameObject hunterAttackPoint = new GameObject("HunterAttackPoint");
            hunterAttackPoint.transform.SetParent(hunter.transform, false);
            hunterAttackPoint.transform.localPosition = new Vector3(0f, 0.25f, 0.65f);

            PrototypeCandle frontLeft = CreateCandle(
                "Candle_FrontLeft", cartRoot.transform, new Vector3(-1.25f, 1f, 2f), candleMaterial, flameMaterial);
            PrototypeCandle frontRight = CreateCandle(
                "Candle_FrontRight", cartRoot.transform, new Vector3(1.25f, 1f, 2f), candleMaterial, flameMaterial);
            PrototypeCandle backLeft = CreateCandle(
                "Candle_BackLeft", cartRoot.transform, new Vector3(-1.25f, 1f, -2f), candleMaterial, flameMaterial);
            PrototypeCandle backRight = CreateCandle(
                "Candle_BackRight", cartRoot.transform, new Vector3(1.25f, 1f, -2f), candleMaterial, flameMaterial);

            references.ConfigurePrototypeReferences(
                cartRoot.transform,
                cartMovement,
                hunter.transform,
                hunterAttackPoint.transform,
                frontLeft,
                frontRight,
                backLeft,
                backRight);

            CreateDesktopCamera(hunter.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Unity could not save the prototype scene at {ScenePath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ValidateOpenPrototypeScene(out string report))
            {
                throw new InvalidOperationException($"Prototype scene was created but failed validation:\n{report}");
            }

            // Validation deliberately exercises mutable state. Reopen the saved scene so the user
            // receives the pristine version with all four candles lit and the cart at its start.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("SimulatedCart");

            Debug.Log($"Created and validated prototype scene: {ScenePath}\n{report}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Monsters prototype ready",
                    "The scene was created and validated. Press Play to run the desktop prototype.",
                    "OK");
            }
        }

        [MenuItem("Tools/Monsters/Validate Prototype Scene")]
        public static void ValidatePrototypeScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Scene not found", $"Create {ScenePath} first.", "OK");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool isValid = ValidateOpenPrototypeScene(out string report);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log($"Prototype validation {(isValid ? "passed" : "failed")}:\n{report}");
            EditorUtility.DisplayDialog(
                isValid ? "Prototype validation passed" : "Prototype validation failed",
                report,
                "OK");
        }

        /// <summary>Entry point used by Unity batch mode during repository verification.</summary>
        public static void CreatePrototypeSceneFromCommandLine()
        {
            CreatePrototypeScene();

            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("Prototype scene was not created.", ScenePath);
            }
        }

        /// <summary>Entry point used by Unity batch mode during repository verification.</summary>
        public static void ValidatePrototypeSceneFromCommandLine()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("Prototype scene does not exist.", ScenePath);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool isValid = ValidateOpenPrototypeScene(out string report);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log(report);

            if (!isValid)
            {
                throw new InvalidOperationException(report);
            }
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.05f, 0.065f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.018f, 0.025f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 125f;
        }

        private static void CreateRoad(Material material)
        {
            CreatePrimitive(
                PrimitiveType.Plane,
                "PrototypeRoad",
                null,
                new Vector3(0f, -0.02f, 0f),
                new Vector3(2f, 1f, 20f),
                material);
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Moonlight");
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.34f, 0.42f, 0.62f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.Soft;
        }

        private static void CreateDesktopCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("PrototypeCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.01f, 0.018f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();

            SmoothFollowCamera followCamera = cameraObject.AddComponent<SmoothFollowCamera>();
            followCamera.ConfigurePrototypeTarget(target);
        }

        private static PrototypeCandle CreateCandle(
            string candleName,
            Transform parent,
            Vector3 localPosition,
            Material candleMaterial,
            Material flameMaterial)
        {
            GameObject candleRoot = new GameObject(candleName);
            candleRoot.transform.SetParent(parent, false);
            candleRoot.transform.localPosition = localPosition;

            CreatePrimitive(
                PrimitiveType.Cylinder,
                "CandleBody",
                candleRoot.transform,
                new Vector3(0f, 0.3f, 0f),
                new Vector3(0.12f, 0.3f, 0.12f),
                candleMaterial);

            GameObject flame = CreatePrimitive(
                PrimitiveType.Sphere,
                "Flame",
                candleRoot.transform,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(0.15f, 0.22f, 0.15f),
                flameMaterial);

            Collider flameCollider = flame.GetComponent<Collider>();
            if (flameCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(flameCollider);
            }

            Light flameLight = flame.AddComponent<Light>();
            flameLight.type = LightType.Point;
            flameLight.color = new Color(1f, 0.32f, 0.04f);
            flameLight.intensity = 2.2f;
            flameLight.range = 5f;
            flameLight.shadows = LightShadows.None;

            GameObject attackPoint = new GameObject("AttackPoint");
            attackPoint.transform.SetParent(candleRoot.transform, false);
            attackPoint.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            PrototypeCandle candle = candleRoot.AddComponent<PrototypeCandle>();
            candle.ConfigurePrototypeReferences(flame, flameLight, attackPoint.transform);
            return candle;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;

            if (parent != null)
            {
                primitive.transform.SetParent(parent, false);
                primitive.transform.localPosition = localPosition;
            }
            else
            {
                primitive.transform.position = localPosition;
            }

            primitive.transform.localScale = localScale;
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static Material GetOrCreateMaterial(string materialName, Color color, bool emissive = false)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existingMaterial != null)
            {
                return existingMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible Lit shader was found for prototype materials.");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static bool ValidateOpenPrototypeScene(out string report)
        {
            List<string> failures = new List<string>();
            GameObject cart = GameObject.Find("SimulatedCart");
            PrototypeSceneReferences references = cart != null
                ? cart.GetComponent<PrototypeSceneReferences>()
                : null;

            Require(cart != null, "SimulatedCart exists.", failures);
            Require(references != null, "PrototypeSceneReferences exists on SimulatedCart.", failures);

            if (references != null)
            {
                Require(references.IsConfigured, "All integration references are assigned.", failures);
                Require(references.CartTransform == cart.transform, "Cart transform reference is correct.", failures);
                Require(references.HunterTransform != null && references.HunterTransform.parent == cart.transform,
                    "SimulatedHunter is a child of SimulatedCart.", failures);
                Require(references.HunterAttackPoint != null &&
                        references.HunterAttackPoint.IsChildOf(references.HunterTransform),
                    "HunterAttackPoint belongs to SimulatedHunter.", failures);

                PrototypeCandle[] candles =
                {
                    references.FrontLeftCandle,
                    references.FrontRightCandle,
                    references.BackLeftCandle,
                    references.BackRightCandle
                };

                foreach (PrototypeCandle candle in candles)
                {
                    if (candle == null)
                    {
                        failures.Add("A candle reference is null.");
                        continue;
                    }

                    Require(candle.transform.parent == cart.transform,
                        $"{candle.name} remains attached to SimulatedCart.", failures);
                    Require(candle.IsLit, $"{candle.name} starts lit.", failures);
                    Require(candle.FlameVisual != null && candle.FlameVisual.activeSelf,
                        $"{candle.name} flame is visible initially.", failures);
                    Require(candle.FlameLight != null && candle.FlameLight.enabled,
                        $"{candle.name} light is enabled initially.", failures);
                    Require(candle.AttackPoint != null,
                        $"{candle.name} attack point is assigned.", failures);

                    int eventCount = 0;
                    Action<PrototypeCandle> handler = _ => eventCount++;
                    candle.Extinguished += handler;
                    bool firstRequest = candle.RequestExtinguish();
                    bool secondRequest = candle.RequestExtinguish();
                    candle.Extinguished -= handler;

                    Require(firstRequest && !secondRequest && eventCount == 1,
                        $"{candle.name} extinguishes once and emits one event.", failures);
                    Require(!candle.IsLit && !candle.FlameVisual.activeSelf && !candle.FlameLight.enabled,
                        $"{candle.name} hides flame and disables light when extinguished.", failures);
                }

                SimulatedCartMovement movement = references.CartMovement;
                if (movement != null)
                {
                    Vector3 initialPosition = movement.transform.position;
                    movement.StartMovement();
                    movement.Advance(1f);
                    Vector3 expectedPosition = initialPosition + movement.transform.forward * movement.Speed;
                    Require(Vector3.Distance(movement.transform.position, expectedPosition) < 0.001f,
                        "Cart advances in a straight line at its configured speed.", failures);

                    movement.StopMovement();
                    Vector3 stoppedPosition = movement.transform.position;
                    movement.Advance(1f);
                    Require(Vector3.Distance(movement.transform.position, stoppedPosition) < 0.001f,
                        "Cart can be stopped.", failures);
                    movement.transform.position = initialPosition;
                }
            }

            GameObject cartBody = GameObject.Find("CartBody");
            Require(cartBody != null && Approximately(cartBody.transform.localScale, new Vector3(3f, 1f, 5f)),
                "CartBody uses the requested approximate 3 x 1 x 5 dimensions.", failures);

            GameObject road = GameObject.Find("PrototypeRoad");
            Require(road != null, "Prototype road exists.", failures);

            SmoothFollowCamera followCamera = UnityEngine.Object.FindAnyObjectByType<SmoothFollowCamera>();
            Require(followCamera != null && references != null && cart != null &&
                    followCamera.Target == references.HunterTransform &&
                    references.HunterTransform.IsChildOf(cart.transform),
                "Desktop camera follows the hunter head while the hunter remains attached to SimulatedCart.", failures);

            if (failures.Count == 0)
            {
                report = "All prototype configuration and deterministic behavior checks passed.";
                return true;
            }

            report = string.Join("\n", failures);
            return false;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Vector3.SqrMagnitude(left - right) < 0.0001f;
        }

        private static void Require(bool condition, string successDescription, ICollection<string> failures)
        {
            if (!condition)
            {
                failures.Add($"FAILED: {successDescription}");
            }
        }
    }
}
