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

public static class GiantLikhoSetupEditor
{
    private const string SourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Хотьба";
    private const string AttackStartupSourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Атака1\Начало";
    private const string ChargeLoopSourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Атака1\Бег цикл";
    private const string WallImpactSourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Атака1\Удар об стену";
    private const string JumpPrepareSourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Атака2\Подготовка к прыжку";
    private const string JumpSourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Огромное Лихо\Атака2\Прыжок";
    private const string DestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Walk";
    private const string AttackStartupDestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Attack1/Startup";
    private const string ChargeLoopDestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Attack1/Charge";
    private const string WallImpactDestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Attack1/WallImpact";
    private const string JumpPrepareDestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Attack2/Prepare";
    private const string JumpDestinationFolder =
        "Assets/Art/Sprites/Characters/Enemies/GiantLikho/Attack2/Jump";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_GiantLikho.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string ObjectName = "ОгромноеЛихо";
    private const int FrameCount = 15;
    private const int MaxHealth = 200;
    private const float VisualScale = 1.35f;
    private static readonly Vector3 ScenePosition = new Vector3(14.5f, -65.7f, 0f);

    [MenuItem("Tools/Castlevania 2D/Setup Giant Likho Miniboss")]
    public static void SetupFromMenu()
    {
        try
        {
            Debug.Log(Setup());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static string Setup()
    {
        EnsureFolder(DestinationFolder);
        EnsureFolder(AttackStartupDestinationFolder);
        EnsureFolder(ChargeLoopDestinationFolder);
        EnsureFolder(WallImpactDestinationFolder);
        EnsureFolder(JumpPrepareDestinationFolder);
        EnsureFolder(JumpDestinationFolder);
        EnsureFolder("Assets/Prefabs/Enemies");

        Sprite[] frames = ImportWalkFrames();
        Sprite[] startupFrames = ImportNumberedFrames(
            AttackStartupSourceFolder,
            AttackStartupDestinationFolder,
            "GiantLikho_Attack1_Startup_",
            expectedFrameCount: 10);
        Sprite[] chargeFrames = ImportNumberedFrames(
            ChargeLoopSourceFolder,
            ChargeLoopDestinationFolder,
            "GiantLikho_Attack1_Charge_",
            expectedFrameCount: 8);
        Sprite[] impactFrames = ImportNumberedFrames(
            WallImpactSourceFolder,
            WallImpactDestinationFolder,
            "GiantLikho_Attack1_WallImpact_",
            expectedFrameCount: 9);
        Sprite[] jumpPrepareFrames = ImportNumberedFrames(
            JumpPrepareSourceFolder,
            JumpPrepareDestinationFolder,
            "GiantLikho_Attack2_Prepare_",
            expectedFrameCount: 11);
        Sprite[] jumpFrames = ImportNumberedFrames(
            JumpSourceFolder,
            JumpDestinationFolder,
            "GiantLikho_Attack2_Jump_",
            expectedFrameCount: 17);

        GameObject prefab = BuildPrefab(
            frames,
            startupFrames,
            chargeFrames,
            impactFrames,
            jumpPrepareFrames,
            jumpFrames);
        PlaceInPrototype(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return $"Giant Likho ready: walk {frames.Length}, startup {startupFrames.Length}, " +
               $"charge {chargeFrames.Length}, impact {impactFrames.Length}, " +
               $"jump prepare {jumpPrepareFrames.Length}, jump {jumpFrames.Length}.";
    }

    private static Sprite[] ImportWalkFrames()
    {
        var frames = new List<Sprite>(FrameCount);
        for (int i = 0; i < FrameCount; i++)
        {
            string sourcePath = Path.Combine(
                SourceFolder,
                $"Лихо Хотьба_{i + 1:D4}.png");
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Missing Giant Likho frame {i + 1:D4}.",
                    sourcePath);
            }

            string destinationPath =
                $"{DestinationFolder}/GiantLikho_Walk_{i + 1:D3}.png";
            string absoluteDestination = Path.GetFullPath(destinationPath);
            File.Copy(sourcePath, absoluteDestination, true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"Could not import Giant Likho sprite: {destinationPath}");
            }

            frames.Add(sprite);
        }

        return frames.ToArray();
    }

    private static Sprite[] ImportNumberedFrames(
        string sourceFolder,
        string destinationFolder,
        string destinationPrefix,
        int expectedFrameCount)
    {
        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png");
        Array.Sort(sourceFiles, (left, right) =>
            GetFrameNumber(left).CompareTo(GetFrameNumber(right)));

        if (sourceFiles.Length != expectedFrameCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedFrameCount} PNG frames in {sourceFolder}, " +
                $"found {sourceFiles.Length}.");
        }

        var frames = new List<Sprite>(sourceFiles.Length);
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destinationPath =
                $"{destinationFolder}/{destinationPrefix}{i + 1:D3}.png";
            File.Copy(sourceFiles[i], Path.GetFullPath(destinationPath), true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"Could not import Giant Likho sprite: {destinationPath}");
            }

            frames.Add(sprite);
        }

        return frames.ToArray();
    }

    private static int GetFrameNumber(string path)
    {
        Match match = Regex.Match(
            Path.GetFileNameWithoutExtension(path),
            @"(\d+)\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value, out int number)
            ? number
            : int.MaxValue;
    }

    private static GameObject BuildPrefab(
        Sprite[] frames,
        Sprite[] startupFrames,
        Sprite[] chargeFrames,
        Sprite[] impactFrames,
        Sprite[] jumpPrepareFrames,
        Sprite[] jumpFrames)
    {
        var root = new GameObject(ObjectName);
        try
        {
            root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 3;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(1.5f, 2f);
            collider.offset = new Vector2(0f, 1f);

            Health health = root.AddComponent<Health>();
            var healthSerialized = new SerializedObject(health);
            healthSerialized.FindProperty("maxHealth").intValue = MaxHealth;
            healthSerialized.FindProperty("destroyOnDeath").boolValue = true;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();

            GiantLikhoEnemy2D controller = root.AddComponent<GiantLikhoEnemy2D>();
            controller.EditorAssignFrames(
                frames,
                startupFrames,
                chargeFrames,
                impactFrames,
                jumpPrepareFrames,
                jumpFrames);

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void PlaceInPrototype(GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null
                && transforms[i].name == ObjectName
                && transforms[i].gameObject.scene == scene)
            {
                UnityEngine.Object.DestroyImmediate(transforms[i].gameObject);
            }
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate Giant Likho prefab.");
        }

        instance.name = ObjectName;
        instance.transform.position = ScenePosition;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
