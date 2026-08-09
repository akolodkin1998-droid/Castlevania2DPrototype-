using System.IO;
using Castlevania2D.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Imports HP / Block HUD bar sprites from Desktop and wires HUD_Canvas in the open scene.
/// </summary>
public static class HudBarSetupEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Setup HUD Bars";

    private const string SourceFolderRelative = @"Desktop\Персонаж\Интерфейс";
    private const string EmptySourceName = "Emptybar.png";
    private const string HpSourceName = "HPbar.png";
    private const string BlockSourceName = "DAbar.png";

    private const string UiFolder = "Assets/Art/Sprites/UI";
    private const string EmptyDest = UiFolder + "/HudBar_Empty.png";
    private const string HpDest = UiFolder + "/HudBar_HP_Full.png";
    private const string BlockDest = UiFolder + "/HudBar_Shield_Full.png";

    private const float BarDisplayWidth = 160f;
    private const float TopLeftMarginX = 12f;
    private const float TopLeftMarginY = 12f;
    private const float BarGap = 6f;

    [MenuItem(MenuPath)]
    public static void SetupHudBars()
    {
        CopySources();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        ImportSprite(EmptyDest);
        ImportSprite(HpDest);
        ImportSprite(BlockDest);

        Sprite emptySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyDest);
        Sprite hpSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HpDest);
        Sprite blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlockDest);

        if (emptySprite == null || hpSprite == null || blockSprite == null)
        {
            Debug.LogError("HudBarSetupEditor: failed to load one or more bar sprites after import.");
            return;
        }

        Transform canvasTransform = FindHudCanvas();
        if (canvasTransform == null)
        {
            Debug.LogError("HudBarSetupEditor: HUD_Canvas not found in the open scene.");
            return;
        }

        ConfigureBar(canvasTransform, "HealthBar", emptySprite, hpSprite);
        ConfigureBar(canvasTransform, "BlockDurabilityBar", emptySprite, blockSprite);

        Canvas canvas = canvasTransform.GetComponent<Canvas>();
        Camera mainCamera = Camera.main;
        if (canvas != null && mainCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 1f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        HudGameViewportFit fit = canvasTransform.GetComponent<HudGameViewportFit>();
        if (fit != null)
        {
            SerializedObject so = new SerializedObject(fit);
            if (mainCamera != null)
            {
                so.FindProperty("targetCamera").objectReferenceValue = mainCamera;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            fit.ApplyLayout();
        }

        EditorUtility.SetDirty(canvasTransform.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log(
            $"HUD bars updated: Empty={EmptyDest}, HP={HpDest}, Block={BlockDest}, width={BarDisplayWidth}px.");
    }

    private static void CopySources()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Art/Sprites/UI"));
        CopyIfExists(Path.Combine(SourceFolderRelative, EmptySourceName), EmptyDest);
        CopyIfExists(Path.Combine(SourceFolderRelative, HpSourceName), HpDest);
        CopyIfExists(Path.Combine(SourceFolderRelative, BlockSourceName), BlockDest);
    }

    private static void CopyIfExists(string sourceRelative, string destAssetPath)
    {
        string sourcePath = ResolveDesktopPath(sourceRelative);
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"HudBarSetupEditor: source not found: {sourcePath}");
            return;
        }

        string destFs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", destAssetPath));
        File.Copy(sourcePath, destFs, overwrite: true);
    }

    private static string ResolveDesktopPath(string relativePath)
    {
        string oneDriveDesktop = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "OneDrive",
            relativePath.Replace('\\', Path.DirectorySeparatorChar));
        if (File.Exists(oneDriveDesktop))
        {
            return oneDriveDesktop;
        }

        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
            relativePath.Replace('\\', Path.DirectorySeparatorChar));
    }

    private static void ImportSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static Transform FindHudCanvas()
    {
        GameObject canvas = GameObject.Find("HUD_Canvas");
        return canvas != null ? canvas.transform : null;
    }

    private static void ConfigureBar(Transform canvas, string barName, Sprite emptySprite, Sprite fillSprite)
    {
        Transform barTransform = canvas.Find(barName);
        if (barTransform == null)
        {
            Debug.LogWarning($"HudBarSetupEditor: bar '{barName}' not found under HUD_Canvas.");
            return;
        }

        Image background = barTransform.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = emptySprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.preserveAspect = false;
            background.raycastTarget = false;
        }

        Transform fillTransform = barTransform.Find("Fill");
        if (fillTransform == null)
        {
            Debug.LogWarning($"HudBarSetupEditor: Fill child missing on '{barName}'.");
            return;
        }

        Image fill = fillTransform.GetComponent<Image>();
        if (fill == null)
        {
            return;
        }

        fill.sprite = fillSprite;
        fill.color = Color.white;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.preserveAspect = false;
        fill.raycastTarget = false;

        RectTransform fillRect = fillTransform.GetComponent<RectTransform>();
        if (fillRect != null)
        {
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }
    }
}
