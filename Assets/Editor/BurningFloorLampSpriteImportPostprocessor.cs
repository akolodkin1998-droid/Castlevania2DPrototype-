using System;
using UnityEditor;
using UnityEngine;

public sealed class BurningFloorLampSpriteImportPostprocessor : AssetPostprocessor
{
    private const string SpriteFolder = "Assets/Resources/Environment/BurningFloorLamp/";
    private static bool prefabBuildQueued;

    private void OnPreprocessTexture()
    {
        if (!IsLampSprite(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spritePivot = new Vector2(0.5f, 0f);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (!IsLampSprite(importedAssets[i]))
            {
                continue;
            }

            QueuePrefabBuild();
            return;
        }
    }

    private static bool IsLampSprite(string path)
    {
        return path.StartsWith(SpriteFolder, StringComparison.Ordinal)
               && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static void QueuePrefabBuild()
    {
        if (prefabBuildQueued)
        {
            return;
        }

        prefabBuildQueued = true;
        EditorApplication.delayCall += BuildPrefabAfterImport;
    }

    private static void BuildPrefabAfterImport()
    {
        prefabBuildQueued = false;
        BurningFloorLampPrefabSetupEditor.BuildPrefab();
    }
}
