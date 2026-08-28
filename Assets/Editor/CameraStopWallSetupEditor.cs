using Castlevania2D.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Solid 15-tile walls the player collides with. The camera also stops when its view hits them.
/// </summary>
[InitializeOnLoad]
public static class CameraStopWallSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string FlagPath = "Temp/setup_camera_stop_wall.flag";
    private const string LeftWallName = "CameraStopWall";
    private const string RightWallName = "CameraStopWall_Right";
    private const float WallHeightTiles = 15f;
    private const float WallThickness = 1f;
    private static readonly Vector3 LeftWallPosition = new Vector3(-29.76f, -36.9f, 0f);
    private static readonly Vector3 RightWallPosition = new Vector3(81.1f, -36.87f, 0f);

    static CameraStopWallSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Camera Stop Wall")]
    public static void SetupMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        string summary = EnsureInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Camera Stop Wall", summary, "OK");
    }

    public static void SetupFromBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string summary = EnsureInScene(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log(summary + (saved ? "\nScene saved." : "\nFAILED to save scene."));
        EditorApplication.Exit(saved ? 0 : 1);
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!System.IO.File.Exists(FlagPath))
        {
            return;
        }

        System.IO.File.Delete(FlagPath);
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EnsureInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static string EnsureInScene(Scene scene)
    {
        BoxCollider2D left = EnsureWall(scene, LeftWallName, LeftWallPosition);
        BoxCollider2D right = EnsureWall(scene, RightWallName, RightWallPosition);
        AssignWallsToCamera(left, right);
        return $"{LeftWallName} at {LeftWallPosition}, {RightWallName} at {RightWallPosition}, height {WallHeightTiles} tiles.";
    }

    private static BoxCollider2D EnsureWall(Scene scene, string wallName, Vector3 position)
    {
        GameObject wall = FindNamed(scene, wallName);
        if (wall == null)
        {
            wall = new GameObject(wallName);
            SceneManager.MoveGameObjectToScene(wall, scene);
        }

        wall.layer = 0;
        wall.transform.SetParent(null, true);
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;

        BoxCollider2D box = wall.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = wall.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = false;
        box.offset = new Vector2(0f, WallHeightTiles * 0.5f);
        box.size = new Vector2(WallThickness, WallHeightTiles);

        Rigidbody2D body = wall.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = wall.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
        body.gravityScale = 0f;
        return box;
    }

    private static void AssignWallsToCamera(BoxCollider2D left, BoxCollider2D right)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(follow);
        SerializedProperty wallsProperty = so.FindProperty("cameraStopWalls");
        if (wallsProperty == null || !wallsProperty.isArray)
        {
            return;
        }

        wallsProperty.arraySize = 2;
        wallsProperty.GetArrayElementAtIndex(0).objectReferenceValue = left;
        wallsProperty.GetArrayElementAtIndex(1).objectReferenceValue = right;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }
}
