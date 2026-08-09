using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Parents all decorative background sprites under one Background root
/// and assigns a single shared sortingOrder so they render as one layer.
/// Depth between pieces is kept via Transform.z.
/// </summary>
public static class MergeBackgroundLayersEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string BackgroundRootName = "Background";
    private const int UnifiedSortingOrder = -100;

    private static readonly string[] ExactBackgroundNames =
    {
        "Red Sky",
        "Sky",
        "Mountains 1",
        "Mountains 2",
        "Mountains 3",
        "Mountains 4",
        "Cloud 1",
        "Cloud 2",
        "Cloud 3",
        "Cloud 4",
        "Cloud 5",
        "Cloud 5 (1)",
        "Cloud 6",
    };

    [MenuItem("Tools/Castlevania 2D/Merge Background Into One Layer")]
    public static void MergeBackgroundMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before merging background layers.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("No loaded active scene to merge background in.");
            return;
        }

        string summary = MergeBackgroundInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Merge Background", summary, "OK");
    }

    /// <summary>
    /// Unity: -executeMethod MergeBackgroundLayersEditor.MergeAndSaveBatch
    /// </summary>
    public static void MergeAndSaveBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string summary = MergeBackgroundInScene(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log(summary + (saved ? "\nScene saved." : "\nFAILED to save scene."));
        EditorApplication.Exit(saved ? 0 : 1);
    }

    public static string MergeBackgroundInScene(Scene scene)
    {
        List<GameObject> backgrounds = CollectBackgroundObjects(scene);
        if (backgrounds.Count == 0)
        {
            return "No background objects found to merge.";
        }

        GameObject root = FindOrCreateBackgroundRoot(scene);
        int parented = 0;
        int sorted = 0;

        for (int i = 0; i < backgrounds.Count; i++)
        {
            GameObject background = backgrounds[i];
            if (background == null || background == root)
            {
                continue;
            }

            if (background.transform.parent != root.transform)
            {
                background.transform.SetParent(root.transform, true);
                parented++;
            }

            SpriteRenderer[] renderers = background.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r].sortingOrder != UnifiedSortingOrder)
                {
                    renderers[r].sortingOrder = UnifiedSortingOrder;
                    sorted++;
                }
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Background root: '{BackgroundRootName}'");
        sb.AppendLine($"Objects under root: {root.transform.childCount}");
        sb.AppendLine($"Reparented: {parented}");
        sb.AppendLine($"SpriteRenderers set to sortingOrder {UnifiedSortingOrder}: {sorted}");
        sb.AppendLine("Relative depth kept via Transform.z.");
        return sb.ToString();
    }

    private static GameObject FindOrCreateBackgroundRoot(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == BackgroundRootName)
            {
                return rootObject;
            }
        }

        GameObject created = new GameObject(BackgroundRootName);
        SceneManager.MoveGameObjectToScene(created, scene);
        created.transform.position = Vector3.zero;
        created.transform.localScale = Vector3.one;
        return created;
    }

    private static List<GameObject> CollectBackgroundObjects(Scene scene)
    {
        List<GameObject> result = new List<GameObject>();
        HashSet<int> seen = new HashSet<int>();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            CollectFromHierarchy(rootObject, result, seen);
        }

        return result;
    }

    private static void CollectFromHierarchy(GameObject candidate, List<GameObject> result, HashSet<int> seen)
    {
        if (candidate == null || candidate.name == BackgroundRootName)
        {
            // Still scan children of an existing Background root.
            if (candidate != null)
            {
                for (int i = 0; i < candidate.transform.childCount; i++)
                {
                    CollectFromHierarchy(candidate.transform.GetChild(i).gameObject, result, seen);
                }
            }

            return;
        }

        if (IsBackgroundObject(candidate) && seen.Add(candidate.GetInstanceID()))
        {
            result.Add(candidate);
            return;
        }

        for (int i = 0; i < candidate.transform.childCount; i++)
        {
            CollectFromHierarchy(candidate.transform.GetChild(i).gameObject, result, seen);
        }
    }

    private static bool IsBackgroundObject(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        string name = gameObject.name;
        for (int i = 0; i < ExactBackgroundNames.Length; i++)
        {
            if (name == ExactBackgroundNames[i])
            {
                return true;
            }
        }

        if (name.StartsWith("Background2") ||
            name.StartsWith("Background3") ||
            name.StartsWith("Background5") ||
            name.StartsWith("Background "))
        {
            return true;
        }

        // Decorative sprite-only roots without physics/gameplay components.
        if (gameObject.GetComponent<SpriteRenderer>() == null)
        {
            return false;
        }

        if (gameObject.GetComponentInChildren<Tilemap>(true) != null ||
            gameObject.GetComponent<Camera>() != null ||
            gameObject.GetComponent<Canvas>() != null ||
            gameObject.GetComponent<Rigidbody2D>() != null ||
            gameObject.GetComponent<Animator>() != null ||
            gameObject.GetComponent<Collider2D>() != null)
        {
            return false;
        }

        return name.Contains("Sky") ||
               name.StartsWith("Mountains") ||
               name.StartsWith("Cloud");
    }
}
