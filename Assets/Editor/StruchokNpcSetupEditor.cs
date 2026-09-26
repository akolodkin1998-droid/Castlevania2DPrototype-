using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Castlevania2D.Hub;
using Castlevania2D.Npcs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports Стручок idle (Покой) and places a friendly NPC in Prototype.
/// Idle plays pairs twice: 1-2-1-2, 3-4-3-4, 5-6-5-6, then repeats.
/// Visual scale is 1/3 of the imported sprite.
/// </summary>
[InitializeOnLoad]
public static class StruchokNpcSetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Characters/Npcs/Struchok/Idle";
    private const string PrefabFolder = "Assets/Prefabs/Npcs";
    private const string PrefabPath = PrefabFolder + "/Npc_Struchok.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string SetupFlagPath = "Temp/setup_struchok_npc.flag";
    private const string ObjectName = "Npc_Struchok";
    private const string PlayerObjectName = "Player_HeroKnight";
    private const float FrameRate = 2f;
    private const float VisualScale = 1f;
    private const int SortingOrder = 3;
    private static readonly Vector3 FallbackOffset = new Vector3(2.4f, 0f, 0f);
    private static readonly Vector3 FallbackPosition = new Vector3(10.7f, -37f, 0f);

    static StruchokNpcSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.update += PollSetupFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Struchok NPC")]
    public static void SetupFromMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Стручок", SetupStruchok(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Стручок", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        string summary = SetupStruchok();
        Debug.Log("[StruchokNpcSetupEditor] " + summary);
    }

    private static void PollSetupFlag()
    {
        if (!File.Exists(SetupFlagPath)
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TrySetupFromFlag();
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SetupFlagPath))
        {
            return;
        }

        File.Delete(SetupFlagPath);
        try
        {
            string summary = SetupStruchok();
            Debug.Log("[StruchokNpcSetupEditor] " + summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_struchok_npc_result.txt", "OK\n" + summary);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_struchok_npc_result.txt", "FAIL\n" + exception);
        }
    }

    public static string SetupStruchok()
    {
        string sourceFolder = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            throw new DirectoryNotFoundException(
                "Struchok idle not found. Expected OneDrive/Рабочий стол/Персонаж/Носильщики/1/Покой.");
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Npcs/Struchok");
        EnsureFolder(DestFolder);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);

        Sprite[] frames = ImportFrames(sourceFolder, DestFolder, "Struchok_Idle_");
        if (frames.Length < 6)
        {
            throw new InvalidOperationException("Need 6 idle PNGs in Покой.");
        }

        GameObject prefab = BuildPrefab(frames);
        PlaceInPrototypeScene(prefab, frames);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "Стручок ready. Idle 1-2-1-2 / 3-4-3-4 / 5-6-5-6, sprites at 300 ppu.\n" +
               $"Prefab: {PrefabPath}";
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Носильщики", "1", "Покой"),
            Path.Combine(desktop, "Персонаж", "Носильщики", "1", "Покой"),
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
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
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

    private static int CompareFrameNames(string left, string right)
    {
        return GetFrameNumber(left).CompareTo(GetFrameNumber(right));
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
        importer.spritePixelsPerUnit = 300f;
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

    private static GameObject BuildPrefab(Sprite[] frames)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureNpc(prefabRoot, frames);
                prefabRoot.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        var root = new GameObject(ObjectName);
        root.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
        ConfigureNpc(root, frames);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureNpc(GameObject root, Sprite[] frames)
    {
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = root.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = frames[0];
        renderer.sortingOrder = SortingOrder;

        PairLoopIdleSprite2D idle = root.GetComponent<PairLoopIdleSprite2D>();
        if (idle == null)
        {
            idle = root.AddComponent<PairLoopIdleSprite2D>();
        }

        idle.AssignFrames(frames, FrameRate);
        SerializedObject idleSo = new SerializedObject(idle);
        SerializedProperty framesProp = idleSo.FindProperty("frames");
        framesProp.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        idleSo.FindProperty("frameRate").floatValue = FrameRate;
        idleSo.FindProperty("pairSize").intValue = 2;
        idleSo.FindProperty("pairPlayCount").intValue = 2;
        idleSo.ApplyModifiedPropertiesWithoutUndo();

        NpcTalk2D talk = root.GetComponent<NpcTalk2D>();
        if (talk == null)
        {
            talk = root.AddComponent<NpcTalk2D>();
        }

        Sprite prompt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/InteractPrompt_F.png");
        talk.EditorAssign(
            "Стручок",
            "Я Стручок. Маленький, но свой. Если что — я рядом.",
            Array.Empty<string>(),
            prompt);

        SerializedObject talkSo = new SerializedObject(talk);
        talkSo.FindProperty("interactionDistance").floatValue = 1.6f;
        talkSo.FindProperty("promptLocalPosition").vector3Value = new Vector3(0f, 1.15f, 0f);
        talkSo.ApplyModifiedPropertiesWithoutUndo();

        Collider2D[] colliders = root.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(colliders[i]);
        }

        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            UnityEngine.Object.DestroyImmediate(body);
        }
    }

    private static void PlaceInPrototypeScene(GameObject prefab, Sprite[] frames)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(ObjectName);
        if (existing != null)
        {
            existing.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            ConfigureNpc(existing, frames);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = ObjectName;
        instance.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);

        GameObject player = GameObject.Find(PlayerObjectName);
        instance.transform.position = player != null
            ? player.transform.position + FallbackOffset
            : FallbackPosition;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
