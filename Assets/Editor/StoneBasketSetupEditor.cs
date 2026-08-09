using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Castlevania2D.Enemies;
using Castlevania2D.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports stone-basket sprites (Корзина_0001…0015 + Падение fall frames), wires hit-stage
/// and fall anims on both baskets, and hooks EagleBasketAssault2D on Enemy_Eagle.
/// Menu: Tools → Castlevania 2D → Setup Stone Baskets
/// </summary>
public static class StoneBasketSetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Environment/StoneBasket";
    private const string FallDestFolder = DestFolder + "/Fall";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string Basket01Name = "StoneBasket_01";
    private const string Basket02Name = "StoneBasket_02";
    private const string EagleName = "Enemy_Eagle";
    private const float PixelsPerUnit = 100f;
    private const float PropScale = 0.85f;
    private const int SortingOrder = 2;
    private const float FrameRate = 12f;
    private const int ExpectedFallFrameCount = 9;

    // Defaults only used when objects are missing; existing scene positions are preserved.
    private static readonly Vector3 Basket01Fallback = new Vector3(76.18f, -33.94f, 9f);
    private static readonly Vector3 Basket02Fallback = new Vector3(55.9f, -34.13f, 9f);

    [MenuItem("Tools/Castlevania 2D/Setup Stone Baskets")]
    public static void SetupMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Stone Baskets", SetupStoneBaskets(), "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stone Baskets", "Failed:\n" + ex.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        Debug.Log("[StoneBasketSetupEditor] " + SetupStoneBaskets());
    }

    public static string SetupStoneBaskets()
    {
        EnsureFolder("Assets/Art/Sprites/Environment");
        EnsureFolder(DestFolder);
        EnsureFolder(FallDestFolder);
        CopyAllFromSource();
        int fallCopied = CopyFallFromSource();

        Sprite[] frames = LoadNumberedSprites(15);
        if (frames[0] == null)
        {
            throw new FileNotFoundException(
                "Missing StoneBasket_01.png. Place Корзина_0001.png under Desktop/Персонаж/Корзина с камнями.");
        }

        Sprite[] fallFrames = LoadFallSprites();
        if (fallFrames.Length == 0)
        {
            Debug.LogWarning(
                "[StoneBasketSetupEditor] No fall frames under " + FallDestFolder +
                ". Place PNGs in Desktop/.../Корзина с камнями/Падение and re-run setup.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlaceOrUpdateBasket(Basket01Name, Basket01Fallback, frames, fallFrames);
        PlaceOrUpdateBasket(Basket02Name, Basket02Fallback, frames, fallFrames);
        WireEagleAssault();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        return
            $"Sprites: {DestFolder} (01–15)\n" +
            $"Fall: {FallDestFolder} ({fallFrames.Length} frames, copied {fallCopied}) @ 12 FPS once on BeginFallOnto\n" +
            "Hit stages: 1=02–06, 2=07, 3=08, 4=09–11, 5=12–15 @ 12 FPS\n" +
            "Basket mode: player X>=54 → eagle throws at baskets on Ent/Mushomor kill\n" +
            $"{Basket01Name} preferred for Ent, {Basket02Name} for Mushomor; " +
            "if preferred is full, redirect to the other non-full basket (no throw if both full)\n" +
            "Existing basket world positions preserved when objects already exist.";
    }

    private static void CopyAllFromSource()
    {
        string sourceDir = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceDir))
        {
            return;
        }

        string[] pngs = Directory.GetFiles(sourceDir, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < pngs.Length; i++)
        {
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(pngs[i]), @"(\d+)");
            if (!m.Success)
            {
                continue;
            }

            int n = int.Parse(m.Groups[1].Value);
            string destRel = $"{DestFolder}/StoneBasket_{n:D2}.png";
            string destFull = Path.GetFullPath(destRel.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(pngs[i], destFull, overwrite: true);
        }

        AssetDatabase.Refresh();
        for (int n = 1; n <= 15; n++)
        {
            string destRel = $"{DestFolder}/StoneBasket_{n:D2}.png";
            if (File.Exists(destRel.Replace('/', Path.DirectorySeparatorChar)))
            {
                ConfigureImporter(destRel);
            }
        }
    }

    /// <summary>
    /// Copies Desktop/.../Падение frames into Fall/ as StoneBasket_Fall_01…N (sorted by source index).
    /// Source may skip numbers (e.g. missing 0003) or use names like Корзина_0001 (1).png.
    /// </summary>
    private static int CopyFallFromSource()
    {
        string fallSource = ResolveFallSourceFolder();
        if (string.IsNullOrEmpty(fallSource))
        {
            return ConfigureExistingFallImporters();
        }

        var ordered = new List<(int num, string path)>();
        string[] pngs = Directory.GetFiles(fallSource, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < pngs.Length; i++)
        {
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(pngs[i]), @"(\d+)");
            if (!m.Success)
            {
                continue;
            }

            ordered.Add((int.Parse(m.Groups[1].Value), pngs[i]));
        }

        ordered.Sort((a, b) => a.num.CompareTo(b.num));
        for (int i = 0; i < ordered.Count; i++)
        {
            string destRel = $"{FallDestFolder}/StoneBasket_Fall_{i + 1:D2}.png";
            string destFull = Path.GetFullPath(destRel.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(ordered[i].path, destFull, overwrite: true);
        }

        AssetDatabase.Refresh();
        for (int i = 1; i <= ordered.Count; i++)
        {
            string destRel = $"{FallDestFolder}/StoneBasket_Fall_{i:D2}.png";
            if (File.Exists(destRel.Replace('/', Path.DirectorySeparatorChar)))
            {
                ConfigureImporter(destRel);
            }
        }

        return ordered.Count;
    }

    private static int ConfigureExistingFallImporters()
    {
        int count = 0;
        for (int i = 1; i <= ExpectedFallFrameCount + 8; i++)
        {
            string destRel = $"{FallDestFolder}/StoneBasket_Fall_{i:D2}.png";
            if (!File.Exists(destRel.Replace('/', Path.DirectorySeparatorChar)))
            {
                break;
            }

            ConfigureImporter(destRel);
            count++;
        }

        return count;
    }

    private static Sprite[] LoadFallSprites()
    {
        var list = new List<Sprite>(ExpectedFallFrameCount);
        for (int i = 1; i <= ExpectedFallFrameCount + 8; i++)
        {
            string path = $"{FallDestFolder}/StoneBasket_Fall_{i:D2}.png";
            if (!File.Exists(path.Replace('/', Path.DirectorySeparatorChar)))
            {
                break;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                list.Add(sprite);
            }
        }

        return list.ToArray();
    }

    private static string ResolveFallSourceFolder()
    {
        string parent = ResolveSourceFolder();
        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }

        string fall = Path.Combine(parent, "Падение");
        return Directory.Exists(fall) ? fall : null;
    }

    private static Sprite[] LoadNumberedSprites(int count)
    {
        var sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string path = $"{DestFolder}/StoneBasket_{i + 1:D2}.png";
            if (!File.Exists(path.Replace('/', Path.DirectorySeparatorChar)))
            {
                continue;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path);
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return sprites;
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Корзина с камнями"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Корзина с камнями"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Корзина с камнями",
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

    private static void ConfigureImporter(string destPath)
    {
        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0f);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void PlaceOrUpdateBasket(
        string objectName,
        Vector3 fallbackPosition,
        Sprite[] frames,
        Sprite[] fallFrames)
    {
        GameObject go = GameObject.Find(objectName);
        bool created = go == null;
        if (created)
        {
            go = new GameObject(objectName);
            go.transform.position = fallbackPosition;
        }

        go.transform.localScale = new Vector3(PropScale, PropScale, 1f);

        var renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = frames[0];
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;

        var box = go.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = go.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = false;
        if (frames[0] != null)
        {
            Bounds bounds = frames[0].bounds;
            box.size = bounds.size;
            box.offset = bounds.center;
        }

        var stages = go.GetComponent<StoneBasketHitStages2D>();
        if (stages == null)
        {
            stages = go.AddComponent<StoneBasketHitStages2D>();
        }

        stages.EditorAssignStages(
            frames[0],
            Slice(frames, 1, 5),   // 02–06
            Slice(frames, 6, 1),   // 07
            Slice(frames, 7, 1),   // 08
            Slice(frames, 8, 3),   // 09–11
            Slice(frames, 11, 4),  // 12–15
            fallFrames);

        var so = new SerializedObject(stages);
        so.FindProperty("frameRate").floatValue = FrameRate;
        so.FindProperty("maxHits").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(stages);
    }

    private static Sprite[] Slice(Sprite[] all, int startIndex, int count)
    {
        var list = new List<Sprite>(count);
        for (int i = 0; i < count; i++)
        {
            int idx = startIndex + i;
            if (idx >= 0 && idx < all.Length && all[idx] != null)
            {
                list.Add(all[idx]);
            }
        }

        return list.ToArray();
    }

    private static void WireEagleAssault()
    {
        GameObject eagleGo = GameObject.Find(EagleName);
        if (eagleGo == null)
        {
            Debug.LogWarning("[StoneBasketSetupEditor] Enemy_Eagle not found — skip assault wire.");
            return;
        }

        var eagle = eagleGo.GetComponent<EagleFlyerEnemy2D>();
        if (eagle == null)
        {
            return;
        }

        var assault = eagleGo.GetComponent<EagleBasketAssault2D>();
        if (assault == null)
        {
            assault = eagleGo.AddComponent<EagleBasketAssault2D>();
        }

        eagle.ConfigureBasketMode(54f, enabled: true);

        var so = new SerializedObject(assault);
        so.FindProperty("basketModeMinPlayerX").floatValue = 54f;
        Transform b1 = GameObject.Find(Basket01Name)?.transform;
        Transform b2 = GameObject.Find(Basket02Name)?.transform;
        so.FindProperty("stoneBasket01").objectReferenceValue = b1;
        so.FindProperty("stoneBasket02").objectReferenceValue = b2;
        so.ApplyModifiedPropertiesWithoutUndo();

        var eagleSo = new SerializedObject(eagle);
        var basketEnabled = eagleSo.FindProperty("basketModeEnabled");
        var basketMinX = eagleSo.FindProperty("basketModeMinPlayerX");
        var basketTrigger = eagleSo.FindProperty("basketAttackTriggerDistanceX");
        if (basketEnabled != null)
        {
            basketEnabled.boolValue = true;
        }

        if (basketMinX != null)
        {
            basketMinX.floatValue = 54f;
        }

        if (basketTrigger != null)
        {
            basketTrigger.floatValue = 11f;
        }

        eagleSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(eagleGo);
        PrefabUtility.RecordPrefabInstancePropertyModifications(eagleGo);
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
}
