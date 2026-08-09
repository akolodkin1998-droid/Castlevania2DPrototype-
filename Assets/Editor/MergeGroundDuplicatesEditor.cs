using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Merges Unity-duplicated Ground tilemaps (Ground (1), Ground (2), etc.) into the primary Ground.
/// Use when duplicates exist only in the open/unsaved Editor scene and not yet on disk.
/// </summary>
public static class MergeGroundDuplicatesEditor
{
    private static readonly string[] DuplicateNameExact =
    {
        "Ground (1)",
        "Ground (2)",
        "Ground(1)",
        "Ground(2)",
    };

    [MenuItem("Tools/Castlevania 2D/Merge Ground Duplicates")]
    public static void MergeGroundDuplicatesMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before merging Ground duplicates.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("No loaded active scene to merge Ground duplicates in.");
            return;
        }

        string summary = MergeGroundDuplicatesInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Merge Ground Duplicates", summary, "OK");
    }

    /// <summary>
    /// Batch/CLI entry: merges Ground duplicates and saves the active Prototype scene.
    /// Unity: -executeMethod MergeGroundDuplicatesEditor.MergeAndSaveBatch
    /// </summary>
    public static void MergeAndSaveBatch()
    {
        string scenePath = "Assets/Scenes/Prototype.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string summary = MergeGroundDuplicatesInScene(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log(summary + (saved ? "\nScene saved." : "\nFAILED to save scene."));
        if (!saved)
        {
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    public static string MergeGroundDuplicatesInScene(Scene scene)
    {
        List<GameObject> candidates = FindGroundCandidates(scene);
        if (candidates.Count == 0)
        {
            return "No Ground / Ground (1) / Ground (2) objects found in the open scene.";
        }

        GameObject primary = SelectPrimaryGround(candidates);
        Tilemap primaryTilemap = primary.GetComponent<Tilemap>();
        if (primaryTilemap == null)
        {
            return $"Primary '{primary.name}' has no Tilemap component; aborting merge.";
        }

        EnsureSolidGroundColliders(primary);

        int mergedCells = 0;
        int movedSprites = 0;
        int deletedCount = 0;
        var deletedNames = new List<string>();

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null || candidate == primary)
            {
                continue;
            }

            Tilemap sourceTilemap = candidate.GetComponent<Tilemap>();
            if (sourceTilemap != null)
            {
                mergedCells += CopyTilesIntoPrimary(sourceTilemap, primaryTilemap);
            }

            movedSprites += ReparentSpriteChildren(candidate.transform, primary.transform);

            deletedNames.Add(candidate.name);
            Object.DestroyImmediate(candidate);
            deletedCount++;
        }

        primaryTilemap.CompressBounds();
        RefreshGroundColliders(primary);

        RewireGroundTilemapReferences(scene, primaryTilemap);

        var sb = new StringBuilder();
        sb.AppendLine($"Primary Ground: '{GetHierarchyPath(primary.transform)}'");
        sb.AppendLine($"Merged tile cells: {mergedCells}");
        sb.AppendLine($"Moved sprite children: {movedSprites}");
        sb.AppendLine($"Deleted duplicates: {deletedCount}");
        if (deletedNames.Count > 0)
        {
            sb.AppendLine("Deleted: " + string.Join(", ", deletedNames));
        }

        sb.AppendLine("Scene marked dirty — Save the scene (Ctrl+S) to persist.");
        return sb.ToString().TrimEnd();
    }

    private static List<GameObject> FindGroundCandidates(Scene scene)
    {
        var results = new List<GameObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            CollectGroundCandidates(roots[i].transform, results);
        }

        return results;
    }

    private static void CollectGroundCandidates(Transform current, List<GameObject> results)
    {
        if (current == null)
        {
            return;
        }

        if (IsGroundCandidateName(current.name))
        {
            results.Add(current.gameObject);
        }

        for (int i = 0; i < current.childCount; i++)
        {
            CollectGroundCandidates(current.GetChild(i), results);
        }
    }

    private static bool IsGroundCandidateName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        if (objectName == "Ground")
        {
            return true;
        }

        for (int i = 0; i < DuplicateNameExact.Length; i++)
        {
            if (objectName == DuplicateNameExact[i])
            {
                return true;
            }
        }

        // Unity duplicate naming: "Ground (3)", "Ground (12)", etc.
        if (objectName.StartsWith("Ground (") && objectName.EndsWith(")"))
        {
            string inner = objectName.Substring("Ground (".Length, objectName.Length - "Ground (".Length - 1);
            return int.TryParse(inner, out _);
        }

        if (objectName.StartsWith("Ground(") && objectName.EndsWith(")"))
        {
            string inner = objectName.Substring("Ground(".Length, objectName.Length - "Ground(".Length - 1);
            return int.TryParse(inner, out _);
        }

        return false;
    }

    private static GameObject SelectPrimaryGround(List<GameObject> candidates)
    {
        // Prefer the arena Ground under the shared grid parent (pos ~0,0), named exactly "Ground".
        GameObject bestNamed = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null || candidate.name != "Ground")
            {
                continue;
            }

            float score = 0f;
            if (candidate.GetComponent<Tilemap>() != null)
            {
                score += 1000f;
            }

            Vector3 local = candidate.transform.localPosition;
            score -= Mathf.Abs(local.x) + Mathf.Abs(local.y) * 0.5f;

            // Prefer children of the platform grid if present.
            if (candidate.transform.parent != null)
            {
                string parentName = candidate.transform.parent.name;
                if (parentName.IndexOf("Grid", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    parentName.IndexOf("Platform", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 500f;
                }
            }

            if (bestNamed == null || score > bestScore)
            {
                bestNamed = candidate;
                bestScore = score;
            }
        }

        if (bestNamed != null)
        {
            return bestNamed;
        }

        GameObject best = candidates[0];
        int bestSibling = best.transform.GetSiblingIndex();
        for (int i = 1; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            int sibling = candidate.transform.GetSiblingIndex();
            if (sibling < bestSibling)
            {
                best = candidate;
                bestSibling = sibling;
            }
        }

        return best;
    }

    private static int CopyTilesIntoPrimary(Tilemap source, Tilemap destination)
    {
        BoundsInt bounds = source.cellBounds;
        int copied = 0;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int z = bounds.zMin; z < bounds.zMax; z++)
                {
                    var sourceCell = new Vector3Int(x, y, z);
                    TileBase tile = source.GetTile(sourceCell);
                    if (tile == null)
                    {
                        continue;
                    }

                    Vector3 worldCenter = source.GetCellCenterWorld(sourceCell);
                    Vector3Int destCell = destination.WorldToCell(worldCenter);

                    // Keep existing primary tiles; only fill empty cells from duplicates.
                    if (destination.GetTile(destCell) != null)
                    {
                        continue;
                    }

                    destination.SetTile(destCell, tile);
                    destination.SetTransformMatrix(destCell, source.GetTransformMatrix(sourceCell));
                    destination.SetColor(destCell, source.GetColor(sourceCell));
                    copied++;
                }
            }
        }

        return copied;
    }

    private static int ReparentSpriteChildren(Transform sourceRoot, Transform destinationRoot)
    {
        int moved = 0;
        for (int i = sourceRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = sourceRoot.GetChild(i);
            if (child.GetComponent<SpriteRenderer>() == null)
            {
                continue;
            }

            Vector3 worldPosition = child.position;
            Quaternion worldRotation = child.rotation;
            Vector3 worldScale = child.lossyScale;
            child.SetParent(destinationRoot, true);
            child.position = worldPosition;
            child.rotation = worldRotation;
            SetWorldScale(child, worldScale);
            moved++;
        }

        return moved;
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            AlmostZero(parentScale.x) ? worldScale.x : worldScale.x / parentScale.x,
            AlmostZero(parentScale.y) ? worldScale.y : worldScale.y / parentScale.y,
            AlmostZero(parentScale.z) ? worldScale.z : worldScale.z / parentScale.z);
    }

    private static bool AlmostZero(float value)
    {
        return Mathf.Abs(value) < 0.0001f;
    }

    private static void EnsureSolidGroundColliders(GameObject groundObject)
    {
        TilemapCollider2D tilemapCollider = groundObject.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
        {
            tilemapCollider = groundObject.AddComponent<TilemapCollider2D>();
        }

        Rigidbody2D body = groundObject.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = groundObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        CompositeCollider2D composite = groundObject.GetComponent<CompositeCollider2D>();
        if (composite == null)
        {
            composite = groundObject.AddComponent<CompositeCollider2D>();
        }

        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tilemapCollider.usedByComposite = true;
        tilemapCollider.usedByEffector = false;
    }

    private static void RefreshGroundColliders(GameObject groundObject)
    {
        TilemapCollider2D tilemapCollider = groundObject.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null)
        {
            // Force composite path rebuild after tile merge.
            bool wasEnabled = tilemapCollider.enabled;
            tilemapCollider.enabled = false;
            tilemapCollider.enabled = wasEnabled;
        }

        CompositeCollider2D composite = groundObject.GetComponent<CompositeCollider2D>();
        if (composite != null)
        {
            Physics2D.SyncTransforms();
            composite.GenerateGeometry();
        }
    }

    private static void RewireGroundTilemapReferences(Scene scene, Tilemap primaryTilemap)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int b = 0; b < behaviours.Length; b++)
            {
                MonoBehaviour behaviour = behaviours[b];
                if (behaviour == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty iterator = serialized.GetIterator();
                bool changed = false;
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object referenced = iterator.objectReferenceValue;
                    if (referenced == null)
                    {
                        continue;
                    }

                    if (referenced is Tilemap referencedTilemap && referencedTilemap != primaryTilemap)
                    {
                        if (!IsGroundCandidateName(referencedTilemap.gameObject.name))
                        {
                            continue;
                        }

                        iterator.objectReferenceValue = primaryTilemap;
                        changed = true;
                    }
                }

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
