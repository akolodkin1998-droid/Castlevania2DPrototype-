using System.IO;
using Castlevania2D.Intro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class IntroSceneSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Intro.unity";
    private const string BackgroundAssetPath = "Assets/Resources/Backgrounds/Intro/IzbaIntro.png";
    private const string SourceBackgroundPath =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Первая сцена в игре\pixellab-2D-pixel-art-interior-of-a-tra-1791013006693.png";
    private const string SourceRoot =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Первая сцена в игре";
    private const string SourceIdleFolder = SourceRoot + @"\начало диалога";
    private const string SourceLookFolder = SourceRoot + @"\Середина";
    private const string SourceTalkFolder = SourceRoot + @"\разговор";
    private const string SourcePortraitPath = SourceRoot + @"\pixellab--------------------------------1791014719794.png";
    private const string IdleFolder = "Assets/Resources/Npcs/IntroHost/Idle";
    private const string LookFolder = "Assets/Resources/Npcs/IntroHost/Look";
    private const string TalkFolder = "Assets/Resources/Npcs/IntroHost/Talk";
    private const string PortraitAssetPath = "Assets/Resources/UI/Dialogue/IntroHostPortrait.png";
    private const string SourceWomanFolder = SourceRoot + @"\Женщина кашель";
    private const string WomanFolder = "Assets/Resources/Npcs/IntroWoman";
    private const string OpenFlagPath = "Temp/open_intro_scene.flag";
    private const string SetupFlagPath = "Temp/setup_intro_scene.flag";

    static IntroSceneSetupEditor()
    {
        EditorApplication.delayCall += TrySetupAndOpen;
        EditorApplication.update += PollFlags;
    }

    private static void PollFlags()
    {
        if (File.Exists(SetupFlagPath) || File.Exists(OpenFlagPath))
        {
            TrySetupAndOpen();
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Intro Scene")]
    public static void SetupFromMenu()
    {
        PrepareScene();
        EditorUtility.DisplayDialog("Intro", "Сцена готова:\n" + ScenePath, "OK");
    }

    [MenuItem("Tools/Castlevania 2D/Open Intro Scene")]
    public static void OpenFromMenu()
    {
        PrepareScene();
    }

    private static void TrySetupAndOpen()
    {
        bool setupRequested = File.Exists(SetupFlagPath);
        bool openRequested = File.Exists(OpenFlagPath);
        if (setupRequested)
        {
            File.Delete(SetupFlagPath);
        }

        if (openRequested)
        {
            File.Delete(OpenFlagPath);
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (setupRequested || !File.Exists(ScenePath))
        {
            PrepareScene();
            return;
        }

        if (openRequested)
        {
            OpenScene();
        }
    }

    private static void PrepareScene()
    {
        ImportBackground();
        ImportHostSprites();
        ImportWomanSprites();

        Scene scene;
        if (!File.Exists(ScenePath))
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureSceneObjects();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureSceneObjects();
            EditorSceneManager.SaveOpenScenes();
        }

        InsertFirstInBuildSettings();
        AssetDatabase.SaveAssets();
        OpenScene();
        Debug.Log("[IntroSceneSetupEditor] Ready: " + ScenePath);
    }

    private static void OpenScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[IntroSceneSetupEditor] Missing " + ScenePath);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[IntroSceneSetupEditor] Opened " + ScenePath);
    }

    private static void EnsureSceneObjects()
    {
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.07f, 0.05f, 1f);
        camera.transform.position = new Vector3(0f, 0f, -10f);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        EnsureBackground(camera);
        IntroSeatedNpc2D host = EnsureHost();
        IntroWomanNpc2D woman = EnsureWoman();

        IntroDialogueDirector director = Object.FindFirstObjectByType<IntroDialogueDirector>();
        if (director == null)
        {
            var directorObject = new GameObject("IntroDialogue", typeof(IntroDialogueDirector));
            director = directorObject.GetComponent<IntroDialogueDirector>();
        }

        director.EditorAssignHost(host);
        director.EditorAssignWoman(woman);
        EditorUtility.SetDirty(director);
        Selection.activeGameObject = woman.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    private static IntroSeatedNpc2D EnsureHost()
    {
        IntroSeatedNpc2D host = Object.FindFirstObjectByType<IntroSeatedNpc2D>();
        bool created = false;
        if (host == null)
        {
            GameObject leftover = GameObject.Find("IntroHost");
            if (leftover == null)
            {
                leftover = GameObject.Find("IntroVaryag");
            }

            if (leftover != null)
            {
                host = leftover.GetComponent<IntroSeatedNpc2D>();
                if (host == null)
                {
                    host = leftover.AddComponent<IntroSeatedNpc2D>();
                }
            }
            else
            {
                var hostObject = new GameObject("IntroHost", typeof(SpriteRenderer), typeof(IntroSeatedNpc2D));
                host = hostObject.GetComponent<IntroSeatedNpc2D>();
                created = true;
            }
        }

        host.gameObject.name = "IntroHost";
        SpriteRenderer renderer = host.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 5;
        renderer.color = Color.white;
        if (created)
        {
            host.transform.position = Vector3.zero;
            host.transform.localScale = Vector3.one;
        }

        Sprite[] idle = LoadSortedSprites(IdleFolder);
        Sprite[] look = LoadSortedSprites(LookFolder);
        Sprite[] talk = LoadSortedSprites(TalkFolder);
        host.EditorAssign(idle, look, talk);
        EditorUtility.SetDirty(host);
        return host;
    }

    private static IntroWomanNpc2D EnsureWoman()
    {
        IntroWomanNpc2D woman = Object.FindFirstObjectByType<IntroWomanNpc2D>();
        bool created = false;
        if (woman == null)
        {
            GameObject leftover = GameObject.Find("IntroWoman");
            if (leftover != null)
            {
                woman = leftover.GetComponent<IntroWomanNpc2D>();
                if (woman == null)
                {
                    woman = leftover.AddComponent<IntroWomanNpc2D>();
                }
            }
            else
            {
                var womanObject = new GameObject("IntroWoman", typeof(SpriteRenderer), typeof(IntroWomanNpc2D));
                woman = womanObject.GetComponent<IntroWomanNpc2D>();
                created = true;
            }
        }

        woman.gameObject.name = "IntroWoman";
        SpriteRenderer renderer = woman.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 6;
        renderer.color = Color.white;
        if (created)
        {
            woman.transform.position = new Vector3(2.5f, 1.2f, 0f);
            woman.transform.localScale = Vector3.one;
        }

        woman.EditorAssign(LoadSortedSprites(WomanFolder));
        EditorUtility.SetDirty(woman);
        return woman;
    }

    private static void ImportWomanSprites()
    {
        CopyNumberedPngs(SourceWomanFolder, WomanFolder, "Cough");
        ConfigureFolderSprites(WomanFolder, SpriteAlignment.Center);
    }

    private static Sprite[] LoadSortedSprites(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var sprites = new Sprite[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        System.Array.Sort(sprites, (a, b) =>
        {
            string left = a != null ? a.name : string.Empty;
            string right = b != null ? b.name : string.Empty;
            return string.CompareOrdinal(left, right);
        });
        return sprites;
    }

    private static void ImportHostSprites()
    {
        CopyNumberedPngs(SourceIdleFolder, IdleFolder, "Idle");
        CopyNumberedPngs(SourceLookFolder, LookFolder, "Look");
        CopyNumberedPngs(SourceTalkFolder, TalkFolder, "Talk");
        CopyPortrait();
        ConfigureFolderSprites(IdleFolder, SpriteAlignment.BottomCenter);
        ConfigureFolderSprites(LookFolder, SpriteAlignment.BottomCenter);
        ConfigureFolderSprites(TalkFolder, SpriteAlignment.BottomCenter);
        ConfigureSprite(PortraitAssetPath, SpriteAlignment.Center);
    }

    private static void CopyNumberedPngs(string sourceFolder, string destFolder, string prefix)
    {
        Directory.CreateDirectory(ToFullPath(destFolder));
        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogError("[IntroSceneSetupEditor] Missing folder " + sourceFolder);
            return;
        }

        string[] files = Directory.GetFiles(sourceFolder, "*.png");
        System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string dest = Path.Combine(ToFullPath(destFolder), prefix + "_" + (i + 1).ToString("00") + ".png");
            File.Copy(files[i], dest, overwrite: true);
        }
    }

    private static void CopyPortrait()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ToFullPath(PortraitAssetPath)));
        if (!File.Exists(SourcePortraitPath))
        {
            Debug.LogError("[IntroSceneSetupEditor] Missing portrait " + SourcePortraitPath);
            return;
        }

        File.Copy(SourcePortraitPath, ToFullPath(PortraitAssetPath), overwrite: true);
    }

    private static void ConfigureFolderSprites(string folder, SpriteAlignment alignment)
    {
        string[] files = Directory.Exists(ToFullPath(folder))
            ? Directory.GetFiles(ToFullPath(folder), "*.png")
            : System.Array.Empty<string>();
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = folder + "/" + Path.GetFileName(files[i]);
            ConfigureSprite(assetPath, alignment);
        }
    }

    private static void ConfigureSprite(string assetPath, SpriteAlignment alignment)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.spritePivot = alignment == SpriteAlignment.BottomCenter
            ? new Vector2(0.5f, 0f)
            : new Vector2(0.5f, 0.5f);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)alignment;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(assetPath);
    }

    private static void EnsureBackground(Camera camera)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundAssetPath);
        if (sprite == null)
        {
            Debug.LogError("[IntroSceneSetupEditor] Missing sprite " + BackgroundAssetPath);
            return;
        }

        GameObject background = GameObject.Find("Background");
        if (background == null)
        {
            background = new GameObject("Background", typeof(SpriteRenderer), typeof(IntroBackgroundFit2D));
        }

        SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = background.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = -100;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Simple;

        IntroBackgroundFit2D fit = background.GetComponent<IntroBackgroundFit2D>();
        if (fit == null)
        {
            fit = background.AddComponent<IntroBackgroundFit2D>();
        }

        SerializedObject fitObject = new SerializedObject(fit);
        fitObject.FindProperty("targetCamera").objectReferenceValue = camera;
        fitObject.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        fitObject.ApplyModifiedPropertiesWithoutUndo();

        background.transform.position = Vector3.zero;
        fit.Fit();
    }

    private static void ImportBackground()
    {
        string destFull = Path.GetFullPath(BackgroundAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destFull));
        if (File.Exists(SourceBackgroundPath))
        {
            File.Copy(SourceBackgroundPath, destFull, overwrite: true);
        }

        AssetDatabase.ImportAsset(BackgroundAssetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(BackgroundAssetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void InsertFirstInBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        var next = new EditorBuildSettingsScene[current.Length + 1];
        int write = 1;
        bool alreadyListed = false;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == ScenePath)
            {
                alreadyListed = true;
                continue;
            }

            if (write >= next.Length)
            {
                var grown = new EditorBuildSettingsScene[write + 1];
                for (int j = 0; j < write; j++)
                {
                    grown[j] = next[j];
                }

                next = grown;
            }

            next[write] = current[i];
            write++;
        }

        if (alreadyListed)
        {
            var trimmed = new EditorBuildSettingsScene[write];
            trimmed[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (int i = 1; i < write; i++)
            {
                trimmed[i] = next[i];
            }

            EditorBuildSettings.scenes = trimmed;
            return;
        }

        next[0] = new EditorBuildSettingsScene(ScenePath, true);
        if (write != next.Length)
        {
            var trimmed = new EditorBuildSettingsScene[write];
            trimmed[0] = next[0];
            for (int i = 1; i < write; i++)
            {
                trimmed[i] = next[i];
            }

            EditorBuildSettings.scenes = trimmed;
            return;
        }

        EditorBuildSettings.scenes = next;
    }
}
