using System;
using System.Collections.Generic;
using System.IO;
using Castlevania2D.Health;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports snake sprites from Desktop/Персонаж/Змея and places Enemy_Snake in Prototype.
/// Idle: 9-Photoroom (2). Attack: numbered frames 10..21 (inclusive, missing numbers skipped).
/// Attack plays one ping-pong cycle on contact, then returns to idle. Health max = 1.
/// </summary>
public static class SnakeEnemySetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Characters/Enemies/Snake";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_Snake.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string IdleFileName = "9-Photoroom (2).png";
    private const int AttackFrameMin = 10;
    private const int AttackFrameMax = 21;
    private const int SnakeMaxHealth = 1;
    /// <summary>HD Photoroom frames are ~1920x1080; 0.35/3 ≈ arena-readable small size.</summary>
    private const float VisualScale = 0.11666667f;

    [MenuItem("Tools/Castlevania 2D/Setup Snake Enemy")]
    public static void SetupSnakeEnemyMenu()
    {
        try
        {
            string summary = SetupSnakeEnemy();
            EditorUtility.DisplayDialog("Snake Enemy", summary, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Snake Enemy", "Failed:\n" + ex.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod SnakeEnemySetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            string summary = SetupSnakeEnemy();
            Debug.Log(summary);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupSnakeEnemy()
    {
        string sourceDir = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException(
                "Snake folder not found. Expected Desktop/Персонаж/Змея");
        }

        EnsureFolders();
        ImportSprites(sourceDir, out Sprite idle, out Sprite[] attack);
        DeleteObsoleteAttackAssets(attack);

        if (idle == null)
        {
            throw new FileNotFoundException("Idle sprite missing: " + IdleFileName);
        }

        if (attack == null || attack.Length == 0)
        {
            throw new InvalidOperationException(
                $"No attack frames found in range {AttackFrameMin}..{AttackFrameMax}.");
        }

        GameObject prefabRoot = BuildOrUpdatePrefab(idle, attack);
        PlaceInPrototypeScene(prefabRoot, idle);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            $"Snake ready.\nIdle: {idle.name}\nAttack frames: {attack.Length} " +
            $"({AttackFrameMin}..{AttackFrameMax})\nHealth: {SnakeMaxHealth}\n" +
            $"Attack: one ping-pong then idle\nScale: {VisualScale}\nPrefab: {PrefabPath}\n" +
            "Placed: Enemy_Snake at (-5, -37.2) in Prototype\n" +
            "Contact = attack anim + instant player Kill().";
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Змея"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Змея"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Змея",
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
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
        {
            AssetDatabase.CreateFolder("Assets", "Art");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Sprites"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Sprites");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Sprites/Characters"))
        {
            AssetDatabase.CreateFolder("Assets/Art/Sprites", "Characters");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Sprites/Characters/Enemies"))
        {
            AssetDatabase.CreateFolder("Assets/Art/Sprites/Characters", "Enemies");
        }

        if (!AssetDatabase.IsValidFolder(DestFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Sprites/Characters/Enemies", "Snake");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
        }
    }

    private static void ImportSprites(string sourceDir, out Sprite idle, out Sprite[] attack)
    {
        string[] files = Directory.GetFiles(sourceDir, "*.png");
        var attackList = new List<(int order, string path)>();
        string idleSource = null;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (string.Equals(name, IdleFileName, StringComparison.OrdinalIgnoreCase))
            {
                idleSource = file;
                continue;
            }

            // "10-Photoroom.png" → 10
            string stem = Path.GetFileNameWithoutExtension(name);
            int dash = stem.IndexOf('-');
            string numberPart = dash > 0 ? stem.Substring(0, dash) : stem;
            if (!int.TryParse(numberPart, out int order))
            {
                continue;
            }

            if (order < AttackFrameMin || order > AttackFrameMax)
            {
                continue;
            }

            attackList.Add((order, file));
        }

        attackList.Sort((a, b) => a.order.CompareTo(b.order));

        idle = null;
        if (idleSource != null)
        {
            idle = ImportOne(idleSource, "Snake_Idle");
        }

        var attacks = new List<Sprite>();
        for (int i = 0; i < attackList.Count; i++)
        {
            Sprite sprite = ImportOne(attackList[i].path, $"Snake_Attack_{attackList[i].order:D2}");
            if (sprite != null)
            {
                attacks.Add(sprite);
            }
        }

        attack = attacks.ToArray();
    }

    private static void DeleteObsoleteAttackAssets(Sprite[] keepAttack)
    {
        var keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Snake_Idle.png",
        };

        if (keepAttack != null)
        {
            for (int i = 0; i < keepAttack.Length; i++)
            {
                if (keepAttack[i] != null)
                {
                    keepNames.Add(keepAttack[i].name + ".png");
                }
            }
        }

        string[] existing = Directory.Exists(DestFolder.Replace('/', Path.DirectorySeparatorChar))
            ? Directory.GetFiles(DestFolder.Replace('/', Path.DirectorySeparatorChar), "Snake_Attack_*.png")
            : Array.Empty<string>();

        for (int i = 0; i < existing.Length; i++)
        {
            string fileName = Path.GetFileName(existing[i]);
            if (keepNames.Contains(fileName))
            {
                continue;
            }

            string assetPath = $"{DestFolder}/{fileName}";
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static Sprite ImportOne(string sourcePath, string assetBaseName)
    {
        string destPath = $"{DestFolder}/{assetBaseName}.png";
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

    private static GameObject BuildOrUpdatePrefab(Sprite idle, Sprite[] attack)
    {
        GameObject root = new GameObject("Enemy_Snake");
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = idle;
        renderer.sortingOrder = 3;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;

        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        if (idle != null)
        {
            Bounds bounds = idle.bounds;
            box.size = bounds.size;
            box.offset = bounds.center;
        }

        var health = root.AddComponent<Health>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("maxHealth").intValue = SnakeMaxHealth;
        healthSo.FindProperty("destroyOnDeath").boolValue = true;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        var snake = root.AddComponent<SnakeContactEnemy2D>();
        snake.EditorAssignSprites(idle, attack);

        SerializedObject snakeSo = new SerializedObject(snake);
        snakeSo.FindProperty("loopAttackPingPong").boolValue = false;
        snakeSo.FindProperty("faceLeft").boolValue = true;
        snakeSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void PlaceInPrototypeScene(GameObject prefab, Sprite idle)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Enemy_Snake");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Enemy_Snake";
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        // Etalon Prototype_backup_20260725_1329 — intentional world layout (Y ≈ -37).
        Vector3 position = new Vector3(-5f, -37.2f, 0f);
        GameObject player = GameObject.Find("Player_HeroKnight");
        if (player != null)
        {
            bool faceLeft = player.transform.position.x < position.x;
            var snake = instance.GetComponent<SnakeContactEnemy2D>();
            if (snake != null)
            {
                SerializedObject so = new SerializedObject(snake);
                so.FindProperty("faceLeft").boolValue = faceLeft;
                so.FindProperty("loopAttackPingPong").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = faceLeft;
                if (idle != null)
                {
                    renderer.sprite = idle;
                }
            }
        }

        instance.transform.position = position;

        var health = instance.GetComponent<Health>();
        if (health != null)
        {
            SerializedObject healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").intValue = SnakeMaxHealth;
            healthSo.FindProperty("destroyOnDeath").boolValue = true;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
