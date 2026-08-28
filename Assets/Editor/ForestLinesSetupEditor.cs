using System.IO;
using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports 1/2/3ЛинияЛЕС plates and replaces Prototype Background (1) with parallax layers.
/// Line 1 = near ground, line 2 = mid forest, line 3 = far trees/sky.
/// </summary>
[InitializeOnLoad]
public static class ForestLinesSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string DestFolder = "Assets/Art/Backgrounds/Forest/Lines";
    private const string FlagPath = "Temp/setup_forest_lines.flag";
    private const string ParallaxFlagPath = "Temp/ensure_forest_parallax.flag";
    private const string BackgroundRootName = "Background";
    private const string ForestGroupName = "Background (1)";
    private const float PixelsPerUnit = 32f / 0.75f;
    private const int NearSortingOrder = -100;
    private static readonly Vector3 GroupLocalPosition = new Vector3(38.5f, -34.9f, 0f);
    private static readonly Vector2 TiledWorldSize = new Vector2(129f, 9f);
    private const float LayerLocalY = -0.9f;

    private static readonly LayerSpec[] Layers =
    {
        new LayerSpec("ForestLine3_Far", DestFolder + "/ForestLine3_Far.png", 20f, NearSortingOrder - 2),
        new LayerSpec("ForestLine2_Mid", DestFolder + "/ForestLine2_Mid.png", 12f, NearSortingOrder - 1),
        new LayerSpec("ForestLine1_Near", DestFolder + "/ForestLine1_Near.png", 6f, NearSortingOrder)
    };

    static ForestLinesSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.delayCall += TryEnsureForestParallaxFromFlag;
        EditorApplication.update += PollForestParallaxFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Forest Line Backgrounds")]
    public static void SetupMenu()
    {
        try
        {
            string summary = SetupForestLines();
            EditorUtility.DisplayDialog("Forest Line Backgrounds", summary, "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Forest Line Backgrounds", "Failed:\n" + exception.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod ForestLinesSetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupForestLines());
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RequestSetup()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, System.DateTime.UtcNow.ToString("O"));
        TrySetupFromFlag();
    }

    public static void RequestForestParallax()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ParallaxFlagPath, System.DateTime.UtcNow.ToString("O"));
        TryEnsureForestParallaxFromFlag();
    }

    private static void PollForestParallaxFlag()
    {
        if (!File.Exists(ParallaxFlagPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TryEnsureForestParallaxFromFlag();
    }

    private static void TryEnsureForestParallaxFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ParallaxFlagPath))
        {
            return;
        }

        File.Delete(ParallaxFlagPath);
        try
        {
            Debug.Log(EnsureForestParallaxOnly());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Ensure Forest Parallax")]
    public static void EnsureForestParallaxMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Forest Parallax", EnsureForestParallaxOnly(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Forest Parallax", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string EnsureForestParallaxOnly()
    {
        Scene scene = FindPrototypeScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return "Prototype scene is not loaded. Open it and run Tools → Castlevania 2D → Ensure Forest Parallax.";
        }

        GameObject group = FindForestGroup(scene);
        if (group == null)
        {
            return "No Background (1) in Prototype.";
        }

        bool alreadyHad = group.GetComponent<SceneBackgroundParallax2D>() != null;
        EnsureParallax(group);
        if (!alreadyHad)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return alreadyHad
            ? "Background (1) already had parallax."
            : "Parallax on Background (1): ForestLine 1/2/3, Sky, UndergroundLine4.";
    }

    private static Scene FindPrototypeScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath)
        {
            return active;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == ScenePath)
            {
                return scene;
            }
        }

        return default;
    }

    private static GameObject FindForestGroup(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == ForestGroupName)
            {
                return root;
            }

            Transform nested = FindNamedRecursive(root.transform, ForestGroupName);
            if (nested != null)
            {
                return nested.gameObject;
            }
        }

        return null;
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(FlagPath))
        {
            return;
        }

        File.Delete(FlagPath);
        try
        {
            Debug.Log(SetupForestLines());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static string SetupForestLines()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        string summary = EnsureInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return summary;
    }

    public static string EnsureInScene(Scene scene)
    {
        EnsureFolder(DestFolder);
        CopySourcesIfNeeded();
        AssetDatabase.Refresh();

        GameObject group = FindOrCreateForestGroup(scene);
        HideLegacyForestChildren(group);
        PlaceLayers(group);
        EnsureParallax(group);
        SceneBackgroundParallaxSetupEditor.EnsureInScene(scene);
        return "Replaced Background (1) with ForestLine 1/2/3 (near / mid / far) and parallax.";
    }

    private static void CopySourcesIfNeeded()
    {
        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Бэкграунд ФОН",
                "Лес"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Бэкграунд ФОН",
                "Лес")
        };

        for (int i = 0; i < sourceFolders.Length; i++)
        {
            string desktop = sourceFolders[i];
            CopyIfPresent(Path.Combine(desktop, "1ЛинияЛЕС.png"), DestFolder + "/ForestLine1_Near.png");
            CopyIfPresent(Path.Combine(desktop, "2ЛинияЛЕС.png"), DestFolder + "/ForestLine2_Mid.png");
            CopyIfPresent(Path.Combine(desktop, "3ЛинияЛЕС.png"), DestFolder + "/ForestLine3_Far.png");
        }
    }

    private static void CopyIfPresent(string sourceFullPath, string destAssetPath)
    {
        string destFull = ToFullPath(destAssetPath);
        if (!File.Exists(sourceFullPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destFull));
        File.Copy(sourceFullPath, destFull, true);
        AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureAsSprite(destAssetPath);
    }

    private static void PlaceLayers(GameObject group)
    {
        for (int i = 0; i < Layers.Length; i++)
        {
            LayerSpec spec = Layers[i];
            ConfigureAsSprite(spec.AssetPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.AssetPath);
            if (sprite == null)
            {
                throw new FileNotFoundException("Missing sprite " + spec.AssetPath);
            }

            Transform existing = group.transform.Find(spec.ObjectName);
            GameObject layer = existing != null ? existing.gameObject : new GameObject(spec.ObjectName);
            layer.name = spec.ObjectName;
            layer.transform.SetParent(group.transform, false);
            layer.transform.localPosition = new Vector3(0f, LayerLocalY, spec.LocalZ);
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = layer.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = TiledWorldSize;
            renderer.sortingOrder = spec.SortingOrder;
            renderer.color = Color.white;
            renderer.maskInteraction = SpriteMaskInteraction.None;
        }

        group.transform.localPosition = GroupLocalPosition;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
    }

    private static void EnsureParallax(GameObject group)
    {
        SceneBackgroundParallax2D parallax = group.GetComponent<SceneBackgroundParallax2D>();
        if (parallax == null)
        {
            parallax = group.AddComponent<SceneBackgroundParallax2D>();
        }

        SerializedObject so = new SerializedObject(parallax);
        SerializedProperty cameraProperty = so.FindProperty("cameraTransform");
        SerializedProperty affectY = so.FindProperty("affectY");
        SerializedProperty overrides = so.FindProperty("followOverrides");

        Camera camera = Camera.main;
        if (camera != null && cameraProperty != null)
        {
            cameraProperty.objectReferenceValue = camera.transform;
        }

        if (affectY != null)
        {
            affectY.boolValue = false;
        }

        EnsureLine4FollowOverride(overrides);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureLine4FollowOverride(SerializedProperty overrides)
    {
        if (overrides == null)
        {
            return;
        }

        for (int i = 0; i < overrides.arraySize; i++)
        {
            SerializedProperty rule = overrides.GetArrayElementAtIndex(i);
            SerializedProperty prefix = rule.FindPropertyRelative("namePrefix");
            if (prefix != null && prefix.stringValue == "UndergroundLine4")
            {
                SerializedProperty follow = rule.FindPropertyRelative("cameraFollow");
                if (follow != null)
                {
                    follow.floatValue = 0.48f;
                }

                return;
            }
        }

        int index = overrides.arraySize;
        overrides.arraySize++;
        SerializedProperty created = overrides.GetArrayElementAtIndex(index);
        created.FindPropertyRelative("namePrefix").stringValue = "UndergroundLine4";
        created.FindPropertyRelative("cameraFollow").floatValue = 0.48f;
    }

    private static GameObject FindOrCreateForestGroup(Scene scene)
    {
        GameObject background = FindNamed(scene, BackgroundRootName);
        if (background == null)
        {
            background = new GameObject(BackgroundRootName);
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.position = Vector3.zero;
        }

        Transform groupTransform = background.transform.Find(ForestGroupName);
        if (groupTransform != null)
        {
            return groupTransform.gameObject;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform nested = FindNamedRecursive(root.transform, ForestGroupName);
            if (nested != null)
            {
                nested.SetParent(background.transform, true);
                return nested.gameObject;
            }
        }

        GameObject created = new GameObject(ForestGroupName);
        created.transform.SetParent(background.transform, false);
        return created;
    }

    private static void HideLegacyForestChildren(GameObject group)
    {
        for (int i = 0; i < group.transform.childCount; i++)
        {
            Transform child = group.transform.GetChild(i);
            if (child.name.StartsWith("BackGroundForest", System.StringComparison.Ordinal) ||
                child.name.StartsWith("BackgroundForest", System.StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static Transform FindNamedRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindNamedRecursive(current.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ConfigureAsSprite(string assetPath)
    {
        if (!File.Exists(ToFullPath(assetPath)))
        {
            throw new FileNotFoundException("Forest line PNG not found:\n" + assetPath);
        }

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
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = 2048;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
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

    private readonly struct LayerSpec
    {
        public readonly string ObjectName;
        public readonly string AssetPath;
        public readonly float LocalZ;
        public readonly int SortingOrder;

        public LayerSpec(string objectName, string assetPath, float localZ, int sortingOrder)
        {
            ObjectName = objectName;
            AssetPath = assetPath;
            LocalZ = localZ;
            SortingOrder = sortingOrder;
        }
    }
}
