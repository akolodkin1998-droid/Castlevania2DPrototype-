using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Castlevania2D.Level;
using System.IO;

/// <summary>
/// After LeftWall↔RightWall swap:
/// - RightWall (Ground_5) sits on the LEFT arena side (former LeftWall placement).
/// - LeftWall (original tiles) sits on the RIGHT arena side (column x=13).
/// Also removes obsolete LeftWall (1).
/// </summary>
public static class RightWallRebuildEditor
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string RebuildFlagPath = "Temp/rebuild_right_wall.flag";
    private const string WallBodyTilePath =
        "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_5.asset";

    // After swap: RightWall occupies the former LeftWall layout.
    private const int RightWallLeftSideColumnX = -12;
    private static readonly Vector3 RightWallLeftSideLocalPosition = new Vector3(-0.97f, 1f, 0f);

    // After swap: LeftWall occupies the former RightWall column.
    private const int LeftWallRightSideColumnX = 13;
    private const int WallMinY = -1;
    private const int WallMaxY = 5;
    private const int LeftWallMaxY = 4;

    static RightWallRebuildEditor()
    {
        EditorApplication.delayCall += TryRebuildFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Rebuild Right Wall")]
    public static void RebuildRightWallMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RebuildRightWallMenu;
            return;
        }

        RebuildRightWall();
        SaveProject();
        Debug.Log("Arena walls rebuilt (LeftWall↔RightWall swapped layout) and project saved.");
    }

    public static void RebuildRightWallBatch()
    {
        RebuildRightWall();
        SaveProject();
        EditorApplication.Exit(0);
    }

    public static void RequestRebuild()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RebuildFlagPath, System.DateTime.UtcNow.ToString("O"));
        TryRebuildFromFlag();
    }

    private static void TryRebuildFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(RebuildFlagPath))
        {
            return;
        }

        File.Delete(RebuildFlagPath);
        RebuildRightWall();
        SaveProject();
        Debug.Log("Arena walls rebuilt from flag and project saved.");
    }

    public static void RebuildRightWall()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Transform gridTransform = FindPlatformGrid();
        if (gridTransform == null)
        {
            Debug.LogError("Platform_Grid was not found. Arena walls were not rebuilt.");
            return;
        }

        DestroyNamedChild(gridTransform, "LeftWall (1)");
        DestroyNamedChild(gridTransform, "RightWall");
        GameObject orphanSecondary = GameObject.Find("LeftWall (1)");
        if (orphanSecondary != null)
        {
            Object.DestroyImmediate(orphanSecondary);
        }

        Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(WallBodyTilePath);
        if (wallTile == null)
        {
            Debug.LogError($"Wall tile was not found at {WallBodyTilePath}.");
            return;
        }

        // RightWall → LEFT side of arena (Ground_5).
        GameObject rightWallObject = CreateWallColumn(
            gridTransform,
            "RightWall",
            RightWallLeftSideLocalPosition,
            RightWallLeftSideColumnX,
            WallMinY,
            LeftWallMaxY,
            wallTile,
            inwardInset: 0f);

        // LeftWall → RIGHT side of arena (keep existing tile if present, else Ground_5).
        MoveOrRebuildLeftWallOnRight(gridTransform, wallTile);

        WireBossTentacleWalls(rightWallObject.GetComponent<Tilemap>());
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void MoveOrRebuildLeftWallOnRight(Transform gridTransform, Tile fallbackTile)
    {
        Transform leftWallTransform = FindNamedChild(gridTransform, "LeftWall");
        Tilemap leftTilemap;
        Tile keepTile = null;

        if (leftWallTransform != null)
        {
            leftTilemap = leftWallTransform.GetComponent<Tilemap>();
            if (leftTilemap != null)
            {
                keepTile = FindAnyTile(leftTilemap);
            }

            Object.DestroyImmediate(leftWallTransform.gameObject);
        }

        Tile bodyTile = keepTile != null ? keepTile : fallbackTile;
        CreateWallColumn(
            gridTransform,
            "LeftWall",
            new Vector3(0f, 1f, 0f),
            LeftWallRightSideColumnX,
            WallMinY,
            LeftWallMaxY,
            bodyTile,
            inwardInset: 0f);
    }

    private static GameObject CreateWallColumn(
        Transform gridTransform,
        string objectName,
        Vector3 localPosition,
        int columnX,
        int minY,
        int maxY,
        Tile wallTile,
        float inwardInset)
    {
        GameObject wallObject = new GameObject(objectName);
        wallObject.transform.SetParent(gridTransform, false);
        wallObject.transform.localPosition = localPosition;
        wallObject.transform.localRotation = Quaternion.identity;
        wallObject.transform.localScale = Vector3.one;

        Tilemap tilemap = wallObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = wallObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        for (int y = minY; y <= maxY; y++)
        {
            tilemap.SetTile(new Vector3Int(columnX, y, 0), wallTile);
        }

        Rigidbody2D body = wallObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        WallColumnGridCollider2D gridCollider = wallObject.AddComponent<WallColumnGridCollider2D>();
        SerializedObject serializedCollider = new SerializedObject(gridCollider);
        SerializedProperty inwardInsetProp = serializedCollider.FindProperty("inwardInset");
        if (inwardInsetProp != null)
        {
            inwardInsetProp.floatValue = Mathf.Max(0f, inwardInset);
        }

        SerializedProperty manualProp = serializedCollider.FindProperty("manualCollider");
        if (manualProp != null)
        {
            // After tile rebuild, sync once from tiles; then leave Offset/Size editable.
            manualProp.boolValue = true;
        }

        serializedCollider.ApplyModifiedPropertiesWithoutUndo();
        gridCollider.RebuildFromTiles();
        return wallObject;
    }

    private static void WireBossTentacleWalls(Tilemap rightWallTilemap)
    {
        GameObject boss = GameObject.Find("Boss_Flower");
        if (boss == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = boss.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour.GetType().Name != "FlowerBossTentacleAttack2D")
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty rightProp = serialized.FindProperty("rightWallTilemap");
            if (rightProp != null)
            {
                rightProp.objectReferenceValue = rightWallTilemap;
            }

            SerializedProperty rightNameProp = serialized.FindProperty("rightWallObjectName");
            if (rightNameProp != null)
            {
                rightNameProp.stringValue = "RightWall";
            }

            GameObject leftWallObject = GameObject.Find("LeftWall");
            SerializedProperty leftProp = serialized.FindProperty("leftWallTilemap");
            if (leftProp != null && leftWallObject != null)
            {
                leftProp.objectReferenceValue = leftWallObject.GetComponent<Tilemap>();
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            break;
        }
    }

    private static Tile FindAnyTile(Tilemap tilemap)
    {
        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile is Tile concrete)
                {
                    return concrete;
                }
            }
        }

        return null;
    }

    private static Transform FindPlatformGrid()
    {
        GameObject gridObject = GameObject.Find("Platform_Grid");
        return gridObject != null ? gridObject.transform : null;
    }

    private static Transform FindNamedChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void DestroyNamedChild(Transform parent, string childName)
    {
        Transform child = FindNamedChild(parent, childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        GameObject orphan = GameObject.Find(childName);
        if (orphan != null)
        {
            Object.DestroyImmediate(orphan);
        }
    }

    private static void SaveProject()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }
}
