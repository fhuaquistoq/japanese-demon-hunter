using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Prototype.Editor
{
    public static class DesktopHunterCameraSetup
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";

        [InitializeOnLoadMethod]
        private static void RunFromCommandLine()
        {
            if (!Environment.GetCommandLineArgs().Contains("-setupDesktopHunterCamera"))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    SetupCamera();
                    EditorApplication.Exit(0);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorApplication.Exit(1);
                }
            };
        }

        [MenuItem("Tools/Monsters/Setup Desktop Hunter Camera")]
        public static void SetupCamera()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrototypeSceneReferences references = UnityEngine.Object.FindAnyObjectByType<PrototypeSceneReferences>();
            SmoothFollowCamera cameraController = UnityEngine.Object.FindAnyObjectByType<SmoothFollowCamera>();
            if (references == null || !references.IsConfigured || cameraController == null)
            {
                throw new InvalidOperationException(
                    "MonstersPrototype must contain configured PrototypeSceneReferences and SmoothFollowCamera.");
            }

            Undo.RecordObject(cameraController, "Configure desktop hunter camera");
            cameraController.ConfigureDesktopHunterView(
                references.HunterTransform,
                new Vector3(0f, 0.38f, 0f),
                0.12f,
                false);
            EditorUtility.SetDirty(cameraController);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Unity could not save {ScenePath}.");
            }

            Selection.activeGameObject = cameraController.gameObject;
            Debug.Log("Desktop hunter camera configured. Mouse looks freely; Escape releases the cursor.", cameraController);
        }
    }
}
