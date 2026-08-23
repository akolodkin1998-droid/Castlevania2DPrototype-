using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Castlevania2D.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SaveLoadMenuSetupEditor
{
    private const string DestinationFolder = "Assets/Art/Sprites/UI/SaveLoadMenu";
    private const string BackgroundDestinationName = "Background";
    private const string ScenePath = "Assets/Scenes/SaveLoadMenu.unity";
    private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const float ArtworkSize = 768f;
    private const float SourceSize = 256f;
    private const int SlotButtonCount = 5;

    private static readonly string[] DestinationNames =
    {
        "SaveButton",
        "LoadButton",
        "CloseButton",
        "Title",
        "Panel",
    };

    [MenuItem("Tools/Castlevania 2D/Setup Save Load Menu")]
    public static void SetupFromMenu()
    {
        try
        {
            SetupScene();
            EditorUtility.DisplayDialog("Save/Load Menu", $"Scene created:\n{ScenePath}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Save/Load Menu", exception.Message, "OK");
        }
    }

    public static void SetupScene()
    {
        CopyAndImportSprites();
        Sprite[] sprites = LoadSprites();
        Sprite backgroundSprite = LoadBackgroundSprite();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject canvasObject = CreateCanvas(backgroundSprite);
        RectTransform artworkRoot = CreateArtworkRoot(canvasObject.transform);

        CreateOverlayImage(artworkRoot, "Panel", sprites[4]);
        RectTransform saveButtonVisual =
            CreateOverlayImage(artworkRoot, "SaveButtonVisual", sprites[0]);
        RectTransform loadButtonVisual =
            CreateOverlayImage(artworkRoot, "LoadButtonVisual", sprites[1]);
        RectTransform closeButtonVisual =
            CreateOverlayImage(artworkRoot, "CloseButtonVisual", sprites[2]);

        var slotButtons = new Button[SlotButtonCount];
        var slotFeedbacks = new MenuButtonSlideFeedback[SlotButtonCount];
        var slotTimestampLabels = new Text[SlotButtonCount];
        for (int i = 0; i < SlotButtonCount; i++)
        {
            float targetCenterY = 84f + (i * 32f);
            RectTransform slotVisual = CreateShiftedOverlayImage(
                artworkRoot,
                $"SlotButtonVisual_{i + 1:D2}",
                sprites[3],
                sourceCenter: new Vector2(192.5f, 37f),
                targetCenter: new Vector2(88f, targetCenterY));
            slotButtons[i] = CreateButtonHotspot(
                artworkRoot,
                $"SlotButton_{i + 1:D2}",
                left: 40.5f,
                top: targetCenterY - 12f,
                width: 95f,
                height: 24f);
            slotFeedbacks[i] = AttachSlideFeedback(slotButtons[i], slotVisual);
            slotTimestampLabels[i] = CreateTimestampLabel(slotButtons[i]);
        }

        Button saveButton = CreateButtonHotspot(
            artworkRoot,
            "SaveButton",
            left: 162f,
            top: 60f,
            width: 79f,
            height: 34f);
        Button loadButton = CreateButtonHotspot(
            artworkRoot,
            "LoadButton",
            left: 162f,
            top: 110f,
            width: 79f,
            height: 34f);
        Button closeButton = CreateButtonHotspot(
            artworkRoot,
            "CloseButton",
            left: 162f,
            top: 161f,
            width: 79f,
            height: 35f);

        AttachSlideFeedback(saveButton, saveButtonVisual);
        AttachSlideFeedback(loadButton, loadButtonVisual);
        AttachSlideFeedback(closeButton, closeButtonVisual);

        SaveLoadMenuView view = canvasObject.GetComponent<SaveLoadMenuView>();
        view.EditorAssignButtons(
            saveButton,
            loadButton,
            closeButton,
            slotButtons,
            slotFeedbacks,
            slotTimestampLabels);

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddScenesToBuildSettings();
        Selection.activeGameObject = canvasObject;
        AssetDatabase.SaveAssets();
        Debug.Log($"[SaveLoadMenuSetupEditor] Created {ScenePath}.");
    }

    private static GameObject CreateCanvas(Sprite backgroundSprite)
    {
        var canvasObject = new GameObject(
            "SaveLoadMenu_Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(SaveLoadMenuView));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreateImageObject("Background", canvasObject.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        SetStretch(backgroundRect);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = Color.white;
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = false;

        return canvasObject;
    }

    private static RectTransform CreateArtworkRoot(Transform parent)
    {
        var root = new GameObject("MenuArtwork", typeof(RectTransform));
        root.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(ArtworkSize, ArtworkSize);
        return rect;
    }

    private static RectTransform CreateOverlayImage(RectTransform parent, string name, Sprite sprite)
    {
        GameObject imageObject = CreateImageObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        SetStretch(rect);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform CreateShiftedOverlayImage(
        RectTransform parent,
        string name,
        Sprite sprite,
        Vector2 sourceCenter,
        Vector2 targetCenter)
    {
        GameObject imageObject = CreateImageObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        float scale = ArtworkSize / SourceSize;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ArtworkSize, ArtworkSize);
        rect.anchoredPosition = new Vector2(
            (targetCenter.x - sourceCenter.x) * scale,
            (sourceCenter.y - targetCenter.y) * scale);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private static Button CreateButtonHotspot(
        RectTransform parent,
        string name,
        float left,
        float top,
        float width,
        float height)
    {
        GameObject buttonObject = CreateImageObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();

        float scale = ArtworkSize / SourceSize;
        float centerX = left + (width * 0.5f);
        float centerYFromTop = top + (height * 0.5f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(
            (centerX - (SourceSize * 0.5f)) * scale,
            ((SourceSize * 0.5f) - centerYFromTop) * scale);
        rect.sizeDelta = new Vector2(width * scale, height * scale);

        Image target = buttonObject.GetComponent<Image>();
        target.color = new Color(1f, 1f, 1f, 0.001f);
        target.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static MenuButtonSlideFeedback AttachSlideFeedback(
        Button button,
        RectTransform visual)
    {
        MenuButtonSlideFeedback feedback =
            button.gameObject.AddComponent<MenuButtonSlideFeedback>();
        feedback.EditorAssignVisual(visual);
        return feedback;
    }

    private static Text CreateTimestampLabel(Button button)
    {
        var labelObject = new GameObject(
            "Timestamp",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline));
        labelObject.transform.SetParent(button.transform, worldPositionStays: false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        SetStretch(rect);
        rect.offsetMin = new Vector2(12f, 4f);
        rect.offsetMax = new Vector2(-12f, -4f);

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.92f, 0.84f, 0.68f, 1f);
        label.raycastTarget = false;
        label.supportRichText = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 18;
        label.text = string.Empty;

        Outline outline = labelObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.025f, 0.015f, 0.95f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        return label;
    }

    private static GameObject CreateImageObject(string name, Transform parent)
    {
        var imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, worldPositionStays: false);
        return imageObject;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CreateEventSystem()
    {
        var eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystemObject.GetComponent<EventSystem>().firstSelectedGameObject = null;
    }

    private static void CopyAndImportSprites()
    {
        string sourceFolder = ResolveSourceFolder();
        EnsureFolder("Assets/Art/Sprites/UI", "SaveLoadMenu");

        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly);
        var sourceByNumber = new Dictionary<int, string>();
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(sourceFiles[i]), @"_(\d{4})$");
            if (match.Success)
            {
                sourceByNumber[int.Parse(match.Groups[1].Value)] = sourceFiles[i];
            }
        }

        for (int i = 0; i < DestinationNames.Length; i++)
        {
            int sourceNumber = i + 2;
            if (!sourceByNumber.TryGetValue(sourceNumber, out string sourcePath))
            {
                throw new FileNotFoundException($"Missing source frame _{sourceNumber:D4}.png.");
            }

            string destinationPath = $"{DestinationFolder}/{DestinationNames[i]}.png";
            string fullDestinationPath = Path.GetFullPath(
                destinationPath.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(sourcePath, fullDestinationPath, overwrite: true);
        }

        string backgroundSourcePath = ResolveBackgroundSourcePath(sourceFolder);
        string backgroundDestinationPath = $"{DestinationFolder}/{BackgroundDestinationName}.png";
        string fullBackgroundDestinationPath = Path.GetFullPath(
            backgroundDestinationPath.Replace('/', Path.DirectorySeparatorChar));
        File.Copy(backgroundSourcePath, fullBackgroundDestinationPath, overwrite: true);

        AssetDatabase.Refresh();
        for (int i = 0; i < DestinationNames.Length; i++)
        {
            ConfigureImporter($"{DestinationFolder}/{DestinationNames[i]}.png");
        }

        ConfigureImporter(backgroundDestinationPath);
    }

    private static void ConfigureImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Texture importer not found for {assetPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadSprites()
    {
        var sprites = new Sprite[DestinationNames.Length];
        for (int i = 0; i < DestinationNames.Length; i++)
        {
            string path = $"{DestinationFolder}/{DestinationNames[i]}.png";
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null)
            {
                throw new FileNotFoundException($"Sprite could not be loaded: {path}");
            }
        }

        return sprites;
    }

    private static Sprite LoadBackgroundSprite()
    {
        string path = $"{DestinationFolder}/{BackgroundDestinationName}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException($"Background sprite could not be loaded: {path}");
        }

        return sprite;
    }

    private static string ResolveSourceFolder()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Меню загрузки"),
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Меню загрузки"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Меню загрузки",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (Directory.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        throw new DirectoryNotFoundException("Save/load menu source folder was not found.");
    }

    private static string ResolveBackgroundSourcePath(string sourceFolder)
    {
        string[] pngFiles = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < pngFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(pngFiles[i]).Trim();
            if (fileName.Equals("ЭкранСохранения", StringComparison.OrdinalIgnoreCase))
            {
                return pngFiles[i];
            }
        }

        throw new FileNotFoundException("ЭкранСохранения .png was not found.");
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        string path = parentFolder + "/" + childFolder;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }

    private static void AddScenesToBuildSettings()
    {
        var scenePaths = new List<string>
        {
            PrototypeScenePath,
            ScenePath,
            MainMenuScenePath,
        };

        var scenes = new List<EditorBuildSettingsScene>();
        for (int i = 0; i < scenePaths.Count; i++)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePaths[i], true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
