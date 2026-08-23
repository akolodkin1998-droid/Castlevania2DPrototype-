using Castlevania2D.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BurningFloorLampPrefabSetupEditor
{
    private const string SpriteFolder = "Assets/Resources/Environment/BurningFloorLamp";
    private const string PrefabFolder = "Assets/Prefabs/Environment";
    private const string PrefabPath = PrefabFolder + "/BurningFloorLamp.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string PlayerObjectName = "Player_HeroKnight";
    private const string LampObjectName = "BurningFloorLamp";
    private const int FrameCount = 10;
    private const int SortingOrder = 2;

    [MenuItem("Tools/Castlevania 2D/Setup Burning Floor Lamp Prefab")]
    public static void SetupFromMenu()
    {
        if (BuildPrefab())
        {
            EditorUtility.DisplayDialog("Burning Floor Lamp", $"Prefab updated:\n{PrefabPath}", "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Place Burning Floor Lamp Near Player")]
    public static void PlaceNearPlayer()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogWarning($"[BurningFloorLampPrefabSetupEditor] Open {ScenePath} before placing the lamp.");
            return;
        }

        GameObject existing = GameObject.Find(LampObjectName);
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
            Debug.LogError("[BurningFloorLampPrefabSetupEditor] Floor lamp prefab could not be loaded.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = LampObjectName;

        GameObject player = GameObject.Find(PlayerObjectName);
        Vector3 playerPosition = player != null
            ? player.transform.position
            : new Vector3(8.3f, -37f, 0f);
        instance.transform.position = playerPosition + new Vector3(3.2f, 0f, 0f);

        Undo.RegisterCreatedObjectUndo(instance, "Place Burning Floor Lamp");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[BurningFloorLampPrefabSetupEditor] Placed BurningFloorLamp at {instance.transform.position}.");
    }

    public static bool BuildPrefab()
    {
        Sprite[] frames = LoadFrames();
        if (frames == null)
        {
            return false;
        }

        EnsureFolder("Assets/Prefabs", "Environment");

        var root = new GameObject(LampObjectName);
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = SortingOrder;

            BurningFloorLampAnimator2D animator = root.AddComponent<BurningFloorLampAnimator2D>();
            animator.EditorAssignFrames(frames);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Sprite[] LoadFrames()
    {
        var frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            string path = $"{SpriteFolder}/FloorLamp_{i + 1:D2}.png";
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[i] == null)
            {
                return null;
            }
        }

        return frames;
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
