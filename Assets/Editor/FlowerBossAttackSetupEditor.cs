using System;
using System.Collections.Generic;
using System.IO;
using Castlevania2D.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlowerBossAttackSetup
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string BossObjectName = "Boss_Flower";
    private const string Attack1Folder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Attack1";
    private const string Attack2Folder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Attack2";
    private const string DeathFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Death";
    private const string Attack1Prefix = "FlowerBoss_Attack1_";
    private const string Attack2Prefix = "FlowerBoss_Attack2_";
    private const string DeathPrefix = "FlowerBoss_Death_";
    private const string Attack1SourceFolder = @"C:\Users\Bensh\OneDrive\Рабочий стол\Босс\Аттака1";
    private const string Attack2SourceFolder = @"C:\Users\Bensh\OneDrive\Рабочий стол\Босс\Атака 2";
    private const string DeathSourceFolderPrimary = @"C:\Users\Bensh\OneDrive\Рабочий стол\Босс\Реинкорнация";
    private const string DeathSourceFolderAlternate = @"C:\Users\Bensh\OneDrive\Рабочий стол\Босс\Реинкарнация";
    private const float AttackFrameRate = 12f;
    private const string ProjectileSpritePath = "Assets/Evil Wizard 3/Sprites/Projectile/Moving.png";
    private const int ProjectileDamage = 10;
    private const float ProjectileSpeed = 6f;

    [MenuItem("Tools/Castlevania 2D/Setup Flower Boss Attack Animation")]
    public static void SetupMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Setup;
            return;
        }

        Setup();
    }

    [MenuItem("Tools/Castlevania 2D/Setup Flower Boss Death Animation")]
    public static void SetupDeathMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += SetupDeathOnly;
            return;
        }

        SetupDeathOnly();
    }

    public static void SetupBatch()
    {
        Setup();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Attack1");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Attack2");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Death");

        ImportAttackSprites(Attack1SourceFolder, Attack1Folder, Attack1Prefix);
        ImportAttackSprites(Attack2SourceFolder, Attack2Folder, Attack2Prefix);
        ImportDeathSprites();

        Sprite idleReference = LoadIdleReferenceSprite();
        ConfigureSpriteImports(Attack1Folder, Attack1Prefix, idleReference);
        ConfigureSpriteImports(Attack2Folder, Attack2Prefix, idleReference);
        ConfigureSpriteImports(DeathFolder, DeathPrefix, idleReference);
        AssetDatabase.Refresh();

        // Second pass after reimport: fine-tune pivots so mesh bottom-center matches Idle.
        AlignAttackPivotsToIdle(Attack1Folder, Attack1Prefix, idleReference);
        AlignAttackPivotsToIdle(Attack2Folder, Attack2Prefix, idleReference);
        AlignAttackPivotsToIdle(DeathFolder, DeathPrefix, idleReference);
        AssetDatabase.Refresh();

        Sprite[] attack1 = LoadSprites(Attack1Folder, Attack1Prefix);
        Sprite[] attack2 = LoadSprites(Attack2Folder, Attack2Prefix);
        Sprite[] death = LoadSprites(DeathFolder, DeathPrefix);
        if (attack1.Length == 0 && attack2.Length == 0)
        {
            throw new InvalidOperationException(
                "No Flower Boss attack sprites found in Attack1/Attack2 folders.");
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject boss = GameObject.Find(BossObjectName);
        if (boss == null)
        {
            throw new InvalidOperationException($"Boss object '{BossObjectName}' was not found.");
        }

        ConfigureBossAttackAnimator(boss, attack1, attack2, death);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Flower Boss attack animation ready: Attack1={attack1.Length} frames, " +
            $"Attack2={attack2.Length} frames, Death={death.Length} frames @ {AttackFrameRate} FPS " +
            "(pivots aligned to Idle stem/base; first hit → Attack1 once → Attack2 ping-pong; " +
            "Died → Death once → disable Boss_Flower; Attack2 _050 → yellow projectiles radial).");
    }

    public static void SetupDeathOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Death");
        ImportDeathSprites();

        Sprite idleReference = LoadIdleReferenceSprite();
        ConfigureSpriteImports(DeathFolder, DeathPrefix, idleReference);
        AssetDatabase.Refresh();
        AlignAttackPivotsToIdle(DeathFolder, DeathPrefix, idleReference);
        AssetDatabase.Refresh();

        Sprite[] death = LoadSprites(DeathFolder, DeathPrefix);
        if (death.Length == 0)
        {
            throw new InvalidOperationException(
                "No Flower Boss death sprites found in Death folder (source: Desktop/Босс/Реинкорнация).");
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject boss = GameObject.Find(BossObjectName);
        if (boss == null)
        {
            throw new InvalidOperationException($"Boss object '{BossObjectName}' was not found.");
        }

        FlowerBossAttackAnimator2D attackAnimator = GetOrAddComponent<FlowerBossAttackAnimator2D>(boss);
        attackAnimator.enabled = true;
        SerializedObject serialized = new SerializedObject(attackAnimator);
        AssignSpriteArray(serialized, "deathFrames", death);
        serialized.FindProperty("frameRate").floatValue = AttackFrameRate;
        serialized.FindProperty("spriteRenderer").objectReferenceValue = boss.GetComponent<SpriteRenderer>();
        serialized.FindProperty("idleAnimator").objectReferenceValue = boss.GetComponent<Animator>();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Keep Health alive so Died → death frames can play under our control.
        Castlevania2D.Health.Health health = boss.GetComponent<Castlevania2D.Health.Health>();
        if (health != null)
        {
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(attackAnimator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        int validDeath = CountNonNull(death);
        Debug.Log(
            $"Flower Boss death animation ready: Death={validDeath}/{death.Length} frames @ {AttackFrameRate} FPS " +
            "(Health.Died → Death once forward → SetActive(false) on Boss_Flower; destroyOnDeath stays false).");
    }

    private static int CountNonNull(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static void ImportDeathSprites()
    {
        string sourceFolder = ResolveDeathSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            Debug.LogWarning(
                "Death source folder not found (tried Реинкорнация / Реинкарнация under Desktop/Босс).");
            return;
        }

        ImportAttackSprites(sourceFolder, DeathFolder, DeathPrefix);
    }

    private static string ResolveDeathSourceFolder()
    {
        if (Directory.Exists(DeathSourceFolderPrimary))
        {
            return DeathSourceFolderPrimary;
        }

        if (Directory.Exists(DeathSourceFolderAlternate))
        {
            return DeathSourceFolderAlternate;
        }

        return null;
    }

    private static void ConfigureBossAttackAnimator(
        GameObject boss,
        Sprite[] attack1,
        Sprite[] attack2,
        Sprite[] death)
    {
        FlowerBossAttackAnimator2D attackAnimator = GetOrAddComponent<FlowerBossAttackAnimator2D>(boss);
        attackAnimator.enabled = true;
        SerializedObject serialized = new SerializedObject(attackAnimator);
        AssignSpriteArray(serialized, "attack1Frames", attack1);
        AssignSpriteArray(serialized, "attack2Frames", attack2);
        AssignSpriteArray(serialized, "deathFrames", death);
        serialized.FindProperty("frameRate").floatValue = AttackFrameRate;
        serialized.FindProperty("spriteRenderer").objectReferenceValue = boss.GetComponent<SpriteRenderer>();
        serialized.FindProperty("idleAnimator").objectReferenceValue = boss.GetComponent<Animator>();

        Castlevania2D.Health.Health health = boss.GetComponent<Castlevania2D.Health.Health>();
        if (health != null)
        {
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        Sprite projectileSprite = LoadProjectileSprite();
        serialized.FindProperty("projectileSprite").objectReferenceValue = projectileSprite;
        serialized.FindProperty("projectileColor").colorValue = new Color(1f, 0.92f, 0.2f, 1f);
        serialized.FindProperty("projectileDamage").intValue = ProjectileDamage;
        serialized.FindProperty("projectileSpeed").floatValue = ProjectileSpeed;
        serialized.FindProperty("projectileSpawnOffset").vector2Value = new Vector2(0f, 1.2f);
        serialized.FindProperty("projectileCountBase").intValue = 15;
        serialized.FindProperty("hpLostPerExtraProjectile").intValue = 25;
        serialized.FindProperty("projectileScale").floatValue = 1.4f;
        serialized.FindProperty("projectileSortingOrder").intValue = 4;
        serialized.FindProperty("projectileColliderRadius").floatValue = 0.18f;
        serialized.FindProperty("attack2ProjectileFrameNumber").intValue = 50;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite LoadProjectileSprite()
    {
        // Evil Wizard Moving.png is a multi-sprite sheet; match scene fileID used by Enemy_EvilWizard.
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectileSpritePath);
        Sprite firstSprite = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                if (sprite.name.IndexOf("Moving", StringComparison.OrdinalIgnoreCase) >= 0 || firstSprite == null)
                {
                    firstSprite = sprite;
                }

                // Prefer the same sub-sprite Evil Wizard AI uses in Prototype.unity (internal id -943592288).
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long localId) &&
                    localId == -943592288L)
                {
                    return sprite;
                }
            }
        }

        if (firstSprite != null)
        {
            return firstSprite;
        }

        Debug.LogWarning($"Flower Boss projectile sprite not found at {ProjectileSpritePath}");
        return null;
    }

    private static void ImportAttackSprites(string sourceFolder, string destinationFolder, string prefix)
    {
        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogWarning($"Attack source folder not found: {sourceFolder}");
            return;
        }

        FileInfo[] sourceFiles = new DirectoryInfo(sourceFolder)
            .GetFiles("*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(sourceFiles, (left, right) =>
            ExtractSourceFrameNumber(left.Name).CompareTo(ExtractSourceFrameNumber(right.Name)));

        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destinationName = $"{prefix}{i + 1:D3}.png";
            string destinationPath = Path.Combine(
                destinationFolder.Replace('/', Path.DirectorySeparatorChar),
                destinationName);
            File.Copy(sourceFiles[i].FullName, destinationPath, overwrite: true);
        }
    }

    private const string IdleSpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Idle";
    private const string IdlePrefix = "FlowerBoss_Idle_";

    private static Sprite LoadIdleReferenceSprite()
    {
        Sprite[] idleSprites = LoadSprites(IdleSpritesFolder, IdlePrefix);
        if (idleSprites.Length == 0)
        {
            Debug.LogWarning("Flower Boss Idle sprites not found; Attack pivots will use Center (0.5, 0.5).");
            return null;
        }

        return idleSprites[0];
    }

    private static void ConfigureSpriteImports(string folder, string prefix, Sprite idleReference)
    {
        // Match Idle: Center pivot. Bottom-center on differently sized Attack canvases caused the stance jump.
        Vector2 importPivot = new Vector2(0.5f, 0.5f);
        if (idleReference != null && idleReference.rect.height > 0.01f)
        {
            // Prefer Idle's normalized pivot when custom; Center Idle still reports (0.5, 0.5).
            importPivot = new Vector2(
                idleReference.pivot.x / idleReference.rect.width,
                idleReference.pivot.y / idleReference.rect.height);
        }

        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = true;
            settings.spritePixelsPerUnit = 100f;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = importPivot;
            importer.SetTextureSettings(settings);
            importer.spritePivot = importPivot;
            importer.SaveAndReimport();
        }
    }

    private static void AlignAttackPivotsToIdle(string folder, string prefix, Sprite idleReference)
    {
        if (idleReference == null)
        {
            return;
        }

        Vector2 idleRoot = GetSpriteVisualRoot(idleReference);
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Sprite attackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (attackSprite == null || importer == null)
            {
                continue;
            }

            Vector2 currentRoot = GetSpriteVisualRoot(attackSprite);
            Vector2 localDelta = idleRoot - currentRoot;
            if (localDelta.sqrMagnitude < 0.0000001f)
            {
                continue;
            }

            Vector2 pivotPixels = attackSprite.pivot - (localDelta * attackSprite.pixelsPerUnit);
            Vector2 pivotNormalized = new Vector2(
                pivotPixels.x / attackSprite.rect.width,
                pivotPixels.y / attackSprite.rect.height);

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivotNormalized;
            importer.SetTextureSettings(settings);
            importer.spritePivot = pivotNormalized;
            importer.SaveAndReimport();
        }
    }

    private static Vector2 GetSpriteVisualRoot(Sprite sprite)
    {
        // Match runtime lock: pivot axis (local x≈0) + bottom of mesh bounds.
        Bounds bounds = sprite.bounds;
        return new Vector2(0f, bounds.min.y);
    }

    private static Sprite[] LoadSprites(string folder, string prefix)
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        List<(int index, Sprite sprite)> sprites = new List<(int, Sprite)>();

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                continue;
            }

            sprites.Add((ExtractFrameNumber(fileName), sprite));
        }

        sprites.Sort((left, right) => left.index.CompareTo(right.index));
        Sprite[] result = new Sprite[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            result[i] = sprites[i].sprite;
        }

        return result;
    }

    private static int ExtractSourceFrameNumber(string fileName)
    {
        int digitCount = 0;
        while (digitCount < fileName.Length && char.IsDigit(fileName[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0)
        {
            return 0;
        }

        return int.TryParse(fileName.Substring(0, digitCount), out int index) ? index : 0;
    }

    private static int ExtractFrameNumber(string fileName)
    {
        int separatorIndex = fileName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex == fileName.Length - 1)
        {
            return 0;
        }

        string indexText = fileName.Substring(separatorIndex + 1);
        return int.TryParse(indexText, out int index) ? index : 0;
    }

    private static void AssignSpriteArray(SerializedObject serializedObject, string propertyName, Sprite[] sprites)
    {
        SerializedProperty arrayProperty = serializedObject.FindProperty(propertyName);
        arrayProperty.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
