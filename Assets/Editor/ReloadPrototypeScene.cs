using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Castlevania2D.Player;

/// <summary>
/// Reloads Assets/Scenes/Prototype.unity from disk (discarding unsaved in-memory scene state).
/// Menu: Tools → Castlevania 2D → Reload Prototype Scene
/// Batch: -executeMethod ReloadPrototypeScene.Reload
/// External signal: write Temp/reload_prototype_scene.flag (picked up on domain reload / update poll).
/// </summary>
[InitializeOnLoad]
public static class ReloadPrototypeScene
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string FlagPath = "Temp/reload_prototype_scene.flag";

    static ReloadPrototypeScene()
    {
        EditorApplication.delayCall += TryReloadFromFlag;
        EditorApplication.update += PollReloadFlag;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            TryReloadFromFlag();
        }
    }

    [MenuItem("Tools/Castlevania 2D/Reload Prototype Scene")]
    public static void ReloadMenu()
    {
        Reload(showDialog: true);
    }

    /// <summary>BatchMode / executeMethod entry: -executeMethod ReloadPrototypeScene.Reload</summary>
    public static void Reload()
    {
        Reload(showDialog: !Application.isBatchMode);
    }

    public static void Reload(bool showDialog)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            const string playMsg = "Exit Play Mode before reloading Prototype from disk.";
            Debug.LogWarning("Reload Prototype Scene: " + playMsg);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Reload Prototype Scene", playMsg, "OK");
            }

            return;
        }

        if (!File.Exists(ScenePath))
        {
            const string missingMsg = "Missing Assets/Scenes/Prototype.unity on disk.";
            Debug.LogError("Reload Prototype Scene: " + missingMsg);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Reload Prototype Scene", missingMsg, "OK");
            }

            return;
        }

        Scene prototype = SceneManager.GetSceneByPath(ScenePath);
        bool prototypeLoaded = prototype.IsValid() && prototype.isLoaded;

        if (prototypeLoaded && prototype.isDirty)
        {
            int choice = showDialog
                ? EditorUtility.DisplayDialogComplex(
                    "Reload Prototype Scene",
                    "Prototype has unsaved changes. Discard them and reload from disk?",
                    "Discard & Reload",
                    "Cancel",
                    "Save Then Reload")
                : 0;

            if (choice == 1)
            {
                return;
            }

            if (choice == 2)
            {
                EditorSceneManager.SaveScene(prototype);
            }
        }
        else
        {
            // Prompt for other dirty scenes (not Prototype) before Single open replaces them.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
        }

        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);

        // ReloadScene / ClearSceneDirtiness are unavailable in this Editor.
        // Empty NewScene first discards dirty Prototype without re-prompt, then open from disk.
        prototype = SceneManager.GetSceneByPath(ScenePath);
        if (prototype.IsValid() && prototype.isLoaded && prototype.isDirty)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        Scene opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        string summary = BuildCameraFollowSummary(opened);
        string message = "Reloaded Prototype from disk.\n\n" + summary;
        Debug.Log("Reload Prototype Scene: " + message.Replace("\n", " | "));
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Reload Prototype Scene", message, "OK");
        }
    }

    private static string BuildCameraFollowSummary(Scene scene)
    {
        var sb = new StringBuilder();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            sb.Append("Camera: (scene not loaded)");
            return sb.ToString();
        }

        CameraFollow2D follow = null;
        ArenaFixedCamera2D arena = null;
        Camera cam = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "Main Camera")
            {
                continue;
            }

            cam = roots[i].GetComponent<Camera>();
            follow = roots[i].GetComponent<CameraFollow2D>();
            arena = roots[i].GetComponent<ArenaFixedCamera2D>();
            break;
        }

        if (cam == null)
        {
            sb.Append("Main Camera: not found");
            return sb.ToString();
        }

        sb.Append("CameraFollow2D: ").Append(follow != null && follow.enabled ? "ON" : "OFF");
        if (follow != null && follow.enabled)
        {
            Transform target = GetFollowTarget(follow);
            sb.Append(" → ").Append(target != null ? target.name : "(no target)");
        }

        sb.Append('\n');
        sb.Append("ArenaFixedCamera2D: ").Append(arena != null && arena.enabled ? "ON" : "OFF");
        sb.Append('\n');
        sb.Append("Position: ").Append(FormatVec(cam.transform.position));
        Rect rect = cam.rect;
        bool fullViewport = Mathf.Approximately(rect.x, 0f)
            && Mathf.Approximately(rect.y, 0f)
            && Mathf.Approximately(rect.width, 1f)
            && Mathf.Approximately(rect.height, 1f);
        sb.Append('\n');
        sb.Append("Viewport: ").Append(fullViewport ? "full" : $"letterbox {rect}");
        return sb.ToString();
    }

    private static Transform GetFollowTarget(CameraFollow2D follow)
    {
        SerializedObject so = new SerializedObject(follow);
        SerializedProperty targetProp = so.FindProperty("target");
        return targetProp != null ? targetProp.objectReferenceValue as Transform : null;
    }

    private static string FormatVec(Vector3 v)
    {
        return $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
    }

    /// <summary>
    /// Request reload from outside the Editor (write flag; open Editor will pick it up).
    /// </summary>
    public static void RequestReload()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("O"));
        TryReloadFromFlag();
    }

    private static void PollReloadFlag()
    {
        if (!File.Exists(FlagPath))
        {
            return;
        }

        TryReloadFromFlag();
    }

    private static void TryReloadFromFlag()
    {
        if (!File.Exists(FlagPath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            File.Delete(FlagPath);
        }
        catch (IOException)
        {
            return;
        }

        Reload(showDialog: false);
    }
}
