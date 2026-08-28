using System.IO;
using Castlevania2D.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneWarpTotemSetupEditor
{
    private const string FlagPath = "Temp/setup_warp_totem.flag";
    private const string OpenPrototypeFlagPath = "Temp/open_prototype_scene.flag";
    private const string DestFolder = "Assets/Art/Sprites/Environment/WarpTotem";
    private const string HubScenePath = "Assets/Scenes/Hub.unity";
    private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";
    private const string HubTotemName = "HubWarpTotem";
    private const string PrototypeTotemName = "PrototypeWarpTotem";
    private const string IzbaObjectName = "HubIzba";
    private const string PlayerObjectName = "Player_HeroKnight";
    private const float PixelsPerUnit = 32f;
    private const int SortingOrder = -1;
    private const float FrameRate = 10f;
    private static readonly Vector3 HubFallbackPosition = new Vector3(-28.6f, -5.48f, 0f);
    private static readonly Vector3 HubOffsetFromIzba = new Vector3(3.4f, 0f, 0f);
    private static readonly Vector3 PrototypeOffsetFromPlayer = new Vector3(3.4f, 0f, 0f);

    static SceneWarpTotemSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.delayCall += TryOpenPrototypeFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Place Warp Totems")]
    public static void PlaceFromMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Warp Totem", PlaceTotems(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Warp Totem", "Failed:\n" + exception.Message, "OK");
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
            Debug.Log(PlaceTotems());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryOpenPrototypeFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(OpenPrototypeFlagPath))
        {
            return;
        }

        File.Delete(OpenPrototypeFlagPath);
        EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
    }

    public static string PlaceTotems()
    {
        CopySourcesIfNeeded();
        Sprite[] frames = ImportFrames();
        Sprite prompt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/InteractPrompt_F.png");
        string previousPath = SceneManager.GetActiveScene().path;

        PlaceInScene(
            HubScenePath,
            HubTotemName,
            ResolveHubPosition,
            "Prototype",
            PrototypeTotemName,
            frames,
            prompt);

        PlaceInScene(
            PrototypeScenePath,
            PrototypeTotemName,
            ResolvePrototypePosition,
            "Hub",
            HubTotemName,
            frames,
            prompt);

        if (!string.IsNullOrEmpty(previousPath) && File.Exists(previousPath))
        {
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
        }

        return "Warp totems placed: Hub near izba (to Prototype), Prototype at start (back to Hub).";
    }

    private static void PlaceInScene(
        string scenePath,
        string totemName,
        System.Func<Scene, Vector3> resolvePosition,
        string destinationScene,
        string arrivalTotemName,
        Sprite[] frames,
        Sprite prompt)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject existing = FindNamed(scene, totemName);
        bool created = existing == null;
        GameObject totem = created ? new GameObject(totemName) : existing;
        totem.name = totemName;
        if (created)
        {
            SceneManager.MoveGameObjectToScene(totem, scene);
            totem.transform.position = resolvePosition(scene);
            totem.transform.rotation = Quaternion.identity;
            totem.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = totem.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = totem.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = frames[0];
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;

        SceneWarpTotemAnimator2D animator = totem.GetComponent<SceneWarpTotemAnimator2D>();
        if (animator == null)
        {
            animator = totem.AddComponent<SceneWarpTotemAnimator2D>();
        }

        animator.EditorAssignFrames(frames[0], frames);
        SerializedObject animatorSo = new SerializedObject(animator);
        SerializedProperty idleProp = animatorSo.FindProperty("idleSprite");
        SerializedProperty framesProp = animatorSo.FindProperty("warpFrames");
        SerializedProperty rate = animatorSo.FindProperty("frameRate");
        if (idleProp != null)
        {
            idleProp.objectReferenceValue = frames[0];
        }

        if (framesProp != null && framesProp.isArray)
        {
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }

        if (rate != null)
        {
            rate.floatValue = FrameRate;
        }

        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        SceneWarpTotem2D warp = totem.GetComponent<SceneWarpTotem2D>();
        if (warp == null)
        {
            warp = totem.AddComponent<SceneWarpTotem2D>();
        }

        warp.EditorAssign(destinationScene, arrivalTotemName, prompt);
        SerializedObject warpSo = new SerializedObject(warp);
        SerializedProperty destProp = warpSo.FindProperty("destinationSceneName");
        SerializedProperty arrivalProp = warpSo.FindProperty("arrivalTotemName");
        SerializedProperty promptProp = warpSo.FindProperty("promptSprite");
        if (destProp != null)
        {
            destProp.stringValue = destinationScene;
        }

        if (arrivalProp != null)
        {
            arrivalProp.stringValue = arrivalTotemName;
        }

        if (promptProp != null)
        {
            promptProp.objectReferenceValue = prompt;
        }

        warpSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Vector3 ResolveHubPosition(Scene scene)
    {
        GameObject izba = FindNamedDeep(scene, IzbaObjectName);
        if (izba != null)
        {
            Vector3 position = izba.transform.position + HubOffsetFromIzba;
            position.z = 0f;
            return position;
        }

        return HubFallbackPosition;
    }

    private static Vector3 ResolvePrototypePosition(Scene scene)
    {
        GameObject player = FindNamed(scene, PlayerObjectName);
        if (player != null)
        {
            Vector3 position = player.transform.position + PrototypeOffsetFromPlayer;
            position.z = 0f;
            return position;
        }

        return new Vector3(-19f, -36.87f, 0f);
    }

    private static Sprite[] ImportFrames()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Sprites");
        EnsureFolder("Assets/Art/Sprites/Environment");
        EnsureFolder(DestFolder);

        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            string assetPath = DestFolder + "/WarpTotem_" + i.ToString("00") + ".png";
            ConfigureAsSprite(assetPath);
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (frames[i] == null)
            {
                throw new FileNotFoundException("Missing warp totem sprite:\n" + assetPath);
            }
        }

        return frames;
    }

    private static void CopySourcesIfNeeded()
    {
        string destFull = ToFullPath(DestFolder);
        Directory.CreateDirectory(destFull);
        if (Directory.GetFiles(destFull, "WarpTotem_*.png").Length >= 4)
        {
            return;
        }

        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Мара",
                "ТП"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Мара",
                "ТП")
        };

        string sourceFolder = null;
        for (int i = 0; i < sourceFolders.Length; i++)
        {
            if (Directory.Exists(sourceFolders[i]))
            {
                sourceFolder = sourceFolders[i];
                break;
            }
        }

        if (sourceFolder == null)
        {
            return;
        }

        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png");
        System.Array.Sort(sourceFiles, System.StringComparer.OrdinalIgnoreCase);
        int count = Mathf.Min(4, sourceFiles.Length);
        for (int i = 0; i < count; i++)
        {
            File.Copy(
                sourceFiles[i],
                Path.Combine(destFull, "WarpTotem_" + i.ToString("00") + ".png"),
                true);
        }

        AssetDatabase.Refresh();
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
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
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

    private static GameObject FindNamedDeep(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChildNamed(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildNamed(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildNamed(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string name = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(assetPath);
    }
}
