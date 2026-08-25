using System.IO;
using Castlevania2D.Minigames.Hnefatafl;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HnefataflSceneSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Hnefatafl.unity";
    private const string OpenFlagPath = "Temp/open_hnefatafl_scene.flag";

    static HnefataflSceneSetupEditor()
    {
        EditorApplication.delayCall += TryOpenFromFlag;
        EditorApplication.update += PollOpenFlag;
    }

    private static void PollOpenFlag()
    {
        if (File.Exists(OpenFlagPath))
        {
            TryOpenFromFlag();
        }
    }

    private static void TryOpenFromFlag()
    {
        if (!File.Exists(OpenFlagPath))
        {
            return;
        }

        File.Delete(OpenFlagPath);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[HnefataflSceneSetupEditor] Missing {ScenePath}.");
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Debug.Log($"[HnefataflSceneSetupEditor] Opened {ScenePath}.");
    }

    [MenuItem("Tools/Castlevania 2D/Setup Hnefatafl Scene")]
    public static void SetupFromMenu()
    {
        PrepareScene(enterPlayMode: false);
        EditorUtility.DisplayDialog("Hnefatafl", $"Scene ready:\n{ScenePath}", "OK");
    }

    [MenuItem("Tools/Castlevania 2D/Play Hnefatafl Scene")]
    public static void PlayFromMenu()
    {
        PrepareScene(enterPlayMode: true);
    }

    private static void PrepareScene(bool enterPlayMode)
    {
        AddToBuildSettings();

        if (!File.Exists(ScenePath))
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureSceneObjects();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureSceneObjects();
            EditorSceneManager.SaveOpenScenes();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[HnefataflSceneSetupEditor] Ready: {ScenePath}.");

        if (enterPlayMode && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void EnsureSceneObjects()
    {
        HnefataflSceneBootstrap bootstrap = Object.FindFirstObjectByType<HnefataflSceneBootstrap>();
        if (bootstrap == null)
        {
            var go = new GameObject("HnefataflBootstrap", typeof(HnefataflSceneBootstrap));
            Selection.activeGameObject = go;
        }
        else
        {
            Selection.activeGameObject = bootstrap.gameObject;
        }

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private static void AddToBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == ScenePath)
            {
                return;
            }
        }

        var next = new EditorBuildSettingsScene[current.Length + 1];
        for (int i = 0; i < current.Length; i++)
        {
            next[i] = current[i];
        }

        next[current.Length] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = next;
    }
}
