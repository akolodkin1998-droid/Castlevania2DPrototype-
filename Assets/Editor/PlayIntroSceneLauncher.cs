using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayIntroSceneLauncher
{
    private const string ScenePath = "Assets/Scenes/Intro.unity";
    private const string FlagPath = "Temp/play_intro_scene.flag";

    static PlayIntroSceneLauncher()
    {
        EditorApplication.delayCall += TryLaunchFromFlag;
        EditorApplication.update += PollLaunchFlag;
    }

    private static void PollLaunchFlag()
    {
        if (File.Exists(FlagPath))
        {
            TryLaunchFromFlag();
        }
    }

    [MenuItem("Tools/Castlevania 2D/Play Intro Scene")]
    public static void PlayFromMenu()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, "play");
        TryLaunchFromFlag();
    }

    private static void TryLaunchFromFlag()
    {
        if (!File.Exists(FlagPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        File.Delete(FlagPath);

        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[PlayIntroSceneLauncher] Missing " + ScenePath);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
        Debug.Log("[PlayIntroSceneLauncher] Playing " + ScenePath);
    }
}
