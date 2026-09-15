using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copies hit-blood VFX frames from Desktop/Персонаж/Получение урона into Resources.
/// </summary>
public static class EnemyHitVfxSetupEditor
{
    private const string DestinationFolder = "Assets/Resources/Vfx/HitBlood";
    private const string FilePrefix = "HitBlood_";

    [MenuItem("Tools/Castlevania 2D/Import Enemy Hit VFX")]
    public static void ImportMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Enemy Hit VFX", ImportFrames(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Enemy Hit VFX", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string ImportFrames()
    {
        string sourceFolder = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            throw new DirectoryNotFoundException(
                "Hit VFX not found. Expected Desktop/Персонаж/Получение урона with PNG frames.");
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Vfx");
        EnsureFolder(DestinationFolder);

        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png");
        Array.Sort(sourceFiles, CompareFrameNames);
        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException("No PNG frames in " + sourceFolder);
        }

        DeleteExistingFrames();
        int imported = 0;
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destinationPath = $"{DestinationFolder}/{FilePrefix}{i + 1:D2}.png";
            File.Copy(sourceFiles[i], destinationPath, true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(destinationPath);
            imported++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return $"Imported {imported} hit VFX frames into {DestinationFolder}.";
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Получение урона"),
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Получение урона"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (Directory.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void DeleteExistingFrames()
    {
        string absoluteFolder = Path.Combine(Application.dataPath, "Resources/Vfx/HitBlood");
        if (!Directory.Exists(absoluteFolder))
        {
            return;
        }

        string[] existingFiles = Directory.GetFiles(absoluteFolder, FilePrefix + "*.png");
        for (int i = 0; i < existingFiles.Length; i++)
        {
            AssetDatabase.DeleteAsset($"{DestinationFolder}/{Path.GetFileName(existingFiles[i])}");
        }
    }

    private static void ConfigureImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0.5f);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static int CompareFrameNames(string left, string right)
    {
        int leftNumber = GetFrameNumber(left);
        int rightNumber = GetFrameNumber(right);
        int numberComparison = leftNumber.CompareTo(rightNumber);
        return numberComparison != 0
            ? numberComparison
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFrameNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)(?!.*\d)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int number)
            ? number
            : int.MaxValue;
    }
}
