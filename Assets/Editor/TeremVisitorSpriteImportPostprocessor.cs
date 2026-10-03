using UnityEditor;
using UnityEngine;

public sealed class TeremVisitorSpriteImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath.IndexOf("/Npcs/TeremVisitor") < 0
            && assetPath.IndexOf("/Npcs/TeremVisitorKneel") < 0)
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
