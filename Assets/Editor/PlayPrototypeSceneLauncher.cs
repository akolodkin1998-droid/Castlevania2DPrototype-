using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayPrototypeSceneLauncher
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string FlagPath = "Temp/play_prototype_scene.flag";

    static PlayPrototypeSceneLauncher()
    {
        EditorApplication.delayCall += TryLaunchFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Play Prototype Scene")]
    public static void PlayPrototypeSceneMenu()
    {
        RequestLaunch();
    }

    public static void PlayPrototypeSceneBatch()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("O"));
        TryLaunchFromFlag();
    }

    private static void RequestLaunch()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("O"));
        TryLaunchFromFlag();
    }

    private static void TryLaunchFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(FlagPath))
        {
            return;
        }

        File.Delete(FlagPath);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        PrototypeSceneSnapshotRestore.RestorePrototypeSceneSnapshot();
        EditorSceneManager.SaveOpenScenes();

        EditorApplication.EnterPlaymode();
        Debug.Log("Prototype scene play mode started.");
    }
}

// refresh
