using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters.Editor
{
    public static class MonsterModelReporter
    {
        public const string BatModelPath = "Assets/Art/Monsters/Bat/Models/Bat.fbx";
        public const string ZombieModelPath = "Assets/Art/Monsters/Zombie/Models/zombo.fbx";
        private const string BatSourcePath = "Assets/Art/Monsters/Bat by Quaternius - hNO9XvjlKa/Bat.fbx";
        private const string ZombieSourcePath = "Assets/Art/Monsters/Zombie by bachosoftdesign - xqEzosAVYX/zombo.fbx";
        private const string ReportFileName = "MonsterModelInspection.txt";
        private const string DemonSourcePath = "Assets/Art/Monsters/Demon by Quaternius - Mo2ky6vkf8/Demon.fbx";
        private const string SpiderSourcePath = "Assets/Art/Monsters/Spider by Quaternius - yRYJiAJyiM/Spider.fbx";
        private const string GhostSourcePath = "Assets/Art/Monsters/Ghost by Quaternius - Iip30bDHmu/Ghost.fbx";
        private const string GiantZombieSourcePath = "Assets/Art/Monsters/Zombie by Quaternius - VlXjG0N8Eg/Zombie_Basic.fbx";

        [InitializeOnLoadMethod]
        private static void RunPhaseThreeInspectionFromCommandLine()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-phase3Inspect"))
            {
                return;
            }

            try
            {
                InspectSourceModels();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Monsters/Inspect Source Models")]
        public static void InspectSourceModels()
        {
            var report = new StringBuilder();
            AppendModelReport(report, "BAT", ResolvePath(BatModelPath, BatSourcePath));
            AppendModelReport(report, "SMALL ZOMBIE", ResolvePath(ZombieModelPath, ZombieSourcePath));
            AppendModelReport(report, "DEMON", DemonSourcePath);
            AppendModelReport(report, "SPIDER", SpiderSourcePath);
            AppendModelReport(report, "GHOST", GhostSourcePath);
            AppendModelReport(report, "GIANT ZOMBIE", GiantZombieSourcePath);

            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string reportPath = System.IO.Path.Combine(projectRoot, "Logs", ReportFileName);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log($"Monster model inspection written to {reportPath}\n{report}");
        }

        public static void InspectFromCommandLine()
        {
            InspectSourceModels();
        }

        private static string ResolvePath(string organizedPath, string sourcePath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(organizedPath) != null
                ? organizedPath
                : sourcePath;
        }

        private static void AppendModelReport(StringBuilder report, string label, string assetPath)
        {
            report.AppendLine($"=== {label} ===");
            report.AppendLine($"Path: {assetPath}");

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (importer == null || modelAsset == null)
            {
                report.AppendLine("ERROR: ModelImporter or model asset is unavailable.");
                report.AppendLine();
                return;
            }

            report.AppendLine($"Animation type: {importer.animationType}");
            report.AppendLine($"Avatar setup: {importer.avatarSetup}");
            report.AppendLine($"Import animation: {importer.importAnimation}");
            report.AppendLine($"Global scale: {importer.globalScale}");
            report.AppendLine($"Use file scale: {importer.useFileScale}");
            report.AppendLine($"Bake axis conversion: {importer.bakeAxisConversion}");
            report.AppendLine($"Material import mode: {importer.materialImportMode}");

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();
            report.AppendLine($"Animation clips ({clips.Length}):");
            foreach (AnimationClip clip in clips)
            {
                report.AppendLine(
                    $"  - {clip.name} | length={clip.length:0.###}s | fps={clip.frameRate:0.##} | loop={clip.isLooping}");
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            report.AppendLine($"Meshes: {subAssets.OfType<Mesh>().Count()}");
            report.AppendLine($"Embedded materials: {subAssets.OfType<Material>().Count()}");
            report.AppendLine($"Avatars: {subAssets.OfType<Avatar>().Count()}");

            GameObject instance = Object.Instantiate(modelAsset);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                SkinnedMeshRenderer[] skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Animation legacyAnimation = instance.GetComponentInChildren<Animation>(true);

                report.AppendLine($"Hierarchy transforms: {transforms.Length}");
                report.AppendLine($"Renderers: {renderers.Length}");
                report.AppendLine($"Skinned renderers: {skinnedRenderers.Length}");
                report.AppendLine($"Animator present: {animator != null}");
                report.AppendLine($"Legacy Animation present: {legacyAnimation != null}");
                report.AppendLine($"Root children: {string.Join(", ", transforms.Where(t => t.parent == instance.transform).Select(t => t.name))}");

                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int index = 1; index < renderers.Length; index++)
                    {
                        bounds.Encapsulate(renderers[index].bounds);
                    }

                    report.AppendLine($"Rendered bounds size at import scale: {bounds.size}");
                    report.AppendLine($"Rendered bounds center: {bounds.center}");
                    string materialNames = string.Join(", ", renderers
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material != null)
                        .Select(material => material.name)
                        .Distinct());
                    report.AppendLine($"Referenced materials: {materialNames}");
                }

                foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
                {
                    int boneCount = skinnedRenderer.bones != null ? skinnedRenderer.bones.Length : 0;
                    report.AppendLine($"Skinned mesh '{skinnedRenderer.name}' bones: {boneCount}");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            report.AppendLine();
        }
    }
}
