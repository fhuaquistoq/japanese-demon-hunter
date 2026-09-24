using System.Collections.Generic;
using System.IO;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.OVR.Editor.QuickActions;
using Reins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>
    /// Builds the complete playable VR scene (hand-tracked reins, monsters, combat, night lighting,
    /// curved road and the kingdom at the end) with editor tooling only. Re-runnable and
    /// non-destructive: if the scene already exists it is opened, never overwritten. It never
    /// touches the team's SampleScene.
    /// </summary>
    public static class PlayableSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/JapanDemonHunter.unity";
        private const string GameMaterialFolder = "Assets/Materials/Game";

        private const string CameraRigPrefabPath =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string CarriagePrefabPath = "Assets/Prefabs/Carriage/CarriagePrototype.prefab";
        private const string RopeProxyPrefabPath = "Assets/Prefabs/Interaction/RopeProxy.prefab";
        private const string RopeMaterialPath = "Assets/Materials/Prototype/Mat_RopeProxy.mat";
        private const string ZombiePrefabPath = "Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab";
        private const string BatPrefabPath = "Assets/Art/Monsters/Bat/Prefabs/BatDemon.prefab";
        private const string GiantPrefabPath = "Assets/Art/Monsters/GiantZombie/Prefabs/GiantZombie.prefab";
        private const string HorseModelPath = "Assets/Art/Monsters/Horse/Horse.fbx";
        private const string KnifeModelPath = "Assets/Art/Monsters/Knife/Knife.fbx";
        private const string RockModelPath = "Assets/Art/Monsters/Rock/Resource_Rock_2.fbx";
        private const string AccelerateClipPath = "Assets/Art/Audio/latigo-avanza.mp3";
        private const string BrakeClipPath = "Assets/Art/Audio/latigo-frena.mp3";
        private const string LaneClipPath = "Assets/Art/Audio/jalar latigo para girar.mp3";
        private const string DefaultVolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        private static readonly string[] TreeModelPaths =
        {
            "Assets/Art/Monsters/Tree/CommonTree_1.fbx",
            "Assets/Art/Monsters/Tree/CommonTree_3.fbx",
            "Assets/Art/Monsters/Tree/CommonTree_4.fbx",
            "Assets/Art/Monsters/Tree/CommonTree_5.fbx"
        };

        // 40 tiles of 18 m: a finite ride that ends at the fortified kingdom.
        private const int TotalRoadTiles = 40;
        // Horse.fbx measures 4.81 m from hooves to ears before scaling, and its head points to +Z,
        // so every instance is turned 180 degrees to face the direction of travel.
        private const float HorseTargetHeight = 2.35f;
        private const float HorseYawDegrees = 180f;
        private const float KnifeTargetLength = 0.85f;
        private const float PunchDamage = 5f;
        private const float KnifeDamage = 12f;
        private const float SecondsBeforeFirstMonster = 38f;
        private const float MonsterSpawnInterval = 11f;

        // The rope hangs down to the carriage deck instead of floating at chest height.
        private static readonly Vector3 LeftReinRest = new Vector3(-0.34f, 0.85f, -0.18f);
        private static readonly Vector3 RightReinRest = new Vector3(0.34f, 0.85f, -0.18f);

        // Invisible, overlapping grab volumes laid along each side of the rope.
        private const int RopeGrabZonesPerRein = 6;
        private const float RopeGrabZoneRadius = 0.08f;
        private const float RopeGrabZoneLength = 0.42f;

        /// <summary>Set by the command line entry points so dialogs never block an automated run.</summary>
        private static bool suppressDialogs;

        /// <summary>Set by the rebuild entry point so a generated scene can be regenerated safely.</summary>
        private static bool overwriteExisting;

        [MenuItem("Tools/Game/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            if (File.Exists(ScenePath) && !overwriteExisting)
            {
                bool shouldOpen = Application.isBatchMode || suppressDialogs || EditorUtility.DisplayDialog(
                    "The playable scene already exists",
                    $"{ScenePath} already exists. It will be opened without being overwritten.",
                    "Open existing scene",
                    "Cancel");
                if (shouldOpen)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    Debug.Log($"Opened existing playable scene without modifying it: {ScenePath}");
                }

                return;
            }

            if (Application.isBatchMode || suppressDialogs)
            {
                // Never discard unsaved work: this creates a brand new empty scene next.
                if (!EditorSceneManager.SaveOpenScenes())
                {
                    throw new System.InvalidOperationException(
                        "Unsaved changes could not be saved before creating the playable scene.");
                }
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (overwriteExisting)
            {
                BackUpExistingScene();
            }

            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "Game");

            Material lampMaterial = GetOrCreateLitMaterial("Lamp_Flame", new Color(1f, 0.60f, 0.22f), true);
            Material overlayMaterial = GetOrCreateOverlayMaterial("Defeat_Overlay", new Color(0.30f, 0.01f, 0.01f, 0f));
            Material kingdomMaterial = GetOrCreateLitMaterial("Kingdom_Stone", new Color(0.22f, 0.21f, 0.25f));
            Material kingdomRoofMaterial = GetOrCreateLitMaterial("Kingdom_Roof", new Color(0.12f, 0.05f, 0.05f));
            Material nightSky = GetOrCreateNightSky("Sky_Night");
            Material ropeMaterial = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "JapanDemonHunter";

            ConfigureEnvironment(nightSky);
            CreateMoonlight();
            CreateGlobalVolume();

            var vehicleRoot = new GameObject("VehicleRoot");
            CarriageMotor motor = vehicleRoot.AddComponent<CarriageMotor>();

            GameObject carriage = InstantiatePrefab(CarriagePrefabPath, vehicleRoot.transform, Vector3.zero);
            AddRealHorses(carriage.transform, motor, out Transform leftHorseHead, out Transform rightHorseHead);
            if (leftHorseHead == null)
            {
                leftHorseHead = FindChild(carriage.transform, "HorsePlaceholders/Horse_Left/Horse_Head");
            }

            if (rightHorseHead == null)
            {
                rightHorseHead = FindChild(carriage.transform, "HorsePlaceholders/Horse_Right/Horse_Head");
            }

            Transform rearAnchor = FindChild(carriage.transform, "RopeAnchors/Anchor_CarriageRear");

            List<PrototypeCandle> candleLamps = CreateCarriageLamps(vehicleRoot.transform, lampMaterial);

            GameObject roadRoot = CreateRoadRoot(vehicleRoot.transform, nightSky);
            ForestRoad road = roadRoot.GetComponent<ForestRoad>();

            CreateGround(vehicleRoot.transform);

            ReinHandle leftRein = CreateRein(
                vehicleRoot.transform, "Left_ReinPin", "RopeGrab_L", LeftReinRest, 0, leftHorseHead, ropeMaterial);
            ReinHandle rightRein = CreateRein(
                vehicleRoot.transform, "Right_ReinPin", "RopeGrab_R", RightReinRest, 1, rightHorseHead, ropeMaterial);
            CreateClosedReinLoop(vehicleRoot.transform, leftRein, rightRein, leftHorseHead, rightHorseHead, ropeMaterial);
            WireObject(motor, "leftRein", leftRein);
            WireObject(motor, "rightRein", rightRein);
            SetFloat(motor, "minimumLoadSpeedMultiplier", 0.3f);
            SetFloat(motor, "maximumYawRate", 25f);
            SetBool(motor, "followRoadCurvature", true);
            SetFloat(motor, "maximumSpeed", 5f);
            SetFloat(motor, "startingSpeed", 3.5f);
            SetFloat(motor, "accelerationPerStroke", 1.6f);
            SetFloat(motor, "brakingPerPull", 0.9f);
            SetFloat(motor, "coastingDeceleration", 0.7f);
            WireGestureAudio(vehicleRoot.transform, motor);

            Camera centerEye = CreateVrRig(vehicleRoot.transform);
            Transform headAnchor = centerEye != null ? centerEye.transform : vehicleRoot.transform;
            Transform hunterAttackPoint = CreateChild(headAnchor, "HunterAttackPoint", new Vector3(0f, 0f, 0.35f));

            CreateCombat(vehicleRoot.transform, centerEye);
            GameObject giantSpawnerObject = CreateMonsterSystems(
                vehicleRoot.transform, headAnchor, hunterAttackPoint, candleLamps, rearAnchor);
            GiantZombieSpawner giantSpawner = giantSpawnerObject.GetComponent<GiantZombieSpawner>();
            MonsterSpawner spawner = giantSpawnerObject.GetComponent<MonsterSpawner>();
            WireSoundscape(vehicleRoot, motor, spawner, giantSpawner);

            GameObject kingdom = BuildKingdom(road, kingdomMaterial, kingdomRoofMaterial, lampMaterial, out Light[] kingdomLights);
            GameObject victoryBanner = BuildVictoryBanner(kingdom.transform, lampMaterial);

            LevelVictoryController victory = giantSpawnerObject.AddComponent<LevelVictoryController>();
            victory.Configure(road, motor, spawner, giantSpawner);
            victory.ConfigurePresentation(victoryBanner, kingdomLights);
            CreateDefeatEffect(giantSpawner, centerEye, overlayMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new System.InvalidOperationException($"Unity could not save the playable scene at {ScenePath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnsureSceneInBuildSettings();

            if (!ValidateOpenScene(out string report))
            {
                throw new System.InvalidOperationException(
                    $"The playable scene was created but failed validation:\n{report}");
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("VehicleRoot");
            Debug.Log($"Created and validated the playable scene: {ScenePath}\n{report}");
            if (!Application.isBatchMode && !suppressDialogs)
            {
                EditorUtility.DisplayDialog(
                    "Playable scene ready",
                    "JapanDemonHunter.unity was created and validated.\n\n" +
                    "Activate the Meta XR Simulator (Device = Meta Quest 2), press Play and grab both rein handles.\n" +
                    "Launch the simulator with the XR Simulator app before pressing Play.",
                    "OK");
            }
        }

        /// <summary>
        /// Regenerates the playable scene. The scene is entirely produced by this tool, so
        /// regenerating is safe; the previous file is copied outside the project first.
        /// </summary>
        [MenuItem("Tools/Game/Rebuild Playable Scene (backup + overwrite)")]
        public static void RebuildPlayableScene()
        {
            overwriteExisting = true;
            try
            {
                CreatePlayableScene();
            }
            finally
            {
                overwriteExisting = false;
            }
        }

        /// <summary>Batch entry point: regenerates the playable scene after backing it up.</summary>
        public static void RebuildPlayableSceneFromCommandLine()
        {
            suppressDialogs = true;
            overwriteExisting = true;
            try
            {
                CreatePlayableScene();
            }
            finally
            {
                overwriteExisting = false;
                suppressDialogs = false;
            }
        }

        private static void BackUpExistingScene()
        {
            if (!File.Exists(ScenePath))
            {
                return;
            }

            string backupPath = Path.Combine(
                Application.temporaryCachePath,
                $"JapanDemonHunter_{System.DateTime.Now:yyyyMMdd_HHmmss}.unity");
            File.Copy(ScenePath, backupPath, true);
            Debug.Log($"Previous playable scene backed up to {backupPath} before regenerating.");
        }

        [MenuItem("Tools/Game/Validate Playable Scene")]
        public static void ValidatePlayableSceneMenu()
        {
            if (!File.Exists(ScenePath))
            {
                if (!suppressDialogs)
                {
                    EditorUtility.DisplayDialog("Scene not found", $"Create {ScenePath} first.", "OK");
                }

                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool valid = ValidateOpenScene(out string report);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"Playable scene validation {(valid ? "passed" : "failed")}:\n{report}");
            if (!Application.isBatchMode && !suppressDialogs)
            {
                EditorUtility.DisplayDialog(valid ? "Validation passed" : "Validation failed", report, "OK");
            }
        }

        /// <summary>Batch entry point. Never shows dialogs.</summary>
        public static void CreatePlayableSceneFromCommandLine()
        {
            suppressDialogs = true;
            try
            {
                CreatePlayableScene();
            }
            finally
            {
                suppressDialogs = false;
            }

            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("The playable scene was not created.", ScenePath);
            }
        }

        /// <summary>Batch entry point. Never shows dialogs.</summary>
        public static void ValidatePlayableSceneFromCommandLine()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("The playable scene does not exist.", ScenePath);
            }

            suppressDialogs = true;
            try
            {
                // Opening the scene single-mode would discard other scenes' unsaved work.
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                bool valid = ValidateOpenScene(out string report);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log(report);
                if (!valid)
                {
                    throw new System.InvalidOperationException(report);
                }
            }
            finally
            {
                suppressDialogs = false;
            }
        }

        // ---------------------------------------------------------------- environment

        private static void ConfigureEnvironment(Material nightSky)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.030f, 0.038f, 0.060f);
            RenderSettings.skybox = nightSky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.012f, 0.016f, 0.026f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 85f;
        }

        private static void CreateMoonlight()
        {
            var moonObject = new GameObject("Moonlight");
            moonObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.42f, 0.50f, 0.74f);
            moon.intensity = 0.45f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;
        }

        private static void CreateGlobalVolume()
        {
            var volumeObject = new GameObject("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumeProfilePath);
        }

        // ---------------------------------------------------------------- carriage

        private static void AddRealHorses(
            Transform carriage, MonoBehaviour speedSource, out Transform leftHead, out Transform rightHead)
        {
            leftHead = null;
            rightHead = null;

            GameObject horseModel = AssetDatabase.LoadAssetAtPath<GameObject>(HorseModelPath);
            if (horseModel == null)
            {
                Debug.LogWarning($"Horse model not found at {HorseModelPath}: the placeholder horses stay visible.");
                return;
            }

            HidePlaceholder(carriage, "HorsePlaceholders/Horse_Left/Horse_Body");
            HidePlaceholder(carriage, "HorsePlaceholders/Horse_Right/Horse_Body");
            leftHead = AddHorse(carriage, "HorsePlaceholders/Horse_Left", horseModel, "Horse_Model_Left", speedSource);
            rightHead = AddHorse(carriage, "HorsePlaceholders/Horse_Right", horseModel, "Horse_Model_Right", speedSource);
        }

        private static void HidePlaceholder(Transform carriage, string path)
        {
            Transform placeholder = FindChild(carriage, path);
            if (placeholder == null)
            {
                return;
            }

            foreach (Renderer renderer in placeholder.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        /// <summary>
        /// Places a real horse: turned to face the direction of travel, scaled up, stood on the road
        /// surface and centred on its shaft node. It returns the anchor the reins attach to, taken
        /// from the actual model head instead of the placeholder.
        /// </summary>
        private static Transform AddHorse(
            Transform carriage, string parentPath, GameObject model, string name, MonoBehaviour speedSource)
        {
            Transform parent = FindChild(carriage, parentPath);
            if (parent == null)
            {
                Debug.LogWarning($"Could not find {parentPath} to attach the real horse model.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.Euler(0f, HorseYawDegrees, 0f);

            if (!TryGetRendererBounds(instance, out Bounds bounds) || bounds.size.y <= 0.0001f)
            {
                return null;
            }

            instance.transform.localScale = Vector3.one * (HorseTargetHeight / bounds.size.y);
            if (!TryGetRendererBounds(instance, out bounds))
            {
                return null;
            }

            // Hooves on the road surface, body centred on the shaft node.
            instance.transform.position += new Vector3(
                parent.position.x - bounds.center.x,
                0f - bounds.min.y,
                parent.position.z - bounds.center.z);

            // Keep the hind legs clear of the carriage floor even at the longest gallop stride.
            Transform floor = FindChild(carriage, "CarriageFloor");
            if (floor != null && TryGetRendererBounds(floor.gameObject, out Bounds floorBounds) &&
                TryGetRendererBounds(instance, out bounds))
            {
                const float gallopClearance = 0.85f;
                float overlap = bounds.max.z - (floorBounds.min.z - gallopClearance);
                if (overlap > 0f) instance.transform.position += Vector3.back * overlap;
            }

            HorseLocomotionDriver driver = instance.AddComponent<HorseLocomotionDriver>();
            driver.Configure(speedSource, instance.transform, 0.15f, 3.2f, 2.4f);
            return CreateHeadAnchor(instance, parent);
        }

        private static Transform CreateHeadAnchor(GameObject horse, Transform parent)
        {
            if (!TryGetRendererBounds(horse, out Bounds bounds))
            {
                return null;
            }

            Vector3 localMin = parent.InverseTransformPoint(bounds.min);
            Vector3 localMax = parent.InverseTransformPoint(bounds.max);
            float headZ = Mathf.Min(localMin.z, localMax.z);
            float depth = Mathf.Abs(localMax.z - localMin.z);
            float headY = Mathf.Lerp(localMin.y, localMax.y, 0.88f);
            var parentLocal = new Vector3(
                (localMin.x + localMax.x) * 0.5f, headY, headZ + depth * 0.12f);

            // Parent the anchor to the horse itself so it follows the procedural gait instead of
            // staying pinned while the head bobs.
            Transform anchor = CreateChild(horse.transform, "ReinHeadAnchor", Vector3.zero);
            anchor.position = parent.TransformPoint(parentLocal);
            return anchor;
        }

        private static List<PrototypeCandle> CreateCarriageLamps(
            Transform vehicleRoot, Material lampMaterial)
        {
            var candles = new List<PrototypeCandle>
            {
                CreateCandleLamp(vehicleRoot, "Lamp_FrontLeft", new Vector3(-1.28f, 1.55f, -2.30f), lampMaterial),
                CreateCandleLamp(vehicleRoot, "Lamp_FrontRight", new Vector3(1.28f, 1.55f, -2.30f), lampMaterial)
            };

            CreateSimpleLamp(vehicleRoot, "Lamp_RearLeft", new Vector3(-1.05f, 1.45f, 1.75f), lampMaterial);
            CreateSimpleLamp(vehicleRoot, "Lamp_RearRight", new Vector3(1.05f, 1.45f, 1.75f), lampMaterial);
            return candles;
        }

        private static PrototypeCandle CreateCandleLamp(
            Transform parent, string name, Vector3 localPosition, Material lampMaterial)
        {
            GameObject root = CreateChild(parent, name, localPosition).gameObject;
            CreatePrimitive(PrimitiveType.Cylinder, "LampBody", root.transform,
                new Vector3(0f, 0.16f, 0f), new Vector3(0.06f, 0.16f, 0.06f), lampMaterial);
            GameObject flame = CreatePrimitive(PrimitiveType.Sphere, "Flame", root.transform,
                new Vector3(0f, 0.40f, 0f), new Vector3(0.20f, 0.26f, 0.20f), lampMaterial);
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            Light light = AddPointLight(flame.transform, new Color(1f, 0.55f, 0.18f), 2.6f, 13f);

            Transform attackPoint = CreateChild(root.transform, "AttackPoint", new Vector3(0f, 0.7f, 0f));
            PrototypeCandle candle = root.AddComponent<PrototypeCandle>();
            candle.ConfigurePrototypeReferences(flame, light, attackPoint);
            return candle;
        }

        private static void CreateSimpleLamp(
            Transform parent, string name, Vector3 localPosition, Material lampMaterial)
        {
            GameObject root = CreateChild(parent, name, localPosition).gameObject;
            CreatePrimitive(PrimitiveType.Cylinder, "LampBody", root.transform,
                new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.14f, 0.05f), lampMaterial);
            GameObject glass = CreatePrimitive(PrimitiveType.Sphere, "Glass", root.transform,
                new Vector3(0f, 0.34f, 0f), new Vector3(0.17f, 0.22f, 0.17f), lampMaterial);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            AddPointLight(glass.transform, new Color(1f, 0.58f, 0.22f), 2.2f, 12f);
        }

        // ---------------------------------------------------------------- reins

        /// <summary>
        /// Builds one rein. There is no handle: a bare pin (the point the rope simulation follows)
        /// plus a row of invisible grab volumes spread along the rope, so the player can take the
        /// rope anywhere instead of hunting for a grip.
        /// </summary>
        private static ReinHandle CreateRein(
            Transform vehicleRoot,
            string pinName,
            string zonePrefix,
            Vector3 restLocalPosition,
            int handedness,
            Transform horseHead,
            Material ropeMaterial)
        {
            Transform pin = CreateChild(vehicleRoot, pinName, restLocalPosition);
            LineRenderer tether = CreateTether(vehicleRoot, pinName + "_Tether", ropeMaterial);
            List<HandGrabInteractable> zones = CreateRopeGrabZones(vehicleRoot, zonePrefix, restLocalPosition);

            ReinHandle rein = pin.gameObject.AddComponent<ReinHandle>();
            SetInt(rein, "expectedHand", handedness);
            SetVector3(rein, "restLocalPosition", restLocalPosition);
            SetObjectArray(rein, "grabPoints", zones.ToArray());
            WireObject(rein, "tether", tether);
            WireObject(rein, "horseHead", horseHead);
            return rein;
        }

        /// <summary>Overlapping, invisible grab volumes laid along the reachable part of the rope.</summary>
        private static List<HandGrabInteractable> CreateRopeGrabZones(
            Transform vehicleRoot, string prefix, Vector3 pinLocalPosition)
        {
            var zones = new List<HandGrabInteractable>();
            float side = Mathf.Sign(pinLocalPosition.x);
            var start = new Vector3(side * Mathf.Abs(pinLocalPosition.x) * 1.55f, pinLocalPosition.y + 0.32f, pinLocalPosition.z - 1.15f);
            var end = new Vector3(side * Mathf.Abs(pinLocalPosition.x) * 0.82f, pinLocalPosition.y - 0.05f, pinLocalPosition.z + 0.72f);

            for (var i = 0; i < RopeGrabZonesPerRein; i++)
            {
                float t = RopeGrabZonesPerRein == 1 ? 0f : i / (float)(RopeGrabZonesPerRein - 1);
                Vector3 position = Vector3.Lerp(start, end, t);

                GameObject zone = InstantiatePrefab(RopeProxyPrefabPath, vehicleRoot, position);
                zone.name = $"{prefix}{i}";
                // The rope grip prefab is squashed and visible: normalise it and hide it so the rope
                // itself is what the player reaches for.
                zone.transform.localScale = Vector3.one;
                zone.transform.localRotation = Quaternion.identity;
                foreach (Renderer renderer in zone.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }

                CapsuleCollider capsule = zone.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.radius = RopeGrabZoneRadius;
                    capsule.height = RopeGrabZoneLength;
                    capsule.direction = 2;
                    capsule.center = Vector3.zero;
                }

                MeshCollider hull = zone.GetComponent<MeshCollider>();
                if (hull != null)
                {
                    hull.enabled = false;
                }

                Rigidbody body = zone.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                HandGrabInteractable interactable = zone.GetComponentInChildren<HandGrabInteractable>();
                if (interactable != null)
                {
                    zones.Add(interactable);
                }
            }

            return zones;
        }

        private static LineRenderer CreateTether(Transform parent, string name, Material material)
        {
            var tetherObject = new GameObject(name);
            tetherObject.transform.SetParent(parent, false);
            LineRenderer line = tetherObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.025f;
            line.numCapVertices = 4;
            if (material != null)
            {
                line.sharedMaterial = material;
            }

            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward);
            return line;
        }

        private static void CreateClosedReinLoop(
            Transform vehicleRoot,
            ReinHandle leftRein,
            ReinHandle rightRein,
            Transform leftHorseHead,
            Transform rightHorseHead,
            Material ropeMaterial)
        {
            var loopObject = new GameObject("ClosedReinLoop");
            loopObject.transform.SetParent(vehicleRoot, false);
            LineRenderer line = loopObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.widthMultiplier = 0.025f;
            line.numCapVertices = 4;
            if (ropeMaterial != null)
            {
                line.sharedMaterial = ropeMaterial;
            }

            ClosedReinLoop loop = loopObject.AddComponent<ClosedReinLoop>();
            WireObject(loop, "vehicleRoot", vehicleRoot);
            WireObject(loop, "leftHorseHead", leftHorseHead);
            WireObject(loop, "leftGrip", leftRein.transform);
            WireObject(loop, "rightGrip", rightRein.transform);
            WireObject(loop, "rightHorseHead", rightHorseHead);
            WireObject(loop, "rope", line);
        }

        /// <summary>
        /// The carriage plays a whip clip when a rein gesture is detected, which tells apart a
        /// gesture that was not recognised from one that was.
        /// </summary>
        private static void WireGestureAudio(Transform vehicleRoot, CarriageMotor motor)
        {
            AudioSource source = vehicleRoot.gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = vehicleRoot.gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 35f;
            source.rolloffMode = AudioRolloffMode.Linear;

            WireObject(motor, "gestureAudioSource", source);
            WireObject(motor, "accelerateClip", AssetDatabase.LoadAssetAtPath<AudioClip>(AccelerateClipPath));
            WireObject(motor, "brakeClip", AssetDatabase.LoadAssetAtPath<AudioClip>(BrakeClipPath));
            WireObject(motor, "laneClip", AssetDatabase.LoadAssetAtPath<AudioClip>(LaneClipPath));
        }

        /// <summary>Applies the new atmosphere to the existing scene without regenerating it.</summary>
        [MenuItem("Tools/Game/Upgrade Playable Scene Atmosphere")]
        public static void UpgradePlayableSceneAtmosphere()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"Playable scene not found: {ScenePath}");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            ForestRoad road = Object.FindAnyObjectByType<ForestRoad>();
            if (road != null)
            {
                SetFloatIfMissing(road, "heightAmplitude", 0.55f);
                SetFloatIfMissing(road, "heightWavelength", 90f);
                SetFloatIfMissing(road, "forkDivergence", 6f);
                SetIntIfMissing(road, "forestRows", 3);
                SetIntIfMissing(road, "treesPerRow", 5);
            }

            CarriageMotor motor = Object.FindAnyObjectByType<CarriageMotor>();
            MonsterSpawner spawner = Object.FindAnyObjectByType<MonsterSpawner>();
            GiantZombieSpawner giant = Object.FindAnyObjectByType<GiantZombieSpawner>();
            if (spawner != null && Mathf.Approximately(spawner.InitialSpawnDelay, 25f) &&
                Mathf.Approximately(spawner.SpawnInterval, 7f))
            {
                Undo.RecordObject(spawner, "Delay monster spawns");
                spawner.ConfigureTiming(false, SecondsBeforeFirstMonster, MonsterSpawnInterval);
            }

            if (giant != null) SetFloatIfMissing(giant, "giantChaseSpeed", 2.2f);
            GameObject vehicle = GameObject.Find("VehicleRoot");
            if (vehicle != null)
            {
                MoveHorsesClearOfCarriage();
                MoveAttachmentPointsBehindCart(vehicle.transform);
                if (motor != null && spawner != null && giant != null &&
                    vehicle.GetComponent<GameSoundscape>() == null)
                {
                    WireSoundscape(vehicle, motor, spawner, giant);
                }
                if (motor != null) SetObjectIfMissing(motor, "laneClip", LoadAudio("jalar latigo para girar.mp3"));
            }

            if (Mathf.Approximately(RenderSettings.fogEndDistance, 85f))
                RenderSettings.fogEndDistance = 65f;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Playable scene atmosphere upgraded in place; existing custom Inspector values were kept.");
        }

        private static void MoveHorsesClearOfCarriage()
        {
            GameObject floor = GameObject.Find("CarriageFloor");
            if (floor == null || !TryGetRendererBounds(floor, out Bounds floorBounds)) return;
            foreach (string horseName in new[] { "Horse_Model_Left", "Horse_Model_Right" })
            {
                GameObject horse = GameObject.Find(horseName);
                if (horse == null || !TryGetRendererBounds(horse, out Bounds bounds)) continue;
                float overlap = bounds.max.z - (floorBounds.min.z - 0.85f);
                if (overlap <= 0f) continue;
                Undo.RecordObject(horse.transform, "Advance horse clear of carriage");
                horse.transform.position += Vector3.back * overlap;
            }
        }

        private static void MoveAttachmentPointsBehindCart(Transform vehicle)
        {
            GameObject floor = GameObject.Find("CarriageFloor");
            if (floor == null || !TryGetRendererBounds(floor, out Bounds bounds)) return;
            float rearZ = vehicle.InverseTransformPoint(bounds.max).z + 0.8f;
            foreach (string name in new[] { "Attach_RearCenter", "Attach_LeftRear", "Attach_RightRear",
                                          "Attach_LeftFront", "Attach_RightFront" })
            {
                GameObject point = GameObject.Find(name);
                if (point == null) continue;
                Vector3 local = vehicle.InverseTransformPoint(point.transform.position);
                if (local.z < rearZ)
                {
                    Undo.RecordObject(point.transform, "Keep monster attachment behind carriage");
                    local.z = rearZ;
                    point.transform.position = vehicle.TransformPoint(local);
                }
            }
        }

        private static void SetFloatIfMissing(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null && property.floatValue <= 0f)
            {
                property.floatValue = value;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void SetIntIfMissing(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null && property.intValue <= 0)
            {
                property.intValue = value;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void SetObjectIfMissing(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void WireSoundscape(GameObject vehicleRoot, CarriageMotor motor,
            MonsterSpawner spawner, GiantZombieSpawner giantSpawner)
        {
            GameSoundscape soundscape = vehicleRoot.GetComponent<GameSoundscape>();
            if (soundscape == null) soundscape = vehicleRoot.AddComponent<GameSoundscape>();
            WireObject(soundscape, "speedSource", motor);
            WireObject(soundscape, "monsterSpawner", spawner);
            WireObject(soundscape, "giantSpawner", giantSpawner);
            WireObject(soundscape, "gallopClip", LoadAudio("caballo_galope.mp3"));
            WireObject(soundscape, "horseBreathClip", LoadAudio("caballo_soplido.mp3"));
            WireObject(soundscape, "horseNeighClip", LoadAudio("caballo_relinche.mp3"));
            WireObject(soundscape, "zombieVoiceClip", LoadAudio("zombie_sonidos.mp3"));
            WireObject(soundscape, "zombieDeathClip", LoadAudio("zombie_muerte_Mahaha.mp3"));
            WireObject(soundscape, "batWingsClip", LoadAudio("Murcielago/murcielago_aleteo.mp3"));
            WireObject(soundscape, "batDeathClip", LoadAudio("Murcielago/murcielago_muerte.mp3"));
            WireObject(soundscape, "hitClip", LoadAudio("golpe.mp3"));
            WireObject(soundscape, "giantFootstepsClip", LoadAudio("pisadas_gigante.mp3"));
        }

        private static AudioClip LoadAudio(string relativePath)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/" + relativePath);
        }

        // ---------------------------------------------------------------- road and ground
        private static GameObject CreateRoadRoot(Transform vehicleRoot, Material nightSky)
        {
            var roadRoot = new GameObject("RoadRoot");
            ForestRoad road = roadRoot.AddComponent<ForestRoad>();

            SetInt(road, "tileCount", 8);
            SetFloat(road, "tileLength", 18f);
            SetFloat(road, "roadWidth", 9.6f);
            SetFloat(road, "laneWidth", 2.8f);
            SetFloat(road, "curvatureScale", 0.8f);
            SetFloat(road, "turnRadius", 100f);
            SetFloat(road, "heightAmplitude", 0.55f);
            SetFloat(road, "heightWavelength", 90f);
            SetFloat(road, "forkDivergence", 6f);
            SetInt(road, "forestRows", 3);
            SetInt(road, "treesPerRow", 5);
            SetInt(road, "totalTiles", TotalRoadTiles);
            WireObject(road, "vehicleRoot", vehicleRoot);
            WireObject(road, "rockPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath));
            SetObjectArray(road, "treePrefabs", LoadAssets(TreeModelPaths));
            SetFloat(road, "treeHeight", 7.5f);
            return roadRoot;
        }

        private static void CreateGround(Transform vehicleRoot)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.transform.position = new Vector3(0f, -0.01f, 0f);
            ground.AddComponent<MonsterGroundSurface>();
            CartFollowGround follow = ground.AddComponent<CartFollowGround>();
            follow.Configure(vehicleRoot, new Vector3(0f, -0.01f, 0f));
        }

        // ---------------------------------------------------------------- VR rig

        private static Camera CreateVrRig(Transform vehicleRoot)
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            if (rigPrefab == null)
            {
                Debug.LogError($"The OVR camera rig prefab was not found at {CameraRigPrefabPath}.");
                return null;
            }

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, vehicleRoot);
            rig.name = "OVRCameraRig";
            rig.transform.localPosition = new Vector3(0f, 0.695f, 0f);
            rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            ConfigureOvrManager(rig);
            OVRQuickActionsAPI.AddOVRInteractionRig(false);

            Camera centerEye = FindCenterEyeCamera(rig);
            if (centerEye == null)
            {
                Debug.LogWarning("Could not find the center eye camera under the camera rig.");
            }
            else
            {
                centerEye.clearFlags = CameraClearFlags.Skybox;
            }

            return centerEye;
        }

        private static void ConfigureOvrManager(GameObject rig)
        {
            foreach (MonoBehaviour behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "OVRManager")
                {
                    continue;
                }

                var serialized = new SerializedObject(behaviour);
                SetSerializedInt(serialized, "_trackingOriginType", 1);
                SetSerializedBool(serialized, "_enableDynamicResolution", true);
                SetSerializedInt(serialized, "controllerDrivenHandPosesType", 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogWarning("No OVRManager was found on the camera rig; tracking origin stays at its default.");
        }

        private static Camera FindCenterEyeCamera(GameObject rig)
        {
            Transform centerEye = FindChild(rig.transform, "TrackingSpace/CenterEyeAnchor");
            if (centerEye == null)
            {
                centerEye = FindChild(rig.transform, "CenterEyeAnchor");
            }

            if (centerEye != null)
            {
                Camera camera = centerEye.GetComponent<Camera>();
                if (camera != null)
                {
                    return camera;
                }
            }

            return rig.GetComponentInChildren<Camera>(true);
        }

        // ---------------------------------------------------------------- combat

        private static void CreateCombat(Transform vehicleRoot, Camera centerEye)
        {
            HandStrikeController strikes = vehicleRoot.gameObject.GetComponent<HandStrikeController>();
            if (strikes == null)
            {
                strikes = vehicleRoot.gameObject.AddComponent<HandStrikeController>();
            }

            strikes.Configure(vehicleRoot, PunchDamage, 0.16f, 2f, ~0);
            CreateKnife(vehicleRoot);
        }

        private static void CreateKnife(Transform vehicleRoot)
        {
            GameObject knife = InstantiatePrefab(RopeProxyPrefabPath, vehicleRoot, new Vector3(0.95f, 0.74f, 0.35f));
            knife.name = "Knife";
            // The rope grip prefab is squashed (0.04, 0.2, 0.04), which would distort any model
            // parented under it. It is normalised here and given a knife shaped grab volume instead.
            knife.transform.localScale = Vector3.one;

            Rigidbody body = knife.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            foreach (Renderer renderer in knife.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            CapsuleCollider capsule = knife.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = 0.05f;
                capsule.height = 0.8f;
                capsule.direction = 2;
                capsule.center = new Vector3(0f, 0f, 0.1f);
            }

            MeshCollider ropeHull = knife.GetComponent<MeshCollider>();
            if (ropeHull != null)
            {
                ropeHull.enabled = false;
            }

            Transform bladeTip = CreateChild(knife.transform, "BladeTip", new Vector3(0f, 0f, 0.58f));
            GameObject knifeModel = AssetDatabase.LoadAssetAtPath<GameObject>(KnifeModelPath);
            if (knifeModel != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(knifeModel);
                instance.name = "Knife_Model";
                instance.transform.SetParent(knife.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localScale = Vector3.one;
                instance.transform.localRotation = Quaternion.identity;

                // Measure at an identity rotation: the mesh path is exact there and the blade axis is
                // read straight from the model, so the placement is solved analytically instead of
                // relying on a post-rotation measurement.
                if (TryGetRendererBounds(instance, out Bounds world))
                {
                    Vector3 localMin = knife.transform.InverseTransformPoint(world.min);
                    Vector3 localMax = knife.transform.InverseTransformPoint(world.max);
                    Vector3 size = localMax - localMin;
                    float bladeLength = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    float scale = KnifeTargetLength / Mathf.Max(0.0001f, bladeLength);
                    instance.transform.localScale = Vector3.one * scale;

                    // Knife.fbx already lies with its blade along Z (the carriage forward axis), so the
                    // model only has to be scaled and centred with the grip behind the blade.
                    Vector3 centre = (localMin + localMax) * 0.5f * scale;
                    instance.transform.localPosition = new Vector3(
                        -centre.x, -centre.y, 0.2f - centre.z);
                }
            }
            else
            {
                Debug.LogWarning($"Knife model not found at {KnifeModelPath}: the grabbable has no visible blade.");
                CreatePrimitive(PrimitiveType.Cube, "Blade_Placeholder", knife.transform,
                    new Vector3(0f, 0f, -0.35f), new Vector3(0.03f, 0.05f, 0.5f), null);
            }

            SwordDamage damage = bladeTip.gameObject.AddComponent<SwordDamage>();
            damage.Configure(bladeTip, KnifeDamage, 0.25f, ~0);
            damage.ConfigureSwingWindows(true, 2.2f, 0.14f);
            damage.ConfigureVelocityReference(vehicleRoot);

            // Real physics: dropped weapons fall to the deck, thrown weapons fly and stay dangerous.
            GrabbableWeapon weapon = knife.AddComponent<GrabbableWeapon>();
            weapon.Configure(
                knife.GetComponentInChildren<HandGrabInteractable>(),
                knife.GetComponent<Rigidbody>(),
                damage,
                vehicleRoot,
                knife.transform.localPosition.y);
        }

        // ---------------------------------------------------------------- monsters

        private static GameObject CreateMonsterSystems(
            Transform vehicleRoot,
            Transform headAnchor,
            Transform hunterAttackPoint,
            List<PrototypeCandle> candleLamps,
            Transform rearAnchor)
        {
            var systemObject = new GameObject("MonsterSystem");

            GameObject hunterObject = CreateChild(headAnchor, "HunterTarget", Vector3.zero).gameObject;
            PrototypeHunterMonsterTarget hunterTarget = hunterObject.AddComponent<PrototypeHunterMonsterTarget>();
            hunterTarget.Configure(headAnchor, hunterAttackPoint);

            var targetComponents = new List<MonoBehaviour> { hunterTarget };
            foreach (PrototypeCandle candle in candleLamps)
            {
                PrototypeCandleMonsterTarget candleTarget = candle.gameObject.AddComponent<PrototypeCandleMonsterTarget>();
                candleTarget.Configure(candle);
                targetComponents.Add(candleTarget);
            }

            MonsterTargetRegistry registry = systemObject.AddComponent<MonsterTargetRegistry>();
            registry.Configure(targetComponents);

            CartAttachmentPoints attachmentPoints = systemObject.AddComponent<CartAttachmentPoints>();
            attachmentPoints.Configure(CreateAttachmentPoints(vehicleRoot));

            CartMonsterLoad cartLoad = systemObject.AddComponent<CartMonsterLoad>();
            cartLoad.Configure(60f, 0.3f, vehicleRoot.GetComponent<CarriageMotor>());

            MonsterSpawner spawner = systemObject.AddComponent<MonsterSpawner>();
            spawner.Configure(
                vehicleRoot,
                headAnchor,
                registry,
                new[]
                {
                    new MonsterSpawnEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath),
                        movementType = MonsterMovementType.Ground,
                        weight = 1f,
                        attachmentLoad = 15f,
                        overrideTargetStrategy = true,
                        targetStrategy = MonsterTargetStrategy.PrioritizeHunter
                    },
                    new MonsterSpawnEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath),
                        movementType = MonsterMovementType.Flying,
                        weight = 1f,
                        minimumFlyingHeight = 3.5f,
                        maximumFlyingHeight = 7f,
                        attachmentLoad = 5f,
                        overrideTargetStrategy = true,
                        targetStrategy = MonsterTargetStrategy.PrioritizeHunter
                    }
                },
                ~0,
                ~0,
                attachmentPoints,
                cartLoad,
                hunterTarget);
            // The ride starts calm: no monster at all until the first delay elapses.
            spawner.ConfigureTiming(false, SecondsBeforeFirstMonster, MonsterSpawnInterval);

            GiantZombieSpawner giantSpawner = systemObject.AddComponent<GiantZombieSpawner>();
            GameObject giantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GiantPrefabPath);
            giantSpawner.Configure(giantPrefab, vehicleRoot, rearAnchor, 22f, ~0);
            giantSpawner.ConfigureSpeedTrigger(true, 1.4f, 2.5f, vehicleRoot.GetComponent<CarriageMotor>(), 30f);

            CreateRearReachPoint(vehicleRoot, rearAnchor);
            return systemObject;
        }

        private static void CreateRearReachPoint(Transform vehicleRoot, Transform rearAnchor)
        {
            if (rearAnchor != null)
            {
                return;
            }

            CreateChild(vehicleRoot, "CartRearReachPoint", new Vector3(0f, 0.6f, 3.2f));
        }

        private static List<MonsterAttachmentPoint> CreateAttachmentPoints(Transform vehicleRoot)
        {
            var points = new List<MonsterAttachmentPoint>();
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RearCenter",
                new Vector3(0f, 1.10f, 2.35f), MonsterAttachmentKind.Ground, 2));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_LeftRear",
                new Vector3(-1.15f, 1.00f, 1.55f), MonsterAttachmentKind.Ground, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RightRear",
                new Vector3(1.15f, 1.00f, 1.55f), MonsterAttachmentKind.Ground, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_LeftRearFlying",
                new Vector3(-1.35f, 1.75f, 2.6f), MonsterAttachmentKind.Flying, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RightRearFlying",
                new Vector3(1.35f, 1.75f, 2.6f), MonsterAttachmentKind.Flying, 1));
            return points;
        }

        private static MonsterAttachmentPoint CreateAttachmentPoint(
            Transform parent, string name, Vector3 localPosition, MonsterAttachmentKind kinds, int capacity)
        {
            Transform pointTransform = CreateChild(parent, name, localPosition);
            MonsterAttachmentPoint point = pointTransform.gameObject.AddComponent<MonsterAttachmentPoint>();
            point.Configure(kinds, capacity, Vector3.back);
            return point;
        }

        // ---------------------------------------------------------------- kingdom and victory

        private static GameObject BuildKingdom(
            ForestRoad road,
            Material stoneMaterial,
            Material roofMaterial,
            Material lampMaterial,
            out Light[] kingdomLights)
        {
            RoadPathModel path = road.CreatePathModel();
            path.GetChunkPose(road.TotalTiles, out Vector3 position, out float heading);
            var kingdom = new GameObject("Kingdom");
            kingdom.transform.SetParent(road.transform, false);
            kingdom.transform.position = position;
            kingdom.transform.rotation = Quaternion.Euler(0f, heading + 180f, 0f);

            var lights = new List<Light>();

            CreatePrimitive(PrimitiveType.Cube, "KingdomFloor", kingdom.transform,
                new Vector3(0f, 0.15f, 6f), new Vector3(30f, 0.3f, 26f), stoneMaterial);

            // Outer wall behind the gate.
            CreatePrimitive(PrimitiveType.Cube, "KingdomWall", kingdom.transform,
                new Vector3(0f, 4f, 16f), new Vector3(30f, 8f, 0.8f), stoneMaterial);

            // Torii gate.
            CreatePrimitive(PrimitiveType.Cylinder, "GatePillar_Left", kingdom.transform,
                new Vector3(-3.6f, 5f, 0f), new Vector3(0.5f, 5f, 0.5f), roofMaterial);
            CreatePrimitive(PrimitiveType.Cylinder, "GatePillar_Right", kingdom.transform,
                new Vector3(3.6f, 5f, 0f), new Vector3(0.5f, 5f, 0.5f), roofMaterial);
            CreatePrimitive(PrimitiveType.Cube, "GateBeam_Top", kingdom.transform,
                new Vector3(0f, 9.6f, 0f), new Vector3(10f, 0.6f, 0.9f), roofMaterial);
            CreatePrimitive(PrimitiveType.Cube, "GateBeam_Low", kingdom.transform,
                new Vector3(0f, 7.2f, 0f), new Vector3(8.6f, 0.45f, 0.7f), roofMaterial);

            // Defence towers.
            var towerPositions = new[]
            {
                new Vector3(-11f, 0f, 8f),
                new Vector3(11f, 0f, 8f),
                new Vector3(-11f, 0f, 20f),
                new Vector3(11f, 0f, 20f)
            };
            for (int index = 0; index < towerPositions.Length; index++)
            {
                Vector3 towerPosition = towerPositions[index];
                Transform tower = CreateChild(kingdom.transform, "Tower_" + index, towerPosition);
                CreatePrimitive(PrimitiveType.Cylinder, "Body", tower,
                    new Vector3(0f, 4.5f, 0f), new Vector3(2.2f, 4.5f, 2.2f), stoneMaterial);
                CreatePrimitive(PrimitiveType.Cylinder, "Roof", tower,
                    new Vector3(0f, 9.4f, 0f), new Vector3(2.9f, 0.9f, 2.9f), roofMaterial);
                GameObject lantern = CreatePrimitive(PrimitiveType.Sphere, "Lantern", tower,
                    new Vector3(0f, 7.4f, -2.1f), new Vector3(0.55f, 0.7f, 0.55f), lampMaterial);
                Object.DestroyImmediate(lantern.GetComponent<Collider>());
                lights.Add(AddPointLight(lantern.transform, new Color(1f, 0.62f, 0.24f), 1.6f, 16f));
            }

            kingdomLights = lights.ToArray();
            return kingdom;
        }

        private static GameObject BuildVictoryBanner(Transform kingdom, Material lampMaterial)
        {
            var canvasObject = new GameObject("VictoryBanner", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(kingdom, false);
            canvasObject.transform.localPosition = new Vector3(0f, 9.0f, -2.5f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.008f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, 170f);

            CreatePrimitive(PrimitiveType.Cube, "BannerPlate", canvasObject.transform,
                new Vector3(0f, 0f, -0.35f), new Vector3(700f, 170f, 6f), lampMaterial);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 96);
            }

            if (font == null)
            {
                Debug.LogWarning("No usable font was found: the victory banner keeps its lit plate without text.");
                canvasObject.SetActive(false);
                return canvasObject;
            }

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(canvasObject.transform, false);
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 96;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.88f, 0.62f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "Llegaste al reino\nVICTORIA";

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            canvasObject.SetActive(false);
            return canvasObject;
        }

        private static void CreateDefeatEffect(GiantZombieSpawner giantSpawner, Camera centerEye, Material overlayMaterial)
        {
            if (centerEye == null)
            {
                Debug.LogWarning("No center eye camera: the defeat overlay cannot be attached.");
                return;
            }

            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "DefeatOverlay";
            Object.DestroyImmediate(overlay.GetComponent<Collider>());
            overlay.transform.SetParent(centerEye.transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, 0.40f);
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = new Vector3(1.4f, 0.9f, 1f);

            Renderer renderer = overlay.GetComponent<Renderer>();
            renderer.sharedMaterial = overlayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            DeathScreenEffect effect = centerEye.gameObject.GetComponent<DeathScreenEffect>();
            if (effect == null)
            {
                effect = centerEye.gameObject.AddComponent<DeathScreenEffect>();
            }

            effect.Configure(giantSpawner, renderer, 1.6f);
        }

        // ---------------------------------------------------------------- validation

        private static bool ValidateOpenScene(out string report)
        {
            var failures = new List<string>();

            CarriageMotor motor = Object.FindAnyObjectByType<CarriageMotor>();
            Require(motor != null, "CarriageMotor exists.", failures);
            Require(motor is ICartSpeedPenaltyReceiver, "CarriageMotor implements ICartSpeedPenaltyReceiver.", failures);
            Require(motor != null && motor.HasGestureAudio,
                "The carriage has the whip audio wired so gesture detection is audible.", failures);
            ValidateHorses(failures);

            ReinHandle[] reins = Object.FindObjectsByType<ReinHandle>();
            Require(reins.Length == 2, "Two rein pins exist.", failures);
            foreach (ReinHandle rein in reins)
            {
                Require(rein.GrabPointCount >= 4,
                    $"{rein.name} offers several grab points along the rope (found {rein.GrabPointCount}).", failures);
                Require(rein.GetComponent<Renderer>() == null && rein.GetComponentInChildren<Renderer>(true) == null,
                    $"{rein.name} has no visible handle: the rope itself is grabbed.", failures);
                Require(rein.RestLocalPosition.y <= 1.0f,
                    $"{rein.name} hangs down at deck height so it can be lifted and yanked.", failures);
            }

            HandGrabInteractor[] handInteractors =
                Object.FindObjectsByType<HandGrabInteractor>(FindObjectsInactive.Include);
            Require(handInteractors.Length >= 2,
                $"The VR interaction rig provides both hand grab interactors (found {handInteractors.Length}).",
                failures);
            Require(HasOvrManager(),
                "The OVR camera rig exposes an OVRManager component.", failures);

            ClosedReinLoop loop = Object.FindAnyObjectByType<ClosedReinLoop>();
            Require(loop != null, "ClosedReinLoop exists.", failures);

            ForestRoad road = Object.FindAnyObjectByType<ForestRoad>();
            Require(road != null, "ForestRoad exists.", failures);
            Require(road != null && road.TotalTiles > 0, "The road has a finite length ending at the kingdom.", failures);

            GameObject ground = GameObject.Find("Ground");
            Require(ground != null && ground.GetComponent<MonsterGroundSurface>() != null,
                "The ground is marked with MonsterGroundSurface.", failures);
            Require(ground != null && ground.GetComponent<Collider>() != null,
                "The ground has a collider so monsters can be placed on it.", failures);
            Require(ground != null && ground.GetComponent<CartFollowGround>() != null,
                "The ground follows the carriage.", failures);

            MonsterSpawner spawner = Object.FindAnyObjectByType<MonsterSpawner>();
            Require(spawner != null && spawner.SpawnEntries.Count == 2,
                "The spawner has exactly two monster entries (zombie and bat).", failures);
            Require(spawner != null && spawner.IsConfigured, "The spawner has every required reference.", failures);
            Require(spawner != null && !spawner.SpawnsOneOfEachOnStart,
                "The scene starts with no monsters at all.", failures);
            Require(spawner != null && spawner.InitialSpawnDelay > 0f,
                "The first monster only appears after an initial delay.", failures);

            MonsterTargetRegistry registry = Object.FindAnyObjectByType<MonsterTargetRegistry>();
            Require(registry != null && registry.IsConfigured && registry.Targets.Count >= 1,
                "The target registry contains at least the player target.", failures);

            CartMonsterLoad cartLoad = Object.FindAnyObjectByType<CartMonsterLoad>();
            Require(cartLoad != null, "CartMonsterLoad exists.", failures);
            if (cartLoad != null)
            {
                Require(cartLoad.HasReceiver,
                    "CartMonsterLoad is connected to the carriage speed receiver.", failures);
                Require(cartLoad.ReferenceMaximumLoad > 0f && cartLoad.MinimumSpeedMultiplier > 0f,
                    "CartMonsterLoad exposes positive balance values.", failures);
                Require(Mathf.Approximately(cartLoad.SpeedMultiplier, 1f),
                    "An unloaded cart starts with a neutral speed multiplier.", failures);
            }

            GiantZombieSpawner giantSpawner = Object.FindAnyObjectByType<GiantZombieSpawner>();
            Require(giantSpawner != null && giantSpawner.SpawnWhenSpeedDrops,
                "The giant is triggered by a drop in carriage speed.", failures);
            Require(giantSpawner != null && giantSpawner.GiantPrefab != null,
                "The giant spawner has a prefab.", failures);
            ValidateGiantArming(giantSpawner, failures);

            Require(AssetDatabase.LoadAssetAtPath<GameObject>(HorseModelPath) != null, "The horse model exists.", failures);
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(KnifeModelPath) != null, "The knife model exists.", failures);
            Require(Object.FindAnyObjectByType<HandStrikeController>() != null,
                "Bare-fist damage controller exists.", failures);
            GameObject knife = GameObject.Find("Knife");
            Require(knife != null, "The grabbable weapon exists in the carriage.", failures);
            if (knife != null)
            {
                Require(knife.GetComponent<GrabbableWeapon>() != null,
                    "The weapon has real physics (falls when released, dangerous when thrown).", failures);
                Rigidbody knifeBody = knife.GetComponent<Rigidbody>();
                Require(knifeBody != null, "The weapon has a rigidbody for its flight.", failures);
            }
            Require(Object.FindAnyObjectByType<DeathScreenEffect>() != null,
                "Defeat screen effect exists.", failures);
            Require(Object.FindAnyObjectByType<LevelVictoryController>() != null,
                "Victory controller exists.", failures);

            // Functional check on the load model through the production motor.
            if (motor != null)
            {
                float baseline = motor.EffectiveMaximumSpeed;
                motor.SetMonsterLoadMultiplier(0.5f);
                bool reduced = motor.EffectiveMaximumSpeed < baseline;
                motor.SetMonsterLoadMultiplier(1f);
                Require(reduced, "A monster load of 0.5 reduces the carriage maximum speed.", failures);
                Require(Mathf.Approximately(motor.EffectiveMaximumSpeed, baseline),
                    "Releasing the load restores the carriage maximum speed.", failures);
            }

            report = failures.Count == 0
                ? "All playable scene checks passed: rig, reins, ground, monsters, combat, lighting and level end are wired."
                : string.Join("\n", failures.ConvertAll(failure => "FAILED: " + failure));
            return failures.Count == 0;
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject InstantiatePrefab(string path, Transform parent, Vector3 localPosition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Required prefab not found at {path}.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static GameObject[] LoadAssets(IEnumerable<string> paths)
        {
            var assets = new List<GameObject>();
            foreach (string path in paths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.ToArray();
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static Light AddPointLight(Transform parent, Color color, float intensity, float range)
        {
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(parent, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// World-space bounds of every mesh under an object. Skinned meshes use
        /// <c>Renderer.bounds</c> (already in world units; the raw mesh is authored in centimetres),
        /// while static meshes are solved from the mesh bounds and the transform chain so the result
        /// is exact and never stale right after a transform change.
        /// </summary>
        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer is SkinnedMeshRenderer)
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = toWorld.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return hasBounds;
        }

        private static Transform FindChild(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found != null)
            {
                return found;
            }

            string leaf = path;
            int separator = path.LastIndexOf('/');
            if (separator >= 0)
            {
                leaf = path.Substring(separator + 1);
            }

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != root && candidate.name == leaf)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material GetOrCreateLitMaterial(string name, Color color, bool emissive = false)
        {
            string path = $"{GameMaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 3f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material GetOrCreateOverlayMaterial(string name, Color color)
        {
            string path = $"{GameMaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material GetOrCreateNightSky(string name)
        {
            string path = $"{GameMaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_SkyTint", new Color(0.035f, 0.050f, 0.105f));
            material.SetColor("_GroundColor", new Color(0.010f, 0.012f, 0.020f));
            material.SetFloat("_AtmosphereThickness", 0.75f);
            material.SetFloat("_SunSize", 0.02f);
            material.SetFloat("_SunSizeConvergence", 2f);
            material.SetFloat("_Exposure", 0.55f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void WireObject(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"Field '{fieldName}' was not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string fieldName, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetVector3(Object target, string fieldName, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.vector3Value = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetObjectArray(Object target, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedInt(SerializedObject serialized, string fieldName, int value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetSerializedBool(SerializedObject serialized, string fieldName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        /// <summary>Checks the real horses are upright, facing forward, big enough and animated.</summary>
        private static void ValidateHorses(ICollection<string> failures)
        {
            var names = new[] { "Horse_Model_Left", "Horse_Model_Right" };
            var found = 0;
            foreach (string name in names)
            {
                GameObject horse = GameObject.Find(name);
                if (horse == null)
                {
                    continue;
                }

                found++;
                Require(Mathf.Abs(Mathf.DeltaAngle(horse.transform.eulerAngles.y, HorseYawDegrees)) < 2f,
                    $"{name} faces the direction of travel instead of backwards.", failures);
                Require(horse.transform.lossyScale.y > 0f, $"{name} has a valid scale.", failures);
                if (TryGetRendererBounds(horse, out Bounds bounds))
                {
                    Require(bounds.size.y > 1.9f, $"{name} is scaled up to a believable size.", failures);
                    Require(Mathf.Abs(bounds.min.y) < 0.8f, $"{name} stands on the road surface.", failures);
                }

                Require(horse.GetComponent<HorseLocomotionDriver>() != null,
                    $"{name} has the procedural idle/gallop driver.", failures);
            }

            Require(found == 2, "Both real horse models are present.", failures);
            Require(GameObject.Find("ReinHeadAnchor") != null,
                "The reins attach to an anchor taken from the real horse head.", failures);

            GameObject knifeModel = GameObject.Find("Knife_Model");
            if (knifeModel != null && TryGetRendererBounds(knifeModel, out Bounds knifeBounds))
            {
                Vector3 knifeSize = knifeBounds.size;
                float longest = Mathf.Max(knifeSize.x, Mathf.Max(knifeSize.y, knifeSize.z));
                int thinAxes = 0;
                if (knifeSize.x < 0.4f) thinAxes++;
                if (knifeSize.y < 0.4f) thinAxes++;
                if (knifeSize.z < 0.4f) thinAxes++;
                Require(longest > 0.6f, "The knife is scaled up to a hand-sized weapon.", failures);
                Require(thinAxes == 2,
                    "The knife blade is thin in two axes instead of keeping the squashed grip proportions.", failures);
            }
            else
            {
                Require(false, "The knife model is present and measurable.", failures);
            }
        }

        /// <summary>
        /// Deterministic check of the defeat trigger: it must ignore the parked carriage and arm
        /// only once the carriage has actually ridden the arming distance. The transform is restored.
        /// </summary>
        private static void ValidateGiantArming(GiantZombieSpawner giantSpawner, ICollection<string> failures)
        {
            if (giantSpawner == null || !giantSpawner.SpawnWhenSpeedDrops)
            {
                return;
            }

            Transform cart = giantSpawner.CartTransform;
            Require(cart != null, "The giant spawner knows the carriage transform.", failures);
            if (cart == null)
            {
                return;
            }

            Vector3 originalPosition = cart.position;
            try
            {
                Require(!giantSpawner.IsArmed,
                    "The giant trigger starts disarmed so it cannot fire before the first ride.", failures);

                cart.position = originalPosition + Vector3.back * (giantSpawner.ArmDistance + 5f);
                Require(giantSpawner.IsArmed,
                    "The giant trigger arms once the carriage has ridden the arming distance.", failures);
            }
            finally
            {
                cart.position = originalPosition;
            }

            Require(!giantSpawner.IsArmed,
                "The giant trigger disarms again when the carriage returns to its start.", failures);
        }

        private static bool HasOvrManager()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour != null && behaviour.GetType().Name == "OVRManager")
                {
                    return true;
                }
            }

            return false;
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
