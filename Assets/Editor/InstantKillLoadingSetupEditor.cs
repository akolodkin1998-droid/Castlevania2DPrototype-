using System;
using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires InstantKillOnContact2D + solid composite colliders on Prototype Loading_3_09.
/// </summary>
public static class InstantKillLoadingSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string RootName = "Loading_3_09";

    [MenuItem("Tools/Castlevania 2D/Setup Loading_3_09 Instant Kill")]
    public static void SetupMenu()
    {
        try
        {
            string summary = SetupLoadingInstantKill();
            EditorUtility.DisplayDialog("Loading_3_09 Instant Kill", summary, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Loading_3_09 Instant Kill", "Failed:\n" + ex.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod InstantKillLoadingSetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        string summary = SetupLoadingInstantKill();
        Debug.Log("[InstantKillLoadingSetupEditor] " + summary);
    }

    private static string SetupLoadingInstantKill()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = FindRootLoadingObject(scene);
        if (root == null)
        {
            throw new InvalidOperationException(
                "Scene root '" + RootName + "' not found in " + ScenePath + ".");
        }

        InstantKillOnContact2D kill = root.GetComponent<InstantKillOnContact2D>();
        if (kill == null)
        {
            kill = Undo.AddComponent<InstantKillOnContact2D>(root);
        }

        kill.EnsureSolidSurfaces();
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        int spriteCount = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
        int boxCount = root.GetComponentsInChildren<BoxCollider2D>(true).Length;
        return "Wired InstantKillOnContact2D on '" + RootName + "'. Sprites=" + spriteCount +
               ", BoxCollider2D=" + boxCount +
               ", Composite=" + (root.GetComponent<CompositeCollider2D>() != null) +
               ", Rigidbody2D Static=" + (root.GetComponent<Rigidbody2D>() != null) + ".";
    }

    private static GameObject FindRootLoadingObject(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == RootName)
            {
                return roots[i];
            }
        }

        return GameObject.Find(RootName);
    }
}
