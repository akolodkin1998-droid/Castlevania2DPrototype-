using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Castlevania2D.Enemies;
using Castlevania2D.Health;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports wizard Idle / Attack1 / Attack2 / Portal frames from Desktop/Персонаж/Wizzard
/// and wires Enemy_Wizard: idle → sight → Attack1 → Attack2 → loop 17–19 (flipX),
/// portals at fixed world positions that spawn Ents + Mushomors.
/// </summary>
[InitializeOnLoad]
public static class WizardEnemySetupEditor
{
    private const string IdleDestFolder = "Assets/Art/Sprites/Characters/Enemies/Wizard/Idle";
    private const string Attack1DestFolder = "Assets/Art/Sprites/Characters/Enemies/Wizard/Attack1";
    private const string Attack2DestFolder = "Assets/Art/Sprites/Characters/Enemies/Wizard/Attack2";
    private const string PortalDestFolder = "Assets/Art/Sprites/Characters/Enemies/Wizard/Portal";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Wizard.prefab";
    private const string EntPrefabPath = "Assets/Prefabs/Enemies/Enemy_Ent.prefab";
    private const string MushomorPrefabPath = "Assets/Prefabs/Enemies/Enemy_Mushomor.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private static string SetupFlagPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "setup_wizard_enemy.flag"));
    private static string SetupResultPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "setup_wizard_enemy_result.txt"));

    private const int WizardMaxHealth = 10000;
    private const float FrameRate = 12f;
    /// <summary>Was 0.65; +20% → 0.78.</summary>
    private const float VisualScale = 0.78f;
    private const float SightRange = 18f;
    private const bool UseWorldXActivationGate = true;
    private const float ActivationMinTargetWorldX = 64f;
    private const float PortalScale = 1f;
    private const int PortalSortingOrder = 4;
    private const float EntSpawnInterval = 10f;
    private const int MaxEntsPerPortal = 1;
    private const int MaxMushomorsPerPortal = 1;
    private const bool SpawnEntImmediately = true;
    private const int LoopStartFrameNumber = 17;
    private const int LoopEndFrameNumber = 19;

    /// <summary>Live Prototype placement near portal arena (not boss arena).</summary>
    private static readonly Vector3 ScenePosition = new Vector3(79.1f, -30.1f, 0f);
    private static readonly Vector2 Portal1World = new Vector2(50.76f, -37.16f);
    private static readonly Vector2 Portal2World = new Vector2(76.5f, -37.16f);

    static WizardEnemySetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.update += PollSetupFlag;
    }

    private static double nextFlagPollTime;

    private static void PollSetupFlag()
    {
        if (EditorApplication.timeSinceStartup < nextFlagPollTime)
        {
            return;
        }

        nextFlagPollTime = EditorApplication.timeSinceStartup + 1.0;
        TrySetupFromFlag();
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
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
            string result = SetupWizardEnemy();
            Debug.Log(result);
            WriteSetupResult("OK\n" + result);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            WriteSetupResult("FAIL\n" + ex);
        }
    }

    private static void WriteSetupResult(string text)
    {
        try
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(SetupResultPath, text);
        }
        catch (Exception writeEx)
        {
            Debug.LogWarning("Could not write wizard setup result: " + writeEx.Message);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Wizard Enemy")]
    public static void SetupWizardEnemyMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Wizard Enemy", SetupWizardEnemy(), "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Wizard Enemy", "Failed:\n" + ex.Message, "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Wizard Attack Portals")]
    public static void SetupWizardAttackPortalsMenu()
    {
        SetupWizardEnemyMenu();
    }

    /// <summary>Batch: -executeMethod WizardEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            string summary = SetupWizardEnemy();
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

    public static void RequestSetup()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(SetupFlagPath, DateTime.UtcNow.ToString("O"));
        TrySetupFromFlag();
    }

    public static string SetupWizardEnemy()
    {
        string idleSource = ResolveWizzardSubfolder("Покой");
        string attack1Source = ResolveWizzardSubfolder("Аттака");
        string attack2Source = ResolveWizzardSubfolder("Аттака 2");
        string portalSource = ResolvePortalSourceFolder();

        if (string.IsNullOrEmpty(idleSource))
        {
            throw new DirectoryNotFoundException(
                "Wizard idle folder not found. Expected Desktop/Персонаж/Wizzard/Покой");
        }

        if (string.IsNullOrEmpty(attack1Source))
        {
            throw new DirectoryNotFoundException(
                "Wizard Attack1 folder not found. Expected Desktop/Персонаж/Wizzard/Аттака");
        }

        if (string.IsNullOrEmpty(attack2Source))
        {
            throw new DirectoryNotFoundException(
                "Wizard Attack2 folder not found. Expected Desktop/Персонаж/Wizzard/Аттака 2");
        }

        if (string.IsNullOrEmpty(portalSource))
        {
            throw new DirectoryNotFoundException(
                "Wizard portal folder not found. Expected Desktop/Персонаж/Wizzard/Пртал (or Портал)");
        }

        GameObject entPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntPrefabPath);
        if (entPrefab == null)
        {
            throw new InvalidOperationException(
                "Enemy_Ent prefab missing at " + EntPrefabPath + ". Run Setup Ent Enemy first.");
        }

        GameObject mushomorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MushomorPrefabPath);
        if (mushomorPrefab == null)
        {
            throw new InvalidOperationException(
                "Enemy_Mushomor prefab missing at " + MushomorPrefabPath +
                ". Run Setup Mushomor Enemy first.");
        }

        EnsureFolders();

        Sprite[] idleFrames = ImportNumberedSprites(
            idleSource, IdleDestFolder, "Wizard_Idle_", "Wizard_Idle_*.png");
        Sprite[] attack1Frames = ImportNumberedSprites(
            attack1Source, Attack1DestFolder, "Wizard_Attack1_", "Wizard_Attack1_*.png");
        Sprite[] attack2Frames = ImportNumberedSprites(
            attack2Source, Attack2DestFolder, "Wizard_Attack2_", "Wizard_Attack2_*.png");
        // Prefer untitled_*.png from Пртал; ImportNumberedSprites sorts by trailing digits.
        Sprite[] portalFrames = ImportNumberedSprites(
            portalSource, PortalDestFolder, "Wizard_Portal_", "Wizard_Portal_*.png");
        if (portalFrames.Length > 6)
        {
            // Appear/loop uses 001–006 only (untitled_0001 … untitled_0006).
            var appearLoop = new Sprite[6];
            System.Array.Copy(portalFrames, appearLoop, 6);
            portalFrames = appearLoop;
        }

        if (idleFrames.Length == 0)
        {
            throw new InvalidOperationException("No idle frames imported.");
        }

        if (attack1Frames.Length == 0)
        {
            throw new InvalidOperationException("No Attack1 frames imported.");
        }

        if (attack2Frames.Length == 0)
        {
            throw new InvalidOperationException("No Attack2 frames imported.");
        }

        if (portalFrames.Length == 0)
        {
            throw new InvalidOperationException("No portal frames imported.");
        }

        GameObject prefab = BuildOrUpdatePrefab(
            idleFrames, attack1Frames, attack2Frames, portalFrames, entPrefab, mushomorPrefab);
        PlaceInPrototypeScene(
            prefab, idleFrames, attack1Frames, attack2Frames, portalFrames, entPrefab, mushomorPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            $"Wizard ready (attack + portals).\n" +
            $"Idle: {idleFrames.Length} | Attack1: {attack1Frames.Length} | " +
            $"Attack2: {attack2Frames.Length} | Portal: {portalFrames.Length}\n" +
            $"FPS: {FrameRate}\n" +
            $"Flow: Idle → Attack1 once → Attack2 once → loop Attack2 {LoopStartFrameNumber}–{LoopEndFrameNumber}\n" +
            $"Attack flipX: true (same as idle)\n" +
            $"Activation: player world X ≥ {ActivationMinTargetWorldX} (sight range disabled)\n" +
            $"Portals @ {Portal1World} / {Portal2World} (Inspector Portal1Position / Portal2Position; setup preserves authored)\n" +
            $"Summons: Ent {EntPrefabPath} + Mushomor {MushomorPrefabPath}, " +
            $"interval {EntSpawnInterval}s, maxEnts/portal {MaxEntsPerPortal}, " +
            $"maxMushomors/portal {MaxMushomorsPerPortal}, immediate={SpawnEntImmediately}\n" +
            $"Wizard scale: {VisualScale} (X/Y)\n" +
            $"Health: {WizardMaxHealth}\nPrefab: {PrefabPath}\n" +
            $"Placed: Enemy_Wizard at {ScenePosition}\n" +
            $"Art: Idle/Attack1/Attack2/Portal under Assets/Art/Sprites/Characters/Enemies/Wizard/\n" +
            $"Menu: Tools → Castlevania 2D → Setup Wizard Enemy (or Setup Wizard Attack Portals)";
    }

    private static string ResolvePortalSourceFolder()
    {
        // Disk typo is Пртал; also accept Портал.
        string prtal = ResolveWizzardSubfolder("Пртал");
        if (!string.IsNullOrEmpty(prtal))
        {
            return prtal;
        }

        return ResolveWizzardSubfolder("Портал");
    }

    private static string ResolveWizzardSubfolder(string subfolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Wizzard", subfolder),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Wizzard", subfolder),
            Path.Combine(@"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Wizzard", subfolder),
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
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Sprites");
        EnsureFolder("Assets/Art/Sprites", "Characters");
        EnsureFolder("Assets/Art/Sprites/Characters", "Enemies");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies", "Wizard");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Wizard", "Idle");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Wizard", "Attack1");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Wizard", "Attack2");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Wizard", "Portal");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Enemies");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Sprite[] ImportNumberedSprites(
        string sourceDir,
        string destFolder,
        string assetPrefix,
        string obsoleteGlob)
    {
        string[] files = Directory.GetFiles(sourceDir, "*.png");
        var ordered = new List<(int order, string path)>();

        for (int i = 0; i < files.Length; i++)
        {
            string stem = Path.GetFileNameWithoutExtension(files[i]);
            Match match = Regex.Match(stem, @"(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int order))
            {
                continue;
            }

            ordered.Add((order, files[i]));
        }

        ordered.Sort((a, b) => a.order.CompareTo(b.order));

        var keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sprites = new List<Sprite>();
        for (int i = 0; i < ordered.Count; i++)
        {
            string assetBaseName = $"{assetPrefix}{ordered[i].order:D3}";
            keepNames.Add(assetBaseName + ".png");
            Sprite sprite = ImportOne(ordered[i].path, destFolder, assetBaseName);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        DeleteObsoleteAssets(destFolder, obsoleteGlob, keepNames);
        return sprites.ToArray();
    }

    private static void DeleteObsoleteAssets(
        string destFolder,
        string obsoleteGlob,
        HashSet<string> keepNames)
    {
        string relative = destFolder.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar);
        string absFolder = Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        if (!Directory.Exists(absFolder))
        {
            return;
        }

        string pattern = obsoleteGlob.Contains("/")
            ? Path.GetFileName(obsoleteGlob)
            : obsoleteGlob;
        string[] existing = Directory.GetFiles(absFolder, pattern);
        for (int i = 0; i < existing.Length; i++)
        {
            string fileName = Path.GetFileName(existing[i]);
            if (keepNames.Contains(fileName))
            {
                continue;
            }

            AssetDatabase.DeleteAsset($"{destFolder}/{fileName}");
        }
    }

    private static Sprite ImportOne(string sourcePath, string destFolder, string assetBaseName)
    {
        string destPath = $"{destFolder}/{assetBaseName}.png";
        File.Copy(sourcePath, destPath, overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer == null)
        {
            return null;
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

        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
    }

    private static GameObject BuildOrUpdatePrefab(
        Sprite[] idleFrames,
        Sprite[] attack1Frames,
        Sprite[] attack2Frames,
        Sprite[] portalFrames,
        GameObject entPrefab,
        GameObject mushomorPrefab)
    {
        Vector2 portal1 = Portal1World;
        Vector2 portal2 = Portal2World;
        TryReadExistingPortalPositions(ref portal1, ref portal2);

        Sprite first = idleFrames[0];
        GameObject root = new GameObject("Enemy_Wizard");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = first;
        renderer.sortingOrder = 3;
        renderer.flipX = true;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;

        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        if (first != null)
        {
            Bounds bounds = first.bounds;
            box.size = bounds.size;
            box.offset = bounds.center;
        }

        var health = root.AddComponent<Health>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = WizardMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = true;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        var idle = root.AddComponent<WizardIdleEnemy2D>();
        ApplyIdle(idle, idleFrames);

        var sight = root.AddComponent<WizardPlayerSight2D>();
        ApplySight(sight);

        var attack = root.AddComponent<WizardAttackAnimator2D>();
        var portals = root.AddComponent<WizardPortalSpawner2D>();
        EnsurePortalSpawnerScriptReference(portals);
        ApplyAttack(attack, attack1Frames, attack2Frames);
        ApplyPortals(portals, portalFrames, entPrefab, mushomorPrefab, portal1, portal2);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void PlaceInPrototypeScene(
        GameObject prefab,
        Sprite[] idleFrames,
        Sprite[] attack1Frames,
        Sprite[] attack2Frames,
        Sprite[] portalFrames,
        GameObject entPrefab,
        GameObject mushomorPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Clean leftover runtime portal roots from prior Play Mode / setups.
        GameObject oldPortals = GameObject.Find("Wizard_Portals");
        if (oldPortals != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPortals);
        }

        Vector2 portal1 = Portal1World;
        Vector2 portal2 = Portal2World;
        Vector3 keepPosition = ScenePosition;

        GameObject existing = GameObject.Find("Enemy_Wizard");
        if (existing != null)
        {
            keepPosition = existing.transform.position;
            var existingPortals = existing.GetComponent<WizardPortalSpawner2D>();
            if (existingPortals != null)
            {
                portal1 = existingPortals.Portal1Position;
                portal2 = existingPortals.Portal2Position;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(existing);
            UnityEngine.Object.DestroyImmediate(existing);
        }
        else
        {
            TryReadExistingPortalPositions(ref portal1, ref portal2);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Wizard";
        instance.transform.position = keepPosition;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(instance);

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.flipX = true;
            if (idleFrames != null && idleFrames.Length > 0)
            {
                renderer.sprite = idleFrames[0];
            }
        }

        var portals = instance.GetComponent<WizardPortalSpawner2D>();
        if (portals == null)
        {
            portals = instance.AddComponent<WizardPortalSpawner2D>();
        }

        EnsurePortalSpawnerScriptReference(portals);
        ApplyIdle(instance.GetComponent<WizardIdleEnemy2D>(), idleFrames);
        ApplySight(instance.GetComponent<WizardPlayerSight2D>());
        ApplyAttack(instance.GetComponent<WizardAttackAnimator2D>(), attack1Frames, attack2Frames);
        ApplyPortals(portals, portalFrames, entPrefab, mushomorPrefab, portal1, portal2);

        var health = instance.GetComponent<Health>();
        if (health == null)
        {
            health = instance.AddComponent<Health>();
        }

        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = WizardMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = true;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ApplyIdle(WizardIdleEnemy2D idle, Sprite[] idleFrames)
    {
        if (idle == null)
        {
            return;
        }

        idle.EditorAssignFrames(idleFrames);
        SerializedObject so = new SerializedObject(idle);
        so.FindProperty("frameRate").floatValue = FrameRate;
        so.FindProperty("flipX").boolValue = true;
        so.FindProperty("pingPong").boolValue = true;
        AssignSpriteArray(so, "idleFrames", idleFrames);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplySight(WizardPlayerSight2D sight)
    {
        if (sight == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(sight);
        so.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
        so.FindProperty("useWorldXActivationGate").boolValue = UseWorldXActivationGate;
        so.FindProperty("activationMinTargetWorldX").floatValue = ActivationMinTargetWorldX;
        so.FindProperty("sightRange").floatValue = SightRange;
        so.FindProperty("useHorizontalDistanceOnly").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyAttack(
        WizardAttackAnimator2D attack,
        Sprite[] attack1Frames,
        Sprite[] attack2Frames)
    {
        if (attack == null)
        {
            return;
        }

        attack.EditorAssignFrames(attack1Frames, attack2Frames);
        SerializedObject so = new SerializedObject(attack);
        so.FindProperty("frameRate").floatValue = FrameRate;
        so.FindProperty("flipX").boolValue = true;
        so.FindProperty("loopStartFrameNumber").intValue = LoopStartFrameNumber;
        so.FindProperty("loopEndFrameNumber").intValue = LoopEndFrameNumber;
        AssignSpriteArray(so, "attack1Frames", attack1Frames);
        AssignSpriteArray(so, "attack2Frames", attack2Frames);

        so.FindProperty("spriteRenderer").objectReferenceValue =
            attack.GetComponent<SpriteRenderer>();
        so.FindProperty("idleEnemy").objectReferenceValue =
            attack.GetComponent<WizardIdleEnemy2D>();
        so.FindProperty("playerSight").objectReferenceValue =
            attack.GetComponent<WizardPlayerSight2D>();

        var portals = attack.GetComponent<WizardPortalSpawner2D>();
        if (portals != null)
        {
            so.FindProperty("portalSpawner").objectReferenceValue = portals;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyPortals(
        WizardPortalSpawner2D portals,
        Sprite[] portalFrames,
        GameObject entPrefab,
        GameObject mushomorPrefab,
        Vector2 portal1World,
        Vector2 portal2World)
    {
        if (portals == null)
        {
            Debug.LogError(
                "WizardEnemySetupEditor: WizardPortalSpawner2D missing on Enemy_Wizard — " +
                "ensure WizardPortalSpawner2D.cs exists (one MonoBehaviour per file).");
            return;
        }

        EnsurePortalSpawnerScriptReference(portals);

        portals.EditorAssign(
            portalFrames,
            entPrefab,
            mushomorPrefab,
            FrameRate,
            PortalScale,
            EntSpawnInterval,
            MaxEntsPerPortal,
            MaxMushomorsPerPortal,
            SpawnEntImmediately);
        portals.EditorAssignPortalPositions(portal1World, portal2World);

        SerializedObject so = new SerializedObject(portals);
        // Force correct MonoScript GUID (repairs Missing Script / wrong GUID).
        MonoScript mono = MonoScript.FromMonoBehaviour(portals);
        if (mono != null)
        {
            so.FindProperty("m_Script").objectReferenceValue = mono;
        }

        AssignSpriteArray(so, "portalFrames", portalFrames);
        so.FindProperty("portalFrameRate").floatValue = FrameRate;
        so.FindProperty("portalScale").floatValue = PortalScale;
        so.FindProperty("portalSortingOrder").intValue = PortalSortingOrder;
        so.FindProperty("portal1Position").vector2Value = portal1World;
        so.FindProperty("portal2Position").vector2Value = portal2World;
        so.FindProperty("entPrefab").objectReferenceValue = entPrefab;
        so.FindProperty("mushomorPrefab").objectReferenceValue = mushomorPrefab;
        so.FindProperty("entSpawnInterval").floatValue = EntSpawnInterval;
        so.FindProperty("maxEnts").intValue = MaxEntsPerPortal;
        so.FindProperty("maxMushomors").intValue = MaxMushomorsPerPortal;
        so.FindProperty("spawnEntImmediately").boolValue = SpawnEntImmediately;
        so.FindProperty("entSpawnOffset").vector2Value = Vector2.zero;

        var attack = portals.GetComponent<WizardAttackAnimator2D>();
        if (attack != null)
        {
            so.FindProperty("attackAnimator").objectReferenceValue = attack;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(portals);
    }

    private static void EnsurePortalSpawnerScriptReference(WizardPortalSpawner2D portals)
    {
        if (portals == null)
        {
            return;
        }

        MonoScript expected = MonoScript.FromMonoBehaviour(portals);
        if (expected == null)
        {
            expected = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Scripts/Enemies/WizardPortalSpawner2D.cs");
        }

        if (expected == null)
        {
            Debug.LogError("WizardEnemySetupEditor: could not load WizardPortalSpawner2D MonoScript.");
            return;
        }

        SerializedObject so = new SerializedObject(portals);
        SerializedProperty scriptProp = so.FindProperty("m_Script");
        if (scriptProp != null && scriptProp.objectReferenceValue != expected)
        {
            scriptProp.objectReferenceValue = expected;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void TryReadExistingPortalPositions(ref Vector2 portal1, ref Vector2 portal2)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab == null)
        {
            return;
        }

        var portals = existingPrefab.GetComponent<WizardPortalSpawner2D>();
        if (portals == null)
        {
            return;
        }

        portal1 = portals.Portal1Position;
        portal2 = portals.Portal2Position;
    }

    private static void AssignSpriteArray(SerializedObject so, string propertyName, Sprite[] frames)
    {
        SerializedProperty array = so.FindProperty(propertyName);
        array.arraySize = frames != null ? frames.Length : 0;
        for (int i = 0; i < array.arraySize; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
