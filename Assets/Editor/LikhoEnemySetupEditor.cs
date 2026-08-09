using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Castlevania2D.Combat;
using Castlevania2D.Enemies;
using Castlevania2D.Health;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports Likho sleep/rise/kick art and places the enemy in Prototype.</summary>
public static class LikhoEnemySetupEditor
{
    private const string SleepDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Likho/Sleep";
    private const string RiseDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Likho/Rise";
    private const string KickDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Likho/Kick";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Likho.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const int MaxHealth = 8;
    private const float SleepFrameRate = 8f;
    private const float ActionFrameRate = 12f;
    private const float VisualScale = 1f;
    private static readonly Vector3 ScenePosition = new Vector3(8.6f, -65.7f, 0f);
    private static readonly Vector2 AggroArea = new Vector2(6f, 3.5f);

    [MenuItem("Tools/Castlevania 2D/Setup Likho Enemy")]
    public static void SetupLikhoEnemyMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Likho Enemy", SetupLikhoEnemy(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Likho Enemy", "Failed:\n" + exception.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod LikhoEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupLikhoEnemy());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupLikhoEnemy()
    {
        string sleepSource = ResolveSourceFolder("Сон");
        string riseSource = ResolveSourceFolder("Подъем");
        if (string.IsNullOrEmpty(riseSource))
        {
            riseSource = ResolveSourceFolder("Подъём");
        }

        string kickSource = ResolveSourceFolder("УдарНогой");
        if (string.IsNullOrEmpty(sleepSource)
            || string.IsNullOrEmpty(riseSource)
            || string.IsNullOrEmpty(kickSource))
        {
            throw new DirectoryNotFoundException(
                "Likho art not found. Expected Desktop/Персонаж/Лихо/Сон, Подъем, УдарНогой.");
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Likho");
        EnsureFolder(SleepDestinationFolder);
        EnsureFolder(RiseDestinationFolder);
        EnsureFolder(KickDestinationFolder);
        EnsureFolder("Assets/Prefabs/Enemies");

        Sprite[] sleepFrames = ImportFrames(sleepSource, SleepDestinationFolder, "Likho_Sleep_");
        Sprite[] riseFrames = ImportFrames(riseSource, RiseDestinationFolder, "Likho_Rise_");
        Sprite[] kickFrames = ImportFrames(kickSource, KickDestinationFolder, "Likho_Kick_");
        if (sleepFrames.Length == 0 || riseFrames.Length == 0 || kickFrames.Length == 0)
        {
            throw new InvalidOperationException("Likho import needs PNGs in Sleep, Rise, and Kick.");
        }

        GameObject prefab = BuildPrefab(sleepFrames, riseFrames, kickFrames);
        EnsurePlayerKnockbackReceiver();
        PlaceInPrototypeScene(prefab, sleepFrames);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Likho ready.\nSleep: {sleepFrames.Length}, rise: {riseFrames.Length}, kick: {kickFrames.Length}\n" +
               $"Prefab: {PrefabPath}\nPlaced: Enemy_Likho at {ScenePosition}\n" +
               "Cycle: sleep → rise → kick (launch) → rise reverse → sleep.";
    }

    private static string ResolveSourceFolder(string animationFolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Лихо", animationFolder),
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Лихо", animationFolder),
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

    private static Sprite[] ImportFrames(string sourceFolder, string destinationFolder, string filePrefix)
    {
        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png");
        Array.Sort(sourceFiles, CompareFrameNames);
        DeleteExistingFrames(destinationFolder, filePrefix);

        var frames = new List<Sprite>(sourceFiles.Length);
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destinationPath = $"{destinationFolder}/{filePrefix}{i + 1:D3}.png";
            File.Copy(sourceFiles[i], destinationPath, true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(destinationPath);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
            if (sprite != null)
            {
                frames.Add(sprite);
            }
        }

        return frames.ToArray();
    }

    private static int CompareFrameNames(string left, string right)
    {
        int leftNumber = GetFrameNumber(left);
        int rightNumber = GetFrameNumber(right);
        int numberComparison = leftNumber.CompareTo(rightNumber);
        return numberComparison != 0
            ? numberComparison
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFrameNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)(?!.*\d)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int number)
            ? number
            : int.MaxValue;
    }

    private static void DeleteExistingFrames(string destinationFolder, string filePrefix)
    {
        string absoluteFolder = Path.Combine(Application.dataPath, destinationFolder.Substring("Assets/".Length));
        if (!Directory.Exists(absoluteFolder))
        {
            return;
        }

        string[] existingFiles = Directory.GetFiles(absoluteFolder, filePrefix + "*.png");
        for (int i = 0; i < existingFiles.Length; i++)
        {
            AssetDatabase.DeleteAsset($"{destinationFolder}/{Path.GetFileName(existingFiles[i])}");
        }
    }

    private static void ConfigureImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0f);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static GameObject BuildPrefab(Sprite[] sleepFrames, Sprite[] riseFrames, Sprite[] kickFrames)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureLikhoComponents(prefabRoot, sleepFrames, riseFrames, kickFrames);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        var root = new GameObject("Enemy_Likho");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var spriteRenderer = root.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sleepFrames[0];
        spriteRenderer.sortingOrder = 3;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        Bounds bounds = sleepFrames[0].bounds;
        box.size = new Vector2(Mathf.Max(1.2f, bounds.size.x * 0.75f), Mathf.Max(1.6f, bounds.size.y * 0.9f));
        box.offset = new Vector2(0f, box.size.y * 0.5f);

        ConfigureLikhoComponents(root, sleepFrames, riseFrames, kickFrames);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureLikhoComponents(
        GameObject root,
        Sprite[] sleepFrames,
        Sprite[] riseFrames,
        Sprite[] kickFrames)
    {
        var health = root.GetComponent<Health>() ?? root.AddComponent<Health>();
        var healthSerialized = new SerializedObject(health);
        healthSerialized.FindProperty("maxHealth").intValue = MaxHealth;
        healthSerialized.FindProperty("destroyOnDeath").boolValue = true;
        healthSerialized.ApplyModifiedPropertiesWithoutUndo();

        var likho = root.GetComponent<LikhoEnemy2D>() ?? root.AddComponent<LikhoEnemy2D>();
        likho.EditorAssignFrames(sleepFrames, riseFrames, kickFrames);
        var likhoSerialized = new SerializedObject(likho);
        AssignSpriteArray(likhoSerialized, "sleepFrames", sleepFrames);
        AssignSpriteArray(likhoSerialized, "riseFrames", riseFrames);
        AssignSpriteArray(likhoSerialized, "kickFrames", kickFrames);
        likhoSerialized.FindProperty("sleepFrameRate").floatValue = SleepFrameRate;
        likhoSerialized.FindProperty("actionFrameRate").floatValue = ActionFrameRate;
        likhoSerialized.FindProperty("sleepPingPong").boolValue = true;
        likhoSerialized.FindProperty("attackAndFinalRiseScale").floatValue = 0.85f;
        likhoSerialized.FindProperty("finalRiseSmallFrameCount").intValue = 3;
        likhoSerialized.FindProperty("aggroArea").vector2Value = AggroArea;
        likhoSerialized.FindProperty("kickDamage").intValue = 12;
        likhoSerialized.FindProperty("attackCooldown").floatValue = 1.4f;
        likhoSerialized.FindProperty("hitActiveStartFrame").intValue = 6;
        likhoSerialized.FindProperty("hitActiveEndFrame").intValue = 10;
        likhoSerialized.FindProperty("kickLaunchSpeed").floatValue = 30f;
        likhoSerialized.FindProperty("kickLaunchUpward").floatValue = 6.5f;
        likhoSerialized.FindProperty("kickLaunchDuration").floatValue = 0.7f;
        likhoSerialized.ApplyModifiedPropertiesWithoutUndo();

        var renderer = root.GetComponent<SpriteRenderer>();
        if (renderer != null && sleepFrames.Length > 0)
        {
            renderer.sprite = sleepFrames[0];
        }
    }

    private static void AssignSpriteArray(SerializedObject serializedObject, string propertyName, Sprite[] sprites)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static void EnsurePlayerKnockbackReceiver()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player_HeroKnight");
        if (player == null)
        {
            return;
        }

        if (player.GetComponent<CombatKnockbackReceiver2D>() == null)
        {
            player.AddComponent<CombatKnockbackReceiver2D>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void PlaceInPrototypeScene(GameObject prefab, Sprite[] sleepFrames)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player_HeroKnight");
        GameObject existing = GameObject.Find("Enemy_Likho");
        if (existing != null)
        {
            existing.transform.position = ScenePosition;
            existing.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            var likho = existing.GetComponent<LikhoEnemy2D>();
            if (likho != null)
            {
                var serialized = new SerializedObject(likho);
                AssignSpriteArray(serialized, "sleepFrames", sleepFrames);
                if (player != null)
                {
                    serialized.FindProperty("target").objectReferenceValue = player.transform;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Likho";
        instance.transform.position = ScenePosition;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var likhoEnemy = instance.GetComponent<LikhoEnemy2D>();
        if (likhoEnemy != null && player != null)
        {
            var serialized = new SerializedObject(likhoEnemy);
            serialized.FindProperty("target").objectReferenceValue = player.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null && sleepFrames.Length > 0)
        {
            renderer.sprite = sleepFrames[0];
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
