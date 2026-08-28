using System.IO;
using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces Prototype BackgroundUnderground children with 3 tiled clay-cave parallax plates.
/// Слой1 = near, Слой2 = mid, Слой3 = far.
/// </summary>
[InitializeOnLoad]
public static class UndergroundLinesSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string DestFolder = "Assets/Art/Backgrounds/Underground/Lines";
    private const string FlagPath = "Temp/setup_underground_lines.flag";
    private const string ReplaceRootsFlagPath = "Temp/replace_roots03_with_layer3.flag";
    private const string GroupName = "BackgroundUnderground";
    private const string ForestGroupName = "Background (1)";
    private const string Layer3AssetPath = DestFolder + "/UndergroundLine3_Far.png";
    private static readonly Vector2 RootsPlateWorldSize = new Vector2(21.5f, 12f);
    private static readonly string[] RootsPlateNames =
    {
        "Roots_Background_03 (6)",
        "Roots_Background_03 (7)"
    };
    private const float PixelsPerUnit = 32f / 0.75f;
    private const int NearSortingOrder = -100;
    private static readonly Vector3 LayerLocalPosition = new Vector3(20f, -61.6f, 0f);
    private static readonly Vector2 TiledWorldSize = new Vector2(129f, 9f);

    private static readonly LayerSpec[] Layers =
    {
        new LayerSpec("UndergroundLine3_Far", DestFolder + "/UndergroundLine3_Far.png", 20f, NearSortingOrder - 2),
        new LayerSpec("UndergroundLine2_Mid", DestFolder + "/UndergroundLine2_Mid.png", 12f, NearSortingOrder - 1),
        new LayerSpec("UndergroundLine1_Near", DestFolder + "/UndergroundLine1_Near.png", 6f, NearSortingOrder)
    };

    static UndergroundLinesSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.delayCall += TryReplaceRootsFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Underground Cave Backgrounds")]
    public static void SetupMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Underground Background", SetupUndergroundLines(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Underground Background", "Failed:\n" + exception.Message, "OK");
        }
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
            Debug.Log(SetupUndergroundLines());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryReplaceRootsFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(ReplaceRootsFlagPath))
        {
            return;
        }

        File.Delete(ReplaceRootsFlagPath);
        try
        {
            Debug.Log(ReplaceRoots03PlatesWithLayer3());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Replace Roots 03 (6)(7) With Underground Layer 3")]
    public static void ReplaceRootsMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Underground Layer 3", ReplaceRoots03PlatesWithLayer3(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Underground Layer 3", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string ReplaceRoots03PlatesWithLayer3()
    {
        CopyLayer3FromDesktop();
        AssetDatabase.Refresh();
        ConfigureAsSprite(Layer3AssetPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Layer3AssetPath);
        if (sprite == null)
        {
            throw new FileNotFoundException("Missing sprite " + Layer3AssetPath);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject forestGroup = FindNamed(scene, ForestGroupName);
        if (forestGroup == null)
        {
            throw new InvalidDataException(ForestGroupName + " not found in Prototype.");
        }

        float nativeWidth = sprite.rect.width / sprite.pixelsPerUnit;
        float nativeHeight = sprite.rect.height / sprite.pixelsPerUnit;
        var plateScale = new Vector3(
            RootsPlateWorldSize.x / Mathf.Max(0.01f, nativeWidth),
            RootsPlateWorldSize.y / Mathf.Max(0.01f, nativeHeight),
            1f);

        int replaced = 0;
        for (int i = 0; i < RootsPlateNames.Length; i++)
        {
            Transform plate = forestGroup.transform.Find(RootsPlateNames[i]);
            if (plate == null)
            {
                string alreadyReplaced = i == 0
                    ? "UndergroundLine3_Far (6)"
                    : "UndergroundLine3_Far (7)";
                plate = forestGroup.transform.Find(alreadyReplaced);
            }

            if (plate == null)
            {
                continue;
            }

            plate.name = i == 0 ? "UndergroundLine3_Far (6)" : "UndergroundLine3_Far (7)";
            plate.localScale = plateScale;
            SpriteRenderer renderer = plate.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = plate.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            plate.gameObject.SetActive(true);
            replaced++;
        }

        if (replaced == 0)
        {
            throw new InvalidDataException(
                "Roots_Background_03 (6)/(7) not found under " + ForestGroupName + ".");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Replaced " + replaced + " Roots_Background_03 plates under " +
               ForestGroupName + " with " + Layer3AssetPath + ".";
    }

    private static void CopyLayer3FromDesktop()
    {
        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Бэкграунд ФОН",
                "Подъземье"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Бэкграунд ФОН",
                "Подъземье")
        };

        for (int i = 0; i < sourceFolders.Length; i++)
        {
            CopyIfPresent(Path.Combine(sourceFolders[i], "ПодъземьеСлой3.png"), Layer3AssetPath);
            CopyIfPresent(Path.Combine(sourceFolders[i], "Земля.png"), Layer3AssetPath);
        }
    }

    public static string SetupUndergroundLines()
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
        EnsureFolder("Assets/Art/Backgrounds");
        EnsureFolder("Assets/Art/Backgrounds/Underground");
        EnsureFolder(DestFolder);
        CopySourcesIfNeeded();
        AssetDatabase.Refresh();

        GameObject group = FindOrCreateGroup(scene);
        HideLegacyChildren(group);
        PlaceLayers(group);
        EnsureParallax(group);
        return "BackgroundUnderground: 3 clay-cave layers (near/mid/far) with X parallax. Old Roots plates hidden.";
    }

    private static void CopySourcesIfNeeded()
    {
        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Бэкграунд ФОН",
                "Подъземье"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Бэкграунд ФОН",
                "Подъземье")
        };

        for (int i = 0; i < sourceFolders.Length; i++)
        {
            string desktop = sourceFolders[i];
            CopyIfPresent(Path.Combine(desktop, "ПодъземьеСлой1.png"), DestFolder + "/UndergroundLine1_Near.png");
            CopyIfPresent(Path.Combine(desktop, "ПодъземьеСлой2.png"), DestFolder + "/UndergroundLine2_Mid.png");
            CopyIfPresent(Path.Combine(desktop, "ПодъземьеСлой3.png"), DestFolder + "/UndergroundLine3_Far.png");
            CopyIfPresent(Path.Combine(desktop, "Земля.png"), DestFolder + "/UndergroundLine3_Far.png");
        }
    }

    private static void CopyIfPresent(string sourceFullPath, string destAssetPath)
    {
        if (!File.Exists(sourceFullPath))
        {
            return;
        }

        string destFull = ToFullPath(destAssetPath);
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
            layer.SetActive(true);
            layer.transform.localPosition = new Vector3(
                LayerLocalPosition.x,
                LayerLocalPosition.y,
                spec.LocalZ);
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
    }

    private static GameObject FindOrCreateGroup(Scene scene)
    {
        GameObject group = FindNamed(scene, GroupName);
        if (group != null)
        {
            return group;
        }

        group = new GameObject(GroupName);
        SceneManager.MoveGameObjectToScene(group, scene);
        group.transform.position = Vector3.zero;
        return group;
    }

    private static void HideLegacyChildren(GameObject group)
    {
        for (int i = 0; i < group.transform.childCount; i++)
        {
            Transform child = group.transform.GetChild(i);
            if (child.name.StartsWith("UndergroundLine", System.StringComparison.Ordinal))
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private static void EnsureParallax(GameObject group)
    {
        SceneBackgroundParallax2D parallax = group.GetComponent<SceneBackgroundParallax2D>();
        if (parallax == null)
        {
            parallax = group.AddComponent<SceneBackgroundParallax2D>();
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(parallax);
        SerializedProperty cameraProperty = so.FindProperty("cameraTransform");
        SerializedProperty affectY = so.FindProperty("affectY");
        if (cameraProperty != null)
        {
            cameraProperty.objectReferenceValue = camera.transform;
        }

        if (affectY != null)
        {
            affectY.boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
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

    private static void ConfigureAsSprite(string assetPath)
    {
        if (!File.Exists(ToFullPath(assetPath)))
        {
            throw new FileNotFoundException("Underground line PNG not found:\n" + assetPath);
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
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
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
