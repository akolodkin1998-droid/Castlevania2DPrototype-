using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports the dungeon backdrop as a Sprite and creates a drag-ready prefab / scene instance.
/// </summary>
public static class DungeonBackgroundSetupEditor
{
    private const string SourceCyrillicPath = "Assets/Art/Backgrounds/Dungeon/Бэкграунд.png";
    private const string SourceAsciiPath = "Assets/Art/Backgrounds/Dungeon/Dungeon_Background.png";
    private const string PrefabPath = "Assets/Prefabs/Environment/Background_Dungeon.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string SceneObjectName = "Background_Dungeon";

    // Covers the lower dungeon area around Likho (~y -65).
    private static readonly Vector3 ScenePosition = new Vector3(8.6f, -65.7f, 12f);
    private static readonly Vector2 TiledWorldSize = new Vector2(48f, 28f);
    private const int SortingOrder = -20;
    private const float PixelsPerUnit = 100f;

    [MenuItem("Tools/Castlevania 2D/Setup Dungeon Background")]
    public static void SetupMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Dungeon Background", SetupDungeonBackground(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Dungeon Background", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupDungeonBackground());
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupDungeonBackground()
    {
        EnsureFolder("Assets/Art/Backgrounds/Dungeon");
        EnsureFolder("Assets/Prefabs/Environment");

        string spritePath = ResolveSourcePath();
        ConfigureAsSprite(spritePath);
        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            throw new InvalidDataException(
                "Could not load Sprite from " + spritePath +
                ". Select the PNG in Project → Inspector → Texture Type = Sprite (2D and UI), Apply.");
        }

        GameObject prefabRoot = CreateBackgroundObject("Background_Dungeon", sprite);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        PlaceInPrototypeScene(sprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Dungeon background ready as Sprite.\n" +
               $"Source: {spritePath}\n" +
               $"Prefab: {PrefabPath}\n" +
               $"Scene object: {SceneObjectName} at {ScenePosition}\n\n" +
               "Drag the prefab into any scene, or use the placed Prototype instance.\n" +
               "Tiled size / position are editable on the SpriteRenderer / Transform.";
    }

    private static string ResolveSourcePath()
    {
        if (File.Exists(ToFullPath(SourceCyrillicPath)))
        {
            // Keep a stable ASCII copy for Project search / drag-drop.
            string asciiFull = ToFullPath(SourceAsciiPath);
            File.Copy(ToFullPath(SourceCyrillicPath), asciiFull, true);
            AssetDatabase.ImportAsset(SourceAsciiPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsSprite(SourceAsciiPath);
            ConfigureAsSprite(SourceCyrillicPath);
            return SourceAsciiPath;
        }

        if (File.Exists(ToFullPath(SourceAsciiPath)))
        {
            return SourceAsciiPath;
        }

        throw new FileNotFoundException(
            "Dungeon background PNG not found. Put it at:\n" + SourceCyrillicPath);
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
        importer.wrapMode = TextureWrapMode.Repeat;
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
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = TiledWorldSize;
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;
        go.transform.position = ScenePosition;
        return go;
    }

    private static void PlaceInPrototypeScene(Sprite sprite)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(SceneObjectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
            scene) as GameObject;
        if (instance == null)
        {
            instance = CreateBackgroundObject(SceneObjectName, sprite);
        }

        instance.name = SceneObjectName;
        instance.transform.position = ScenePosition;
        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = TiledWorldSize;
            renderer.sortingOrder = SortingOrder;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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
