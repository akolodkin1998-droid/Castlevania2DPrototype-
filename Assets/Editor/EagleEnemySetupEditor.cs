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
/// Imports eagle flight/attack/projectile sprites from Desktop/Персонаж/Орёл
/// and places Enemy_Eagle in Prototype with dive-bomb attack loop.
/// Trigger: Tools menu or Temp/setup_eagle_enemy.flag (auto on domain reload).
/// </summary>
[InitializeOnLoad]
public static class EagleEnemySetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Characters/Enemies/Eagle";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Eagle.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string SetupFlagPath = "Temp/setup_eagle_enemy.flag";
    private const string SetupResultPath = "Temp/setup_eagle_enemy_result.txt";
    private const string ProjectileFileName = "1785035226_6a6579da7b33a-Photoroom.png";
    private const string FireFrameFileName = "9-Photoroom (2).png";
    private const int EagleMaxHealth = 40;
    private const int ContactDamage = 10;
    private const int ProjectileDamage = 10;
    private const float VisualScale = 0.14f;

    static EagleEnemySetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
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

        // Consume flag first so a failing run cannot re-fire on every delayCall / domain reload.
        File.Delete(SetupFlagPath);
        try
        {
            string result = SetupEagleEnemy();
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
            Debug.LogWarning("Could not write eagle setup result: " + writeEx.Message);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Eagle Enemy")]
    public static void SetupEagleEnemyMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Eagle Enemy", SetupEagleEnemy(), "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Eagle Enemy", "Failed:\n" + ex.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupEagleEnemy());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static void RequestSetup()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(SetupFlagPath, DateTime.UtcNow.ToString("O"));
        TrySetupFromFlag();
    }

    public static string SetupEagleEnemy()
    {
        string sourceDir = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException("Eagle folder not found. Expected Desktop/Персонаж/Орёл");
        }

        string attackDir = Path.Combine(sourceDir, "Атака");
        if (!Directory.Exists(attackDir))
        {
            throw new DirectoryNotFoundException("Attack folder not found: " + attackDir);
        }

        string flightDir = ResolveFlightFolder(sourceDir);
        EnsureFolders();
        AssetDatabase.Refresh();

        // Flight lives in Орёл/Плёт (or Полёт), never in the eagle root (root is projectile-only).
        Sprite[] flight = ImportFlightSpritesPreferAssets(flightDir);

        Sprite[] attackApproach = ImportAttackApproach(attackDir, out int fireFrameIndex);
        Sprite[] attackExit = ImportAttackExit(attackDir);
        Sprite projectile = ImportProjectile(sourceDir);

        if (flight == null || flight.Length < 2)
        {
            throw new InvalidOperationException(
                "No flight frames found. Expected numbered PNGs under Орёл/Плёт (or Полёт), " +
                "or existing Eagle_Flight_*.png under " + DestFolder +
                (string.IsNullOrEmpty(flightDir) ? " (Desktop flight folder missing)." : " (tried: " + flightDir + ")."));
        }

        if (attackApproach == null || attackApproach.Length == 0)
        {
            throw new InvalidOperationException("No attack approach frames found in Атака.");
        }

        if (attackExit == null || attackExit.Length == 0)
        {
            throw new InvalidOperationException("No attack exit frames (16..26) found in Атака.");
        }

        if (projectile == null)
        {
            throw new FileNotFoundException("Projectile sprite missing: " + ProjectileFileName);
        }

        if (fireFrameIndex < 0)
        {
            throw new InvalidOperationException("Fire frame not found: " + FireFrameFileName);
        }

        GameObject prefab = BuildOrUpdatePrefab(flight, attackApproach, attackExit, projectile, fireFrameIndex);
        PlaceInPrototypeScene(prefab, flight);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string flightSource = !string.IsNullOrEmpty(flightDir) ? flightDir : DestFolder + " (Assets fallback)";
        return
            $"Eagle attack loop ready.\nFlight: {flight.Length} from {flightSource}\n" +
            $"Attack approach: {attackApproach.Length} (fire index {fireFrameIndex} = frame 9)\n" +
            $"Attack exit: {attackExit.Length} (16..26)\nProjectile: yes\n" +
            $"HP {EagleMaxHealth}, projectile dmg {ProjectileDamage}\n" +
            $"Prefab: {PrefabPath}\n" +
            "Cycle: cruise at world Y -32 → attack near player → shoot → exit → re-enter opposite side.";
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Орёл"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Орёл"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Орёл",
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

    /// <summary>
    /// Flight frames are under Орёл/Плёт (user spelling). Alternate: Полёт.
    /// </summary>
    private static string ResolveFlightFolder(string sourceDir)
    {
        if (string.IsNullOrEmpty(sourceDir))
        {
            return null;
        }

        string[] names = { "Плёт", "Полёт" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Path.Combine(sourceDir, names[i]);
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string AbsoluteDestFolder()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "Art/Sprites/Characters/Enemies/Eagle"));
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Sprites");
        EnsureFolder("Assets/Art/Sprites", "Characters");
        EnsureFolder("Assets/Art/Sprites/Characters", "Enemies");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies", "Eagle");
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

    /// <summary>
    /// Imports numbered flight frames from Орёл/Плёт as Eagle_Flight_XX.png.
    /// Always falls back to already-imported Assets/Eagle_Flight_*.png when Desktop yields &lt; 2.
    /// </summary>
    private static Sprite[] ImportFlightSpritesPreferAssets(string flightDir)
    {
        Sprite[] fromDesktop = null;
        if (!string.IsNullOrEmpty(flightDir) && Directory.Exists(flightDir))
        {
            fromDesktop = ImportNumberedSprites(
                flightDir,
                "Eagle_Flight_",
                excludeProjectile: true,
                useIndexSuffix: false);
            Debug.Log($"Eagle flight from Desktop ({flightDir}): {(fromDesktop != null ? fromDesktop.Length : 0)}");
        }
        else
        {
            Debug.LogWarning("Eagle flight Desktop folder missing (expected Орёл/Плёт or Полёт). Using Assets fallback.");
        }

        if (fromDesktop != null && fromDesktop.Length >= 2)
        {
            return fromDesktop;
        }

        Sprite[] fromAssets = LoadExistingFlightSpritesFromAssets();
        if (fromAssets != null && fromAssets.Length >= 2)
        {
            return fromAssets;
        }

        return fromDesktop != null && fromDesktop.Length > 0 ? fromDesktop : fromAssets;
    }

    private static Sprite[] ImportNumberedSprites(
        string directory,
        string assetPrefix,
        bool excludeProjectile = false,
        bool useIndexSuffix = true)
    {
        string[] files = Directory.GetFiles(directory, "*.png");
        var ordered = new List<(int order, string name, string path)>();

        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            if (excludeProjectile &&
                string.Equals(name, ProjectileFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match match = Regex.Match(Path.GetFileNameWithoutExtension(name), @"^(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int order))
            {
                continue;
            }

            // Ignore projectile-style hash prefixes.
            if (order > 1000)
            {
                continue;
            }

            ordered.Add((order, name, files[i]));
        }

        ordered.Sort((a, b) =>
        {
            int byOrder = a.order.CompareTo(b.order);
            return byOrder != 0 ? byOrder : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        var sprites = new List<Sprite>();
        for (int i = 0; i < ordered.Count; i++)
        {
            string assetName = useIndexSuffix
                ? $"{assetPrefix}{ordered[i].order:D2}_{i:D2}"
                : $"{assetPrefix}{ordered[i].order:D2}";
            Sprite sprite = ImportOne(ordered[i].path, assetName);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static Sprite[] LoadExistingFlightSpritesFromAssets()
    {
        var ordered = new List<(int order, string assetPath)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string absFolder = AbsoluteDestFolder();
        if (Directory.Exists(absFolder))
        {
            string[] files = Directory.GetFiles(absFolder, "Eagle_Flight_*.png");
            for (int i = 0; i < files.Length; i++)
            {
                TryAddFlightAssetPath(Path.GetFileName(files[i]), ordered, seen);
            }
        }

        // Also discover via AssetDatabase in case disk scan missed anything.
        string[] guids = AssetDatabase.FindAssets("Eagle_Flight_ t:Texture2D", new[] { DestFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TryAddFlightAssetPath(Path.GetFileName(assetPath), ordered, seen);
        }

        ordered.Sort((a, b) => a.order.CompareTo(b.order));

        var sprites = new List<Sprite>();
        for (int i = 0; i < ordered.Count; i++)
        {
            string assetPath = ordered[i].assetPath;
            EnsureSpriteImporter(assetPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                // Force reimport once more if Load returned null (wrong texture type).
                ConfigureSpriteImporter(assetPath);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        Debug.Log($"Eagle flight from Assets: {sprites.Count} / {ordered.Count}");
        return sprites.ToArray();
    }

    private static void TryAddFlightAssetPath(
        string fileName,
        List<(int order, string assetPath)> ordered,
        HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(fileName) || !seen.Add(fileName))
        {
            return;
        }

        string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
        Match match = Regex.Match(fileNameNoExt, @"^Eagle_Flight_(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int order) || order > 1000)
        {
            return;
        }

        string assetPath = (DestFolder + "/" + fileName).Replace('\\', '/');
        ordered.Add((order, assetPath));
    }

    private static void EnsureSpriteImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        if (importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single)
        {
            return;
        }

        ConfigureSpriteImporter(assetPath);
    }

    private static Sprite[] ImportAttackApproach(string attackDir, out int fireFrameIndex)
    {
        string[] files = Directory.GetFiles(attackDir, "*.png");
        var ordered = new List<(int order, string name, string path)>();
        fireFrameIndex = -1;

        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"^(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int order))
            {
                continue;
            }

            if (order >= 16)
            {
                continue;
            }

            ordered.Add((order, fileName, files[i]));
        }

        ordered.Sort((a, b) =>
        {
            int byOrder = a.order.CompareTo(b.order);
            return byOrder != 0 ? byOrder : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        var sprites = new List<Sprite>();
        for (int i = 0; i < ordered.Count; i++)
        {
            Sprite sprite = ImportOne(ordered[i].path, $"Eagle_Attack_{ordered[i].order:D2}_{i:D2}");
            if (sprite == null)
            {
                continue;
            }

            sprites.Add(sprite);
            if (string.Equals(ordered[i].name, FireFrameFileName, StringComparison.OrdinalIgnoreCase))
            {
                fireFrameIndex = sprites.Count - 1;
            }
            else if (fireFrameIndex < 0 && ordered[i].order == 9)
            {
                fireFrameIndex = sprites.Count - 1;
            }
        }

        return sprites.ToArray();
    }

    private static Sprite[] ImportAttackExit(string attackDir)
    {
        string[] files = Directory.GetFiles(attackDir, "*.png");
        var ordered = new List<(int order, string name, string path)>();

        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"^(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int order))
            {
                continue;
            }

            if (order < 16 || order > 26)
            {
                continue;
            }

            ordered.Add((order, fileName, files[i]));
        }

        ordered.Sort((a, b) => a.order.CompareTo(b.order));

        var sprites = new List<Sprite>();
        for (int i = 0; i < ordered.Count; i++)
        {
            Sprite sprite = ImportOne(ordered[i].path, $"Eagle_Exit_{ordered[i].order:D2}");
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static Sprite ImportProjectile(string sourceDir)
    {
        string path = Path.Combine(sourceDir, ProjectileFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return ImportOne(path, "Eagle_Projectile");
    }

    private static Sprite ImportOne(string sourcePath, string assetBaseName)
    {
        string destPath = $"{DestFolder}/{assetBaseName}.png";
        File.Copy(sourcePath, destPath, overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
        ConfigureSpriteImporter(destPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
    }

    private static void ConfigureSpriteImporter(string assetPath)
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
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void AssignSpriteArray(SerializedObject so, string propertyName, Sprite[] sprites)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static GameObject BuildOrUpdatePrefab(
        Sprite[] flight,
        Sprite[] attackApproach,
        Sprite[] attackExit,
        Sprite projectile,
        int fireFrameIndex)
    {
        GameObject root = new GameObject("Enemy_Eagle");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = flight[0];
        renderer.sortingOrder = 4;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        Bounds bounds = flight[0].bounds;
        box.size = bounds.size * 0.7f;
        box.offset = bounds.center;

        var health = root.AddComponent<Health>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = EagleMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = true;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        var contact = root.AddComponent<ContactDamage2D>();
        SerializedObject contactSo = new SerializedObject(contact);
        contactSo.FindProperty("damage").intValue = ContactDamage;
        contactSo.FindProperty("hitCooldown").floatValue = 0.6f;
        contactSo.ApplyModifiedPropertiesWithoutUndo();

        var eagle = root.AddComponent<EagleFlyerEnemy2D>();
        eagle.EditorAssignSprites(flight, attackApproach, attackExit, projectile, fireFrameIndex);

        SerializedObject eagleSo = new SerializedObject(eagle);
        AssignSpriteArray(eagleSo, "flightFrames", flight);
        AssignSpriteArray(eagleSo, "attackApproachFrames", attackApproach);
        AssignSpriteArray(eagleSo, "attackExitFrames", attackExit);
        eagleSo.FindProperty("projectileSprite").objectReferenceValue = projectile;
        eagleSo.FindProperty("projectileFireFrameIndex").intValue = fireFrameIndex;
        eagleSo.FindProperty("frameRate").floatValue = 12f;
        eagleSo.FindProperty("pingPongFlight").boolValue = true;
        eagleSo.FindProperty("cruiseSpeed").floatValue = 4.5f;
        eagleSo.FindProperty("exitSpeed").floatValue = 7.5f;
        eagleSo.FindProperty("cruiseWorldY").floatValue = -32f;
        eagleSo.FindProperty("attackTriggerDistanceX").floatValue = 2.8f;
        eagleSo.FindProperty("projectileDamage").intValue = ProjectileDamage;
        eagleSo.FindProperty("projectileSpeed").floatValue = 7f;
        eagleSo.FindProperty("projectileScale").floatValue = 0.12f;
        eagleSo.FindProperty("projectileColliderRadius").floatValue = 0.12f;
        eagleSo.FindProperty("contactDamage").intValue = ContactDamage;
        eagleSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefabAsset;
    }

    private static void PlaceInPrototypeScene(GameObject prefab, Sprite[] flight)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Enemy_Eagle");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Eagle";
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        GameObject player = GameObject.Find("Player_HeroKnight");
        Vector3 position = new Vector3(-8.5f, -32f, 0f);
        if (player != null)
        {
            position = new Vector3(player.transform.position.x, -32f, 0f);

            var eagle = instance.GetComponent<EagleFlyerEnemy2D>();
            if (eagle != null)
            {
                SerializedObject so = new SerializedObject(eagle);
                so.FindProperty("target").objectReferenceValue = player.transform;
                so.FindProperty("cruiseWorldY").floatValue = -32f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        instance.transform.position = position;
        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null && flight != null && flight.Length > 0)
        {
            renderer.sprite = flight[0];
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
