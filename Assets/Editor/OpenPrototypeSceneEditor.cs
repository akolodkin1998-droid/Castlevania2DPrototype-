using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenPrototypeSceneEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string OpenFlagPath = "Temp/open_prototype_scene.flag";

    static OpenPrototypeSceneEditor()
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

    [MenuItem("Tools/Castlevania 2D/Open Prototype Scene")]
    public static void OpenFromMenu()
    {
        OpenPrototypeScene();
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

        OpenPrototypeScene();
    }

    private static void OpenPrototypeScene()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[OpenPrototypeSceneEditor] Missing " + ScenePath);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[OpenPrototypeSceneEditor] Opened " + ScenePath);
    }
}
