#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports required editor resources before Play Mode so package import dialogs
/// do not appear while the editor is playing.
/// </summary>
[InitializeOnLoad]
internal static class ProjectEditorBootstrap
{
    private const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    static ProjectEditorBootstrap()
    {
        EditorApplication.update += TryImportRequiredResources;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            TryImportRequiredResources();
        }
    }

    private static void TryImportRequiredResources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (File.Exists(TmpSettingsAssetPath))
        {
            EditorApplication.update -= TryImportRequiredResources;
            return;
        }

        EditorApplication.update -= TryImportRequiredResources;
        ImportTmpEssentialsSilently();
    }

    private static void ImportTmpEssentialsSilently()
    {
        try
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(
                importEssentials: true,
                importExamples: false,
                interactive: false);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "Could not auto-import TextMesh Pro Essential Resources. " +
                "Stop Play Mode and use Window > TextMesh Pro > Import TMP Essential Resources.\n" +
                exception.Message);
        }
    }
}
#endif
