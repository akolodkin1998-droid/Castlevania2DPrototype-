using UnityEditor;
using UnityEngine;

public sealed class GiantLikhoSpriteImportPostprocessor : AssetPostprocessor
{
    private const string SpriteFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteFolder, System.StringComparison.Ordinal))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
    }
}
