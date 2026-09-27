using UnityEditor;
using UnityEngine;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>
    /// Extracts the textures baked into the lantern fbx files. They reference an absolute path on the
    /// machine the models were authored on, which does not exist here, so the import left the material
    /// plain white and the lanterns read as untextured blocks. The image data is embedded in the fbx;
    /// extracting it next to the models lets Unity remap the material onto it. Re-runnable and safe.
    /// </summary>
    public static class LanternImportSetup
    {
        private const string TextureFolder = "Assets/Art/Lanterns/Textures";

        private static readonly string[] ModelPaths =
        {
            "Assets/Art/Lanterns/lantern_standing.fbx",
            "Assets/Art/Lanterns/post_lantern.fbx"
        };

        [MenuItem("Tools/Game/Setup Lantern Import")]
        public static void SetupLanternImport()
        {
            EnsureFolder("Assets/Art/Lanterns", "Textures");

            foreach (string path in ModelPaths)
            {
                SetupModel(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetupModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"No ModelImporter at {path}.");
                return;
            }

            bool extracted = importer.ExtractTextures(TextureFolder);
            if (extracted)
            {
                // A reimport makes the embedded material pick up the texture that was just written out.
                importer.SaveAndReimport();
            }

            ReportMaterial(path, extracted);
        }

        private static void ReportMaterial(string path, bool extracted)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is Material material))
                {
                    continue;
                }

                Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (texture != null)
                {
                    Debug.Log($"{path}: material '{material.name}' now uses texture '{texture.name}'.");
                }
                else
                {
                    Debug.LogWarning(
                        $"{path}: material '{material.name}' still has no base map " +
                        $"(extractTextures returned {extracted}). The texture may not be embedded after all.");
                }
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
