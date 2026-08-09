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

/// <summary>
/// Imports Ent (Древень) walk/attack/projectile/death sprites and places Enemy_Ent in Prototype.
/// Attack: Desktop/Персонаж/Древень/Аттака — fire on source frame 32, then loop 19→32.
/// Death: Desktop/Персонаж/Древень/Новая папка — once forward @ 12 FPS, then Destroy.
/// </summary>
public static class EntEnemySetupEditor
{
    private const string WalkDestFolder = "Assets/Art/Sprites/Characters/Enemies/Ent/Walk";
    private const string AttackDestFolder = "Assets/Art/Sprites/Characters/Enemies/Ent/Attack";
    private const string ProjectileDestFolder = "Assets/Art/Sprites/Characters/Enemies/Ent/Projectile";
    private const string HurtDestFolder = "Assets/Art/Sprites/Characters/Enemies/Ent/Hurt";
    private const string DeathDestFolder = "Assets/Art/Sprites/Characters/Enemies/Ent/Death";
    private const string ProjectileAssetPath = ProjectileDestFolder + "/Ent_Projectile.png";
    private const string HurtAssetPath = HurtDestFolder + "/Ent_Hurt.png";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Ent.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const int EntMaxHealth = 30;
    private const int ContactDamage = 10;
    private const float FrameRate = 12f;
    private const float AttackWindupFrameRate = 20f;
    private const float MoveSpeed = 1.6f;
    private const float ChaseRange = 24f;
    private const float ChaseVerticalRange = 5f;
    private const float AttackRange = 5.5f;
    private const float AttackVerticalRange = 3f;
    private const int ProjectileFireFrameNumber = 32;
    private const int AttackLoopStartFrameNumber = 19;
    private const int ProjectileDamage = 10;
    private const float ProjectileSpeed = 6f;
    private const float ProjectileScale = 0.35f / 3f;
    private const float ProjectileSpinDegreesPerSecond = 360f;
        private const float TargetAimHeight = 0.9f;
    private const float DamageFlashDuration = 0.12f;
    /// <summary>Unused at runtime (no height-match scale). Kept for serialized field compatibility.</summary>
    private const float HurtFlashScaleMultiplier = 1f;
    private const float HurtFlashYOffset = 0f;
    /// <summary>720×1280 @ PPU 100 → ~1.15×2.05 world units at 0.16.</summary>
    private const float VisualScale = 0.16f;
    /// <summary>Walk/attack canvases leave ~100px empty below opaque feet (PPU 100).</summary>
    private const float PhysicsFeetPaddingLocal = 1f;
    /// <summary>Keep 0 — padding already matches opaque feet; extra sink puts sprite under tiles.</summary>
    private const float FeetGroundSinkLocal = 0f;
    /// <summary>Feet capsule width vs sprite bounds — narrower avoids tile edge snagging.</summary>
    private const float PhysicsFeetWidthScale = 0.85f;
    private const float PhysicsMinFeetWidthWorld = 1.25f;
    private const float GroundProbeDistanceWorld = 0.22f;
    private const float WalkHeightLockProbeDistanceWorld = 0.55f;
    private const float SnapToGroundProbeDistanceWorld = 4f;
    private const bool LockWalkHeightToScenePlacement = false;
    private const float EntGravityScale = 2.5f;

    private static readonly Vector2 ProjectileSpawnOffset = new Vector2(0.55f, 1.1f);
    private static readonly Vector3 ScenePosition = new Vector3(-2f, -37.2f, 0f);

    [MenuItem("Tools/Castlevania 2D/Setup Ent Enemy")]
    public static void SetupEntEnemyMenu()
    {
        try
        {
            string summary = SetupEntEnemy();
            EditorUtility.DisplayDialog("Ent Enemy", summary, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Ent Enemy", "Failed:\n" + ex.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod EntEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            string summary = SetupEntEnemy();
            Debug.Log(summary);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupEntEnemy()
    {
        EnsureFolders();

        Sprite[] walkFrames = ImportWalkSprites(ResolveSourceFolder("Ходьба"));
        Sprite[] attackFrames = ImportAttackSprites(ResolveSourceFolder("Аттака"));
        Sprite projectileSprite = ImportProjectileSprite(ResolveProjectileSourcePath());
        Sprite hurtSprite = ImportHurtSprite(ResolveHurtSourcePath());
        Sprite[] deathSprites = ImportDeathSprites(ResolveSourceFolder("Новая папка"));

        if (walkFrames == null || walkFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Ent walk frames found. Expected PNGs in Desktop/Персонаж/Древень/Ходьба " +
                "or already under " + WalkDestFolder);
        }

        if (attackFrames == null || attackFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Ent attack frames found. Expected PNGs in Desktop/Персонаж/Древень/Аттака " +
                "or already under " + AttackDestFolder);
        }

        if (projectileSprite == null)
        {
            throw new InvalidOperationException(
                "Ent projectile sprite missing. Expected Снаряд-Photoroom.png or " + ProjectileAssetPath);
        }

        if (hurtSprite == null)
        {
            throw new InvalidOperationException(
                "Ent hurt sprite missing. Expected 1784984592_6a64b4109e3f9-Photoroom.png or " +
                HurtAssetPath);
        }

        if (deathSprites == null || deathSprites.Length == 0)
        {
            throw new InvalidOperationException(
                "No Ent death frames found. Expected PNGs in Desktop/Персонаж/Древень/Новая папка " +
                "or already under " + DeathDestFolder);
        }

        int fireIndex = FindFrameIndexBySourceNumber(attackFrames, ProjectileFireFrameNumber);
        int loopIndex = FindFrameIndexBySourceNumber(attackFrames, AttackLoopStartFrameNumber);

        GameObject prefabRoot = BuildOrUpdatePrefab(
            walkFrames, attackFrames, projectileSprite, hurtSprite, deathSprites);
        PlaceInPrototypeScene(
            prefabRoot, walkFrames, attackFrames, projectileSprite, hurtSprite, deathSprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            $"Ent ready.\nWalk frames: {walkFrames.Length}\nAttack frames: {attackFrames.Length}\n" +
            $"Death frames: {deathSprites.Length}\n" +
            $"Fire source frame: {ProjectileFireFrameNumber} → array index {fireIndex}\n" +
            $"Loop: source {AttackLoopStartFrameNumber}→{ProjectileFireFrameNumber} " +
            $"(indices {loopIndex}→{fireIndex})\n" +
            $"Hurt flash: {HurtAssetPath} (white silhouette, bottom-center pivot, " +
            $"sprite swap + bounds feet-lock for {DamageFlashDuration}s, " +
            $"Y fine-offset {HurtFlashYOffset}; no height-match scale)\n" +
            $"Death: once forward @ {FrameRate} FPS → Destroy (destroyOnDeath false)\n" +
            $"Projectile: spin {ProjectileSpinDegreesPerSecond} deg/s, dmg {ProjectileDamage}, " +
            $"speed {ProjectileSpeed}, scale {ProjectileScale}\n" +
            $"Health: {EntMaxHealth}\nContact damage: {ContactDamage}\n" +
            $"Move speed: {MoveSpeed}\nChase range: {ChaseRange}\nAttack range: {AttackRange}\n" +
            $"FPS: walk/loop/death {FrameRate}, windup 1→19 @ {AttackWindupFrameRate}\n" +
            $"Scale: {VisualScale}\nPrefab: {PrefabPath}\n" +
            $"Placed: Enemy_Ent at {ScenePosition} in Prototype\n" +
            "Chase while far; in attack range stop + attack anim; flipX when facing right.";
    }

    private static string ResolveSourceFolder(string subFolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Древень", subFolder),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Древень", subFolder),
            Path.Combine(@"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Древень", subFolder),
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

    private static string ResolveProjectileSourcePath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Древень", "Снаряд-Photoroom.png"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Древень", "Снаряд-Photoroom.png"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Древень\Снаряд-Photoroom.png",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static string ResolveHurtSourcePath()
    {
        // White Ent silhouette (not the mud-ball projectile). Bottom-center pivot like attack.
        const string HurtSourceFileName = "1784984592_6a64b4109e3f9-Photoroom.png";
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Древень", HurtSourceFileName),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Древень", HurtSourceFileName),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Древень\" + HurtSourceFileName,
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Sprites");
        EnsureFolder("Assets/Art/Sprites/Characters");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Ent");
        EnsureFolder(WalkDestFolder);
        EnsureFolder(AttackDestFolder);
        EnsureFolder(ProjectileDestFolder);
        EnsureFolder(HurtDestFolder);
        EnsureFolder(DeathDestFolder);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Enemies");
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

    private static Sprite[] ImportWalkSprites(string sourceDir)
    {
        return ImportNumberedSprites(
            sourceDir,
            WalkDestFolder,
            "Ent_Walk_",
            preserveSourceNumbers: false);
    }

    private static Sprite[] ImportAttackSprites(string sourceDir)
    {
        return ImportNumberedSprites(
            sourceDir,
            AttackDestFolder,
            "Ent_Attack_",
            preserveSourceNumbers: true);
    }

    private static Sprite[] ImportDeathSprites(string sourceDir)
    {
        return ImportNumberedSprites(
            sourceDir,
            DeathDestFolder,
            "Ent_Death_",
            preserveSourceNumbers: false);
    }

    private static Sprite[] ImportNumberedSprites(
        string sourceDir,
        string destFolder,
        string destPrefix,
        bool preserveSourceNumbers)
    {
        var orderedSources = CollectOrderedSources(sourceDir);

        if (orderedSources.Count == 0)
        {
            return LoadExistingDestSprites(destFolder, destPrefix + "*.png");
        }

        ClearDestPngs(destFolder, destPrefix + "*.png");

        var sprites = new List<Sprite>(orderedSources.Count);
        if (preserveSourceNumbers)
        {
            foreach (KeyValuePair<int, string> pair in orderedSources)
            {
                Sprite sprite = ImportOne(
                    pair.Value,
                    $"{destFolder}/{destPrefix}{pair.Key:D3}.png",
                    bottomCenterPivot: true);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }
        }
        else
        {
            int index = 1;
            foreach (KeyValuePair<int, string> pair in orderedSources)
            {
                Sprite sprite = ImportOne(
                    pair.Value,
                    $"{destFolder}/{destPrefix}{index:D3}.png",
                    bottomCenterPivot: true);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }

                index++;
            }
        }

        return sprites.ToArray();
    }

    private static SortedDictionary<int, string> CollectOrderedSources(string sourceDir)
    {
        var orderedSources = new SortedDictionary<int, string>();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            return orderedSources;
        }

        string[] files = Directory.GetFiles(sourceDir, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            if (!TryParseFrameNumber(Path.GetFileName(files[i]), out int order))
            {
                continue;
            }

            if (orderedSources.TryGetValue(order, out string existing))
            {
                if (IsPreferredSourceName(files[i], existing))
                {
                    orderedSources[order] = files[i];
                }
            }
            else
            {
                orderedSources[order] = files[i];
            }
        }

        return orderedSources;
    }

    private static void ClearDestPngs(string destFolder, string searchPattern)
    {
        string destFs = destFolder.Replace('/', Path.DirectorySeparatorChar);
        if (!Directory.Exists(destFs))
        {
            return;
        }

        string[] existing = Directory.GetFiles(destFs, searchPattern);
        for (int i = 0; i < existing.Length; i++)
        {
            string assetPath = $"{destFolder}/{Path.GetFileName(existing[i])}";
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static Sprite[] LoadExistingDestSprites(string destFolder, string searchPattern)
    {
        string destFs = destFolder.Replace('/', Path.DirectorySeparatorChar);
        if (!Directory.Exists(destFs))
        {
            return Array.Empty<Sprite>();
        }

        string[] files = Directory.GetFiles(destFs, searchPattern);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var sprites = new List<Sprite>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = $"{destFolder}/{Path.GetFileName(files[i])}";
            ConfigureImporter(assetPath, bottomCenterPivot: true);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static Sprite ImportProjectileSprite(string sourcePath)
    {
        if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
        {
            return ImportOne(sourcePath, ProjectileAssetPath, bottomCenterPivot: false);
        }

        if (File.Exists(ProjectileAssetPath.Replace('/', Path.DirectorySeparatorChar)))
        {
            ConfigureImporter(ProjectileAssetPath, bottomCenterPivot: false);
            return AssetDatabase.LoadAssetAtPath<Sprite>(ProjectileAssetPath);
        }

        return null;
    }

    private static Sprite ImportHurtSprite(string sourcePath)
    {
        if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
        {
            return ImportOne(sourcePath, HurtAssetPath, bottomCenterPivot: true);
        }

        if (File.Exists(HurtAssetPath.Replace('/', Path.DirectorySeparatorChar)))
        {
            ConfigureImporter(HurtAssetPath, bottomCenterPivot: true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(HurtAssetPath);
        }

        return null;
    }

    private static bool TryParseFrameNumber(string fileName, out int order)
    {
        order = 0;
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"^(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out order);
    }

    private static bool IsPreferredSourceName(string candidate, string existing)
    {
        bool candidatePlain = !candidate.Contains(" (");
        bool existingPlain = !existing.Contains(" (");
        if (candidatePlain != existingPlain)
        {
            return candidatePlain;
        }

        return string.Compare(candidate, existing, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static Sprite ImportOne(string sourcePath, string destPath, bool bottomCenterPivot)
    {
        File.Copy(sourcePath, destPath, overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(destPath, bottomCenterPivot);
        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
    }

    private static void ConfigureImporter(string destPath, bool bottomCenterPivot)
    {
        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePivot = bottomCenterPivot ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = bottomCenterPivot
            ? (int)SpriteAlignment.BottomCenter
            : (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static int FindFrameIndexBySourceNumber(Sprite[] frames, int sourceNumber)
    {
        if (frames == null)
        {
            return -1;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
            {
                continue;
            }

            Match match = Regex.Match(frames[i].name, @"(\d+)(?!.*\d)");
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int number) &&
                number == sourceNumber)
            {
                return i;
            }
        }

        return -1;
    }

    private static void ApplyWalkerSettings(
        EntWalkerEnemy2D walker,
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite projectileSprite,
        Sprite hurtSprite,
        Sprite[] deathSprites,
        Transform playerTarget)
    {
        if (walker == null)
        {
            return;
        }

        if (walkFrames != null && walkFrames.Length > 0)
        {
            walker.EditorAssignWalkFrames(walkFrames);
        }

        if (attackFrames != null && attackFrames.Length > 0)
        {
            walker.EditorAssignAttackFrames(attackFrames);
        }

        walker.EditorAssignHurtSprite(hurtSprite);
        walker.EditorAssignDeathSprites(deathSprites);

        walker.EditorAssignProjectile(
            projectileSprite,
            ProjectileDamage,
            ProjectileSpeed,
            ProjectileScale,
            ProjectileSpinDegreesPerSecond,
            ProjectileSpawnOffset,
            TargetAimHeight);

        SerializedObject walkerSo = new SerializedObject(walker);
        walkerSo.FindProperty("moveSpeed").floatValue = MoveSpeed;
        walkerSo.FindProperty("chaseRange").floatValue = ChaseRange;
        walkerSo.FindProperty("chaseVerticalRange").floatValue = ChaseVerticalRange;
        walkerSo.FindProperty("frameRate").floatValue = FrameRate;
        walkerSo.FindProperty("attackWindupFrameRate").floatValue = AttackWindupFrameRate;
        walkerSo.FindProperty("pingPongWalk").boolValue = false;
        walkerSo.FindProperty("attackRange").floatValue = AttackRange;
        walkerSo.FindProperty("attackVerticalRange").floatValue = AttackVerticalRange;
        walkerSo.FindProperty("projectileFireFrameNumber").intValue = ProjectileFireFrameNumber;
        walkerSo.FindProperty("attackLoopStartFrameNumber").intValue = AttackLoopStartFrameNumber;
        walkerSo.FindProperty("hurtSprite").objectReferenceValue = hurtSprite;
        walkerSo.FindProperty("damageFlashDuration").floatValue = DamageFlashDuration;
        walkerSo.FindProperty("hurtFlashScaleMultiplier").floatValue = HurtFlashScaleMultiplier;
        walkerSo.FindProperty("hurtFlashYOffset").floatValue = HurtFlashYOffset;
        walkerSo.FindProperty("targetAimHeight").floatValue = TargetAimHeight;
        walkerSo.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
        walkerSo.FindProperty("gravityScale").floatValue = EntGravityScale;
        walkerSo.FindProperty("bodyColliderSize").vector2Value = FixedBodyColliderSize;
        walkerSo.FindProperty("bodyColliderOffset").vector2Value = FixedBodyColliderOffset;
        walkerSo.FindProperty("physicsFeetPaddingLocal").floatValue = PhysicsFeetPaddingLocal;
        walkerSo.FindProperty("feetGroundSinkLocal").floatValue = FeetGroundSinkLocal;
        walkerSo.FindProperty("physicsFeetWidthScale").floatValue = PhysicsFeetWidthScale;
        walkerSo.FindProperty("physicsMinFeetWidthWorld").floatValue = PhysicsMinFeetWidthWorld;
        walkerSo.FindProperty("groundProbeDistanceWorld").floatValue = GroundProbeDistanceWorld;
        walkerSo.FindProperty("lockWalkHeightToScenePlacement").boolValue = LockWalkHeightToScenePlacement;
        walkerSo.FindProperty("walkHeightLockProbeDistanceWorld").floatValue = WalkHeightLockProbeDistanceWorld;
        walkerSo.FindProperty("snapToGroundProbeDistanceWorld").floatValue = SnapToGroundProbeDistanceWorld;
        if (playerTarget != null)
        {
            walkerSo.FindProperty("target").objectReferenceValue = playerTarget;
        }

        walkerSo.ApplyModifiedPropertiesWithoutUndo();

        // Assign deathSprites after Apply so SerializedObject sees EditorAssignDeathSprites values
        // (EditorAssign already set the field; re-apply array via SO for scene/prefab dirtying).
        walkerSo.Update();
        SerializedProperty deathProp = walkerSo.FindProperty("deathSprites");
        if (deathProp != null)
        {
            deathProp.arraySize = deathSprites != null ? deathSprites.Length : 0;
            for (int i = 0; i < deathProp.arraySize; i++)
            {
                deathProp.GetArrayElementAtIndex(i).objectReferenceValue = deathSprites[i];
            }

            walkerSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static readonly Vector2 FixedBodyColliderSize = new Vector2(5.625f, 8f);
    private static readonly Vector2 FixedBodyColliderOffset = new Vector2(0f, 5.5f);

    private static void ApplyFeetAlignedCapsuleCollider(CapsuleCollider2D capsule, Bounds spriteBounds)
    {
        float width = FixedBodyColliderSize.x;
        float height = Mathf.Max(width, FixedBodyColliderSize.y);
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(width, height);
        capsule.offset = FixedBodyColliderOffset;
    }

    private static GameObject BuildOrUpdatePrefab(
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite projectileSprite,
        Sprite hurtSprite,
        Sprite[] deathSprites)
    {
        GameObject root = new GameObject("Enemy_Ent");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = walkFrames[0];
        renderer.sortingOrder = 3;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var box = root.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            UnityEngine.Object.DestroyImmediate(box);
        }

        var capsule = root.GetComponent<CapsuleCollider2D>();
        if (capsule == null)
        {
            capsule = root.AddComponent<CapsuleCollider2D>();
        }

        capsule.isTrigger = false;
        ApplyFeetAlignedCapsuleCollider(capsule, walkFrames[0].bounds);

        var health = root.AddComponent<Health>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = EntMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = false;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        var contact = root.AddComponent<ContactDamage2D>();
        SerializedObject contactSo = new SerializedObject(contact);
        contactSo.FindProperty("damage").intValue = ContactDamage;
        contactSo.FindProperty("hitCooldown").floatValue = 0.5f;
        contactSo.ApplyModifiedPropertiesWithoutUndo();

        var walker = root.AddComponent<EntWalkerEnemy2D>();
        ApplyWalkerSettings(
            walker, walkFrames, attackFrames, projectileSprite, hurtSprite, deathSprites, null);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void PlaceInPrototypeScene(
        GameObject prefab,
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite projectileSprite,
        Sprite hurtSprite,
        Sprite[] deathSprites)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Enemy_Ent");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Ent";
        instance.transform.position = ScenePosition;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        GameObject player = GameObject.Find("Player_HeroKnight");
        Transform playerTarget = player != null ? player.transform : null;
        ApplyWalkerSettings(
            instance.GetComponent<EntWalkerEnemy2D>(),
            walkFrames,
            attackFrames,
            projectileSprite,
            hurtSprite,
            deathSprites,
            playerTarget);

        var health = instance.GetComponent<Health>();
        if (health != null)
        {
            SerializedObject healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").intValue = EntMaxHealth;
            healthSo.FindProperty("destroyOnDeath").boolValue = false;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
