using Castlevania2D.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveIdolPrefabSetupEditor
{
    private const string SpriteFolder = "Assets/Resources/Environment/SaveIdol";
    private const string PrefabFolder = "Assets/Prefabs/Environment";
    private const string PrefabPath = PrefabFolder + "/SaveIdol.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string PlayerObjectName = "Player_HeroKnight";
    private const string IdolObjectName = "SaveIdol";
    private const int FrameCount = 10;
    private const int SortingOrder = -1;

    [MenuItem("Tools/Castlevania 2D/Setup Save Idol Prefab")]
    public static void SetupFromMenu()
    {
        if (BuildPrefab())
        {
            EditorUtility.DisplayDialog("Save Idol", $"Prefab updated:\n{PrefabPath}", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Save Idol",
                $"All {FrameCount} sprites must exist in:\n{SpriteFolder}",
                "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Place Save Idol Near Player Spawn")]
    public static void PlaceNearPlayerSpawn()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogWarning($"[SaveIdolPrefabSetupEditor] Open {ScenePath} before placing the idol.");
            return;
        }

        GameObject existing = GameObject.Find(IdolObjectName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null && BuildPrefab())
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (prefab == null)
        {
            Debug.LogError("[SaveIdolPrefabSetupEditor] Save idol prefab could not be loaded.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = IdolObjectName;

        GameObject player = GameObject.Find(PlayerObjectName);
        Vector3 spawnPosition = player != null
            ? player.transform.position
            : new Vector3(65.1f, -63.6f, 0f);
        instance.transform.position = spawnPosition + new Vector3(-3.5f, 0f, 0f);

        Undo.RegisterCreatedObjectUndo(instance, "Place Save Idol");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SaveIdolPrefabSetupEditor] Placed SaveIdol at {instance.transform.position}.");
    }

    public static bool BuildPrefab()
    {
        Sprite[] sprites = LoadSprites();
        if (sprites == null)
        {
            return false;
        }

        EnsureFolder("Assets/Prefabs", "Environment");

        var root = new GameObject("SaveIdol");
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];
            renderer.sortingOrder = SortingOrder;

            SaveIdolAnimator2D animator = root.AddComponent<SaveIdolAnimator2D>();
            SaveIdolInteractor2D interactor = root.AddComponent<SaveIdolInteractor2D>();
            Sprite promptSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/UI/InteractPrompt_F.png");
            if (promptSprite != null)
            {
                interactor.EditorAssignPrompt(promptSprite);
            }
            var saveFrames = new Sprite[FrameCount - 1];
            for (int i = 1; i < FrameCount; i++)
            {
                saveFrames[i - 1] = sprites[i];
            }

            animator.EditorAssignFrames(sprites[0], saveFrames);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Sprite[] LoadSprites()
    {
        var sprites = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            string path = $"{SpriteFolder}/Idol_{i + 1:D2}.png";
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null)
            {
                return null;
            }
        }

        return sprites;
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        string path = parentFolder + "/" + childFolder;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}
