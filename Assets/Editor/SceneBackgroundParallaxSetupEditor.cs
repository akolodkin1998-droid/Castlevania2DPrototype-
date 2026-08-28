using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds SceneBackgroundParallax2D to the Prototype Background root.
/// </summary>
public static class SceneBackgroundParallaxSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string BackgroundRootName = "Background";

    [MenuItem("Tools/Castlevania 2D/Setup Background Parallax")]
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
        EditorUtility.DisplayDialog("Background Parallax", summary, "OK");
    }

    /// <summary>Batch: -executeMethod SceneBackgroundParallaxSetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string summary = EnsureInScene(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log(summary + (saved ? "\nScene saved." : "\nFAILED to save scene."));
        EditorApplication.Exit(saved ? 0 : 1);
    }

    public static string EnsureInScene(Scene scene)
    {
        GameObject root = FindBackgroundRoot(scene);
        if (root == null)
        {
            return "No Background root in scene. Run Merge Background Into One Layer first.";
        }

        SceneBackgroundParallax2D parallax = root.GetComponent<SceneBackgroundParallax2D>();
        if (parallax == null)
        {
            parallax = root.AddComponent<SceneBackgroundParallax2D>();
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            SerializedObject so = new SerializedObject(parallax);
            SerializedProperty cameraProperty = so.FindProperty("cameraTransform");
            if (cameraProperty != null)
            {
                cameraProperty.objectReferenceValue = camera.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return camera != null
            ? $"Parallax on '{BackgroundRootName}', camera '{camera.name}'."
            : $"Parallax on '{BackgroundRootName}' (Camera.main at runtime).";
    }

    private static GameObject FindBackgroundRoot(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == BackgroundRootName)
            {
                return rootObject;
            }
        }

        return null;
    }
}
