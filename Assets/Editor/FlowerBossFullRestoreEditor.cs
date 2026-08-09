using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Restores Prototype scene layout + walls + Flower Boss tentacles/attacks/death wiring.
/// Scene etalon: 2026-07-25 (Prototype_backup_20260725_1329 + layout snapshot).
/// </summary>
[InitializeOnLoad]
public static class FlowerBossFullRestoreEditor
{
    private const string RestoreFlagPath = "Temp/restore_full_etalon.flag";

    static FlowerBossFullRestoreEditor()
    {
        EditorApplication.delayCall += TryRestoreFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Restore Full Etalon (Scene + Boss)")]
    public static void RestoreFullEtalonMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RestoreFullEtalonMenu;
            return;
        }

        RestoreFullEtalon();
    }

    public static void RequestRestore()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RestoreFlagPath, System.DateTime.UtcNow.ToString("O"));
        TryRestoreFromFlag();
    }

    public static void RestoreFullEtalonBatch()
    {
        RestoreFullEtalon();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static void TryRestoreFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RestoreFlagPath))
        {
            return;
        }

        File.Delete(RestoreFlagPath);
        RestoreFullEtalon();
    }

    public static void RestoreFullEtalon()
    {
        Debug.Log("Full etalon restore: scene snapshot + backgrounds + boss...");
        PrototypeSceneSnapshotRestore.RestorePrototypeSceneSnapshot();

        Debug.Log("Full etalon restore: Flower Boss tentacles...");
        FlowerBossTentacleSetup.Setup();

        Debug.Log("Full etalon restore: Flower Boss attack + death...");
        FlowerBossAttackSetup.Setup();

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("Full etalon restore complete.");
    }
}
