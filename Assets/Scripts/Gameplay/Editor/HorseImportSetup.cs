using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>
    /// Puts Horse.fbx in a state where its bundled clips can actually play. The model shipped
    /// imported with skin weights discarded and the Humanoid rig type, so even though the fbx
    /// carries idle, walk and gallop takes, the mesh could not deform when they played. The
    /// controller is then built from the clips that really got imported, never from assumed names.
    /// Re-runnable and non-destructive: it only rewrites the importer settings and the generated
    /// controller asset.
    /// </summary>
    public static class HorseImportSetup
    {
        private const string HorseModelPath = "Assets/Art/Monsters/Horse/Horse.fbx";
        private const string ControllerFolder = "Assets/Animations/Horse";
        private const string ControllerPath = ControllerFolder + "/Horse.controller";

        private const float IdleClip = 0f;
        private const float WalkClip = 0.35f;
        private const float GallopClip = 0.7f;
        private const float CrossFade = 0.15f;

        private static readonly string[] IdleKeywords = { "idle", "stand", "breathe" };
        private static readonly string[] WalkKeywords = { "walk", "trot", "amble" };
        private static readonly string[] GallopKeywords = { "gallop", "run", "canter", "charge" };
        // The fbx also ships reaction and transition takes (Idle_HitReact_*, Jump_toIdle, Gallop_Jump).
        // They contain a locomotion keyword but are not locomotion, so a match is rejected outright.
        private static readonly string[] RejectedKeywords =
        {
            "hitreact", "hit_react", "react", "jump", "toidle", "death", "attack", "kick", "eating"
        };

        [MenuItem("Tools/Game/Setup Horse Import")]
        public static void SetupHorseImport()
        {
            if (!FixImporter())
            {
                return;
            }

            AnimationClip[] clips = LoadClips();
            if (clips.Length == 0)
            {
                Debug.LogError(
                    $"{HorseModelPath} imported without any animation clip. The horse will keep the " +
                    "procedural gait; check the fbx actually contains takes.");
                return;
            }

            Debug.Log($"Horse clips imported ({clips.Length}): {string.Join(", ", clips.Select(c => c.name))}");

            // Looping lives in the importer, not on the loaded clip: setting it on the clip itself
            // looks right until the next reimport, which silently drops it. The take list is declared
            // to the importer instead, and the clips are read back afterwards.
            if (DeclareLoopingTakes(clips))
            {
                clips = LoadClips();
            }

            BuildController(clips);
        }

        /// <summary>
        /// Declares every take back to the importer with the locomotion ones set to loop. Each take
        /// keeps the frame range it already has, so no clip is lost or trimmed; only the loop flag
        /// changes. Returns true when a reimport actually happened.
        /// </summary>
        private static bool DeclareLoopingTakes(AnimationClip[] clips)
        {
            var importer = AssetImporter.GetAtPath(HorseModelPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            var looping = new HashSet<string>(System.StringComparer.Ordinal);
            AddLoopingName(looping, Pick(clips, IdleKeywords, 0f));
            AddLoopingName(looping, Pick(clips, WalkKeywords, 0f));
            AddLoopingName(looping, Pick(clips, GallopKeywords, 0f));
            if (looping.Count == 0)
            {
                return false;
            }

            bool needsChange = false;
            var entries = new List<ModelImporterClipAnimation>();
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || clip.length <= 0f)
                {
                    continue;
                }

                bool shouldLoop = looping.Contains(clip.name);
                entries.Add(new ModelImporterClipAnimation
                {
                    name = clip.name,
                    takeName = clip.name,
                    firstFrame = 0f,
                    lastFrame = Mathf.Max(1f, Mathf.Round(clip.length * clip.frameRate)),
                    loopTime = shouldLoop
                });

                if (shouldLoop)
                {
                    needsChange = true;
                }
            }

            if (!needsChange || entries.Count == 0)
            {
                return false;
            }

            importer.clipAnimations = entries.ToArray();
            importer.SaveAndReimport();
            return true;
        }

        private static void AddLoopingName(HashSet<string> target, AnimationClip clip)
        {
            if (clip != null)
            {
                target.Add(clip.name);
            }
        }

        /// <summary>Returns true when the asset needs a reimport, false when it was already correct.</summary>
        private static bool FixImporter()
        {
            var importer = AssetImporter.GetAtPath(HorseModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"No ModelImporter at {HorseModelPath}.");
                return false;
            }

            bool needsChange = importer.animationType != ModelImporterAnimationType.Generic ||
                               importer.skinWeights != ModelImporterSkinWeights.Standard ||
                               importer.importAnimation == false ||
                               importer.animationCompression != ModelImporterAnimationCompression.Optimal ||
                               importer.materialImportMode == ModelImporterMaterialImportMode.None;
            if (!needsChange)
            {
                Debug.Log("Horse import settings are already correct; nothing to reimport.");
                return true;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.generateSecondaryUV = true;
            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip[] LoadClips()
        {
            var clips = new List<AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(HorseModelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        /// <summary>
        /// Resolves a locomotion clip by keyword. The fbx exposes every take twice, once prefixed
        /// with the rig name ("AnimalArmature|Gallop") and once bare ("Gallop"); the bare one is
        /// preferred because it is the clip the source actually authored.
        /// </summary>
        private static AnimationClip Pick(AnimationClip[] clips, string[] keywords, float fallbackIndex)
        {
            foreach (string keyword in keywords)
            {
                // An exact name always wins, then a prefixed take, then a plain containment match.
                AnimationClip best = null;
                int bestScore = 0;
                foreach (AnimationClip clip in clips)
                {
                    string name = clip.name;
                    if (IsRejected(name))
                    {
                        continue;
                    }

                    if (!Matches(name, keyword))
                    {
                        continue;
                    }

                    int score = name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                name.IndexOf('|') < 0 && name.IndexOf(':') < 0
                        ? 2
                        : 1;
                    if (score > bestScore)
                    {
                        best = clip;
                        bestScore = score;
                    }
                }

                if (best != null)
                {
                    return best;
                }
            }

            // No keyword matched: fall back to ordering, but only when there is exactly one sensible
            // candidate, so we never bind a combat clip to the locomotion states.
            return clips.Length == 0 ? null : clips[Mathf.Clamp((int)fallbackIndex, 0, clips.Length - 1)];
        }

        private static bool Matches(string name, string keyword)
        {
            return name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRejected(string name)
        {
            foreach (string rejected in RejectedKeywords)
            {
                if (name.IndexOf(rejected, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildController(AnimationClip[] clips)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }

            if (!AssetDatabase.IsValidFolder(ControllerFolder))
            {
                AssetDatabase.CreateFolder("Assets/Animations", "Horse");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            bool created = controller == null;
            if (created)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                // Wipe the layers in place: assigning an empty array leaves the controller with no
                // layer at all, and the next line would index out of range.
                AnimatorControllerLayer[] existing = controller.layers;
                for (var i = 0; i < existing.Length; i++)
                {
                    controller.RemoveLayer(0);
                }
            }

            // AddParameter throws on a duplicate, so a rebuilt controller clears them first.
            // RemoveParameter takes a position, and removing shifts the rest down, so walk backwards.
            if (!created)
            {
                for (var i = controller.parameters.Length - 1; i >= 0; i--)
                {
                    controller.RemoveParameter(i);
                }
            }

            controller.AddParameter(SpeedParameter(), AnimatorControllerParameterType.Float);
            controller.AddParameter(GallopParameter(), AnimatorControllerParameterType.Float);

            // CreateAnimatorControllerAtPath always yields a base layer; a rebuilt one does not.
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip idle = Pick(clips, IdleKeywords, IdleClip);
            AnimationClip walk = Pick(clips, WalkKeywords, WalkClip);
            AnimationClip gallop = Pick(clips, GallopKeywords, GallopClip);

            // The takes are declared to the importer as looping clips by DeclareLoopingTakes, so the
            // states below play a continuous gait instead of freezing on the last frame.
            AnimatorState idleState = AddState(machine, "Idle", idle, 0f, true);
            AnimatorState walkState = AddState(machine, "Walk", walk, 120f, false);
            AnimatorState gallopState = AddState(machine, "Gallop", gallop, 240f, false);

            // A car that never stops should not flicker between states: the handoff thresholds are
            // separated by a hysteresis band, so a speed sitting on a boundary does not oscillate.
            AddFloatTransition(idleState, walkState, GallopParameter(), 0.15f, true);
            AddFloatTransition(walkState, gallopState, GallopParameter(), 0.65f, true);
            AddFloatTransition(gallopState, walkState, GallopParameter(), 0.45f, false);
            AddFloatTransition(walkState, idleState, GallopParameter(), 0.1f, false);
            // A stroke can take the cart from a standstill straight to a gallop, so allow skipping
            // the walk state instead of forcing a pass through it.
            AddFloatTransition(idleState, gallopState, GallopParameter(), 0.65f, true);
            AddFloatTransition(gallopState, idleState, GallopParameter(), 0.1f, false);

            // The speed parameter stays exposed for tuning and for any future state that needs it.
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(controller);
            Debug.Log($"Built {ControllerPath} with Idle={idle?.name}, Walk={walk?.name}, Gallop={gallop?.name}.");
        }

        private static string SpeedParameter() => "Speed";
        private static string GallopParameter() => "Gallop";

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip, float y, bool isDefault)
        {
            AnimatorState state = machine.AddState(name, new Vector3(300f, y, 0f));
            if (clip != null)
            {
                state.motion = clip;
            }

            if (isDefault)
            {
                machine.defaultState = state;
            }

            return state;
        }

        private static void AddFloatTransition(
            AnimatorState from, AnimatorState to, string parameter, float threshold, bool whenAbove)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = CrossFade;
            transition.hasFixedDuration = true;
            transition.AddCondition(
                whenAbove ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                threshold, parameter);
        }
    }
}
