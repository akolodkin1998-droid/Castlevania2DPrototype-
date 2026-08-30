using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenHubSceneEditor
{
    private const string ScenePath = "Assets/Scenes/Hub.unity";
    private const string OpenFlagPath = "Temp/open_hub_scene.flag";

    static OpenHubSceneEditor()
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

    [MenuItem("Tools/Castlevania 2D/Open Village Hub Scene")]
    public static void OpenFromMenu()
    {
        OpenHubScene();
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

        OpenHubScene();
    }

    private static void OpenHubScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[OpenHubSceneEditor] Missing {ScenePath}.");
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Debug.Log($"[OpenHubSceneEditor] Opened {ScenePath}.");
    }
}
