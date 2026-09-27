using System.Collections.Generic;
using System.IO;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.OVR.Editor.QuickActions;
using Reins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
        private const string ReinGripMaterialPath = "Assets/Materials/Game/Mat_ReinGrip.mat";
        private const string WagonBodyMaterialPath = "Assets/Materials/Game/Mat_Wagon_Body.mat";
        private const string WagonHardwareMaterialPath = "Assets/Materials/Game/Mat_Wagon_Hardware.mat";
        private const string ZombiePrefabPath = "Assets/Art/Monsters/Zombie/Prefabs/ZombieHordeDemon.prefab";
        private const string BatPrefabPath = "Assets/Art/Monsters/Bat/Prefabs/BatDemon.prefab";
        private const string GiantPrefabPath = "Assets/Art/Monsters/GiantZombie/Prefabs/GiantHordeZombie.prefab";
        private const string HorseModelPath = "Assets/Art/Monsters/Horse/Horse.fbx";
        private const string HorseControllerPath = "Assets/Animations/Horse/Horse.controller";
        private const string WagonModelPath = "Assets/Art/Wagon/Wild West Cart.obj";
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
        private const float HorseTargetHeight = 2.115f;
        private const float HorseYawDegrees = 180f;
        // The wagon obj is authored with its length along its own X and its wheel axles along Z, so
        // it is turned a quarter turn to travel along the carriage's -Z. Its fit is otherwise derived
        // from its own bounds because the obj ships without a matching mtl or group names.
        private const float WagonYawDegrees = 90f;
        // The bed must be a little wider than both horses together, so the pair reads as harnessed to
        // it instead of standing beside it.
        private const float WagonWidthMargin = 0.30f;
        // Overall size of the bed once it has been fitted to the horses. Scaling the whole model keeps
        // its proportions; only the width would otherwise leave it wider than it is long.
        private const float WagonSizeMultiplier = 1.4f;
        // Gap between the horses' hindquarters and the front of the bed, so the gait never clips it.
        private const float WagonHorseClearance = 0.20f;
        // Fallback deck ratio, used only if the horse models are missing.
        private const float WagonDeckWidthScale = 0.95f;
        // The lamps sit in from the corners so they stay inside the cart's footprint.
        private const float LampCornerInset = 0.55f;
        // They sit on top of the wagon's rails, as high as they go: with today's wagon that puts their
        // base at y = 1.126, mounted just clear of the rail top.
        private const float LampMountClearance = 0.006f;
        private const string LanternStandingModelPath = "Assets/Art/Lanterns/lantern_standing.fbx";
        private const string PostLanternModelPath = "Assets/Art/Lanterns/post_lantern.fbx";
        private const float LanternStandingHeight = 0.45f;
        // Where the flame burns inside the standing lantern, as a fraction of its height.
        private const float LanternFlameHeight = 0.52f;
        // Warm firelight rather than a lantern-white: the night is dark on purpose and these are candles.
        private static readonly Color LanternFlameColor = new Color(1f, 0.78f, 0.44f);
        private const float LanternFrontIntensity = 6f;
        private const float LanternFrontRange = 24f;
        private const float LanternRearIntensity = 5f;
        private const float LanternRearRange = 22f;
        // The lamp's slow breathing: wide enough to notice, slow enough not to flicker.
        private const float LanternPulseAmplitude = 0.14f;
        private const float LanternPulseFrequency = 0.16f;
        private const float LanternGallopCueStartSpeed = 3.4f;
        private const float LanternGallopCueMaximumSpeed = 8f;
        private const float LanternGallopIntensityBoost = 0.18f;
        // Roadside lights are spaced two tiles apart and only a few tile groups stay lit at once.
        private const float PostLanternHeight = 3.6f;
        private const float PostLanternOffset = 1.1f;
        private const float PostLanternSpacing = 36f;
        private static readonly Color PostLanternColor = new Color(1f, 0.70f, 0.34f);
        private const float PostLanternIntensity = 6f;
        private const float PostLanternRange = 30f;
        // How many tiles either way keep their lanterns really lit. Every extra lit lantern is another
        // real-time light in the forward pass, so this stays deliberately small.
        private const int PostLanternLitChunkSpan = 1;
        // Tiles either way keep their foliage enabled; the rest is switched off as whole batches.
        private const int TreeBatchChunkSpan = 3;
        // Render pipeline budget: only the carriage and the monsters near it are worth shadowing.
        private const int ShadowDistanceMeters = 22;
        // Ordinal inside URP's MsaaQuality enum, whose values are 1/2/4/8: 1 selects 2x.
        private const int AntiAliasingEnumIndex = 1;
        private static readonly string[] RenderPipelinePaths =
        {
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/PC_RPAsset.asset"
        };
        // How far below the head the rein collar sits, so the rope meets the neck, not the muzzle.
        private const float ReinCollarDrop = 0.34f;
        private const float KnifeTargetLength = 0.85f;
        private const float PunchDamage = 5f;
        private const float KnifeDamage = 12f;
        private const float SecondsBeforeFirstMonster = 38f;
        private const float MonsterSpawnInterval = 11f;
        // The horde is always present once the ride starts; only a third of it runs fast enough to
        // reach the cart, and the rest simply keep the player watched from behind.
        private const int MinimumHordeSize = 6;
        private const float FastHordeFraction = 0.34f;
        private const float FastHordeSpeedMultiplier = 1.5f;
        private const int BatRoundSize = 3;
        private const float BatRoundCooldown = 5f;
        private const float BatRoundApproachRadius = 80f;

        // The rope hangs down to the carriage deck instead of floating at chest height. The height is
        // solved from the wagon at generation time so the grips always clear its rails, which is what
        // keeps the rope from cutting through the cart; only the sideways offset is fixed here.
        private static readonly Vector3 LeftReinRest = new Vector3(-0.34f, 0f, -1.00f);
        private static readonly Vector3 RightReinRest = new Vector3(0.34f, 0f, -1.00f);
        // How far above the wagon's rail top the grips and the rope's floor sit.
        private const float ReinGripClearance = 0.14f;
        private const float ReinFloorClearance = 0.04f;
        private const float ReinHandleVisualLength = 0.28f;
        private const float ReinGrabZoneRadius = 0.07f;
        private const float ReinGrabZoneLength = 0.34f;
        // Barely any slack: the rein is meant to read as a taut line to the horses, not a loose loop.
        private const float ReinSlack = 1.02f;

        // ---------------------------------------------------------------- gallop tuning
        private const string LeftWhipName = "LeftWhipHandle";
        private const string RightWhipName = "RightWhipHandle";
        private const string WhipHandleMaterialPath = "Assets/Materials/Prototype/Mat_CarriageWood.mat";
        private static readonly Vector3 LeftWhipRest = new Vector3(-0.62f, 1.02f, 0.22f);
        private static readonly Vector3 RightWhipRest = new Vector3(0.62f, 1.02f, 0.22f);
        private const float WhipHandleLength = 0.36f;
        private const float WhipHandleRadius = 0.028f;
        private const float WhipGripRadius = 0.05f;
        private const float WhipTipDistance = 0.34f;
        private const float WhipRopeLength = 1.05f;
        private const int WhipRopeSegments = 5;

        // The horse's top speed. A stroke must be a step towards it rather than an instant win, so the
        // per-stroke gain is derived from this speed and the starting speed: three strokes reach the
        // ceiling exactly, and a fourth is wasted. Both values stay editable in the Inspector.
        private const float HorseMaximumSpeed = 8f;
        private const float HorseStartingSpeed = 3.5f;
        private const float WhipStrokesToFullSpeed = 3f;
        private const float WhipAccelerationPerStroke =
            (HorseMaximumSpeed - HorseStartingSpeed) / WhipStrokesToFullSpeed;
        private const float WhipCoastingDeceleration = 0.20f;

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
            Material ropeMaterial = GetOrCreateRopeMaterial();
            Material gripMaterial = GetOrCreateLitMaterial("Mat_ReinGrip", new Color(0.42f, 0.24f, 0.15f));
            Material wagonBody = GetOrCreateLitMaterial("Mat_Wagon_Body", new Color(0.32f, 0.20f, 0.12f));
            Material wagonHardware = GetOrCreateLitMaterial("Mat_Wagon_Hardware", new Color(0.30f, 0.30f, 0.33f));
            SetFloat(wagonHardware, "_Metallic", 0.9f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "JapanDemonHunter";

            OptimizeRenderPipeline();
            ConfigureEnvironment(nightSky);
            CreateMoonlight();
            CreateGlobalVolume();

            var vehicleRoot = new GameObject("VehicleRoot");
            CarriageMotor motor = vehicleRoot.AddComponent<CarriageMotor>();

            GameObject carriage = InstantiatePrefab(CarriagePrefabPath, vehicleRoot.transform, Vector3.zero);
            AddRealHorses(carriage.transform, motor, out Transform leftHorseHead, out Transform rightHorseHead);
            GameObject wagon = AddRealWagon(carriage.transform, wagonBody, wagonHardware);
            if (leftHorseHead == null)
            {
                leftHorseHead = FindChild(carriage.transform, "HorsePlaceholders/Horse_Left/Horse_Head");
            }

            if (rightHorseHead == null)
            {
                rightHorseHead = FindChild(carriage.transform, "HorsePlaceholders/Horse_Right/Horse_Head");
            }

            Transform rearAnchor = FindChild(carriage.transform, "RopeAnchors/Anchor_CarriageRear");

            List<PrototypeCandle> candleLamps = CreateCarriageLamps(vehicleRoot.transform, lampMaterial, wagon);

            GameObject roadRoot = CreateRoadRoot(vehicleRoot.transform, nightSky);
            ForestRoad road = roadRoot.GetComponent<ForestRoad>();

            // The road is a raised causeway: the flat ground plane sits below it and the carriageway
            // rises above. CarriageMotor aligns the cart against whichever position the root starts at,
            // so an origin at y=0 makes the whole carriage travel under the road surface; starting at
            // the road's own height puts the wheels, the hooves and the player rig back on top of it.
            float roadStartHeight = road.CreatePathModel().HeightAtDistance(0f);
            vehicleRoot.transform.position = new Vector3(0f, roadStartHeight, 0f);

            CreateGround(vehicleRoot.transform);

            // The grips and the rope's floor are solved from the wagon's rail top, so the rein always
            // rides above the cart instead of cutting through its boards.
            float wagonTop = GetWagonTopLocalY(vehicleRoot.transform);
            ReinHandle leftRein = CreateRein(
                vehicleRoot.transform, "Left_ReinPin",
                new Vector3(LeftReinRest.x, wagonTop + ReinGripClearance, LeftReinRest.z), 0, gripMaterial);
            ReinHandle rightRein = CreateRein(
                vehicleRoot.transform, "Right_ReinPin",
                new Vector3(RightReinRest.x, wagonTop + ReinGripClearance, RightReinRest.z), 1, gripMaterial);
            CreateClosedReinLoop(
                vehicleRoot.transform, leftRein, rightRein, leftHorseHead, rightHorseHead, ropeMaterial,
                wagonTop + ReinFloorClearance);
            WireObject(motor, "leftRein", leftRein);
            WireObject(motor, "rightRein", rightRein);
            SetFloat(motor, "minimumLoadSpeedMultiplier", 0.3f);
            SetFloat(motor, "maximumYawRate", 25f);
            SetBool(motor, "followRoadCurvature", true);
            SetFloat(motor, "maximumSpeed", HorseMaximumSpeed);
            SetFloat(motor, "startingSpeed", HorseStartingSpeed);
            SetFloat(motor, "accelerationPerStroke", WhipAccelerationPerStroke);
            SetFloat(motor, "brakingPerPull", 0.9f);
            SetFloat(motor, "coastingDeceleration", WhipCoastingDeceleration);
            WireGestureAudio(vehicleRoot.transform, motor);

            DisableDetachedWhipHandles(vehicleRoot.transform);

            Camera centerEye = CreateVrRig(vehicleRoot.transform);
            if (TryGetOriginalHandVisual(Handedness.Left, out Renderer leftHandVisual) &&
                TryGetOriginalHandVisual(Handedness.Right, out Renderer rightHandVisual))
            {
                CreateSeparateReinGripVisuals(
                    leftRein, rightRein, leftHandVisual, rightHandVisual, gripMaterial);
            }
            else
            {
                Debug.LogWarning("Safe Oculus hand visual references were unavailable for rein grip alignment.");
            }

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

            // The ambience is what actually drives the lamp look at runtime, including the slow flame
            // pulse. It is wired last, once the victory and session controllers exist to be referenced.
            WireCarriageLampAmbience(vehicleRoot);

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
                    "The two whip handles accept one hand each: Q accelerates with the left handle and E with the right.\n" +
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

        /// <summary>
        /// Trims the render pipeline assets for a headset. Shadow distance is the cheapest big win: it
        /// shrinks the shadow map's world area, and at night only the carriage and the monsters near it
        /// cast anything worth seeing. MSAA drops from 4x to 2x, which is the standard trade on Quest.
        /// The project has no default pipeline asset, it is set per quality level, so both the Quest and
        /// the desktop assets are trimmed and what is judged in the editor matches the headset budget.
        /// </summary>
        private static void OptimizeRenderPipeline()
        {
            foreach (string path in RenderPipelinePaths)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (pipeline == null)
                {
                    Debug.LogWarning($"Render pipeline asset not found at {path}; it was left as is.");
                    continue;
                }

                var serialized = new SerializedObject(pipeline);
                SetSerializedNumber(serialized, "m_ShadowDistance", ShadowDistanceMeters);
                // MSAA is an enum whose *ordinal* is written, not its sample count.
                SetSerializedNumber(serialized, "m_MSAA", AntiAliasingEnumIndex);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                Debug.Log($"Optimized {path}: shadow distance {ShadowDistanceMeters} m, MSAA 2x.");
            }
        }

        private static void ConfigureEnvironment(Material nightSky)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Barely any ambient at all: away from a lantern everything falls to near black.
            RenderSettings.ambientLight = new Color(0.006f, 0.007f, 0.008f);
            RenderSettings.skybox = nightSky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // The mist is clearly lighter than the ambient, so it reads as an opaque wall of haze rather
            // than as more darkness, and it closes early enough that nothing can be seen appearing.
            RenderSettings.fogColor = new Color(0.034f, 0.036f, 0.040f);
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 26f;
        }

        private static void CreateMoonlight()
        {
            var moonObject = new GameObject("Moonlight");
            moonObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            // A paler, brighter looking moon whose light was turned down: the disk in the sky reads
            // brighter while the ground it touches stays properly dark.
            moon.color = new Color(0.62f, 0.68f, 0.86f);
            moon.intensity = 0.34f;
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

            HidePlaceholder(carriage, "HorsePlaceholders");
            HidePlaceholder(carriage, "CarriageFloor");
            HidePlaceholder(carriage, "CarriageWalls");
            HidePlaceholder(carriage, "CarriageWheels");
            HidePlaceholder(carriage, "Shafts");
            leftHead = AddHorse(carriage, "HorsePlaceholders/Horse_Left", horseModel, "Horse_Model_Left", speedSource);
            rightHead = AddHorse(carriage, "HorsePlaceholders/Horse_Right", horseModel, "Horse_Model_Right", speedSource);
        }

        /// <summary>
        /// Replaces the block-built carriage with the real wagon model. The GameObjects of the old
        /// prototype are only hidden, never deleted: the rein anchors, the monster rear anchor and
        /// the horse clearance logic still read their transforms and colliders. Returns the instance
        /// so the lamps can be seated on its rails.
        /// </summary>
        private static GameObject AddRealWagon(Transform carriage, Material bodyMaterial, Material hardwareMaterial)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(WagonModelPath);
            if (model == null)
            {
                Debug.LogWarning($"Wagon model not found at {WagonModelPath}: the prototype carriage stays visible.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Wagon_Model";
            instance.transform.SetParent(carriage, false);
            instance.transform.localPosition = Vector3.zero;
            // The obj is modelled with its length along its own X and its wheel axles along Z, so it
            // is turned a quarter turn before anything is measured: the bounds below are taken after
            // the turn, which makes size.x the width and size.z the length.
            instance.transform.localRotation = Quaternion.Euler(0f, WagonYawDegrees, 0f);
            instance.transform.localScale = Vector3.one;
            ApplyWagonMaterials(instance, bodyMaterial, hardwareMaterial);

            Transform floor = FindChild(carriage, "CarriageFloor");
            Transform leftHorse = FindChild(carriage, "HorsePlaceholders/Horse_Left/Horse_Model_Left");
            Transform rightHorse = FindChild(carriage, "HorsePlaceholders/Horse_Right/Horse_Model_Right");

            if (!TryGetRendererBounds(instance, out Bounds bounds) || bounds.size.x <= 0.0001f)
            {
                Debug.LogWarning("Wagon model has no usable renderer bounds; it was left unadjusted.");
                return instance;
            }

            // The bed is sized to the real horses rather than to a fixed number, so it always clears
            // the pair instead of leaving them wider than the cart they are harnessed to.
            float targetWidth = 0f;
            if (TryGetCombinedBounds(leftHorse, rightHorse, out Bounds horseBounds))
            {
                targetWidth = horseBounds.size.x + WagonWidthMargin;
            }
            else if (floor != null && TryGetRendererBounds(floor.gameObject, out Bounds floorBounds))
            {
                targetWidth = floorBounds.size.x * WagonDeckWidthScale;
            }

            if (targetWidth > 0.0001f)
            {
                instance.transform.localScale = Vector3.one *
                    (targetWidth / bounds.size.x * Mathf.Max(0.01f, WagonSizeMultiplier));
            }

            if (!TryGetRendererBounds(instance, out bounds))
            {
                return instance;
            }

            // Wheels on the road surface, which is this object's own origin, and the front of the bed
            // just clear of the horses' hindquarters. The carriage travels towards -Z, so the horses
            // sit at negative Z and the bed has to run forward to meet them.
            instance.transform.position += new Vector3(
                -bounds.center.x, -bounds.min.y, 0f);
            if (TryGetCombinedBounds(leftHorse, rightHorse, out horseBounds) &&
                TryGetRendererBounds(instance, out bounds))
            {
                instance.transform.position += new Vector3(
                    0f, 0f, horseBounds.max.z + WagonHorseClearance - bounds.min.z);
            }

            return instance;
        }

        private static void ApplyWagonMaterials(GameObject wagon, Material body, Material hardware)
        {
            var renderers = new List<Renderer>(wagon.GetComponentsInChildren<Renderer>(true));
            var materials = new List<Material>();
            var fallback = body;
            bool anyHardware = false;

            for (var i = 0; i < renderers.Count; i++)
            {
                materials.Clear();
                var shared = renderers[i].sharedMaterials;
                for (var m = 0; m < shared.Length; m++)
                {
                    // The source obj ships without its mtl, so the slot names are Maya leftovers
                    // rather than an agreed palette. Match loosely and fall back to the wood body.
                    string slotName = shared[m] != null ? shared[m].name : string.Empty;
                    if (slotName.IndexOf("blinn2", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials.Add(hardware);
                        anyHardware = true;
                    }
                    else
                    {
                        materials.Add(fallback);
                    }
                }

                if (materials.Count > 0)
                {
                    renderers[i].sharedMaterials = materials.ToArray();
                }
            }

            if (!anyHardware)
            {
                Debug.Log("Wagon model exposed no hardware material slot; every renderer uses the body material.");
            }
        }

        private static void HidePlaceholder(Transform carriage, string path)
        {
            Transform placeholder = FindChild(carriage, path);
            if (placeholder == null)
            {
                return;
            }

            // Hide the whole subtree: targeting individual child names previously left the old
            // horse head cubes visible next to the real horse models.
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

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HorseControllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogWarning(
                    $"Horse controller not found at {HorseControllerPath}. Run Tools/Game/Setup Horse Import " +
                    "to import the fbx clips and build the state machine; the horse will idle until then.");
            }

            HorseAnimationDriver driver = instance.AddComponent<HorseAnimationDriver>();
            driver.Configure(speedSource, animator);
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
            var headLocal = new Vector3(
                (localMin.x + localMax.x) * 0.5f, headY, headZ + depth * 0.12f);

            // Parent the anchor to the horse itself so it follows the gait animation instead of
            // staying pinned while the head bobs.
            Transform anchor = CreateChild(horse.transform, "ReinHeadAnchor", Vector3.zero);
            anchor.position = parent.TransformPoint(headLocal);

            // The rein ends where the collar sits, not at the muzzle. Pinning the loop a little
            // back and low makes the rope visually enter the horse instead of stopping in mid air.
            var collarLocal = new Vector3(headLocal.x, headY - ReinCollarDrop, headZ + depth * 0.34f);
            Transform collar = CreateChild(horse.transform, "ReinCollarAnchor", Vector3.zero);
            collar.position = parent.TransformPoint(collarLocal);
            return collar;
        }

        /// <summary>
        /// The four carriage lamps. Their corners come from the wagon's own bounds so they always sit
        /// on the rails of whatever model is fitted; hand-placed offsets left them floating once the
        /// block carriage was replaced.
        /// </summary>
        private static List<PrototypeCandle> CreateCarriageLamps(
            Transform vehicleRoot, Material lampMaterial, GameObject wagon)
        {
            if (wagon == null || !TryGetRendererBounds(wagon, out Bounds bed))
            {
                Debug.LogWarning(
                    "The wagon has no measurable bounds, so the carriage lamps fell back to the " +
                    "prototype positions and may not sit on the bed.");
                return CreateCarriageLampsAt(vehicleRoot, lampMaterial,
                    new Vector3(-1.28f, 1.55f, -2.30f), new Vector3(1.28f, 1.55f, -2.30f),
                    new Vector3(-1.05f, 1.45f, 1.75f), new Vector3(1.05f, 1.45f, 1.75f));
            }

            float x = Mathf.Max(0.05f, bed.extents.x - LampCornerInset);
            float frontZ = bed.min.z + LampCornerInset;
            float rearZ = bed.max.z - LampCornerInset;
            // Mounted on the wagon's rail top rather than part way down its side.
            float railY = bed.max.y + LampMountClearance;

            return CreateCarriageLampsAt(vehicleRoot, lampMaterial,
                vehicleRoot.InverseTransformPoint(new Vector3(-x, railY, frontZ)),
                vehicleRoot.InverseTransformPoint(new Vector3(x, railY, frontZ)),
                vehicleRoot.InverseTransformPoint(new Vector3(-x, railY, rearZ)),
                vehicleRoot.InverseTransformPoint(new Vector3(x, railY, rearZ)));
        }

        private static List<PrototypeCandle> CreateCarriageLampsAt(
            Transform vehicleRoot,
            Material lampMaterial,
            Vector3 frontLeft,
            Vector3 frontRight,
            Vector3 rearLeft,
            Vector3 rearRight)
        {
            var candles = new List<PrototypeCandle>
            {
                CreateCandleLamp(vehicleRoot, "Lamp_FrontLeft", frontLeft, lampMaterial),
                CreateCandleLamp(vehicleRoot, "Lamp_FrontRight", frontRight, lampMaterial)
            };

            CreateSimpleLamp(vehicleRoot, "Lamp_RearLeft", rearLeft, lampMaterial);
            CreateSimpleLamp(vehicleRoot, "Lamp_RearRight", rearRight, lampMaterial);
            return candles;
        }

        private static PrototypeCandle CreateCandleLamp(
            Transform parent, string name, Vector3 localPosition, Material lampMaterial)
        {
            GameObject root = CreateChild(parent, name, localPosition).gameObject;
            GameObject flame = AddStandingLanternVisual(root.transform, lampMaterial);
            Light light = AddPointLight(
                flame.transform, LanternFlameColor, LanternFrontIntensity, LanternFrontRange);

            Transform attackPoint = CreateChild(root.transform, "AttackPoint", new Vector3(0f, 0.7f, 0f));
            PrototypeCandle candle = root.AddComponent<PrototypeCandle>();
            candle.ConfigurePrototypeReferences(flame, light, attackPoint);
            return candle;
        }

        private static void CreateSimpleLamp(
            Transform parent, string name, Vector3 localPosition, Material lampMaterial)
        {
            GameObject root = CreateChild(parent, name, localPosition).gameObject;
            GameObject glass = AddStandingLanternVisual(root.transform, lampMaterial);
            AddPointLight(glass.transform, LanternFlameColor, LanternRearIntensity, LanternRearRange);
        }

        /// <summary>
        /// The standing lantern model with the flame that burns inside it, returned so the lit state and
        /// the slow pulse drive that piece. The lantern body itself always stays in place, so putting a
        /// lamp out hides its flame instead of deleting the lamp.
        /// </summary>
        private static GameObject AddStandingLanternVisual(Transform parent, Material lampMaterial)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(LanternStandingModelPath);
            if (model != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Lantern_Model";
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                if (TryGetRendererBounds(instance, out Bounds bounds) && bounds.size.y > 0.0001f)
                {
                    instance.transform.localScale = Vector3.one * (LanternStandingHeight / bounds.size.y);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Lantern model not found at {LanternStandingModelPath}: the carriage lamps fall " +
                    "back to a block body.");
                CreatePrimitive(PrimitiveType.Cylinder, "LampBody", parent,
                    new Vector3(0f, 0.16f, 0f), new Vector3(0.06f, 0.16f, 0.06f), lampMaterial);
            }

            GameObject flame = CreatePrimitive(PrimitiveType.Sphere, "Flame", parent,
                new Vector3(0f, LanternStandingHeight * LanternFlameHeight, 0f),
                new Vector3(0.15f, 0.19f, 0.15f), lampMaterial);
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            return flame;
        }

        // ---------------------------------------------------------------- reins

        /// <summary>Builds one visible, hand-grabbable end grip that also anchors the continuous rein.</summary>
        private static ReinHandle CreateRein(
            Transform vehicleRoot,
            string pinName,
            Vector3 restLocalPosition,
            int handedness,
            Material gripMaterial)
        {
            GameObject handle = InstantiatePrefab(RopeProxyPrefabPath, vehicleRoot, restLocalPosition);
            handle.name = pinName.Replace("Pin", "Handle");
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = new Vector3(0.04f, ReinHandleVisualLength * 0.5f, 0.04f);

            MeshCollider meshCollider = handle.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.enabled = false;
            }

            CapsuleCollider capsule = handle.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = ReinGrabZoneRadius;
                capsule.height = ReinGrabZoneLength;
                capsule.direction = 1;
                capsule.center = Vector3.zero;
            }

            Rigidbody body = handle.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            foreach (Renderer renderer in handle.GetComponentsInChildren<Renderer>(true))
            {
                if (gripMaterial != null)
                {
                    renderer.sharedMaterial = gripMaterial;
                }
                renderer.enabled = true;
            }

            Grabbable grabbable = handle.GetComponent<Grabbable>();
            if (grabbable != null)
            {
                SetBool(grabbable, "_throwWhenUnselected", false);
            }

            HandGrabInteractable rootGrabPoint = handle.GetComponent<HandGrabInteractable>();
            GameObject grabZone = InstantiatePrefab(RopeProxyPrefabPath, handle.transform, Vector3.zero);
            grabZone.name = "Rein_HandGrabZone";
            grabZone.transform.localScale = Vector3.one;
            DisableGripVisuals(grabZone.transform);
            ConfigureReinGrabProxy(grabZone);
            if (rootGrabPoint != null)
            {
                rootGrabPoint.enabled = false;
            }

            DisableGripColliders(handle);
            ReinHandle rein = handle.AddComponent<ReinHandle>();
            SetInt(rein, "expectedHand", handedness);
            SetBool(rein, "requireExpectedHand", true);
            SetBool(rein, "enableLaneGesture", false);
            SetVector3(rein, "restLocalPosition", restLocalPosition);
            HandGrabInteractable grabPoint = grabZone.GetComponentInChildren<HandGrabInteractable>(true);
            SetObjectArray(rein, "grabPoints", grabPoint != null
                ? new[] { grabPoint }
                : new HandGrabInteractable[0]);
            return rein;
        }

        private static void SetReinHandleVisual(ReinHandle rein, Material gripMaterial)
        {
            Undo.RecordObject(rein.transform, "Shorten separate rein handle");
            rein.transform.localScale = new Vector3(0.04f, ReinHandleVisualLength * 0.5f, 0.04f);
            Renderer renderer = rein.GetComponent<Renderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Show separate rein handle");
                renderer.sharedMaterial = gripMaterial;
                renderer.enabled = true;
            }
        }

        private static void DisableGripVisuals(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        private static void ConfigureGrabProxy(GameObject grabZone)
        {
            MeshCollider mesh = grabZone.GetComponent<MeshCollider>();
            if (mesh != null)
            {
                mesh.enabled = false;
            }

            CapsuleCollider capsule = grabZone.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = 0.5f;
                capsule.height = 2f;
                capsule.direction = 1;
                capsule.center = Vector3.zero;
            }

            Rigidbody body = grabZone.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            Grabbable grabbable = grabZone.GetComponent<Grabbable>();
            if (grabbable != null)
            {
                SetBool(grabbable, "_throwWhenUnselected", false);
            }
        }

        private static void ConfigureReinGrabProxy(GameObject grabZone)
        {
            ConfigureGrabProxy(grabZone);
            CapsuleCollider capsule = grabZone.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return;
            }

            capsule.radius = ReinGrabZoneRadius;
            capsule.height = ReinGrabZoneLength;
            capsule.direction = 1;
            capsule.center = Vector3.zero;
        }

        private static void DisableGripColliders(GameObject pin)
        {
            Collider[] colliders = pin.GetComponents<Collider>();
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }
        }

        private static void CreateSeparateReinGripVisuals(
            ReinHandle left,
            ReinHandle right,
            Renderer leftHandVisual,
            Renderer rightHandVisual,
            Material gloveMaterial)
        {
            if (left == null || right == null)
            {
                return;
            }

            GameObject leftProxy = CreateGripProxy(left.transform, "ReinGripGlove_Left", -1f, gloveMaterial);
            GameObject rightProxy = CreateGripProxy(right.transform, "ReinGripGlove_Right", 1f, gloveMaterial);
            ConfigureGripVisualController(left, leftHandVisual, leftProxy);
            ConfigureGripVisualController(right, rightHandVisual, rightProxy);
        }

        private static GameObject CreateGripProxy(
            Transform grip, string name, float side, Material gloveMaterial)
        {
            Transform existing = grip.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var root = new GameObject(name);
            root.transform.SetParent(grip, false);
            Vector3 parentScale = grip.localScale;
            root.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
            root.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(root, "Create separate rein grip glove");

            CreateGripPrimitive(root.transform, "Palm", PrimitiveType.Sphere,
                new Vector3(0f, 0f, 0.078f), Vector3.zero, new Vector3(0.105f, 0.12f, 0.075f), gloveMaterial);
            for (var finger = 0; finger < 4; finger++)
            {
                float across = (finger - 1.5f) * 0.037f;
                CreateGripPrimitive(root.transform, "CurledFinger_" + finger, PrimitiveType.Capsule,
                    new Vector3(across, 0.006f, 0.036f), new Vector3(78f, 0f, 0f),
                    new Vector3(0.021f, 0.043f, 0.021f), gloveMaterial);
            }

            CreateGripPrimitive(root.transform, "Thumb", PrimitiveType.Capsule,
                new Vector3(side * -0.085f, 0f, 0.045f), new Vector3(35f, 0f, side * 32f),
                new Vector3(0.026f, 0.052f, 0.026f), gloveMaterial);
            return root;
        }

        private static void CreateGripPrimitive(
            Transform parent, string name, PrimitiveType primitiveType,
            Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Undo.RegisterCreatedObjectUndo(part, "Create glove proxy mesh");
        }

        private static void ConfigureGripVisualController(
            ReinHandle rein, Renderer originalHandVisual, GameObject gloveProxy)
        {
            if (rein == null || originalHandVisual == null || gloveProxy == null)
            {
                return;
            }

            SharedReinGripVisuals visuals = rein.GetComponent<SharedReinGripVisuals>();
            if (visuals == null)
            {
                visuals = Undo.AddComponent<SharedReinGripVisuals>(rein.gameObject);
            }
            else
            {
                Undo.RecordObject(visuals, "Wire shared rein hand visuals");
            }

            visuals.Configure(rein, new[] { originalHandVisual }, gloveProxy);
            EditorUtility.SetDirty(visuals);
        }

        private static bool TryGetOriginalHandVisual(Handedness hand, out Renderer visual)
        {
            visual = null;
            string rootName = hand == Handedness.Left ? "OVRHandVisualLeft" : "OVRHandVisualRight";
            string meshName = hand == Handedness.Left ? "l_handMeshNode" : "r_handMeshNode";
            GameObject visualRoot = GameObject.Find(rootName);
            if (visualRoot == null)
            {
                return false;
            }

            SkinnedMeshRenderer match = null;
            foreach (SkinnedMeshRenderer candidate in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (candidate.name != meshName || !candidate.gameObject.activeInHierarchy || !candidate.enabled)
                {
                    continue;
                }
                if (match != null)
                {
                    return false;
                }
                match = candidate;
            }

            visual = match;
            return visual != null;
        }

        private static void CreateClosedReinLoop(
            Transform vehicleRoot,
            ReinHandle leftRein,
            ReinHandle rightRein,
            Transform leftHorseHead,
            Transform rightHorseHead,
            Material ropeMaterial,
            float deckHeight)
        {
            var loopObject = new GameObject("ClosedReinLoop");
            loopObject.transform.SetParent(vehicleRoot, false);
            LineRenderer line = loopObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 4;
            if (ropeMaterial != null)
            {
                line.sharedMaterial = ropeMaterial;
            }

            ClosedReinLoop loop = loopObject.AddComponent<ClosedReinLoop>();
            // The floor keeps every point of the loop above the wagon's rails, so the rein can never be
            // pushed down through the cart by a fast hand movement.
            SetFloat(loop, "minimumDeckHeight", deckHeight);
            SetFloat(loop, "slack", ReinSlack);
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

        /// <summary>Wires the existing four carriage lamps without creating or replacing lights.</summary>
        [MenuItem("Tools/Game/Integrate Existing Carriage Lamps")]
        public static void IntegrateExistingCarriageLamps()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                Debug.LogWarning($"Open {ScenePath} before integrating its existing carriage lamps.");
                return;
            }

            GameObject vehicle = GameObject.Find("VehicleRoot");
            if (vehicle == null)
            {
                Debug.LogWarning("VehicleRoot was not found; no lamp objects were changed.");
                return;
            }

            if (!WireCarriageLampAmbience(vehicle))
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            if (!EditorSceneManager.SaveScene(activeScene, ScenePath))
                throw new System.InvalidOperationException($"Unity could not save {ScenePath}.");
            Debug.Log("Wired the existing front/rear carriage lamps; no light sources were created.");
        }

        /// <summary>
        /// Puts the four carriage lamps under the ambience component, which gives them their warm
        /// colour and the slow breathing of a flame. Returns false when a lamp or its light is missing.
        /// </summary>
        private static bool WireCarriageLampAmbience(GameObject vehicle)
        {
            Transform frontLeft = FindChild(vehicle.transform, "Lamp_FrontLeft");
            Transform frontRight = FindChild(vehicle.transform, "Lamp_FrontRight");
            Transform rearLeft = FindChild(vehicle.transform, "Lamp_RearLeft");
            Transform rearRight = FindChild(vehicle.transform, "Lamp_RearRight");
            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            {
                Debug.LogWarning("One or more existing carriage lamp objects are missing; no lamp integration was applied.");
                return false;
            }

            Light[] frontLights = { FindChild(frontLeft, "Flame/Light")?.GetComponent<Light>(),
                                    FindChild(frontRight, "Flame/Light")?.GetComponent<Light>() };
            // Both lamp kinds carry the same flame piece now that they share the standing lantern model.
            Light[] rearLights = { FindChild(rearLeft, "Flame/Light")?.GetComponent<Light>(),
                                   FindChild(rearRight, "Flame/Light")?.GetComponent<Light>() };
            PrototypeCandle[] candles = { frontLeft != null ? frontLeft.GetComponent<PrototypeCandle>() : null,
                                          frontRight != null ? frontRight.GetComponent<PrototypeCandle>() : null };

            for (int index = 0; index < 2; index++)
            {
                if (frontLights[index] == null || candles[index] == null || rearLights[index] == null)
                {
                    Debug.LogWarning("Expected front candle and four existing carriage Light components; " +
                                     "lamp integration was not applied.");
                    return false;
                }
            }

            CarriageLampAmbience ambience = vehicle.GetComponent<CarriageLampAmbience>();
            if (ambience == null) ambience = vehicle.AddComponent<CarriageLampAmbience>();
            ambience.Configure(frontLights, rearLights, candles,
                vehicle.GetComponent<CarriageMotor>(), Object.FindAnyObjectByType<LevelVictoryController>(),
                Object.FindAnyObjectByType<GameSessionController>());

            // The ambience owns the lamp look at runtime, so its tuning is set here rather than left at
            // the script defaults: these are the values the darker night was balanced against.
            SetColor(ambience, "frontColor", LanternFlameColor);
            SetColor(ambience, "rearColor", LanternFlameColor);
            SetFloat(ambience, "frontIntensity", LanternFrontIntensity);
            SetFloat(ambience, "frontRange", LanternFrontRange);
            SetFloat(ambience, "rearIntensity", LanternRearIntensity);
            SetFloat(ambience, "rearRange", LanternRearRange);
            SetFloat(ambience, "gallopCueStartSpeed", LanternGallopCueStartSpeed);
            SetFloat(ambience, "gallopCueMaximumSpeed", LanternGallopCueMaximumSpeed);
            SetFloat(ambience, "gallopIntensityBoost", LanternGallopIntensityBoost);
            SetFloat(ambience, "modulationAmplitude", LanternPulseAmplitude);
            SetFloat(ambience, "modulationFrequency", LanternPulseFrequency);
            EditorUtility.SetDirty(ambience);
            return true;
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
                spawner.ConfigureFirstGallopSource(motor);
                spawner.ConfigureTiming(false, SecondsBeforeFirstMonster, MonsterSpawnInterval, true);
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
            // Measured against the wagon, not the block deck: the deck is far shorter than the real
            // carriage, so using it left the attachment points buried inside the cart.
            float rearZ = GetCartRearLocalZ(vehicle) + 0.45f;
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

        // ---------------------------------------------------------------- legacy whip cleanup and rein grips

        /// <summary>
        /// Detached one-hand whips are legacy. Keep their scene data recoverable, but turn them off
        /// once the continuous rein loop is present.
        /// </summary>
        private static void DisableDetachedWhipHandles(Transform vehicleRoot)
        {
            if (vehicleRoot.GetComponentInChildren<ClosedReinLoop>(true) == null)
            {
                return;
            }

            // Legacy whips were separate from the reins and allowed one-handed acceleration. Keep
            // their scene data recoverable, but deactivate them once the unified rein loop exists.
            foreach (WhipHandle whip in vehicleRoot.GetComponentsInChildren<WhipHandle>(true))
            {
                if (whip == null || !whip.gameObject.activeSelf)
                {
                    continue;
                }

                Undo.RecordObject(whip.gameObject, "Disable standalone whip handle");
                whip.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Replaces the old detached grab zones with one visible grip at the existing rope pin.
        /// The old objects stay in the scene inactive so the change can be undone or inspected.
        /// </summary>
        private static bool ConfigureUnifiedReinGrip(
            Transform vehicleRoot,
            string pinName,
            Handedness expectedHand)
        {
            Transform pin = FindChild(vehicleRoot, pinName);
            if (pin == null)
            {
                Debug.LogWarning($"Could not find {pinName}; that rope endpoint was left unchanged.");
                return false;
            }

            ReinHandle rein = pin.GetComponent<ReinHandle>();
            if (rein == null)
            {
                rein = Undo.AddComponent<ReinHandle>(pin.gameObject);
            }

            const string gripName = "Unified_ReinGrip";
            Transform gripTransform = pin.Find(gripName);
            if (gripTransform == null)
            {
                GameObject gripObject = InstantiatePrefab(RopeProxyPrefabPath, pin, Vector3.zero);
                gripObject.name = gripName;
                gripTransform = gripObject.transform;
                Undo.RegisterCreatedObjectUndo(gripObject, "Add unified rein grip");
            }

            GameObject grip = gripTransform.gameObject;
            MeshCollider meshCollider = grip.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.enabled = false;
            }

            CapsuleCollider capsule = grip.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = ReinGrabZoneRadius;
                capsule.height = ReinGrabZoneLength;
                capsule.direction = 1;
                capsule.center = Vector3.zero;
            }

            Rigidbody body = grip.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            Grabbable grabbable = grip.GetComponent<Grabbable>();
            if (grabbable != null)
            {
                SetBool(grabbable, "_throwWhenUnselected", false);
            }

            foreach (Renderer renderer in grip.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            HandGrabInteractable gripInteractable = grip.GetComponentInChildren<HandGrabInteractable>(true);
            if (gripInteractable == null)
            {
                Debug.LogWarning($"{gripName} under {pinName} has no HandGrabInteractable.");
                return false;
            }

            DeactivatePreviousReinZones(rein, gripInteractable);

            Undo.RecordObject(rein, "Configure unified rein grip");
            var reinProperties = new SerializedObject(rein);
            reinProperties.Update();
            SetSerializedInt(reinProperties, "expectedHand", (int)expectedHand);
            SetSerializedBool(reinProperties, "requireExpectedHand", true);
            SerializedProperty legacyZone = reinProperties.FindProperty("interactable");
            if (legacyZone != null)
            {
                legacyZone.objectReferenceValue = null;
            }

            SerializedProperty zones = reinProperties.FindProperty("grabPoints");
            if (zones != null)
            {
                zones.arraySize = 1;
                zones.GetArrayElementAtIndex(0).objectReferenceValue = gripInteractable;
            }

            reinProperties.ApplyModifiedProperties();
            return true;
        }

        private static void DeactivatePreviousReinZones(ReinHandle rein, HandGrabInteractable activeGrip)
        {
            var serialized = new SerializedObject(rein);
            SerializedProperty zones = serialized.FindProperty("grabPoints");
            if (zones != null)
            {
                for (int index = 0; index < zones.arraySize; index++)
                {
                    DeactivatePreviousReinZone(
                        zones.GetArrayElementAtIndex(index).objectReferenceValue as HandGrabInteractable,
                        activeGrip);
                }
            }

            SerializedProperty legacyZone = serialized.FindProperty("interactable");
            if (legacyZone != null)
            {
                DeactivatePreviousReinZone(legacyZone.objectReferenceValue as HandGrabInteractable, activeGrip);
            }
        }

        private static void DeactivatePreviousReinZone(
            HandGrabInteractable previousZone,
            HandGrabInteractable activeGrip)
        {
            if (previousZone == null || previousZone == activeGrip ||
                previousZone.transform.IsChildOf(activeGrip.transform))
            {
                return;
            }

            GameObject zoneObject = previousZone.gameObject;
            if (!zoneObject.activeSelf)
            {
                return;
            }

            Undo.RecordObject(zoneObject, "Deactivate detached rein grab zone");
            zoneObject.SetActive(false);
        }

        /// <summary>
        /// Finds the handle or builds it, so running the tool again never duplicates it. An object
        /// with the right name but no WhipHandle is left alone with a warning instead of being
        /// destroyed: manual work is never overwritten.
        /// </summary>
        private static WhipHandle EnsureWhipHandle(
            Transform vehicleRoot,
            string name,
            Vector3 restLocalPosition,
            Handedness hand,
            Key desktopKey,
            CarriageMotor motor,
            AudioClip lashClip)
        {
            Transform existing = FindChild(vehicleRoot, name);
            if (existing != null)
            {
                if (!existing.TryGetComponent(out WhipHandle found))
                {
                    Debug.LogWarning(
                        $"{name} already exists without a WhipHandle component, so it is left untouched. " +
                        "Rename or remove it if you want the tool to build the whip again.");
                    return null;
                }

                WireWhipHandle(found, hand, motor, vehicleRoot, desktopKey, lashClip, isNew: false);
                return found;
            }

            GameObject instance = InstantiatePrefab(RopeProxyPrefabPath, vehicleRoot, restLocalPosition);
            instance.name = name;
            // The rope grip prefab is squashed and visible: normalise it and hide it, exactly like the
            // knife and the rope grab zones, so the handle the player sees is what gets grabbed. It is
            // turned around so the handle points where the rider looks.
            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            MeshCollider proxyHull = instance.GetComponent<MeshCollider>();
            if (proxyHull != null)
            {
                proxyHull.enabled = false;
            }

            CapsuleCollider grip = instance.GetComponent<CapsuleCollider>();
            if (grip != null)
            {
                grip.radius = WhipGripRadius;
                grip.height = WhipHandleLength;
                grip.direction = 2;
                grip.center = new Vector3(0f, 0f, WhipHandleLength * 0.5f);
            }

            // A released whip must not be thrown off the carriage by the grab physics: WhipHandle
            // keeps it kinematic and drifts it back to its rest pose.
            Grabbable grabbable = instance.GetComponent<Grabbable>();
            if (grabbable != null)
            {
                SetBool(grabbable, "_throwWhenUnselected", false);
            }

            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            CreateWhipVisual(instance.transform);
            WhipHandle whip = instance.AddComponent<WhipHandle>();
            WireWhipHandle(whip, hand, motor, vehicleRoot, desktopKey, lashClip, isNew: true);
            return whip;
        }

        /// <summary>
        /// Wires one handle. When the handle already existed only the missing references are filled,
        /// so anything tuned by hand in the Inspector survives.
        /// </summary>
        private static void WireWhipHandle(
            WhipHandle whip,
            Handedness hand,
            CarriageMotor motor,
            Transform vehicleRoot,
            Key desktopKey,
            AudioClip lashClip,
            bool isNew)
        {
            Transform root = whip.transform;
            Transform tip = CreateWhipTip(root);
            LineRenderer rope = CreateWhipRope(root);
            AudioSource lashSource = CreateWhipAudio(whip.gameObject);
            HandGrabInteractable interactable = root.GetComponentInChildren<HandGrabInteractable>();

            WireObjectIfNull(whip, "accelerationRequester", motor);
            WireObjectIfNull(whip, "velocityReference", vehicleRoot);
            WireObjectIfNull(whip, "tip", tip);
            WireObjectIfNull(whip, "interactable", interactable);
            WireObjectIfNull(whip, "rope", rope);
            WireObjectIfNull(whip, "lashAudioSource", lashSource);
            WireObjectIfNull(whip, "lashClip", lashClip);

            // The hand each handle belongs to is its identity, so it is always enforced.
            SetInt(whip, "expectedHand", (int)hand);
            if (isNew)
            {
                SetInt(whip, "desktopKey", (int)desktopKey);
            }
        }

        /// <summary>The provisional handle: a plain cylinder, since no whip model exists yet.</summary>
        private static void CreateWhipVisual(Transform parent)
        {
            if (parent.Find("Handle_Visual") != null)
            {
                return;
            }

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Handle_Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0f, WhipHandleLength * 0.5f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = new Vector3(
                WhipHandleRadius * 2f, WhipHandleLength * 0.5f, WhipHandleRadius * 2f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            Material handleMaterial = AssetDatabase.LoadAssetAtPath<Material>(WhipHandleMaterialPath);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null && handleMaterial != null)
            {
                renderer.sharedMaterial = handleMaterial;
            }
        }

        private static Transform CreateWhipTip(Transform parent)
        {
            Transform existing = parent.Find("Tip");
            if (existing != null)
            {
                return existing;
            }

            return CreateChild(parent, "Tip", new Vector3(0f, 0f, WhipTipDistance));
        }

        /// <summary>A light LineRenderer cord: a handful of points and a small sway, no physics.</summary>
        private static LineRenderer CreateWhipRope(Transform parent)
        {
            Transform existing = parent.Find("Whip_Rope");
            if (existing != null)
            {
                LineRenderer found = existing.GetComponent<LineRenderer>();
                if (found != null)
                {
                    return found;
                }
            }

            var ropeObject = new GameObject("Whip_Rope");
            ropeObject.transform.SetParent(parent, false);
            LineRenderer line = ropeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = WhipRopeSegments;
            line.widthMultiplier = 0.02f;
            line.numCapVertices = 4;

            Material ropeMaterial = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
            if (ropeMaterial != null)
            {
                line.sharedMaterial = ropeMaterial;
            }

            for (var i = 0; i < WhipRopeSegments; i++)
            {
                float t = i / (float)(WhipRopeSegments - 1);
                line.SetPosition(i, new Vector3(0f, 0f, WhipTipDistance) - Vector3.up * (WhipRopeLength * t));
            }

            return line;
        }

        private static AudioSource CreateWhipAudio(GameObject whipObject)
        {
            AudioSource source = whipObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = whipObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 35f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        /// <summary>Integrates two separate short rein grips without changing motor or spawner settings.</summary>
        [MenuItem("Tools/Game/Integrate Separate Short Rein Grips (current scene)")]
        public static void IntegrateSeparateReinGripsMenu()
        {
            if (!TryIntegrateSeparateReinGrips(out string report))
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }
        }

        private static bool TryIntegrateSeparateReinGrips(out string report)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                report = $"Open {ScenePath} first; current scene is '{scene.path}'.";
                return false;
            }

            GameObject vehicleRootObject = GameObject.Find("VehicleRoot");
            ClosedReinLoop loop = vehicleRootObject != null
                ? vehicleRootObject.GetComponentInChildren<ClosedReinLoop>(true)
                : null;
            if (vehicleRootObject == null || loop == null)
            {
                report = "VehicleRoot or ClosedReinLoop is missing; no scene objects were changed.";
                return false;
            }

            ReinHandle[] reins = vehicleRootObject.GetComponentsInChildren<ReinHandle>(true);
            ReinHandle left = System.Array.Find(
                reins, rein => rein.name.StartsWith("Left", System.StringComparison.Ordinal));
            ReinHandle right = System.Array.Find(
                reins, rein => rein.name.StartsWith("Right", System.StringComparison.Ordinal));
            if (reins.Length != 2 || left == null || right == null)
            {
                report = $"Expected exactly one left and one right ReinHandle, found {reins.Length}; no scene objects were changed.";
                return false;
            }

            ReinGripIntegrationPlan leftPlan;
            ReinGripIntegrationPlan rightPlan;
            bool leftReady = TryPrepareReinGrip(left, out leftPlan);
            bool rightReady = TryPrepareReinGrip(right, out rightPlan);
            if (!leftReady || !rightReady)
            {
                report = "Both hand-grab zones and original tracked hand meshes must validate before integration; no scene objects were changed.";
                return false;
            }

            if (!TryGetOriginalHandVisual(Handedness.Left, out Renderer leftHandVisual) ||
                !TryGetOriginalHandVisual(Handedness.Right, out Renderer rightHandVisual))
            {
                report = "Could not resolve both active Oculus hand-mesh renderers safely; no scene objects were changed.";
                return false;
            }

            Material gripMaterial = AssetDatabase.LoadAssetAtPath<Material>(ReinGripMaterialPath);
            if (gripMaterial == null)
            {
                report = $"Required rein grip material was not found at {ReinGripMaterialPath}; no scene objects were changed.";
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate separate short rein grips");
            try
            {
                ApplySeparateReinGrabZone(leftPlan, Handedness.Left);
                ApplySeparateReinGrabZone(rightPlan, Handedness.Right);
                SetReinHandleVisual(left, gripMaterial);
                SetReinHandleVisual(right, gripMaterial);
                CreateSeparateReinGripVisuals(
                    left, right, leftHandVisual, rightHandVisual, gripMaterial);

                Transform existingBar = vehicleRootObject.transform.Find("Shared_ReinBar");
                if (existingBar != null && existingBar.gameObject.activeSelf)
                {
                    Undo.RecordObject(existingBar.gameObject, "Remove shared rein bar from view");
                    existingBar.gameObject.SetActive(false);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new System.InvalidOperationException($"Unity could not save {ScenePath}.");
                }

                Undo.CollapseUndoOperations(undoGroup);
                report = $"Separate short rein grips and hand visuals wired in {ScenePath}; motor and spawner settings were not changed.";
                return true;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                report = $"Shared rein integration reverted in memory; verify the saved scene if saving failed: {exception.Message}";
                return false;
            }
        }

        private sealed class ReinGripIntegrationPlan
        {
            public ReinHandle rein;
            public Transform zone;
            public HandGrabInteractable interactable;
            public bool createZone;
        }

        private static bool TryPrepareReinGrip(
            ReinHandle rein, out ReinGripIntegrationPlan plan)
        {
            plan = null;
            if (rein == null)
            {
                return false;
            }

            const string zoneName = "Rein_HandGrabZone";
            Transform zone = rein.transform.Find(zoneName);
            bool createZone = zone == null;
            HandGrabInteractable interactable;
            if (createZone)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RopeProxyPrefabPath);
                interactable = prefab != null
                    ? prefab.GetComponentInChildren<HandGrabInteractable>(true)
                    : null;
                if (interactable == null)
                {
                    return false;
                }
            }
            else
            {
                interactable = zone.GetComponentInChildren<HandGrabInteractable>(true);
                if (interactable == null)
                {
                    return false;
                }
            }

            plan = new ReinGripIntegrationPlan
            {
                rein = rein,
                zone = zone,
                interactable = interactable,
                createZone = createZone
            };
            return true;
        }

        private static void ApplySeparateReinGrabZone(ReinGripIntegrationPlan plan, Handedness expectedHand)
        {
            Transform zone = plan.zone;
            if (plan.createZone)
            {
                GameObject zoneObject = InstantiatePrefab(RopeProxyPrefabPath, plan.rein.transform, Vector3.zero);
                zoneObject.name = "Rein_HandGrabZone";
                zone = zoneObject.transform;
                zone.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(zoneObject, "Create separate rein hand grab zone");
                plan.interactable = zone.GetComponentInChildren<HandGrabInteractable>(true);
            }

            foreach (Renderer renderer in zone.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Hide SDK grip proxy visuals");
                renderer.enabled = false;
            }
            ConfigureGrabProxyWithUndo(zone.gameObject);

            HandGrabInteractable rootInteractable = plan.rein.GetComponent<HandGrabInteractable>();
            if (rootInteractable != null)
            {
                Undo.RecordObject(rootInteractable, "Separate rein grab transform from pin");
                rootInteractable.enabled = false;
            }

            foreach (Collider collider in plan.rein.GetComponents<Collider>())
            {
                Undo.RecordObject(collider, "Disable competing rein pin collider");
                collider.enabled = false;
            }

            Undo.RecordObject(plan.rein, "Assign rein hand ownership");
            var serialized = new SerializedObject(plan.rein);
            serialized.Update();
            SetSerializedInt(serialized, "expectedHand", (int)expectedHand);
            SetSerializedBool(serialized, "requireExpectedHand", true);
            SerializedProperty legacy = serialized.FindProperty("interactable");
            if (legacy != null)
            {
                legacy.objectReferenceValue = null;
            }

            SerializedProperty zones = serialized.FindProperty("grabPoints");
            if (zones != null)
            {
                zones.arraySize = 1;
                zones.GetArrayElementAtIndex(0).objectReferenceValue = plan.interactable;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(plan.rein);
        }

        private static void ConfigureGrabProxyWithUndo(GameObject grabZone)
        {
            foreach (MonoBehaviour behaviour in grabZone.GetComponents<MonoBehaviour>())
            {
                Undo.RecordObject(behaviour, "Configure rein grab proxy");
            }
            foreach (Collider collider in grabZone.GetComponents<Collider>())
            {
                Undo.RecordObject(collider, "Configure rein grab proxy collider");
            }
            Rigidbody body = grabZone.GetComponent<Rigidbody>();
            if (body != null)
            {
                Undo.RecordObject(body, "Configure rein grab proxy body");
            }
            ConfigureReinGrabProxy(grabZone);
        }

        /// <summary>Adds the handles and the whip speed tuning to the scene that is already open.</summary>
        [MenuItem("Tools/Game/Use Unified Reins (current scene)")]
        public static void ApplyWhipSetupMenu()
        {
            bool applied = TryApplyWhipSetupToOpenScene(out string report);
            if (!applied)
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }

            if (!Application.isBatchMode && !suppressDialogs)
            {
                EditorUtility.DisplayDialog("Rienda unificada", report, "OK");
            }
        }

        /// <summary>Batch entry point. Never shows dialogs.</summary>
        public static void ApplyWhipSetupFromCommandLine()
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
                if (!TryApplyWhipSetupToOpenScene(out string report))
                {
                    throw new System.InvalidOperationException(report);
                }

                Debug.Log(report);
            }
            finally
            {
                suppressDialogs = false;
            }
        }

        /// <summary>
        /// Idempotent: connects the continuous rein system and deactivates detached legacy whips.
        /// </summary>
        private static bool TryApplyWhipSetupToOpenScene(out string report)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                report = $"Open {ScenePath} first; the current scene is '{scene.path}'.";
                return false;
            }

            GameObject vehicleRootObject = GameObject.Find("VehicleRoot");
            CarriageMotor motor = Object.FindAnyObjectByType<CarriageMotor>();
            if (vehicleRootObject == null || motor == null)
            {
                report = $"{ScenePath} has no VehicleRoot with a CarriageMotor.";
                return false;
            }

            if (vehicleRootObject.GetComponentInChildren<ClosedReinLoop>(true) == null)
            {
                report = $"{ScenePath} has no ClosedReinLoop; the rein endpoints were left unchanged.";
                return false;
            }

            if (!TryIntegrateSeparateReinGrips(out string gripReport))
            {
                report = gripReport;
                return false;
            }
            bool leftGripReady = true;
            bool rightGripReady = true;
            DisableDetachedWhipHandles(vehicleRootObject.transform);
            SetFloat(motor, "accelerationPerStroke", WhipAccelerationPerStroke);
            SetFloat(motor, "coastingDeceleration", WhipCoastingDeceleration);

            MonsterSpawner spawner = Object.FindAnyObjectByType<MonsterSpawner>();
            if (spawner != null)
            {
                Undo.RecordObject(spawner, "Wait for first rein gallop before spawning monsters");
                spawner.ConfigureFirstGallopSource(motor);
                spawner.ConfigureFirstGallopWait(true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                report = $"Unity could not save {ScenePath}.";
                return false;
            }

            bool valid = ValidateOpenScene(out string validation);
            report =
                $"Rienda continua configurada en {ScenePath}; mangos izquierdo/derecho: " +
                $"{leftGripReady}/{rightGripReady}; látigos separados desactivados, " +
                $"gallop acceleration = {WhipAccelerationPerStroke}, " +
                $"coastingDeceleration = {WhipCoastingDeceleration}.\n{validation}";
            return valid && leftGripReady && rightGripReady;
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
            SetBool(road, "buildLaneDividers", false);
            SetBool(road, "buildRoadForks", false);
            SetFloat(road, "curvatureScale", 0.8f);
            SetFloat(road, "turnRadius", 100f);
            SetFloat(road, "heightAmplitude", 0.55f);
            SetFloat(road, "heightWavelength", 90f);
            SetFloat(road, "forkDivergence", 6f);
            // Three rows of five covers the carriageway's whole length and still fills the sides; the
            // rows reach past the fog so only the road itself stays open. Kept deliberately modest
            // because foliage is the heaviest thing in the scene, and each tile switches as one batch.
            SetInt(road, "forestRows", 3);
            SetInt(road, "farForestRows", 2);
            SetInt(road, "treesPerRow", 5);
            SetFloat(road, "forestRowSpacing", 5.5f);
            SetBool(road, "buildDecorativeEdgeStones", true);
            SetInt(road, "edgeStonePairsPerTile", 2);
            SetInt(road, "treeBatchChunkSpan", TreeBatchChunkSpan);
            SetInt(road, "lanternLitChunkSpan", PostLanternLitChunkSpan);
            SetInt(road, "totalTiles", TotalRoadTiles);
            WireObject(road, "vehicleRoot", vehicleRoot);
            WireObject(road, "rockPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath));
            SetObjectArray(road, "treePrefabs", LoadAssets(TreeModelPaths));
            SetFloat(road, "treeHeight", 11f);
            // A lit post on each side of the carriageway, recycled with the tiles and only switched on
            // around the carriage so the road is lit without paying for a strip of lights down 700 m.
            WireObject(road, "postLanternPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PostLanternModelPath));
            SetFloat(road, "postLanternSpacing", PostLanternSpacing);
            SetFloat(road, "postLanternOffset", PostLanternOffset);
            SetFloat(road, "postLanternHeight", PostLanternHeight);
            SetColor(road, "postLanternColor", PostLanternColor);
            SetFloat(road, "postLanternIntensity", PostLanternIntensity);
            SetFloat(road, "postLanternRange", PostLanternRange);
            return roadRoot;
        }

        private static void CreateGround(Transform vehicleRoot)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            // A forest floor rather than Unity's default grey: with that grey plane behind it the dirt
            // carriageway had nothing to stand out against and read as part of the same surface.
            ground.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateLitMaterial("Mat_ForestFloor", new Color(0.07f, 0.15f, 0.06f));
            // A six hundred metre plane casts nothing anyone will ever see, so it stops casting.
            ground.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            // Seated at the carriage's own height so the edit-time scene view agrees with where
            // CartFollowGround puts it at runtime; otherwise the carriage reads as floating here.
            var localOffset = new Vector3(0f, -0.01f, 0f);
            ground.transform.position = vehicleRoot.position + localOffset;
            ground.AddComponent<MonsterGroundSurface>();
            CartFollowGround follow = ground.AddComponent<CartFollowGround>();
            follow.Configure(vehicleRoot, localOffset);
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
            // Seated a little forward of the cart's centre so the hunter is not at the very back of the bed.
            rig.transform.localPosition = new Vector3(0f, 0.695f, -0.6f);
            rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            ConfigureOvrManager(rig);
            OVRQuickActionsAPI.AddOVRInteractionRig(false);
            DisableArtificialLocomotion();

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

        /// <summary>
        /// Turns off the interaction rig's free-movement locomotor. It is wired with the VehicleRoot as
        /// its player origin, so whenever the bare player rig "falls" — which is what happens with no
        /// floor to stand on in the editor — it drags the whole carriage down with it, sinking the cart
        /// through the road over a couple of minutes. The hunter rides the cart, so there is no artificial
        /// locomotion to drive here and the rig rule is that the origin is never moved. Disabled, not
        /// removed, so it can be switched back on from the Inspector.
        /// </summary>
        private static void DisableArtificialLocomotion()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Name != "FirstPersonLocomotor")
                {
                    continue;
                }

                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
                Debug.Log(
                    "Disabled the interaction rig's FirstPersonLocomotor: it moved the player origin, " +
                    "which is the carriage root, and sank the cart through the road.");
            }
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
                // HandsOnly project: the hands must come from hand tracking alone. ConformingToController
                // makes the runtime pose them from a controller, and with no controller paired that
                // leaves the hands with no tracked pose at all — the hand tracking looks dead.
                SetSerializedInt(serialized, "controllerDrivenHandPosesType", 0);
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
            // Resting on the bed, just ahead and to the right of where the hunter stands. The old
            // fixed height was below the real wagon's floor once it was fitted, so the knife sat
            // buried inside the boards and read as missing.
            float bedFloor = GetWagonBedFloorLocalY(vehicleRoot);
            var restPosition = new Vector3(0.55f, bedFloor + 0.07f, -0.45f);
            GameObject knife = InstantiatePrefab(RopeProxyPrefabPath, vehicleRoot, restPosition);
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
                        spawnDirection = MonsterSpawnDirection.FrontLane,
                        isFaceThreat = true,
                        frontLaneForwardRadius = BatRoundApproachRadius,
                        weight = 1f,
                        minimumFlyingHeight = 3.5f,
                        maximumFlyingHeight = 7f,
                        attachmentLoad = 0f,
                        overrideTargetStrategy = true,
                        targetStrategy = MonsterTargetStrategy.PrioritizeHunter
                    }
                },
                ~0,
                ~0,
                attachmentPoints,
                cartLoad,
                hunterTarget);
            WireObject(spawner, "rearReachPoint", rearAnchor);
            FaceBatThreatController faceThreat = systemObject.AddComponent<FaceBatThreatController>();
            faceThreat.Configure(spawner, headAnchor);
            SetInt(faceThreat, "batsPerRound", BatRoundSize);
            SetFloat(faceThreat, "betweenRounds", BatRoundCooldown);

            // The delay starts only after the first valid two-handed gallop.
            spawner.ConfigureFirstGallopSource(vehicleRoot.GetComponent<CarriageMotor>());
            spawner.ConfigureTiming(false, SecondsBeforeFirstMonster, MonsterSpawnInterval, true);
            // A permanent horde behind the cart, of which only a few run fast enough to catch it.
            spawner.ConfigureHorde(MinimumHordeSize, FastHordeFraction, FastHordeSpeedMultiplier);
            SetInt(spawner, "maximumActiveMonsters", MinimumHordeSize + BatRoundSize + 1);

            GiantZombieSpawner giantSpawner = systemObject.AddComponent<GiantZombieSpawner>();
            GameObject giantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GiantPrefabPath);
            giantSpawner.Configure(giantPrefab, vehicleRoot, rearAnchor, 22f, ~0);
            giantSpawner.ConfigureSpeedTrigger(true, 1.4f, 2.5f, vehicleRoot.GetComponent<CarriageMotor>(), 30f);
            SetBool(giantSpawner, "showGiantBehindHorde", true);

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
            // The points used to be pinned to the block prototype's rear, which ended up inside the
            // real wagon once it was fitted: attached monsters walked into the cart and jammed there.
            // They are placed relative to the wagon's own rear edge so any change of size still
            // leaves them hanging behind the carriage.
            float rearZ = GetCartRearLocalZ(vehicleRoot);

            var points = new List<MonsterAttachmentPoint>();
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RearCenter",
                new Vector3(0f, 1.10f, rearZ + 1.05f), MonsterAttachmentKind.Ground, 2));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_LeftRear",
                new Vector3(-1.60f, 1.00f, rearZ + 0.65f), MonsterAttachmentKind.Ground, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RightRear",
                new Vector3(1.60f, 1.00f, rearZ + 0.65f), MonsterAttachmentKind.Ground, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_LeftRearFlying",
                new Vector3(-1.85f, 1.75f, rearZ + 0.95f), MonsterAttachmentKind.Flying, 1));
            points.Add(CreateAttachmentPoint(vehicleRoot, "Attach_RightRearFlying",
                new Vector3(1.85f, 1.75f, rearZ + 0.95f), MonsterAttachmentKind.Flying, 1));
            return points;
        }

        /// <summary>
        /// Local Y of the wagon's rail top, expressed under the vehicle root. The rope and the grips
        /// are placed just above it so the rein rides over the cart instead of through it.
        /// </summary>
        private static float GetWagonTopLocalY(Transform vehicleRoot)
        {
            GameObject wagon = GameObject.Find("Wagon_Model");
            if (wagon != null && TryGetRendererBounds(wagon, out Bounds bed))
            {
                return bed.max.y - vehicleRoot.position.y;
            }

            return 0.83f;
        }

        /// <summary>
        /// Local Y of the wagon's bed floor, expressed under the vehicle root. The wheels take the
        /// lower half of the model and the bed the upper half, so the floor sits at half its height;
        /// deriving it keeps the loose props on the bed when the wagon's size changes.
        /// </summary>
        private static float GetWagonBedFloorLocalY(Transform vehicleRoot)
        {
            GameObject wagon = GameObject.Find("Wagon_Model");
            if (wagon != null && TryGetRendererBounds(wagon, out Bounds bed))
            {
                return (bed.max.y - vehicleRoot.position.y) * 0.5f;
            }

            return 0.62f;
        }

        /// <summary>
        /// Local Z of the carriage's rear edge, taken from the wagon that is actually fitted and
        /// falling back to the block deck only when the model is missing.
        /// </summary>
        private static float GetCartRearLocalZ(Transform vehicleRoot)
        {
            GameObject wagon = GameObject.Find("Wagon_Model");
            if (wagon != null && TryGetRendererBounds(wagon, out Bounds bed))
            {
                return vehicleRoot.InverseTransformPoint(
                    new Vector3(vehicleRoot.position.x, vehicleRoot.position.y, bed.max.z)).z;
            }

            Transform floor = FindChild(vehicleRoot, "CarriagePrototype/CarriageFloor");
            if (floor != null && TryGetRendererBounds(floor.gameObject, out Bounds floorBounds))
            {
                return vehicleRoot.InverseTransformPoint(
                    new Vector3(vehicleRoot.position.x, vehicleRoot.position.y, floorBounds.max.z)).z;
            }

            return 1.7f;
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
            var sign = new GameObject("KingdomArrivalSign");
            sign.transform.SetParent(kingdom, false);
            sign.transform.localPosition = new Vector3(0f, 9.0f, -2.5f);
            CreatePrimitive(PrimitiveType.Cube, "WoodenSign", sign.transform,
                Vector3.zero, new Vector3(5.6f, 1.36f, 0.12f), lampMaterial);

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
                Debug.LogWarning("No usable font was found: the kingdom sign has no text.");
                sign.SetActive(false);
                return sign;
            }

            var labelObject = new GameObject("EngravedArrival", typeof(TextMesh), typeof(MeshRenderer));
            labelObject.transform.SetParent(sign.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, 0.07f);
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh label = labelObject.GetComponent<TextMesh>();
            label.font = font;
            label.fontSize = 96;
            label.characterSize = 0.006f;
            label.alignment = TextAlignment.Center;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.88f, 0.62f);
            label.text = "Llegaste al reino\nVICTORIA";
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            sign.SetActive(false);
            return sign;
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
            ValidateKnife(failures);
            ValidateCarriageLamps(failures);

            ReinHandle[] reins = Object.FindObjectsByType<ReinHandle>();
            Require(reins.Length == 2, "Two separate short hand grips exist on the continuous rein.", failures);
            foreach (ReinHandle rein in reins)
            {
                Require(rein.GrabPointCount == 1,
                    $"{rein.name} is assigned one hand-grabbable zone (found {rein.GrabPointCount} grab points).", failures);
                Renderer handleRenderer = rein.GetComponent<Renderer>();
                Require(handleRenderer != null && handleRenderer.enabled,
                    $"{rein.name} has its own visible short handle.", failures);
                Require(Mathf.Approximately(rein.transform.localScale.y, ReinHandleVisualLength * 0.5f),
                    $"{rein.name} uses the configured {ReinHandleVisualLength:F2} m handle length.", failures);
                Transform zone = rein.transform.Find("Rein_HandGrabZone");
                CapsuleCollider grabCollider = zone != null ? zone.GetComponent<CapsuleCollider>() : null;
                Require(grabCollider != null &&
                        Mathf.Approximately(grabCollider.radius, ReinGrabZoneRadius) &&
                        Mathf.Approximately(grabCollider.height, ReinGrabZoneLength),
                    $"{rein.name} uses the reduced, separate hand-grab zone.", failures);
                Require(rein.RestLocalPosition.y <= 1.6f,
                    $"{rein.name} rests low enough to be lifted and yanked.", failures);
                Require(rein.RestLocalPosition.y > GetWagonTopLocalY(rein.transform.parent),
                    $"{rein.name} rides above the wagon's rails so the rein never cuts through it.", failures);
            }

            Transform sharedBar = GameObject.Find("Shared_ReinBar")?.transform;
            Require(sharedBar == null || !sharedBar.gameObject.activeInHierarchy,
                "No shared bar connects the two separate rein grips.", failures);

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
            Require(spawner != null && spawner.WaitsForFirstGallop,
                "The first monster timer waits for the first valid gallop.", failures);
            Require(spawner != null && spawner.HasFirstGallopSource,
                "The monster timer is connected to the carriage's first-gallop event.", failures);

            MonsterTargetRegistry registry = Object.FindAnyObjectByType<MonsterTargetRegistry>();
            Require(registry != null && registry.IsConfigured && registry.Targets.Count >= 1,
                "The target registry contains at least the player target.", failures);

            CartMonsterLoad cartLoad = Object.FindAnyObjectByType<CartMonsterLoad>();
            Require(cartLoad != null, "CartMonsterLoad exists.", failures);
            if (cartLoad != null)
            {
                Require(cartLoad.HasReceiver,
                    "CartMonsterLoad is connected to the carriage speed receiver.", failures);
                Require(cartLoad.ReferenceMaximumLoad > 0f && cartLoad.MinimumSpeedMultiplier >= 0f,
                    "CartMonsterLoad exposes valid balance values.", failures);
                Require(Mathf.Approximately(cartLoad.SpeedMultiplier, 1f),
                    "An unloaded cart starts with a neutral speed multiplier.", failures);
            }

            WhipHandle[] whips = Object.FindObjectsByType<WhipHandle>(FindObjectsInactive.Include);
            bool hasActiveStandaloneWhip = false;
            foreach (WhipHandle whip in whips)
            {
                hasActiveStandaloneWhip |= whip != null && whip.gameObject.activeInHierarchy && whip.enabled;
            }

            Require(!hasActiveStandaloneWhip,
                "Detached single-hand whip handles are inactive; the two rein end grips drive all rein gestures.", failures);
            Require(Mathf.Approximately(motor != null ? motor.SpeedMultiplier : 1f, 1f),
                "The carriage starts with a neutral monster-load speed multiplier.", failures);

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

        /// <summary>Bounds of two objects taken together, used to size the bed against the real horses.</summary>
        private static bool TryGetCombinedBounds(Transform first, Transform second, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            if (first != null && TryGetRendererBounds(first.gameObject, out Bounds firstBounds))
            {
                bounds = firstBounds;
                hasBounds = true;
            }

            if (second != null && TryGetRendererBounds(second.gameObject, out Bounds secondBounds))
            {
                if (hasBounds)
                {
                    bounds.Encapsulate(secondBounds);
                }
                else
                {
                    bounds = secondBounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
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

        /// <summary>
        /// The rein material is authored once and shared, so its look is re-asserted on every run
        /// instead of only on creation. A rein is a plain beige rope: the previous red emissive
        /// finish made it glow in the dark and read as a light source rather than leather.
        /// </summary>
        private static Material GetOrCreateRopeMaterial()
        {
            var beige = new Color(0.85f, 0.76f, 0.58f);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "Mat_RopeProxy" };
                AssetDatabase.CreateAsset(material, RopeMaterialPath);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", beige);
            }

            material.color = beige;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
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

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                return null;
            }

            // The look is re-asserted on every run rather than only when the asset is first created, so
            // tuning the night does not require deleting the material by hand.
            var material = existing != null ? existing : new Material(shader) { name = name };
            // Keep the sky near black and the moon disk small with a crisp edge.
            material.SetColor("_SkyTint", new Color(0.008f, 0.010f, 0.016f));
            material.SetColor("_GroundColor", new Color(0.003f, 0.003f, 0.004f));
            material.SetFloat("_AtmosphereThickness", 0.75f);
            material.SetFloat("_SunSize", 0.035f);
            material.SetFloat("_SunSizeConvergence", 4f);
            material.SetFloat("_Exposure", 0.62f);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
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

        /// <summary>
        /// Fills a reference only when it is still empty, so re-running the tooling never overwrites
        /// a link that was set by hand in the Inspector.
        /// </summary>
        private static void WireObjectIfNull(Object target, string fieldName, Object value)
        {
            if (value == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"Field '{fieldName}' was not found on {target.GetType().Name}.");
                return;
            }

            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
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

        /// <summary>
        /// Writes a number to whichever serialized field it is, picking the accessor from the property's
        /// own type. Writing intValue into a float field, or into an enum, silently produces zero.
        /// </summary>
        private static void SetSerializedNumber(SerializedObject serialized, string fieldName, float value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"Serialized field '{fieldName}' was not found on {serialized.targetObject}.");
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = Mathf.RoundToInt(value);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = Mathf.RoundToInt(value);
                    break;
                default:
                    Debug.LogWarning(
                        $"Serialized field '{fieldName}' is a {property.propertyType} and was not set.");
                    break;
            }
        }

        private static void SetColor(Object target, string fieldName, Color value)
        {
            var serialized = new SerializedObject(target);
            SetSerializedColor(serialized, fieldName, value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedColor(SerializedObject serialized, string fieldName, Color value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.colorValue = value;
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

                Require(horse.GetComponent<HorseAnimationDriver>() != null,
                    $"{name} has the clip-driven idle/gallop state machine.", failures);
            }

            Require(found == 2, "Both real horse models are present.", failures);
            Require(GameObject.Find("ReinCollarAnchor") != null,
                "The reins attach to a collar anchor on the real horse neck.", failures);
            RequireNoActiveRenderers("HorsePlaceholders",
                "The old block horse heads and bodies are hidden.", failures);
            Require(GameObject.Find("Wagon_Model") != null,
                "The real wagon model replaced the block-built carriage.", failures);
        }

        /// <summary>
        /// Guards against the placeholder block art coming back on top of the real models. Only the
        /// built-in primitive meshes are checked: the real horse is parented under the same
        /// HorsePlaceholders node and must stay visible.
        /// </summary>
        private static void RequireNoActiveRenderers(string rootName, string message, ICollection<string> failures)
        {
            GameObject carriage = GameObject.Find("CarriagePrototype");
            Transform root = carriage != null ? FindChild(carriage.transform, rootName) : null;
            if (root == null)
            {
                return;
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !IsBuiltInPrimitive(mesh))
                {
                    continue;
                }

                var renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.enabled)
                {
                    Require(false, message, failures);
                    return;
                }
            }
        }

        private static bool IsBuiltInPrimitive(Mesh mesh)
        {
            string name = mesh.name;
            return name == "Cube" || name == "Sphere" || name == "Cylinder" ||
                   name == "Capsule" || name == "Plane" || name == "Quad";
        }

        /// <summary>
        /// Each lamp must stand over the bed rather than in mid air, and the bed must be wider than
        /// both horses together, so the carriage reads as the thing being pulled.
        /// </summary>
        private static void ValidateCarriageLamps(ICollection<string> failures)
        {
            GameObject wagon = GameObject.Find("Wagon_Model");
            if (wagon == null || !TryGetRendererBounds(wagon, out Bounds bed))
            {
                Require(false, "The wagon model is present and measurable.", failures);
                return;
            }

            GameObject leftHorse = GameObject.Find("Horse_Model_Left");
            GameObject rightHorse = GameObject.Find("Horse_Model_Right");
            if (leftHorse != null && rightHorse != null &&
                TryGetCombinedBounds(leftHorse.transform, rightHorse.transform, out Bounds horses))
            {
                Require(bed.size.x > horses.size.x,
                    "The wagon is wider than both horses together, so they read as harnessed to it.", failures);
            }

            foreach (string name in new[] { "Lamp_FrontLeft", "Lamp_FrontRight", "Lamp_RearLeft", "Lamp_RearRight" })
            {
                GameObject lamp = GameObject.Find(name);
                if (lamp == null)
                {
                    Require(false, $"{name} exists.", failures);
                    continue;
                }

                Vector3 position = lamp.transform.position;
                Require(position.x >= bed.min.x - 0.01f && position.x <= bed.max.x + 0.01f &&
                        position.z >= bed.min.z - 0.01f && position.z <= bed.max.z + 0.01f,
                    $"{name} stands over the wagon bed instead of floating beside it.", failures);
                Require(position.y >= bed.min.y && position.y <= bed.max.y + 0.6f,
                    $"{name} is seated at the level of the wagon bed.", failures);
            }
        }

        private static void ValidateKnife(ICollection<string> failures)
        {
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

            Require(giantSpawner.ShowsGiantBehindHorde,
                "The giant stays visible behind the horde before the low-speed defeat trigger arms.", failures);
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
