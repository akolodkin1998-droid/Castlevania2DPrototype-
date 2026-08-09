using System;
using System.Collections.Generic;
using System.IO;
using Castlevania2D.Enemies;
using Castlevania2D.Movement;
using BossHealthComponent = global::Castlevania2D.Health.Health;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class FlowerBossTentacleSetup
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string BossObjectName = "Boss_Flower";
    private const string TentacleSpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Tentacles";
    private const string TentacleSpritePrefix = "FlowerBoss_Tentacle_";
    private const string SourceSpritesFolder = @"C:\Users\Bensh\OneDrive\Рабочий стол\Босс\Щупальца из стен";
    private const string SetupFlagPath = "Temp/setup_flower_boss_tentacles.flag";
    private const int BossHealth = 300;

    static FlowerBossTentacleSetup()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SetupFlagPath))
        {
            return;
        }

        File.Delete(SetupFlagPath);
        Setup();
    }

    [MenuItem("Tools/Castlevania 2D/Setup Flower Boss Tentacle Attack")]
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

        ImportTentacleSpritesFromSource();
        ConfigureTentacleSpriteImports();
        AssetDatabase.Refresh();

        Sprite[] tentacleFrames = LoadTentacleSprites();
        if (tentacleFrames.Length == 0)
        {
            throw new InvalidOperationException(
                $"No tentacle sprites found in {TentacleSpritesFolder}. Check import settings.");
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject boss = GameObject.Find(BossObjectName);
        if (boss == null)
        {
            throw new InvalidOperationException($"Boss object '{BossObjectName}' was not found in the scene.");
        }

        ConfigureBossForTentacleAttack(boss, tentacleFrames);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Flower Boss tentacle attack ready: {tentacleFrames.Length} frames, " +
            "4 wall (LeftWall + RightWall) + 3 Ground spawns on boss hit.");
    }

    private static void ImportTentacleSpritesFromSource()
    {
        if (!Directory.Exists(SourceSpritesFolder))
        {
            Debug.LogWarning($"Tentacle source folder not found: {SourceSpritesFolder}");
            return;
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Tentacles");

        FileInfo[] sourceFiles = new DirectoryInfo(SourceSpritesFolder)
            .GetFiles("*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(sourceFiles, (left, right) =>
            ExtractFrameNumber(left.Name).CompareTo(ExtractFrameNumber(right.Name)));

        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destinationName = $"{TentacleSpritePrefix}{i + 1:D3}.png";
            string destinationPath = Path.Combine(TentacleSpritesFolder, destinationName);
            File.Copy(sourceFiles[i].FullName, destinationPath, overwrite: true);
        }
    }

    private static void ConfigureTentacleSpriteImports()
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TentacleSpritesFolder });

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(TentacleSpritePrefix, StringComparison.OrdinalIgnoreCase))
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
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureBossForTentacleAttack(GameObject boss, Sprite[] tentacleFrames)
    {
        CapsuleCollider2D existingCapsule = boss.GetComponent<CapsuleCollider2D>();
        if (existingCapsule != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCapsule);
        }

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(boss);
        FlowerBossHitboxFit2D hitboxFit = GetOrAddComponent<FlowerBossHitboxFit2D>(boss);
        SerializedObject serializedHitbox = new SerializedObject(hitboxFit);
        serializedHitbox.FindProperty("hitboxSize").vector2Value = new Vector2(5f, 7f);
        serializedHitbox.FindProperty("hitboxOffset").vector2Value = Vector2.zero;
        serializedHitbox.FindProperty("isTrigger").boolValue = true;
        serializedHitbox.ApplyModifiedPropertiesWithoutUndo();
        hitboxFit.ApplyFit();

        BossHealthComponent health = GetOrAddComponent<BossHealthComponent>(boss);
        SerializedObject serializedHealth = new SerializedObject(health);
        serializedHealth.FindProperty("maxHealth").intValue = BossHealth;
        serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
        serializedHealth.ApplyModifiedPropertiesWithoutUndo();

        Transform tentacleRoot = EnsureTentacleRoot();
        Transform playerTransform = FindPlayerTransform();
        CharacterMotor2D playerMotor = playerTransform != null
            ? playerTransform.GetComponent<CharacterMotor2D>()
            : null;

        FlowerBossTentacleAttack2D tentacleAttack = GetOrAddComponent<FlowerBossTentacleAttack2D>(boss);
        SerializedObject serializedAttack = new SerializedObject(tentacleAttack);
        serializedAttack.FindProperty("leftWallTilemap").objectReferenceValue = FindTilemap("LeftWall");
        SerializedProperty rightWallProp = serializedAttack.FindProperty("rightWallTilemap");
        if (rightWallProp != null)
        {
            rightWallProp.objectReferenceValue = FindTilemap("RightWall");
        }

        SerializedProperty rightWallNameProp = serializedAttack.FindProperty("rightWallObjectName");
        if (rightWallNameProp != null)
        {
            rightWallNameProp.stringValue = "RightWall";
        }

        serializedAttack.FindProperty("groundTilemap").objectReferenceValue = FindTilemap("Ground");
        serializedAttack.FindProperty("tentacleRoot").objectReferenceValue = tentacleRoot;
        serializedAttack.FindProperty("playerTransform").objectReferenceValue = playerTransform;
        serializedAttack.FindProperty("playerMotor").objectReferenceValue = playerMotor;
        AssignSpriteArray(serializedAttack, "tentacleFrames", tentacleFrames);
        serializedAttack.FindProperty("leftWallSpawnCount").intValue = 4;
        serializedAttack.FindProperty("groundSpawnCount").intValue = 3;
        serializedAttack.FindProperty("tentacleDamage").intValue = 5;
        serializedAttack.FindProperty("frameRate").floatValue = 12f;
        serializedAttack.FindProperty("tentacleScale").floatValue = 0.14f;
        serializedAttack.FindProperty("bossHitSpawnCooldown").floatValue = 3f;
        serializedAttack.FindProperty("wallSpawnX").floatValue = -11.4f;
        serializedAttack.FindProperty("rightWallSpawnX").floatValue = 12.4f;
        serializedAttack.FindProperty("groundSpawnY").floatValue = 0.6f;
        serializedAttack.FindProperty("wallSpawnHeightNormalized").floatValue = 0.5f;
        serializedAttack.FindProperty("standTrapDelay").floatValue = 1.5f;
        serializedAttack.FindProperty("standMoveResetDistance").floatValue = 0.75f;
        int playerLayerBits = playerTransform != null
            ? 1 << playerTransform.gameObject.layer
            : 1; // Default layer
        serializedAttack.FindProperty("playerLayerMask").intValue = playerLayerBits;
        serializedAttack.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureTentacleRoot()
    {
        GameObject rootObject = GameObject.Find("FlowerBoss_Tentacles");
        if (rootObject == null)
        {
            rootObject = new GameObject("FlowerBoss_Tentacles");
        }

        return rootObject.transform;
    }

    private static Transform FindPlayerTransform()
    {
        GameObject player = GameObject.Find("Player_HeroKnight");
        return player != null ? player.transform : null;
    }

    private static Sprite[] LoadTentacleSprites()
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TentacleSpritesFolder });
        List<(int index, Sprite sprite)> sprites = new List<(int, Sprite)>();

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(TentacleSpritePrefix, StringComparison.OrdinalIgnoreCase))
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

    private static Tilemap FindTilemap(string objectName)
    {
        GameObject tilemapObject = GameObject.Find(objectName);
        return tilemapObject != null ? tilemapObject.GetComponent<Tilemap>() : null;
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
