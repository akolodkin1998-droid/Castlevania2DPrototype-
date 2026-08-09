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

/// <summary>Imports mouse animations and creates a ceiling-diving mouse in Prototype.</summary>
public static class MouseEnemySetupEditor
{
    private const string IdleDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Idle";
    private const string Attack1DestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Attack1";
    private const string Attack2DestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Attack2";
    private const string HurtDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Hurt";
    private const string KnockbackDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Knockback";
    private const string DeathDestinationFolder = "Assets/Art/Sprites/Characters/Enemies/Mouse/Death";
    private const string HurtSourceFileName = "МышьПолучениеУрона.png";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Mouse.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const int MaxHealth = 5;
    private const float FrameRate = 12f;
    private const float VisualScale = 0.78f;
    private const float AggroHorizontalSpan = 14f;
    private const float AggroVerticalSpan = 16f;
    private const float KnockbackDistance = 4f;
    private const float KnockbackSpeed = 7f;
    private const float KnockbackDuration = 0.6f;
    // Floor level here is about -37. The visible upper route at X=54 is near -30.
    private static readonly Vector3 ScenePosition = new Vector3(54f, -30f, 0f);

    [MenuItem("Tools/Castlevania 2D/Setup Mouse Enemy")]
    public static void SetupMouseEnemyMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Mouse Enemy", SetupMouseEnemy(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Mouse Enemy", "Failed:\n" + exception.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod MouseEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupMouseEnemy());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupMouseEnemy()
    {
        string idleSourceFolder = ResolveSourceFolder("Покой");
        string attack1SourceFolder = ResolveSourceFolder("МышьАтака");
        string attack2SourceFolder = ResolveSourceFolder("МышьАтакаПолёт");
        string hurtSourceFolder = ResolveSourceFolder("Получение урона");
        string knockbackSourceFolder = ResolveSourceFolder("Отлет от блока");
        string deathSourceFolder = ResolveSourceFolder("Финал");
        string hurtSourceFile = string.IsNullOrEmpty(hurtSourceFolder)
            ? null
            : Path.Combine(hurtSourceFolder, HurtSourceFileName);
        if (string.IsNullOrEmpty(idleSourceFolder)
            || string.IsNullOrEmpty(attack1SourceFolder)
            || string.IsNullOrEmpty(attack2SourceFolder)
            || string.IsNullOrEmpty(knockbackSourceFolder)
            || string.IsNullOrEmpty(deathSourceFolder)
            || !File.Exists(hurtSourceFile))
        {
            throw new DirectoryNotFoundException(
                "Mouse art not found. Expected Desktop/Персонаж/Мышь/Покой, МышьАтака, МышьАтакаПолёт, Отлет от блока, Финал, and Получение урона/МышьПолучениеУрона.png.");
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Mouse");
        EnsureFolder(IdleDestinationFolder);
        EnsureFolder(Attack1DestinationFolder);
        EnsureFolder(Attack2DestinationFolder);
        EnsureFolder(HurtDestinationFolder);
        EnsureFolder(KnockbackDestinationFolder);
        EnsureFolder(DeathDestinationFolder);
        EnsureFolder("Assets/Prefabs/Enemies");

        Sprite[] idleFrames = ImportFrames(idleSourceFolder, IdleDestinationFolder, "Mouse_Idle_");
        Sprite[] attack1Frames = ImportFrames(attack1SourceFolder, Attack1DestinationFolder, "Mouse_Attack1_");
        Sprite[] attack2Frames = ImportFrames(attack2SourceFolder, Attack2DestinationFolder, "Mouse_Attack2_");
        Sprite[] hurtFrames = ImportSingleFrame(hurtSourceFile, HurtDestinationFolder, "Mouse_Hurt_001.png");
        Sprite[] knockbackFrames = ImportFrames(knockbackSourceFolder, KnockbackDestinationFolder, "Mouse_Knockback_");
        Sprite[] deathFrames = ImportFrames(deathSourceFolder, DeathDestinationFolder, "Mouse_Death_");
        if (idleFrames.Length == 0 || attack1Frames.Length == 0 || attack2Frames.Length == 0 || hurtFrames.Length == 0 || knockbackFrames.Length == 0 || deathFrames.Length == 0)
        {
            throw new InvalidOperationException("Mouse animation import needs at least one PNG in Idle, Attack1, Attack2, Hurt, Knockback, and Death.");
        }

        GameObject prefab = BuildPrefab(idleFrames, attack1Frames, attack2Frames, hurtFrames, knockbackFrames, deathFrames);
        PlaceInPrototypeScene(prefab, idleFrames, knockbackFrames);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Mouse dive enemy ready.\nIdle: {idleFrames.Length}, windup: {attack1Frames.Length}, dive: {attack2Frames.Length}, hurt: {hurtFrames.Length}, knockback: {knockbackFrames.Length}, death: {deathFrames.Length} @ {FrameRate} FPS\n" +
               $"Art: {IdleDestinationFolder}, {Attack1DestinationFolder}, {Attack2DestinationFolder}, {HurtDestinationFolder}, {KnockbackDestinationFolder}, {DeathDestinationFolder}\n" +
               $"Prefab: {PrefabPath}\nPlaced: Enemy_Mouse at {ScenePosition}\nHealth: {MaxHealth}\n" +
               "Cycle: idle → windup once → dive → block knockback → dive.";
    }

    private static string ResolveSourceFolder(string animationFolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Мышь", animationFolder),
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Мышь", animationFolder),
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

    private static Sprite[] ImportSingleFrame(string sourceFile, string destinationFolder, string destinationFileName)
    {
        DeleteExistingFrames(destinationFolder, "Mouse_Hurt_");

        string destinationPath = $"{destinationFolder}/{destinationFileName}";
        File.Copy(sourceFile, destinationPath, true);
        AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(destinationPath);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
        return sprite != null ? new[] { sprite } : Array.Empty<Sprite>();
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

    private static GameObject BuildPrefab(
        Sprite[] idleFrames,
        Sprite[] attack1Frames,
        Sprite[] attack2Frames,
        Sprite[] hurtFrames,
        Sprite[] knockbackFrames,
        Sprite[] deathFrames)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var existingDiveAttack = prefabRoot.GetComponent<MouseDiveAttack2D>();
                var existingHurtAnimator = prefabRoot.GetComponent<MouseHurtAnimator2D>();
                var existingDeathAnimator = prefabRoot.GetComponent<MouseDeathAnimator2D>();
                if (existingDiveAttack == null || existingHurtAnimator == null)
                {
                    throw new InvalidOperationException("Existing Enemy_Mouse prefab is missing its animation components.");
                }

                if (existingDeathAnimator == null)
                {
                    existingDeathAnimator = prefabRoot.AddComponent<MouseDeathAnimator2D>();
                }

                var existingHealth = prefabRoot.GetComponent<Health>();
                if (existingHealth != null)
                {
                    var existingHealthSerialized = new SerializedObject(existingHealth);
                    existingHealthSerialized.FindProperty("destroyOnDeath").boolValue = false;
                    existingHealthSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var existingDiveSerialized = new SerializedObject(existingDiveAttack);
                AssignSpriteArray(existingDiveSerialized, "attackWindupFrames", attack1Frames);
                AssignSpriteArray(existingDiveSerialized, "diveFrames", attack2Frames);
                AssignSpriteArray(existingDiveSerialized, "knockbackFrames", knockbackFrames);
                existingDiveSerialized.FindProperty("frameRate").floatValue = FrameRate;
                existingDiveSerialized.FindProperty("knockbackFrameRate").floatValue = FrameRate;
                existingDiveSerialized.ApplyModifiedPropertiesWithoutUndo();

                var existingHurtSerialized = new SerializedObject(existingHurtAnimator);
                AssignSpriteArray(existingHurtSerialized, "hurtFrames", hurtFrames);
                existingHurtSerialized.FindProperty("frameRate").floatValue = FrameRate;
                existingHurtSerialized.ApplyModifiedPropertiesWithoutUndo();

                var existingDeathSerialized = new SerializedObject(existingDeathAnimator);
                AssignSpriteArray(existingDeathSerialized, "deathFrames", deathFrames);
                existingDeathSerialized.FindProperty("frameRate").floatValue = FrameRate;
                existingDeathSerialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        var createdRoot = new GameObject("Enemy_Mouse");
        createdRoot.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var createdSpriteRenderer = createdRoot.AddComponent<SpriteRenderer>();
        createdSpriteRenderer.sprite = idleFrames[0];
        createdSpriteRenderer.sortingOrder = 3;

        var createdBody = createdRoot.AddComponent<Rigidbody2D>();
        createdBody.bodyType = RigidbodyType2D.Kinematic;
        createdBody.gravityScale = 0f;
        createdBody.freezeRotation = true;

        var createdCollider = createdRoot.AddComponent<BoxCollider2D>();
        createdCollider.isTrigger = true;
        createdCollider.size = new Vector2(1.5f, 2.3f);
        createdCollider.offset = new Vector2(0f, 1.15f);

        var createdHealth = createdRoot.AddComponent<Health>();
        var createdHealthSerialized = new SerializedObject(createdHealth);
        createdHealthSerialized.FindProperty("maxHealth").intValue = MaxHealth;
        createdHealthSerialized.FindProperty("destroyOnDeath").boolValue = false;
        createdHealthSerialized.ApplyModifiedPropertiesWithoutUndo();

        var createdIdleAnimator = createdRoot.AddComponent<MouseIdleEnemy2D>();
        var createdIdleSerialized = new SerializedObject(createdIdleAnimator);
        SerializedProperty createdIdleFramesProperty = createdIdleSerialized.FindProperty("idleFrames");
        createdIdleFramesProperty.arraySize = idleFrames.Length;
        for (int i = 0; i < idleFrames.Length; i++)
        {
            createdIdleFramesProperty.GetArrayElementAtIndex(i).objectReferenceValue = idleFrames[i];
        }

        createdIdleSerialized.FindProperty("frameRate").floatValue = FrameRate;
        createdIdleSerialized.FindProperty("pingPong").boolValue = true;
        createdIdleSerialized.ApplyModifiedPropertiesWithoutUndo();

        var createdDiveAttack = createdRoot.AddComponent<MouseDiveAttack2D>();
        createdDiveAttack.EditorAssignSprites(attack1Frames, attack2Frames, knockbackFrames);
        var createdDiveSerialized = new SerializedObject(createdDiveAttack);
        AssignSpriteArray(createdDiveSerialized, "attackWindupFrames", attack1Frames);
        AssignSpriteArray(createdDiveSerialized, "diveFrames", attack2Frames);
        AssignSpriteArray(createdDiveSerialized, "knockbackFrames", knockbackFrames);
        createdDiveSerialized.FindProperty("frameRate").floatValue = FrameRate;
        createdDiveSerialized.FindProperty("knockbackFrameRate").floatValue = FrameRate;
        createdDiveSerialized.FindProperty("aggroArea").vector2Value = new Vector2(AggroHorizontalSpan, AggroVerticalSpan);
        createdDiveSerialized.FindProperty("diveSpeed").floatValue = 5.5f;
        createdDiveSerialized.FindProperty("contactRecoilDistance").floatValue = 1.25f;
        createdDiveSerialized.FindProperty("contactRecoilSpeed").floatValue = 6f;
        createdDiveSerialized.FindProperty("targetBodyAimHeight").floatValue = 0.662f;
        createdDiveSerialized.FindProperty("contactDamage").intValue = 10;
        createdDiveSerialized.FindProperty("damageCooldown").floatValue = 0.6f;
        createdDiveSerialized.FindProperty("knockbackDistance").floatValue = KnockbackDistance;
        createdDiveSerialized.FindProperty("knockbackSpeed").floatValue = KnockbackSpeed;
        createdDiveSerialized.FindProperty("knockbackDuration").floatValue = KnockbackDuration;
        createdDiveSerialized.FindProperty("blockedKnockbackUpward").floatValue = 0.5f;
        createdDiveSerialized.ApplyModifiedPropertiesWithoutUndo();

        var createdHurtAnimator = createdRoot.AddComponent<MouseHurtAnimator2D>();
        createdHurtAnimator.EditorAssignHurtFrames(hurtFrames);
        var createdHurtSerialized = new SerializedObject(createdHurtAnimator);
        AssignSpriteArray(createdHurtSerialized, "hurtFrames", hurtFrames);
        createdHurtSerialized.FindProperty("frameRate").floatValue = FrameRate;
        createdHurtSerialized.ApplyModifiedPropertiesWithoutUndo();

        var createdDeathAnimator = createdRoot.AddComponent<MouseDeathAnimator2D>();
        createdDeathAnimator.EditorAssignDeathFrames(deathFrames);
        var createdDeathSerialized = new SerializedObject(createdDeathAnimator);
        AssignSpriteArray(createdDeathSerialized, "deathFrames", deathFrames);
        createdDeathSerialized.FindProperty("frameRate").floatValue = FrameRate;
        createdDeathSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject createdPrefab = PrefabUtility.SaveAsPrefabAsset(createdRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(createdRoot);
        return createdPrefab;
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

    private static void PlaceInPrototypeScene(GameObject prefab, Sprite[] idleFrames, Sprite[] knockbackFrames)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find("Enemy_Mouse");
        if (existing != null)
        {
            var existingDiveAttack = existing.GetComponent<MouseDiveAttack2D>();
            if (existingDiveAttack != null)
            {
                var diveSerialized = new SerializedObject(existingDiveAttack);
                AssignSpriteArray(diveSerialized, "knockbackFrames", knockbackFrames);
                diveSerialized.FindProperty("knockbackFrameRate").floatValue = FrameRate;
                diveSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Mouse";
        instance.transform.position = ScenePosition;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        GameObject player = GameObject.Find("Player_HeroKnight");
        var diveAttack = instance.GetComponent<MouseDiveAttack2D>();
        if (diveAttack != null && player != null)
        {
            var diveSerialized = new SerializedObject(diveAttack);
            diveSerialized.FindProperty("target").objectReferenceValue = player.transform;
            diveSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null && idleFrames.Length > 0)
        {
            renderer.sprite = idleFrames[0];
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
