using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenTeremSceneEditor
{
    private const string ScenePath = "Assets/Scenes/Terem.unity";
    private const string OpenFlagPath = "Temp/open_terem_scene.flag";

    static OpenTeremSceneEditor()
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

    [MenuItem("Tools/Castlevania 2D/Open Terem Scene")]
    public static void OpenFromMenu()
    {
        OpenTeremScene();
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

        OpenTeremScene();
    }

    private static void OpenTeremScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[OpenTeremSceneEditor] Missing " + ScenePath);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[OpenTeremSceneEditor] Opened " + ScenePath);
    }
}
