using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Castlevania2D.Player;
using Castlevania2D.UI;
using Castlevania2D.Level;

[InitializeOnLoad]
public static class PrototypeSceneSnapshotRestore
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string RestoreFlagPath = "Temp/restore_prototype_scene.flag";
    private const string CainosTexturePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

    private struct SpritePlacement
    {
        public string ObjectName;
        public string SpritePath;
        public string CainosSpriteName;
        public string TileAssetPath;
        public Vector3 Position;
        public Vector3 Scale;
        public Vector3 EulerAngles;
        public int SortingOrder;
    }

    static PrototypeSceneSnapshotRestore()
    {
        EditorApplication.delayCall += TryRestoreFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Restore Prototype Scene Snapshot")]
    public static void RestorePrototypeSceneSnapshotMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RestorePrototypeSceneSnapshot;
            return;
        }

        RestorePrototypeSceneSnapshot();
    }

    public static void RequestRestore()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RestoreFlagPath, DateTime.UtcNow.ToString("O"));
        TryRestoreFromFlag();
    }

    private static void TryRestoreFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(RestoreFlagPath))
        {
            return;
        }

        File.Delete(RestoreFlagPath);
        RestorePrototypeSceneSnapshot();
    }

    public static void RestorePrototypeSceneSnapshotBatch()
    {
        RestorePrototypeSceneSnapshot();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void RestorePrototypeSceneSnapshot()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        RestoreHud();
        RestoreSceneLayoutFromSnapshot();
        RestoreBackgroundLayers();
        RestoreDecorOneWayPlatforms();
        // Walls must match post-swap layout (RightWall left / LeftWall right). Rebuild last
        // so layout snapshot never reintroduces LeftWall(1) or pre-swap positions.
        RestoreArenaWalls();
        CameraStopWallSetupEditor.EnsureInScene(scene);
        ClearRightSideObstacles();
        RestoreSnakePlacement();
        DisableHostileEnemiesInScene();
        // Camera last: layout snapshot may still store arena-center Main Camera pose.
        RestoreCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        // Etalon as of 2026-07-27: source backup Prototype_backup_20260727_2047
        // (+ PrototypeSceneLayoutSnapshot_backup_20260727_2047.json → live JSON).
        Debug.Log("Prototype scene snapshot restored (follow camera, etalon 2026-07-27).");
    }

    // Flag-driven restore: Temp/restore_prototype_scene.flag

    private static void RestoreSceneLayoutFromSnapshot()
    {
        if (!System.IO.File.Exists(PrototypeSceneLayoutSnapshotEditor.SnapshotPath))
        {
            RestoreBoss();
            RestoreBackgroundLayers();
            return;
        }

        PrototypeSceneLayoutSnapshotEditor.RestoreSceneLayoutSnapshot();
    }

    // refresh

    private static void RestoreCamera()
    {
        Camera camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            return;
        }

        ConfigureFollowCamera(camera);
    }

    private static void ConfigureFollowCamera(Camera camera)
    {
        ArenaFixedCamera2D fixedCamera = camera.GetComponent<ArenaFixedCamera2D>();
        if (fixedCamera != null)
        {
            fixedCamera.enabled = false;
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow2D>();
        }

        Transform player = null;
        GameObject playerObject = GameObject.Find("Player_HeroKnight");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        // Zoom-out with locked bottom edge: offsetY = oldOffset + (newSize - oldSize).
        const float followOffsetY = 4.815f;

        SerializedObject so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;
        so.FindProperty("offset").vector2Value = new Vector2(0f, followOffsetY);
        so.FindProperty("smoothTime").floatValue = 0.1f;
        so.FindProperty("lockWorldY").boolValue = false;
        so.FindProperty("clampToSceneWalls").boolValue = false;
        SerializedProperty wallsProperty = so.FindProperty("cameraStopWalls");
        if (wallsProperty != null && wallsProperty.isArray)
        {
            GameObject leftWallObject = GameObject.Find("CameraStopWall");
            GameObject rightWallObject = GameObject.Find("CameraStopWall_Right");
            wallsProperty.arraySize = 2;
            wallsProperty.GetArrayElementAtIndex(0).objectReferenceValue =
                leftWallObject != null ? leftWallObject.GetComponent<Collider2D>() : null;
            wallsProperty.GetArrayElementAtIndex(1).objectReferenceValue =
                rightWallObject != null ? rightWallObject.GetComponent<Collider2D>() : null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        follow.enabled = true;

        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.14f, 0.03f, 0.05f, 1f);

        if (player != null)
        {
            camera.transform.position = new Vector3(player.position.x, player.position.y + followOffsetY, -10f);
        }
        else
        {
            // Fallback when Player_HeroKnight missing — etalon spawn + follow offset.
            camera.transform.position = new Vector3(-12f, -37.012f + followOffsetY, -10f);
        }

        camera.transform.localScale = Vector3.one;
        camera.transform.rotation = Quaternion.identity;
    }

    private static void RestoreHud()
    {
        GameObject hudCanvas = GameObject.Find("HUD_Canvas");
        if (hudCanvas == null)
        {
            return;
        }

        Camera camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
        Canvas canvas = hudCanvas.GetComponent<Canvas>();
        if (canvas != null && camera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        HudGameViewportFit viewportFit = hudCanvas.GetComponent<HudGameViewportFit>();
        if (viewportFit == null)
        {
            viewportFit = hudCanvas.AddComponent<HudGameViewportFit>();
        }

        if (camera != null)
        {
            SerializedObject serializedFit = new SerializedObject(viewportFit);
            serializedFit.FindProperty("targetCamera").objectReferenceValue = camera;
            serializedFit.ApplyModifiedPropertiesWithoutUndo();
        }

        viewportFit.ApplyLayout();
    }

    private static void RestoreDecorOneWayPlatforms()
    {
        EnsureSpritePlacement(new SpritePlacement
        {
            ObjectName = "TX Village Props - Road Lamp Support",
            CainosSpriteName = "TX Village Props - Road Lamp Support",
            Position = new Vector3(-11.92f, 1.44f, -2.6782627f)
        }, recreateIfExists: true);
        EnsureSpritePlacement(new SpritePlacement
        {
            ObjectName = "TX Village Props - Bounding Platform 01",
            CainosSpriteName = "TX Village Props - Bounding Platform 01",
            // Thin one-way top only (DecorOneWayPlatform2D) — not a side barrier like old Jump Platform.
            Position = new Vector3(11.978565f, 0.023298044f, -3.6489563f)
        }, recreateIfExists: true);

        ConfigureDecorOneWay("TX Village Props - Road Lamp Support");
        ConfigureDecorOneWay("TX Village Props - Bounding Platform 01");
    }

    private static void RestoreArenaWalls()
    {
        // Authoritative wall etalon (2026-07-25 / Prototype_backup_20260725_1329):
        // RightWall (Ground_5) on left at (-0.97, 1), LeftWall on right column x=13,
        // LeftWall (1) removed.
        RestoreRightWall();
    }

    private static void RestoreLeftWall()
    {
        GameObject wallObject = GameObject.Find("LeftWall");
        if (wallObject == null)
        {
            return;
        }

        wallObject.SetActive(true);

        WallColumnGridCollider2D gridCollider = wallObject.GetComponent<WallColumnGridCollider2D>();
        if (gridCollider != null)
        {
            UnityEngine.Object.DestroyImmediate(gridCollider);
        }

        BoxCollider2D boxCollider = wallObject.GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            UnityEngine.Object.DestroyImmediate(boxCollider);
        }

        TilemapCollider2D tilemapCollider = wallObject.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null)
        {
            tilemapCollider.enabled = true;
        }

        CompositeCollider2D compositeCollider = wallObject.GetComponent<CompositeCollider2D>();
        if (compositeCollider != null)
        {
            compositeCollider.enabled = true;
        }
    }

    private static void RestoreRightWall()
    {
        RightWallRebuildEditor.RebuildRightWall();
    }

    private static void ClearRightSideObstacles()
    {
        // Jump Platform 01 remains unwanted. Brick Wall instances are part of the
        // 2026-07-25 live etalon (Prototype_backup_20260725_1329), including
        // Brick Wall (1), and must not be destroyed.
        DestroySceneObjectIncludingInactive("TX Village Props - Jump Platform 01");
    }

    private static void DestroySceneObjectIncludingInactive(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < transforms.Length; t++)
            {
                if (transforms[t] == null || transforms[t].name != objectName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(transforms[t].gameObject);
                return;
            }
        }
    }

    private static void DisableSceneObject(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        target.SetActive(false);
    }

    private static void ClearDecorPhysics(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        DecorOneWayPlatform2D decor = target.GetComponent<DecorOneWayPlatform2D>();
        if (decor != null)
        {
            UnityEngine.Object.DestroyImmediate(decor);
        }

        Collider2D[] colliders = target.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(colliders[i]);
        }

        PlatformEffector2D effector = target.GetComponent<PlatformEffector2D>();
        if (effector != null)
        {
            UnityEngine.Object.DestroyImmediate(effector);
        }
    }

    private static void ConfigureDecorOneWay(string objectName)
    {
        GameObject target = FindSceneObjectIncludingInactive(objectName);
        if (target == null)
        {
            Debug.LogWarning($"One-way decor setup skipped: '{objectName}' not found.");
            return;
        }

        if (!TryGetSpriteRenderer(target, out _))
        {
            Debug.LogWarning($"One-way decor setup skipped: '{objectName}' has no SpriteRenderer.");
            return;
        }

        DecorOneWayPlatform2D platform = target.GetComponent<DecorOneWayPlatform2D>();
        if (platform == null)
        {
            platform = target.AddComponent<DecorOneWayPlatform2D>();
        }

        platform.Apply();
    }

    private static void RestoreBoss()
    {
        GameObject boss = GameObject.Find("Boss_Flower");
        if (boss == null)
        {
            return;
        }

        boss.transform.position = new Vector3(0f, 1.32f, 0f);
        boss.transform.localScale = new Vector3(0.375f, 0.375f, 1f);
    }

    /// <summary>
    /// Snake is not in the layout snapshot; restore placement from etalon
    /// Prototype_backup_20260725_1329 (intentional world Y ≈ -37). Does not
    /// reset HP / destroyOnDeath / hitbox (etalon: HP 1, destroyOnDeath true).
    /// </summary>
    private static void RestoreSnakePlacement()
    {
        GameObject snake = FindSceneObjectIncludingInactive("Enemy_Snake");
        if (snake == null)
        {
            return;
        }

        snake.transform.position = new Vector3(-5f, -37.2f, 0f);
        snake.transform.localScale = new Vector3(0.11666667f, 0.11666667f, 1f);
    }

    private static void RestoreBackgroundLayers()
    {
        SpritePlacement[] placements =
        {
            new SpritePlacement
            {
                ObjectName = "Red Sky",
                SpritePath = "Assets/karsiori/Props/Red Sky.png",
                Position = new Vector3(0.48f, 4.37f, 8f),
                Scale = new Vector3(1.136953f, 1.05f, 1f)
            },
            new SpritePlacement
            {
                ObjectName = "Mountains 1",
                SpritePath = "Assets/karsiori/Props/Mountains 1.png",
                Position = new Vector3(0.6594712f, -6.71f, 5f),
                Scale = new Vector3(1.0625f, 1f, 1f)
            },
            new SpritePlacement
            {
                ObjectName = "Mountains 2",
                SpritePath = "Assets/karsiori/Props/Mountains 2.png",
                Position = new Vector3(0.56f, -2.8182855f, 5f),
                Scale = new Vector3(1.125f, 0.65015626f, 1f)
            },
            new SpritePlacement
            {
                ObjectName = "Mountains 3",
                SpritePath = "Assets/karsiori/Props/Mountains 3.png",
                Position = new Vector3(0.44f, -1.3083955f, 6f),
                Scale = new Vector3(1.06875f, 0.625f, 1f)
            },
            new SpritePlacement
            {
                ObjectName = "Mountains 4",
                SpritePath = "Assets/karsiori/Props/Mountains 4.png",
                Position = new Vector3(0.54f, -0.24f, 6.96f),
                Scale = new Vector3(1.0788158f, 0.70247275f, 0.99116f),
                EulerAngles = new Vector3(10f, 0f, 0f)
            },
            new SpritePlacement
            {
                ObjectName = "Cloud 1",
                SpritePath = "Assets/karsiori/Props/Cloud 1.png",
                Position = new Vector3(11.15f, 6.13f, 8f)
            },
            new SpritePlacement
            {
                ObjectName = "Cloud 3",
                SpritePath = "Assets/karsiori/Props/Cloud 3.png",
                Position = new Vector3(-4.53f, 5.63f, 7f)
            },
            new SpritePlacement
            {
                ObjectName = "Cloud 5",
                SpritePath = "Assets/karsiori/Props/Cloud 5.png",
                Position = new Vector3(-8.33f, 6.44f, 7f)
            },
            new SpritePlacement
            {
                ObjectName = "Cloud 5 (1)",
                SpritePath = "Assets/karsiori/Props/Cloud 5.png",
                Position = new Vector3(-0.65f, 5.78f, 7f)
            },
            new SpritePlacement
            {
                ObjectName = "Cloud 6",
                SpritePath = "Assets/karsiori/Props/Cloud 6.png",
                Position = new Vector3(6.4f, 6.46f, 7f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Grass 02",
                CainosSpriteName = "TX Village Props - Grass 02",
                Position = new Vector3(2.7145946f, 0.02084428f, -3.3285437f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Grass 03",
                CainosSpriteName = "TX Village Props - Grass 03",
                Position = new Vector3(-7.498914f, 0.020845449f, -3.3591862f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Brick Wall",
                CainosSpriteName = "TX Village Props - Brick Wall 01",
                Position = new Vector3(-12.1f, -1.43f, -4.4291973f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Brick Wall (1)",
                CainosSpriteName = "TX Village Props - Brick Wall 01",
                Position = new Vector3(13.35f, -1.35f, -4.4291973f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Road Lamp Support",
                CainosSpriteName = "TX Village Props - Road Lamp Support",
                Position = new Vector3(-11.92f, 1.44f, -2.6782627f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Village Props - Bounding Platform 01",
                CainosSpriteName = "TX Village Props - Bounding Platform 01",
                // Thin one-way top only (DecorOneWayPlatform2D) — not the old Jump Platform side barrier.
                Position = new Vector3(11.978565f, 0.023298044f, -3.6489563f)
            },
            new SpritePlacement
            {
                ObjectName = "TX Tileset Ground_151",
                TileAssetPath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_151.asset",
                Position = new Vector3(-12.417698f, -0.5214846f, 0f)
            }
        };

        for (int i = 0; i < placements.Length; i++)
        {
            EnsureSpritePlacement(placements[i]);
        }

        // Keep karsiori sky/mountains/clouds under one Background root / sorting layer.
        MergeBackgroundLayersEditor.MergeBackgroundInScene(SceneManager.GetActiveScene());
        SceneBackgroundParallaxSetupEditor.EnsureInScene(SceneManager.GetActiveScene());
        ForestLinesSetupEditor.EnsureInScene(SceneManager.GetActiveScene());
    }

    private static void EnsureSpritePlacement(SpritePlacement placement, bool recreateIfExists = false)
    {
        Sprite sprite = ResolveSprite(placement);
        if (sprite == null)
        {
            Debug.LogWarning($"Restore skipped '{placement.ObjectName}': sprite not found.");
            return;
        }

        ApplySpritePlacement(placement, sprite, recreateIfExists, attempt: 0);
    }

    private static void ApplySpritePlacement(SpritePlacement placement, Sprite sprite, bool recreateIfExists, int attempt)
    {
        GameObject target = FindSceneObjectIncludingInactive(placement.ObjectName);
        if (target != null && (recreateIfExists || attempt > 0 || !HasUsableSpriteRenderer(target)))
        {
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
        }

        if (target == null)
        {
            target = new GameObject(placement.ObjectName);
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);

        if (!target.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer = target.AddComponent<SpriteRenderer>();
        }

        if (renderer == null)
        {
            if (attempt < 1)
            {
                ApplySpritePlacement(placement, sprite, recreateIfExists: true, attempt: attempt + 1);
                return;
            }

            Debug.LogError($"Restore failed: could not add SpriteRenderer to '{placement.ObjectName}'.");
            return;
        }

        try
        {
            renderer.sprite = sprite;
            renderer.sortingOrder = placement.SortingOrder;
        }
        catch (MissingComponentException)
        {
            if (attempt < 1)
            {
                ApplySpritePlacement(placement, sprite, recreateIfExists: true, attempt: attempt + 1);
                return;
            }

            Debug.LogError($"Restore failed: SpriteRenderer on '{placement.ObjectName}' is broken.");
            return;
        }

        Transform transform = target.transform;
        transform.position = placement.Position;
        transform.localScale = placement.Scale == Vector3.zero ? Vector3.one : placement.Scale;
        transform.rotation = Quaternion.Euler(placement.EulerAngles);
    }

    private static bool HasUsableSpriteRenderer(GameObject target)
    {
        if (target == null || !target.TryGetComponent(out SpriteRenderer renderer) || renderer == null)
        {
            return false;
        }

        try
        {
            _ = renderer.enabled;
            return true;
        }
        catch (MissingComponentException)
        {
            return false;
        }
    }

    private static bool TryGetSpriteRenderer(GameObject target, out SpriteRenderer renderer)
    {
        renderer = null;
        return target != null && target.TryGetComponent(out renderer) && renderer != null;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < transforms.Length; t++)
            {
                if (transforms[t] != null && transforms[t].name == objectName)
                {
                    return transforms[t].gameObject;
                }
            }
        }

        return null;
    }

    private static Sprite ResolveSprite(SpritePlacement placement)
    {
        if (!string.IsNullOrEmpty(placement.TileAssetPath))
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(placement.TileAssetPath);
            if (tile != null)
            {
                return tile.sprite;
            }
        }

        if (!string.IsNullOrEmpty(placement.SpritePath))
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(placement.SpritePath);
        }

        if (string.IsNullOrEmpty(placement.CainosSpriteName))
        {
            return null;
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(CainosTexturePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not Sprite sprite || sprite.name != placement.CainosSpriteName)
            {
                continue;
            }

            return sprite;
        }

        return null;
    }

    private static void DisableHostileEnemiesInScene()
    {
        System.Type enemyAiType = System.Type.GetType("Castlevania2D.Enemies.SimpleHostileEnemy2D, Assembly-CSharp");
        if (enemyAiType == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour.GetType() != enemyAiType || behaviour.gameObject.name == "Boss_Flower")
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }
}
