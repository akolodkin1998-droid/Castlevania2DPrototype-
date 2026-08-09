using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class CastlevaniaSceneBackgroundCleanup
{
    private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";

    private static readonly HashSet<string> DecorativeRootNames = new HashSet<string>
    {
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
        "Grass 1",
        "Grass 2",
        "Grass 3",
        "Flower 1",
        "Flower 2",
        "Flower 3",
        "Flower 4",
        "Flower 5",
        "House",
        "Tree 1",
        "Barrel",
        "Fence 1",
        "Stacked Rocks",
        "Rock 1",
        "Rock 5",
        "Standing Torch",
        "GameObject"
    };

    [MenuItem("Tools/Castlevania 2D/Remove Scene Background")]
    public static void RemoveFromOpenSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before removing scene background.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        int removedCount = RemoveKarsioriDecorations(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Removed {removedCount} background object(s) from scene '{scene.name}'.");
    }

    public static void RemoveFromPrototypeSceneBatch()
    {
        EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        RemoveKarsioriDecorations(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.Exit(0);
    }

    public static int RemoveKarsioriDecorations(Scene scene)
    {
        int removedCount = 0;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int i = rootObjects.Length - 1; i >= 0; i--)
        {
            GameObject rootObject = rootObjects[i];

            if (!ShouldRemoveRoot(rootObject))
            {
                continue;
            }

            Object.DestroyImmediate(rootObject);
            removedCount++;
        }

        return removedCount;
    }

    private static bool ShouldRemoveRoot(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return false;
        }

        if (rootObject.GetComponentInChildren<Tilemap>(true) != null)
        {
            return false;
        }

        if (rootObject.GetComponent<Camera>() != null || rootObject.GetComponent<Canvas>() != null)
        {
            return false;
        }

        if (rootObject.GetComponent<Rigidbody2D>() != null || rootObject.GetComponent<Animator>() != null)
        {
            return false;
        }

        if (rootObject.name.Contains("Player") || rootObject.name.Contains("Enemy") || rootObject.name.Contains("HUD"))
        {
            return false;
        }

        if (DecorativeRootNames.Contains(rootObject.name))
        {
            return true;
        }

        return rootObject.GetComponent<SpriteRenderer>() != null &&
               rootObject.GetComponent<Collider2D>() == null;
    }
}
