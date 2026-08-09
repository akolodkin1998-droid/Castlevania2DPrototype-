using System;
using System.IO;
using Castlevania2D.Health;
using Castlevania2D.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports mob drop sprites, builds pickup prefabs, wires LootDropOnDeath on enemies,
/// and adds PlayerLootInventory to the hero.
/// Menu: Tools → Castlevania 2D → Setup Loot Drops (also Temp/setup_loot_drops.flag).
/// </summary>
[InitializeOnLoad]
public static class LootDropSetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Items/Drops";
    private const string PrefabFolder = "Assets/Prefabs/Items";
    private const string CommonPrefabPath = PrefabFolder + "/Drop_Common.prefab";
    private const string EntPrefabPath = PrefabFolder + "/Drop_Ent.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string SetupFlagPath = "Temp/setup_loot_drops.flag";
    private const string CommonSourceFile = "1785144184_6a672378d0234 (1)-Photoroom.png";
    private const string EntSourceFile = "1785144238_6a6723ae06fda-Photoroom.png";
    private const float CommonPickupScale = 0.08f / 7f;
    private const float BonusPickupScale = 0.064f;
    private const float ColliderRadius = 0.35f;

    static LootDropSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.update += PollSetupFlag;
    }

    private static void PollSetupFlag()
    {
        if (!File.Exists(SetupFlagPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TrySetupFromFlag();
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
            string summary = SetupLootDrops();
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_loot_drops_result.txt", "OK\n" + summary);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_loot_drops_result.txt", "FAIL\n" + ex);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Loot Drops")]
    public static void SetupLootDropsMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Loot Drops", SetupLootDrops(), "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Loot Drops", "Failed:\n" + ex.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupLootDrops());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static string SetupLootDrops()
    {
        string sourceDir = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException("Drop folder not found. Expected Desktop/Персонаж/Дроп");
        }

        EnsureFolders();

        Sprite commonSprite = ImportOne(Path.Combine(sourceDir, CommonSourceFile), "Drop_Common");
        Sprite entSprite = ImportOne(Path.Combine(sourceDir, EntSourceFile), "Drop_Ent");
        if (commonSprite == null || entSprite == null)
        {
            throw new FileNotFoundException("Failed to import drop sprites from " + sourceDir);
        }

        GameObject commonPrefab = BuildPickupPrefab(
            CommonPrefabPath, "Drop_Common", commonSprite, LootItemId.Common,
            CommonPickupScale);
        GameObject entPrefab = BuildPickupPrefab(
            EntPrefabPath, "Drop_Ent", entSprite, LootItemId.Ent,
            BonusPickupScale);

        WirePrototypeScene(commonPrefab, entPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            "Loot drops ready.\n" +
            $"Common: {CommonPrefabPath} (mobs, 0-2 coins)\n" +
            $"Ent bonus: {EntPrefabPath} (Ent only)\n" +
            "Potion: 15% on all wired mobs\n" +
            "Physics: Dynamic Rigidbody2D + gravity + pop impulse\n" +
            "Collect: contact / magnet → PlayerLootInventory\n" +
            "Wired: Snake, Ent, Mushomor, Eagle (+ scene duplicates).\n" +
            "Skipped: Boss_Flower, Enemy_Wizard (portal mage).";
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Дроп"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Дроп"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Дроп",
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
        EnsureFolder("Assets/Art/Sprites", "Items");
        EnsureFolder("Assets/Art/Sprites/Items", "Drops");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Items");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Sprite ImportOne(string sourcePath, string assetBaseName)
    {
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        string destPath = DestFolder + "/" + assetBaseName + ".png";
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
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
    }

    private static GameObject BuildPickupPrefab(
        string prefabPath,
        string objectName,
        Sprite sprite,
        LootItemId itemId,
        float pickupScale)
    {
        GameObject root = new GameObject(objectName);
        root.transform.localScale = new Vector3(pickupScale, pickupScale, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 6;

        var body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 2.2f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sharedMaterial = CreateOrLoadBounceMaterial();

        var circle = root.AddComponent<CircleCollider2D>();
        circle.isTrigger = false;
        circle.radius = ColliderRadius;

        var pickup = root.AddComponent<LootPickup2D>();
        SerializedObject so = new SerializedObject(pickup);
        so.FindProperty("itemId").enumValueIndex = (int)itemId;
        so.FindProperty("collectDelay").floatValue = 0.2f;
        so.FindProperty("magnetRadius").floatValue = 1.6f;
        so.FindProperty("magnetSpeed").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static PhysicsMaterial2D CreateOrLoadBounceMaterial()
    {
        const string matPath = PrefabFolder + "/DropBounce.physicsMaterial2D";
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(matPath);
        if (existing != null)
        {
            return existing;
        }

        var mat = new PhysicsMaterial2D("DropBounce")
        {
            friction = 0.35f,
            bounciness = 0.35f,
        };
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    private static void WirePrototypeScene(GameObject commonPrefab, GameObject entPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player_HeroKnight");
        if (player != null && player.GetComponent<PlayerLootInventory>() == null)
        {
            player.AddComponent<PlayerLootInventory>();
        }

        string[] mobNames =
        {
            "Enemy_Snake",
            "Enemy_Snake (1)",
            "Enemy_Ent",
            "Enemy_Ent (1)",
            "Enemy_Mushomor",
            "Enemy_Eagle",
            "Enemy_EvilWizard",
        };

        for (int i = 0; i < mobNames.Length; i++)
        {
            GameObject mob = GameObject.Find(mobNames[i]);
            if (mob == null)
            {
                continue;
            }

            bool isEnt = mobNames[i].IndexOf("Ent", StringComparison.OrdinalIgnoreCase) >= 0;
            WireLootDrop(mob, commonPrefab, entPrefab, isEnt);
        }

        WireEnemyPrefabs(commonPrefab, entPrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireEnemyPrefabs(GameObject commonPrefab, GameObject entPrefab)
    {
        WirePrefabLoot("Assets/Prefabs/Enemies/Enemy_Snake.prefab", commonPrefab, entPrefab, false);
        WirePrefabLoot("Assets/Prefabs/Enemies/Enemy_Ent.prefab", commonPrefab, entPrefab, true);
        WirePrefabLoot("Assets/Prefabs/Enemies/Enemy_Mushomor.prefab", commonPrefab, entPrefab, false);
        WirePrefabLoot("Assets/Prefabs/Enemies/Enemy_Eagle.prefab", commonPrefab, entPrefab, false);
    }

    private static void WirePrefabLoot(
        string prefabPath,
        GameObject commonPrefab,
        GameObject entPrefab,
        bool isEnt)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            return;
        }

        try
        {
            WireLootDrop(root, commonPrefab, entPrefab, isEnt);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireLootDrop(
        GameObject mob,
        GameObject commonPrefab,
        GameObject entPrefab,
        bool isEnt)
    {
        if (mob.GetComponent<Health>() == null)
        {
            return;
        }

        // Portal mage must not drop loot.
        if (mob.name.StartsWith("Enemy_Wizard", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var drop = mob.GetComponent<LootDropOnDeath>();
        if (drop == null)
        {
            drop = mob.AddComponent<LootDropOnDeath>();
        }

        // Sprites load from Resources/Items at runtime; setup only configures drop rules.
        SerializedObject so = new SerializedObject(drop);
        so.FindProperty("commonMinCount").intValue = 0;
        so.FindProperty("commonMaxCount").intValue = 2;
        so.FindProperty("potionDropChance").floatValue = 0.15f;
        so.FindProperty("dropBonusLoot").boolValue = isEnt;
        so.FindProperty("bonusCount").intValue = 1;
        so.FindProperty("spawnOffset").vector2Value = new Vector2(0f, 0.4f);
        so.FindProperty("commonPickupScale").floatValue = CommonPickupScale;
        so.FindProperty("bonusPickupScale").floatValue = BonusPickupScale;
        so.FindProperty("colliderRadius").floatValue = ColliderRadius;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mob);

        // Keep pickup prefabs referenced for inspection / future Instantiation paths.
        if (commonPrefab != null)
        {
            EditorUtility.SetDirty(commonPrefab);
        }

        if (isEnt && entPrefab != null)
        {
            EditorUtility.SetDirty(entPrefab);
        }
    }
}
