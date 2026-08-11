using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the roots backdrop as a Sprite and rebuilds the drag-ready prefab.
/// Does not place it in the scene — drag Background_Roots into any scene when ready.
/// </summary>
[InitializeOnLoad]
public static class RootsBackgroundSetupEditor
{
    private const string SourcePath = "Assets/Art/Backgrounds/Roots/Roots_Background.png";
    private const string SecondSourcePath = "Assets/Art/Backgrounds/Roots/Roots_Background_02.png";
    private const string ThirdSourcePath = "Assets/Art/Backgrounds/Roots/Roots_Background_03.png";
    private const string PrefabPath = "Assets/Prefabs/Environment/Background_Roots.prefab";
    private const string SecondPrefabPath = "Assets/Prefabs/Environment/Background_Roots_02.prefab";
    private const string ThirdPrefabPath = "Assets/Prefabs/Environment/Background_Roots_03.prefab";
    private const int SortingOrder = -18;
    private const float PixelsPerUnit = 16f;

    static RootsBackgroundSetupEditor()
    {
        EditorApplication.delayCall += EnsureAdditionalBackgroundsExist;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Roots Background")]
    public static void SetupMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Roots Background", SetupRootsBackground(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Roots Background", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string SetupRootsBackground()
    {
        EnsureFolder("Assets/Art/Backgrounds/Roots");
        EnsureFolder("Assets/Prefabs/Environment");

        SetupBackground(SourcePath, PrefabPath, "Background_Roots");
        SetupBackground(SecondSourcePath, SecondPrefabPath, "Background_Roots_02");
        SetupBackground(ThirdSourcePath, ThirdPrefabPath, "Background_Roots_03");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "All roots backgrounds are ready as Sprites.\n" +
               $"Prefabs:\n{PrefabPath}\n{SecondPrefabPath}\n{ThirdPrefabPath}\n\n" +
               "Drag any prefab from Prefabs/Environment into the scene.\n" +
               "Adjust Transform position/scale; Sorting Order = -18.";
    }

    private static void EnsureAdditionalBackgroundsExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists(ToFullPath(SecondSourcePath)) ||
            !File.Exists(ToFullPath(ThirdSourcePath)) ||
            AssetDatabase.LoadAssetAtPath<GameObject>(SecondPrefabPath) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPrefabPath) != null)
        {
            return;
        }

        try
        {
            Debug.Log(SetupRootsBackground());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void SetupBackground(string sourcePath, string prefabPath, string objectName)
    {
        if (!File.Exists(ToFullPath(sourcePath)))
        {
            throw new FileNotFoundException(
                "Roots background PNG not found. Put it at:\n" + sourcePath);
        }

        ConfigureAsSprite(sourcePath);
        AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);
        if (sprite == null)
        {
            throw new InvalidDataException(
                "Could not load Sprite from " + sourcePath +
                ". Select the PNG in Project → Inspector → Texture Type = Sprite (2D and UI), Apply.");
        }

        GameObject prefabRoot = CreateBackgroundObject(objectName, sprite);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        Object.DestroyImmediate(prefabRoot);
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static void ConfigureAsSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer == null)
        {
            throw new InvalidDataException("No TextureImporter for " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        var platform = importer.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = 2048;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static GameObject CreateBackgroundObject(string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;
        go.transform.position = new Vector3(0f, 0f, 12f);
        return go;
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
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
