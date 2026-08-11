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
/// Imports Mushomor idle/walk/attack/hurt/death sprites and places Enemy_Mushomor in Prototype.
/// Source: Desktop/Персонаж/Мухомор/{Покой,Ходьба,Атака,Получение урона,Новая папка}.
/// </summary>
[InitializeOnLoad]
public static class MushomorEnemySetupEditor
{
    private const string IdleDestFolder = "Assets/Art/Sprites/Characters/Enemies/Mushomor/Idle";
    private const string WalkDestFolder = "Assets/Art/Sprites/Characters/Enemies/Mushomor/Walk";
    private const string AttackDestFolder = "Assets/Art/Sprites/Characters/Enemies/Mushomor/Attack";
    private const string HurtDestFolder = "Assets/Art/Sprites/Characters/Enemies/Mushomor/Hurt";
    private const string DeathDestFolder = "Assets/Art/Sprites/Characters/Enemies/Mushomor/Death";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Mushomor.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";

    private static string SetupFlagPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "setup_mushomor_enemy.flag"));
    private static string SetupResultPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "setup_mushomor_enemy_result.txt"));
    private static string ReplaceHurtFlagPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "replace_mushomor_hurt.flag"));

    /// <summary>Must exceed player AttackHitbox damage (25) so non-lethal hits can show hurt.</summary>
    private const int MushomorMaxHealth = 50;
    private const int ContactDamage = 10;
    private const float FrameRate = 12f;
    private const float HurtFrameRate = 10f;
    private const float DeathFrameRate = 12f;
    private const float MoveSpeed = 1.8f;
    private const float ChaseRange = 18f;
    /// <summary>Stop chase + start attack anim.</summary>
    private const float AttackTriggerRange = 1.0f;
    /// <summary>Directed OverlapBox damage reach during active swing frames.</summary>
    private const float AttackRange = 4.0f;
    private const int AttackDamage = 10;
    private const int HitActiveStartFrame = 3;
    private const int HitActiveEndFrame = 7;
    private const float AttackCooldown = 1.1f;
    /// <summary>256x256 @ PPU 100 -> ~1.92 world units at 0.75 (small-enemy readable).</summary>
    private const float VisualScale = 0.75f;

    /// <summary>Near player (-12) / snake (-4) on the Y~-37 playable strip.</summary>
    private static readonly Vector3 ScenePosition = new Vector3(-8.5f, -37.2f, 0f);

    static MushomorEnemySetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.delayCall += TryReplaceHurtFromFlag;
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(SetupFlagPath))
        {
            return;
        }

        File.Delete(SetupFlagPath);
        try
        {
            string result = SetupMushomorEnemy();
            Debug.Log(result);
            WriteSetupResult("OK\n" + result);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            WriteSetupResult("FAIL\n" + ex);
        }
    }

    private static void TryReplaceHurtFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(ReplaceHurtFlagPath))
        {
            return;
        }

        File.Delete(ReplaceHurtFlagPath);
        try
        {
            string result = ReplaceHurtAnimationOnly();
            Debug.Log(result);
            WriteReplaceHurtResult("OK\n" + result);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            WriteReplaceHurtResult("FAIL\n" + ex);
        }
    }

    private static void WriteSetupResult(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SetupResultPath) ?? "Temp");
            File.WriteAllText(SetupResultPath, text);
        }
        catch (Exception writeEx)
        {
            Debug.LogWarning("Could not write mushomor setup result: " + writeEx.Message);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Mushomor Enemy")]
    public static void SetupMushomorEnemyMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Mushomor Enemy", SetupMushomorEnemy(), "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Mushomor Enemy", "Failed:\n" + ex.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod MushomorEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            string summary = SetupMushomorEnemy();
            Debug.Log(summary);
            WriteSetupResult("OK\n" + summary);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            WriteSetupResult("FAIL\n" + ex);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Batch: -executeMethod MushomorEnemySetupEditor.ReplaceHurtFromBatch</summary>
    public static void ReplaceHurtFromBatch()
    {
        try
        {
            string summary = ReplaceHurtAnimationOnly();
            Debug.Log(summary);
            WriteReplaceHurtResult("OK\n" + summary);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            WriteReplaceHurtResult("FAIL\n" + ex);
            EditorApplication.Exit(1);
        }
    }

    private static string ReplaceHurtResultPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "replace_mushomor_hurt_result.txt"));

    private static void WriteReplaceHurtResult(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReplaceHurtResultPath) ?? "Temp");
            File.WriteAllText(ReplaceHurtResultPath, text);
        }
        catch (Exception writeEx)
        {
            Debug.LogWarning("Could not write mushomor hurt replace result: " + writeEx.Message);
        }
    }

    /// <summary>
    /// Overwrites Hurt sprites from Desktop/Получение урона and rewires hurtFrames only.
    /// Idle/Walk/Attack arrays and their sprites are left untouched.
    /// </summary>
    public static string ReplaceHurtAnimationOnly()
    {
        EnsureFolders();

        Sprite[] hurtFrames = ImportNumberedSprites(
            ResolveSourceFolder("Получение урона"),
            HurtDestFolder,
            "Mushomor_Hurt_");

        if (hurtFrames == null || hurtFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor hurt frames. Expected Desktop/Персонаж/Мухомор/Получение урона " +
                "or already under " + HurtDestFolder);
        }

        if (!File.Exists(PrefabPath))
        {
            throw new InvalidOperationException("Missing prefab: " + PrefabPath);
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var hurt = prefabRoot.GetComponent<MushomorHurtAnimator2D>();
            if (hurt == null)
            {
                throw new InvalidOperationException("Enemy_Mushomor prefab missing MushomorHurtAnimator2D.");
            }

            hurt.EditorAssignHurtFrames(hurtFrames);
            SerializedObject hurtSo = new SerializedObject(hurt);
            SerializedProperty framesProp = hurtSo.FindProperty("hurtFrames");
            framesProp.arraySize = hurtFrames.Length;
            for (int i = 0; i < hurtFrames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = hurtFrames[i];
            }

            hurtSo.FindProperty("frameRate").floatValue = HurtFrameRate;
            hurtSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hurt);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Prototype PrefabInstance inherits hurtFrames; refresh instance if scene is open/loadable.
        if (File.Exists(ScenePath))
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject instance = GameObject.Find("Enemy_Mushomor");
            if (instance != null)
            {
                var hurt = instance.GetComponent<MushomorHurtAnimator2D>();
                if (hurt != null)
                {
                    hurt.EditorAssignHurtFrames(hurtFrames);
                    SerializedObject hurtSo = new SerializedObject(hurt);
                    SerializedProperty framesProp = hurtSo.FindProperty("hurtFrames");
                    framesProp.arraySize = hurtFrames.Length;
                    for (int i = 0; i < hurtFrames.Length; i++)
                    {
                        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = hurtFrames[i];
                    }

                    hurtSo.FindProperty("frameRate").floatValue = HurtFrameRate;
                    hurtSo.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hurt);
                    EditorUtility.SetDirty(hurt);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string source = ResolveSourceFolder("Получение урона") ?? "(dest only)";
        return
            $"Hurt frames replaced: {hurtFrames.Length}\n" +
            $"Source: {source}\n" +
            $"Dest: {HurtDestFolder}\n" +
            "Import: Sprite Single, Point, pivot bottom-center, PPU 100\n" +
            $"Prefab hurtFrames: {hurtFrames.Length} @ FPS {HurtFrameRate}\n" +
            "Scene: PrefabInstance Enemy_Mushomor hurtFrames rewired\n" +
            "Idle/Walk/Attack: unchanged";
    }

    public static void RequestSetup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SetupFlagPath) ?? "Temp");
        File.WriteAllText(SetupFlagPath, DateTime.UtcNow.ToString("O"));
        TrySetupFromFlag();
    }

    public static string SetupMushomorEnemy()
    {
        EnsureFolders();

        Sprite[] idleFrames = ImportNumberedSprites(
            ResolveSourceFolder("Покой"),
            IdleDestFolder,
            "Mushomor_Idle_");
        Sprite[] walkFrames = ImportNumberedSprites(
            ResolveSourceFolder("Ходьба"),
            WalkDestFolder,
            "Mushomor_Walk_");
        Sprite[] attackFrames = ImportNumberedSprites(
            ResolveSourceFolder("Атака"),
            AttackDestFolder,
            "Mushomor_Attack_");
        Sprite[] hurtFrames = ImportNumberedSprites(
            ResolveSourceFolder("Получение урона"),
            HurtDestFolder,
            "Mushomor_Hurt_");
        Sprite[] deathFrames = ImportNumberedSprites(
            ResolveSourceFolder("Новая папка"),
            DeathDestFolder,
            "Mushomor_Death_");

        if (idleFrames == null || idleFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor idle frames. Expected Desktop/Персонаж/Мухомор/Покой " +
                "or already under " + IdleDestFolder);
        }

        if (walkFrames == null || walkFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor walk frames. Expected Desktop/Персонаж/Мухомор/Ходьба " +
                "or already under " + WalkDestFolder);
        }

        if (attackFrames == null || attackFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor attack frames. Expected Desktop/Персонаж/Мухомор/Атака " +
                "or already under " + AttackDestFolder);
        }

        if (hurtFrames == null || hurtFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor hurt frames. Expected Desktop/Персонаж/Мухомор/Получение урона " +
                "or already under " + HurtDestFolder);
        }

        if (deathFrames == null || deathFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "No Mushomor death frames. Expected Desktop/Персонаж/Мухомор/Новая папка " +
                "or already under " + DeathDestFolder);
        }

        GameObject prefabRoot = BuildOrUpdatePrefab(
            idleFrames, walkFrames, attackFrames, hurtFrames, deathFrames);
        PlaceInPrototypeScene(
            prefabRoot, idleFrames, walkFrames, attackFrames, hurtFrames, deathFrames);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            $"Mushomor ready.\nIdle: {idleFrames.Length}\nWalk: {walkFrames.Length}\n" +
            $"Attack: {attackFrames.Length}\nHurt: {hurtFrames.Length}\nDeath: {deathFrames.Length}\n" +
            $"Health: {MushomorMaxHealth}\nContact damage: {ContactDamage}\n" +
            $"Move: {MoveSpeed}, chase {ChaseRange}, attack trigger {AttackTriggerRange}, hit reach {AttackRange}, cooldown {AttackCooldown}s\n" +
            $"FPS: idle/walk/attack/death {FrameRate}, hurt {HurtFrameRate}\nScale: {VisualScale}\n" +
            $"Prefab: {PrefabPath}\nPlaced: Enemy_Mushomor at {ScenePosition} in Prototype\n" +
            "Idle when still -> Walk while chasing -> Attack in range -> Hurt on Damaged -> Death once on Died -> Destroy.";
    }

    private static string ResolveSourceFolder(string subFolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Мухомор", subFolder),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Мухомор", subFolder),
            Path.Combine(@"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Мухомор", subFolder),
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

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Sprites");
        EnsureFolder("Assets/Art/Sprites/Characters");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Mushomor");
        EnsureFolder(IdleDestFolder);
        EnsureFolder(WalkDestFolder);
        EnsureFolder(AttackDestFolder);
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

    private static Sprite[] ImportNumberedSprites(string sourceDir, string destFolder, string destPrefix)
    {
        var orderedSources = CollectOrderedSources(sourceDir);
        if (orderedSources.Count == 0)
        {
            return LoadExistingDestSprites(destFolder, destPrefix + "*.png");
        }

        ClearDestPngs(destFolder, destPrefix + "*.png");

        var sprites = new List<Sprite>(orderedSources.Count);
        int index = 1;
        foreach (KeyValuePair<int, string> pair in orderedSources)
        {
            Sprite sprite = ImportOne(
                pair.Value,
                $"{destFolder}/{destPrefix}{index:D3}.png");
            if (sprite != null)
            {
                sprites.Add(sprite);
            }

            index++;
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

    /// <summary>
    /// Single trailing number -> that value. Compound names like _0009_0002 -> primary*10000+secondary
    /// so Idle 0001/0002/0009_0001 and Walk 0003-0008/0009_0002-0009_0009 stay unique and ordered.
    /// </summary>
    private static bool TryParseFrameNumber(string fileName, out int order)
    {
        order = 0;
        MatchCollection matches = Regex.Matches(Path.GetFileNameWithoutExtension(fileName), @"\d+");
        if (matches.Count == 0)
        {
            return false;
        }

        if (matches.Count == 1)
        {
            return int.TryParse(matches[0].Value, out order);
        }

        if (!int.TryParse(matches[0].Value, out int primary) ||
            !int.TryParse(matches[matches.Count - 1].Value, out int secondary))
        {
            return false;
        }

        order = primary * 10000 + secondary;
        return true;
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
            ConfigureImporter(assetPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static Sprite ImportOne(string sourcePath, string destPath)
    {
        File.Copy(sourcePath, destPath, overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(destPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
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
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void ApplyComponentSettings(
        MushomorMovement2D movement,
        MushomorMeleeAttack2D attack,
        MushomorHurtAnimator2D hurt,
        Sprite[] idleFrames,
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite[] hurtFrames,
        Sprite[] deathFrames,
        Transform playerTarget)
    {
        if (movement != null)
        {
            movement.EditorAssignIdleFrames(idleFrames);
            movement.EditorAssignWalkFrames(walkFrames);
            SerializedObject moveSo = new SerializedObject(movement);
            SerializedProperty idleProp = moveSo.FindProperty("idleFrames");
            idleProp.arraySize = idleFrames != null ? idleFrames.Length : 0;
            for (int i = 0; i < idleProp.arraySize; i++)
            {
                idleProp.GetArrayElementAtIndex(i).objectReferenceValue = idleFrames[i];
            }

            SerializedProperty walkProp = moveSo.FindProperty("walkFrames");
            walkProp.arraySize = walkFrames != null ? walkFrames.Length : 0;
            for (int i = 0; i < walkProp.arraySize; i++)
            {
                walkProp.GetArrayElementAtIndex(i).objectReferenceValue = walkFrames[i];
            }

            moveSo.FindProperty("moveSpeed").floatValue = MoveSpeed;
            moveSo.FindProperty("chaseRange").floatValue = ChaseRange;
            moveSo.FindProperty("stopDistance").floatValue = 0.2f;
            moveSo.FindProperty("gravityScale").floatValue = 3f;
            moveSo.FindProperty("spawnGroundProbeDistance").floatValue = 30f;
            moveSo.FindProperty("idleFrameRate").floatValue = FrameRate;
            moveSo.FindProperty("pingPongIdle").boolValue = false;
            moveSo.FindProperty("frameRate").floatValue = FrameRate;
            moveSo.FindProperty("pingPongWalk").boolValue = false;
            moveSo.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
            if (playerTarget != null)
            {
                moveSo.FindProperty("target").objectReferenceValue = playerTarget;
            }

            moveSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(movement);
        }

        if (attack != null)
        {
            attack.EditorAssignAttackFrames(attackFrames);
            SerializedObject attackSo = new SerializedObject(attack);
            SerializedProperty framesProp = attackSo.FindProperty("attackFrames");
            framesProp.arraySize = attackFrames != null ? attackFrames.Length : 0;
            for (int i = 0; i < framesProp.arraySize; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = attackFrames[i];
            }

            attackSo.FindProperty("frameRate").floatValue = FrameRate;
            attackSo.FindProperty("attackTriggerRange").floatValue = AttackTriggerRange;
            attackSo.FindProperty("attackRange").floatValue = AttackRange;
            attackSo.FindProperty("attackCooldown").floatValue = AttackCooldown;
            attackSo.FindProperty("attackDamage").intValue = AttackDamage;
            attackSo.FindProperty("hitActiveStartFrame").intValue = HitActiveStartFrame;
            attackSo.FindProperty("hitActiveEndFrame").intValue = HitActiveEndFrame;
            attackSo.FindProperty("hitboxHeight").floatValue = 1.8f;
            attackSo.FindProperty("hitboxYOffset").floatValue = 1.0f;
            attackSo.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
            if (playerTarget != null)
            {
                attackSo.FindProperty("target").objectReferenceValue = playerTarget;
            }

            attackSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
        }

        if (hurt != null)
        {
            hurt.EditorAssignHurtFrames(hurtFrames);
            hurt.EditorAssignDeathFrames(deathFrames);
            SerializedObject hurtSo = new SerializedObject(hurt);
            SerializedProperty framesProp = hurtSo.FindProperty("hurtFrames");
            framesProp.arraySize = hurtFrames != null ? hurtFrames.Length : 0;
            for (int i = 0; i < framesProp.arraySize; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = hurtFrames[i];
            }

            SerializedProperty deathProp = hurtSo.FindProperty("deathFrames");
            deathProp.arraySize = deathFrames != null ? deathFrames.Length : 0;
            for (int i = 0; i < deathProp.arraySize; i++)
            {
                deathProp.GetArrayElementAtIndex(i).objectReferenceValue = deathFrames[i];
            }

            hurtSo.FindProperty("frameRate").floatValue = HurtFrameRate;
            hurtSo.FindProperty("deathFrameRate").floatValue = DeathFrameRate;
            hurtSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hurt);
        }
    }

    private static void ApplyBoxCollider(BoxCollider2D box, Sprite sprite)
    {
        if (box == null || sprite == null)
        {
            return;
        }

        Bounds bounds = sprite.bounds;
        // Match the authored scene Mushomor: shorter body lets the visible feet
        // sit on the floor despite transparent padding at the bottom of the sprite.
        float width = Mathf.Max(0.4f, bounds.size.x * 0.55f);
        float height = Mathf.Max(0.5f, bounds.size.y * 0.76171875f);
        box.isTrigger = false;
        box.size = new Vector2(width, height);
        box.offset = new Vector2(
            bounds.center.x,
            bounds.min.y + bounds.size.y * 0.45f);
    }

    private static GameObject BuildOrUpdatePrefab(
        Sprite[] idleFrames,
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite[] hurtFrames,
        Sprite[] deathFrames)
    {
        GameObject root = new GameObject("Enemy_Mushomor");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = idleFrames[0];
        renderer.sortingOrder = 3;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 3f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var box = root.AddComponent<BoxCollider2D>();
        ApplyBoxCollider(box, idleFrames[0]);

        var health = root.AddComponent<Health>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = MushomorMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = false;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        var contact = root.AddComponent<ContactDamage2D>();
        SerializedObject contactSo = new SerializedObject(contact);
        contactSo.FindProperty("damage").intValue = ContactDamage;
        contactSo.FindProperty("hitCooldown").floatValue = 0.5f;
        contactSo.FindProperty("onlyDamagePlayer").boolValue = true;
        contactSo.ApplyModifiedPropertiesWithoutUndo();

        var movement = root.AddComponent<MushomorMovement2D>();
        var attack = root.AddComponent<MushomorMeleeAttack2D>();
        var hurt = root.AddComponent<MushomorHurtAnimator2D>();
        ApplyComponentSettings(
            movement,
            attack,
            hurt,
            idleFrames,
            walkFrames,
            attackFrames,
            hurtFrames,
            deathFrames,
            null);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void PlaceInPrototypeScene(
        GameObject prefab,
        Sprite[] idleFrames,
        Sprite[] walkFrames,
        Sprite[] attackFrames,
        Sprite[] hurtFrames,
        Sprite[] deathFrames)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Enemy_Mushomor");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Mushomor";
        instance.transform.position = ScenePosition;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        GameObject player = GameObject.Find("Player_HeroKnight");
        Transform playerTarget = player != null ? player.transform : null;
        ApplyComponentSettings(
            instance.GetComponent<MushomorMovement2D>(),
            instance.GetComponent<MushomorMeleeAttack2D>(),
            instance.GetComponent<MushomorHurtAnimator2D>(),
            idleFrames,
            walkFrames,
            attackFrames,
            hurtFrames,
            deathFrames,
            playerTarget);

        var health = instance.GetComponent<Health>();
        if (health != null)
        {
            SerializedObject healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").intValue = MushomorMaxHealth;
            healthSo.FindProperty("destroyOnDeath").boolValue = false;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var box = instance.GetComponent<BoxCollider2D>();
        if (box != null && idleFrames != null && idleFrames.Length > 0)
        {
            ApplyBoxCollider(box, idleFrames[0]);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}

