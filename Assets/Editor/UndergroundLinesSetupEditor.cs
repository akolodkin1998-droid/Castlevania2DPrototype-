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
    private const string ReplaceLine3FlagPath = "Temp/replace_underground_line3.flag";
    private const string ReplaceLine4FlagPath = "Temp/replace_underground_line4.flag";
    private const string GroupName = "BackgroundUnderground";
    private const string ForestGroupName = "Background (1)";
    private const string Layer3AssetPath = DestFolder + "/UndergroundLine3_Far.png";
    private const string Layer4AssetPath = DestFolder + "/UndergroundLine4_soFar.png";
    private const string DirtPlateAssetPath = Layer4AssetPath;
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
        EditorApplication.delayCall += TryReplaceLine3FromFlag;
        EditorApplication.delayCall += TryReplaceLine4FromFlag;
        EditorApplication.update += PollLine3Flag;
        EditorApplication.update += PollLine4Flag;
    }

    private static void PollLine3Flag()
    {
        if (!File.Exists(ReplaceLine3FlagPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TryReplaceLine3FromFlag();
    }

    private static void PollLine4Flag()
    {
        if (!File.Exists(ReplaceLine4FlagPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TryReplaceLine4FromFlag();
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

    private static void TryReplaceLine3FromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ReplaceLine3FlagPath))
        {
            return;
        }

        File.Delete(ReplaceLine3FlagPath);
        try
        {
            Debug.Log(ApplySloy3ToFarLayer());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryReplaceLine4FromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ReplaceLine4FlagPath))
        {
            return;
        }

        File.Delete(ReplaceLine4FlagPath);
        try
        {
            Debug.Log(ApplyZemlyaToLine4());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Replace Underground Line 4 With Zemlya")]
    public static void ReplaceLine4Menu()
    {
        try
        {
            EditorUtility.DisplayDialog("Underground Line 4", ApplyZemlyaToLine4(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Underground Line 4", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string ApplyZemlyaToLine4()
    {
        CopyLine4FromDesktop();
        AssetDatabase.Refresh();
        ConfigureAsSprite(Layer4AssetPath);

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int rebound = RebindLine4Sprites(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "UndergroundLine4_soFar now uses Земля.png. Rebound " + rebound +
               " objects. UndergroundLine3_Far was not changed.";
    }

    [MenuItem("Tools/Castlevania 2D/Replace Underground Line 3 With Sloy3")]
    public static void ReplaceLine3Menu()
    {
        try
        {
            EditorUtility.DisplayDialog("Underground Line 3", ApplySloy3ToFarLayer(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Underground Line 3", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string ApplySloy3ToFarLayer()
    {
        CopyLayer3FromDesktop();
        AssetDatabase.Refresh();
        ConfigureAsSprite(Layer3AssetPath);

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int rebound = RebindForestDirtPlates(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "UndergroundLine3_Far now uses ПодъземьеСлой3.png. Forest dirt plates rebound: " + rebound + ".";
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
        if (File.Exists(ToFullPath(DirtPlateAssetPath)))
        {
            ConfigureAsSprite(DirtPlateAssetPath);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int rebound = RebindForestDirtPlates(scene);
        if (rebound == 0)
        {
            throw new InvalidDataException(
                "Forest dirt plates not found under " + ForestGroupName + ".");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Rebound " + rebound + " forest dirt plates to " + DirtPlateAssetPath + ".";
    }

    private static int RebindForestDirtPlates(Scene scene)
    {
        Sprite dirt = AssetDatabase.LoadAssetAtPath<Sprite>(DirtPlateAssetPath);
        if (dirt == null)
        {
            return 0;
        }

        int rebound = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            SpriteRenderer[] renderers = roots[r].GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.name.StartsWith("UndergroundLine4_soFar"))
                {
                    continue;
                }

                renderer.sprite = dirt;
                rebound++;
            }
        }

        return rebound;
    }

    private static int RebindLine4Sprites(Scene scene)
    {
        return RebindForestDirtPlates(scene);
    }

    private static void CopyLine4FromDesktop()
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
            CopyIfPresent(Path.Combine(sourceFolders[i], "Земля.png"), Layer4AssetPath);
        }
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
            CopyIfPresent(Path.Combine(sourceFolders[i], "Земля.png"), Layer4AssetPath);
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
            CopyIfPresent(Path.Combine(desktop, "Земля.png"), Layer4AssetPath);
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
