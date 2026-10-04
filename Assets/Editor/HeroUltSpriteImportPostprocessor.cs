using UnityEditor;
using UnityEngine;

public sealed class HeroUltSpriteImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        bool sheathe = assetPath.IndexOf("/Player/Ult/Sheathe/") >= 0;
        bool cast = assetPath.IndexOf("/Player/Ult/Cast/") >= 0;
        bool pillar = assetPath.IndexOf("/Player/Ult/Pillar/") >= 0;
        bool heal = assetPath.IndexOf("/Player/Heal/") >= 0;
        if (!sheathe && !cast && !pillar && !heal)
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = pillar ? 64f : 83.5f;
        importer.spritePivot = pillar ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.12f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = importer.spritePivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
