#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Imports Desktop/Интерфейс/загрузка (3).png as a 8×8 / 32×32 sprite sheet
/// and generates Tile assets for Tile Palette painting.
/// </summary>
public static class LoadingTilesImportEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Import Loading Tiles";
    private const string SourcePath =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Интерфейс\загрузка (3).png";
    private const string TextureFolder = "Assets/Art/Tiles/Loading";
    private const string TextureAssetPath = TextureFolder + "/Loading_3.png";
    private const string TilesFolder = TextureFolder + "/Tiles";

    private const int SheetWidth = 256;
    private const int SheetHeight = 256;
    private const int CellSize = 32;
    private const int Columns = 8;
    private const int Rows = 8;
    private const float PixelsPerUnit = 32f;

    [MenuItem(MenuPath)]
    public static void ImportMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Import;
            return;
        }

        Import();
    }

    public static void Import()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets/Art", "Tiles");
        EnsureFolder("Assets/Art/Tiles", "Loading");
        EnsureFolder(TextureFolder, "Tiles");

        CopySourceTexture();
        ConfigureAndSliceTexture();

        Sprite[] sprites = LoadSlicedSprites();
        if (sprites.Length == 0)
        {
            throw new InvalidOperationException(
                $"LoadingTilesImportEditor: no sprites sliced from {TextureAssetPath}.");
        }

        int created = CreateOrUpdateTiles(sprites);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TilesFolder);
        EditorGUIUtility.PingObject(Selection.activeObject);

        Debug.Log(
            $"Loading tiles ready: {sprites.Length} sprites / {created} Tile assets, " +
            $"cell {CellSize}×{CellSize}, grid {Columns}×{Rows}, PPU {PixelsPerUnit}. " +
            $"Texture: {TextureAssetPath}. Tiles: {TilesFolder}. " +
            "Paint via Window → 2D → Tile Palette → create/open palette → drag Tiles folder.");
    }

    private static void CopySourceTexture()
    {
        string absoluteDest = Path.GetFullPath(TextureAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteDest)!);

        if (File.Exists(SourcePath))
        {
            File.Copy(SourcePath, absoluteDest, overwrite: true);
            Debug.Log($"Copied Loading_3 from source → {TextureAssetPath}");
        }
        else if (!File.Exists(absoluteDest))
        {
            throw new FileNotFoundException(
                "Loading source PNG not found and project copy missing.", SourcePath);
        }
        else
        {
            Debug.LogWarning(
                $"Source missing at {SourcePath}; using existing {TextureAssetPath}.");
        }

        AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureAndSliceTexture()
    {
        var importer = AssetImporter.GetAtPath(TextureAssetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No TextureImporter at {TextureAssetPath}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePivot = new Vector2(0.5f, 0.5f);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Multiple;
        settings.spritePixelsPerUnit = PixelsPerUnit;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.filterMode = FilterMode.Point;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        importer.SetTextureSettings(settings);

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(platform);

        var metas = new List<SpriteMetaData>(Columns * Rows);
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int index = row * Columns + col;
                int x = col * CellSize;
                // Unity sprite rect origin is bottom-left; sheet rows are top-to-bottom visually.
                int y = SheetHeight - (row + 1) * CellSize;
                metas.Add(new SpriteMetaData
                {
                    name = $"Loading_3_{index:D2}",
                    rect = new Rect(x, y, CellSize, CellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                });
            }
        }

        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadSlicedSprites()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TextureAssetPath);
        var sprites = new List<Sprite>(Columns * Rows);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }

    private static int CreateOrUpdateTiles(Sprite[] sprites)
    {
        int count = 0;
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
            {
                continue;
            }

            string tilePath = $"{TilesFolder}/{sprite.name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Sprite;
            EditorUtility.SetDirty(tile);
            count++;
        }

        return count;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
