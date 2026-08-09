using UnityEditor;
using UnityEngine;

public sealed class InventorySpriteImportPostprocessor : AssetPostprocessor
{
    private const string ArtInventoryFolder = "Assets/Art/Sprites/Items/Inventory";
    private const string ResourcesInventoryFolder = "Assets/Resources/Items/Inventory";

    private void OnPreprocessTexture()
    {
        if (assetImporter is not TextureImporter importer)
        {
            return;
        }

        if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool isTarget =
            assetPath.StartsWith(ArtInventoryFolder, System.StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith(ResourcesInventoryFolder, System.StringComparison.OrdinalIgnoreCase);

        if (!isTarget)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        // Required: pivot bottom-center (0.5, 0).
        importer.spritePivot = new Vector2(0.5f, 0f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);

        // Let Unity reimport with updated importer settings.
    }
}

