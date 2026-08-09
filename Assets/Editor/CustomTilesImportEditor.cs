#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Explicitly imports standalone custom tile images without slicing them.
/// This importer intentionally never runs automatically and never changes scene content.
/// </summary>
public static class CustomTilesImportEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Import Individual Custom Tiles";
    private const string SourceFolder = @"C:\Users\Bensh\OneDrive\Рабочий стол\Плитки";
    private const string TextureFolder = "Assets/Art/Tiles/Custom/Individual";
    private const string TilesFolder = TextureFolder + "/Tiles";
    private const int DefaultPixelsPerUnit = 32;
    private const string SlopeSourceFileName = "Склон.png";
    private const string SlopeImportedFileName = "Slope.png";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".gif", ".psd", ".exr", ".tif", ".tiff"
    };

    [MenuItem(MenuPath)]
    public static void ImportMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Custom tiles cannot be imported while entering or running Play Mode.");
            return;
        }

        if (!Directory.Exists(SourceFolder))
        {
            Debug.LogError($"Individual custom tile source folder was not found: {SourceFolder}");
            return;
        }

        List<string> sourcePaths = GetSupportedSourcePaths();
        if (sourcePaths.Count == 0)
        {
            Debug.LogWarning($"No supported image files were found in: {SourceFolder}");
            return;
        }

        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Tiles");
        EnsureFolder("Assets/Art/Tiles", "Custom");
        EnsureFolder("Assets/Art/Tiles/Custom", "Individual");
        EnsureFolder(TextureFolder, "Tiles");

        var textureAssetPaths = new List<string>(sourcePaths.Count);
        foreach (string sourcePath in sourcePaths)
        {
            textureAssetPaths.Add(CopySource(sourcePath));
        }

        AssetDatabase.Refresh();
        int tileAssetCount = 0;
        foreach (string textureAssetPath in textureAssetPaths)
        {
            ConfigureSprite(textureAssetPath);
            tileAssetCount += CreateOrUpdateTile(textureAssetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int destinationCount = CountDestinationImages();
        if (destinationCount != sourcePaths.Count)
        {
            throw new InvalidOperationException(
                $"Individual custom tile import verification failed: {sourcePaths.Count} source images, " +
                $"{destinationCount} destination images in {TextureFolder}.");
        }

        Debug.Log(
            $"Individual custom tiles ready: {sourcePaths.Count} source images / {destinationCount} destination images / " +
            $"{tileAssetCount} Tile assets. Single sprites, Point filtering, no mipmaps, uncompressed. " +
            $"Images: {TextureFolder}. Tiles: {TilesFolder}.");
    }

    private static List<string> GetSupportedSourcePaths()
    {
        string[] allFiles = Directory.GetFiles(SourceFolder, "*", SearchOption.AllDirectories);
        var sourcePaths = new List<string>();
        foreach (string path in allFiles)
        {
            if (SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                sourcePaths.Add(path);
            }
        }

        sourcePaths.Sort(StringComparer.OrdinalIgnoreCase);
        return sourcePaths;
    }

    private static string CopySource(string sourcePath)
    {
        string relativePath = sourcePath
            .Substring(SourceFolder.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        relativePath = GetImportedRelativePath(relativePath);
        string assetPath = $"{TextureFolder}/{relativePath.Replace('\\', '/')}";
        string destinationPath = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return assetPath;
    }

    private static string GetImportedRelativePath(string sourceRelativePath)
    {
        if (string.Equals(sourceRelativePath, SlopeSourceFileName, StringComparison.OrdinalIgnoreCase))
        {
            return SlopeImportedFileName;
        }

        return sourceRelativePath;
    }

    private static void ConfigureSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No TextureImporter at {assetPath}.");
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            throw new InvalidOperationException($"No texture was imported from {assetPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = GetPixelsPerUnit(texture.width, texture.height);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = SupportsAlpha(Path.GetExtension(assetPath));
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.overridden = true;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
    }

    private static int CreateOrUpdateTile(string textureAssetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(textureAssetPath);
        if (sprite == null)
        {
            throw new InvalidOperationException($"No single sprite was imported from {textureAssetPath}.");
        }

        string relativePath = textureAssetPath.Substring(TextureFolder.Length).TrimStart('/');
        string tileAssetPath = $"{TilesFolder}/{Path.ChangeExtension(relativePath, ".asset").Replace('\\', '/')}";
        EnsureAssetFolderForFile(tileAssetPath);

        Tile tileAsset = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);
        if (tileAsset == null)
        {
            tileAsset = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tileAsset, tileAssetPath);
        }

        tileAsset.sprite = sprite;
        tileAsset.color = Color.white;
        tileAsset.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tileAsset);
        return 1;
    }

    private static int GetPixelsPerUnit(int width, int height)
    {
        if (width == height)
        {
            if (width == 16 || width == 32)
            {
                return width;
            }

            return width;
        }

        return DefaultPixelsPerUnit;
    }

    private static bool SupportsAlpha(string extension)
    {
        return !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountDestinationImages()
    {
        if (!Directory.Exists(TextureFolder))
        {
            return 0;
        }

        int count = 0;
        foreach (string path in Directory.GetFiles(TextureFolder, "*", SearchOption.AllDirectories))
        {
            if (SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                count++;
            }
        }

        return count;
    }

    private static void EnsureAssetFolderForFile(string assetPath)
    {
        string[] folders = Path.GetDirectoryName(assetPath)!.Replace('\\', '/').Split('/');
        string parent = folders[0];
        for (int index = 1; index < folders.Length; index++)
        {
            EnsureFolder(parent, folders[index]);
            parent += "/" + folders[index];
        }
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
