using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeSceneLayoutSnapshotEditor
{
    public const string ScenePath = "Assets/Scenes/Prototype.unity";
    /// <summary>Live etalon layout JSON (promoted 2026-07-27 from PrototypeSceneLayoutSnapshot_backup_20260727_2047).</summary>
    public const string SnapshotPath = "Assets/Editor/PrototypeSceneLayoutSnapshot.json";
    private const string CaptureFlagPath = "Temp/capture_scene_layout.flag";

    static PrototypeSceneLayoutSnapshotEditor()
    {
        EditorApplication.delayCall += TryCaptureFromFlag;
    }

    private static void TryCaptureFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(CaptureFlagPath))
        {
            return;
        }

        File.Delete(CaptureFlagPath);
        CaptureSceneLayoutSnapshot();
        SaveCapturedProject();
        Debug.Log("Scene layout snapshot captured from flag and project saved.");
    }

    private static void SaveCapturedProject()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Castlevania 2D/Capture Scene Layout Snapshot")]
    public static void CaptureSceneLayoutSnapshotMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += CaptureSceneLayoutSnapshotMenu;
            return;
        }

        CaptureSceneLayoutSnapshot();
        SaveCapturedProject();
        Debug.Log($"Scene layout snapshot saved to {SnapshotPath}.");
    }

    [MenuItem("Tools/Castlevania 2D/Restore Scene Layout Snapshot")]
    public static void RestoreSceneLayoutSnapshotMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RestoreSceneLayoutSnapshotMenu;
            return;
        }

        RestoreSceneLayoutSnapshot();
        Debug.Log("Scene layout snapshot restored.");
    }

    public static void CaptureSceneLayoutSnapshotBatch()
    {
        CaptureSceneLayoutSnapshot();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void CaptureSceneLayoutSnapshot()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        List<SceneLayoutObjectEntry> entries = new List<SceneLayoutObjectEntry>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            CaptureTransformRecursive(roots[i].transform, entries);
        }

        CapturePrefabInstanceOverrides(scene, entries);

        SceneLayoutSnapshotFile snapshot = new SceneLayoutSnapshotFile
        {
            capturedAtUtc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            objects = entries.ToArray()
        };

        string json = JsonUtility.ToJson(snapshot, true);
        File.WriteAllText(SnapshotPath, json);
        AssetDatabase.Refresh();
    }

    public static void RestoreSceneLayoutSnapshot()
    {
        if (!File.Exists(SnapshotPath))
        {
            Debug.LogError($"Scene layout snapshot was not found at {SnapshotPath}.");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        SceneLayoutSnapshotFile snapshot = JsonUtility.FromJson<SceneLayoutSnapshotFile>(File.ReadAllText(SnapshotPath));
        if (snapshot?.objects == null || snapshot.objects.Length == 0)
        {
            Debug.LogError("Scene layout snapshot is empty.");
            return;
        }

        int restoredCount = 0;
        for (int i = 0; i < snapshot.objects.Length; i++)
        {
            if (TryRestoreEntry(snapshot.objects[i]))
            {
                restoredCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Restored layout for {restoredCount}/{snapshot.objects.Length} objects from snapshot captured at {snapshot.capturedAtUtc}.");
    }

    private static void CaptureTransformRecursive(Transform transform, List<SceneLayoutObjectEntry> entries)
    {
        if (TryAddEntry(transform, entries))
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                CaptureTransformRecursive(transform.GetChild(i), entries);
            }

            return;
        }

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                CaptureTransformRecursive(transform.GetChild(i), entries);
            }

            return;
        }

        SceneLayoutObjectEntry entry = CreateEntry(transform);
        entries.Add(entry);

        for (int i = 0; i < transform.childCount; i++)
        {
            CaptureTransformRecursive(transform.GetChild(i), entries);
        }
    }

    private static bool TryAddEntry(Transform transform, List<SceneLayoutObjectEntry> entries)
    {
        if (transform is RectTransform)
        {
            return false;
        }

        entries.Add(CreateEntry(transform));
        return true;
    }

    private static SceneLayoutObjectEntry CreateEntry(Transform transform)
    {
        SceneLayoutObjectEntry entry = new SceneLayoutObjectEntry
        {
            hierarchyPath = BuildHierarchyPath(transform),
            isActive = transform.gameObject.activeSelf,
            position = new SceneLayoutVector3(transform.localPosition),
            eulerAngles = new SceneLayoutVector3(transform.localEulerAngles),
            scale = new SceneLayoutVector3(transform.localScale)
        };

        SpriteRenderer spriteRenderer = transform.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            entry.sortingOrder = spriteRenderer.sortingOrder;
        }

        return entry;
    }

    private static void CapturePrefabInstanceOverrides(Scene scene, List<SceneLayoutObjectEntry> entries)
    {
        HashSet<string> existingPaths = new HashSet<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            existingPaths.Add(entries[i].hierarchyPath);
        }

        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.scene != scene)
            {
                continue;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(candidate))
            {
                continue;
            }

            GameObject rootObject = PrefabUtility.GetNearestPrefabInstanceRoot(candidate);
            if (rootObject == null)
            {
                continue;
            }

            Transform root = rootObject.transform;

            string path = BuildHierarchyPath(root);
            if (existingPaths.Contains(path))
            {
                continue;
            }

            entries.Add(CreateEntry(root));
            existingPaths.Add(path);
        }
    }

    private static bool TryRestoreEntry(SceneLayoutObjectEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.hierarchyPath))
        {
            return false;
        }

        GameObject target = FindByHierarchyPath(entry.hierarchyPath);
        if (target == null)
        {
            Debug.LogWarning($"Restore skipped missing object '{entry.hierarchyPath}'.");
            return false;
        }

        Transform transform = target.transform;
        transform.localPosition = entry.position.ToVector3();
        transform.localEulerAngles = entry.eulerAngles.ToVector3();
        transform.localScale = entry.scale.ToVector3();
        target.SetActive(entry.isActive);

        if (entry.sortingOrder != int.MinValue)
        {
            SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = entry.sortingOrder;
            }
        }

        return true;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return $"{BuildHierarchyPath(transform.parent)}/{transform.name}";
    }

    private static GameObject FindByHierarchyPath(string hierarchyPath)
    {
        string[] parts = hierarchyPath.Split('/');
        if (parts.Length == 0)
        {
            return null;
        }

        GameObject current = GameObject.Find(parts[0]);
        if (current == null)
        {
            return null;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null)
            {
                return null;
            }

            current = child.gameObject;
        }

        return current;
    }
}
