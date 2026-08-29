using System.IO;
using Castlevania2D.Health;
using Castlevania2D.Hub;
using Castlevania2D.Level;
using Castlevania2D.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports village plates (Слой0 foreground / Слой1 near / Слой2 far), creates Hub.unity,
/// tiles them horizontally, and wires X-only parallax.
/// </summary>
[InitializeOnLoad]
public static class VillageHubSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Hub.unity";
    private const string DestFolder = "Assets/Art/Backgrounds/Village/Lines";
    private const string FlagPath = "Temp/setup_village_hub.flag";
    private const string BackgroundRootName = "Background";
    private const string HubGroupName = "HubVillage";
    private const string PlayerPrefabPath = "Assets/Animations/Player/Demo/HeroKnight.prefab";
    private const string PlayerObjectName = "Player_HeroKnight";
    private const string IzbaAssetPath = "Assets/Art/Backgrounds/Village/Props/HubIzba.png";
    private const string IzbaObjectName = "HubIzba";
    private const string IzbaFlagPath = "Temp/setup_hub_izba.flag";
    private const string WallsFlagPath = "Temp/setup_hub_walls.flag";
    private const string InteriorFlagPath = "Temp/setup_hub_interior.flag";
    private const string InteriorArtFlagPath = "Temp/setup_hub_interior_art.flag";
    private const string NpcFlagPath = "Temp/setup_hub_npc.flag";
    private const string RestorePlayerFlagPath = "Temp/restore_hub_player.flag";
    private static readonly Vector3 FallbackPlayerPosition = new Vector3(-30.7f, -5.08f, 0f);
    private const string InteriorFolder = "Assets/Art/Backgrounds/Village/Interior";
    private const string InteriorSpritePath = InteriorFolder + "/IzbaInterior.png";
    private const string TableSpritePath = InteriorFolder + "/IzbaTable.png";
    private const string InteriorRootName = "HubInterior";
    private const string RoomsObjectName = "HubRooms";
    private const int InteriorPropSortingOrder = -20;
    private const string NpcFolder = "Assets/Art/Sprites/Characters/NPCs/VaryagMerchant/Idle";
    private const string NpcObjectName = "VaryagMerchant";
    private const int NpcSortingOrder = InteriorPropSortingOrder - 1;
    private const float NpcPixelsPerUnit = 64f;
    private const float NpcIdleFrameRate = 8f;
    private static readonly Vector3 NpcLocalPosition = new Vector3(-0.45f, 1.18f, 0.5f);
    private const string LeftStopWallName = "HubStopWall_Left";
    private const string RightStopWallName = "HubStopWall_Right";
    private const float StopWallHeight = 15f;
    private const float StopWallThickness = 1f;
    private static readonly Vector3 LeftStopWallPosition = new Vector3(-39.73f, -4.83f, 0f);
    private static readonly Vector3 RightStopWallPosition = new Vector3(23.49f, -4.83f, 0f);
    private const float PixelsPerUnit = 32f / 0.75f;
    private const int NearSortingOrder = -100;
    private static readonly Vector3 GroupLocalPosition = Vector3.zero;
    private static readonly Vector2 TiledWorldSize = new Vector2(80f, 9f);
    private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, -3.7f, 0f);
    private static readonly Vector2 CameraFollowOffset = new Vector2(0f, 4.815f);
    private static readonly Color HubSkyColor = new Color(0.12f, 0.14f, 0.18f, 1f);

    private struct LayerSpec
    {
        public readonly string ObjectName;
        public readonly string AssetPath;
        public readonly float LocalZ;
        public readonly int SortingOrder;

        public LayerSpec(string objectName, string assetPath, float localZ, int sortingOrder)
        {
            ObjectName = objectName;
            AssetPath = assetPath;
            LocalZ = localZ;
            SortingOrder = sortingOrder;
        }
    }

    private static readonly LayerSpec[] Layers =
    {
        new LayerSpec("VillageLine2_Far", DestFolder + "/VillageLine2_Far.png", 20f, NearSortingOrder - 2),
        new LayerSpec("VillageLine1_Near", DestFolder + "/VillageLine1_Near.png", 6f, NearSortingOrder),
        new LayerSpec("VillageLine0_Foreground", DestFolder + "/VillageLine0_Foreground.png", 2f, NearSortingOrder + 2)
    };

    static VillageHubSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.delayCall += TryPlaceIzbaFromFlag;
        EditorApplication.delayCall += TryPlaceWallsFromFlag;
        EditorApplication.delayCall += TryPlaceInteriorFromFlag;
        EditorApplication.delayCall += TryPlaceNpcFromFlag;
        EditorApplication.delayCall += TryRestorePlayerFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Restore Hub Player If Missing")]
    public static void RestoreHubPlayerMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Hub Player", RestoreHubPlayerIfMissing(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Hub Player", "Failed:\n" + exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Place Varyag Merchant In Hub")]
    public static void PlaceVaryagMerchantMenu()
    {
        try
        {
            Scene scene = GetOrCreateHubScene();
            string summary = EnsureVaryagMerchant(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Varyag Merchant", summary, "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Varyag Merchant", "Failed:\n" + exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Fix Hub Interior Camera And Props")]
    public static void FixInteriorViewMenu()
    {
        try
        {
            Scene scene = GetOrCreateHubScene();
            string summary = EnsureHubInterior(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Hub Interior", summary, "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Hub Interior", "Failed:\n" + exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Place Hub Izba")]
    public static void PlaceIzbaMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Hub Izba", PlaceIzbaInHubScene(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Hub Izba", "Failed:\n" + exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Castlevania 2D/Setup Village Hub Scene")]
    public static void SetupMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Village Hub", SetupVillageHub(), "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Village Hub", "Failed:\n" + exception.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod VillageHubSetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        try
        {
            Debug.Log(SetupVillageHub());
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RequestSetup()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FlagPath, System.DateTime.UtcNow.ToString("O"));
        TrySetupFromFlag();
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(FlagPath))
        {
            return;
        }

        File.Delete(FlagPath);
        try
        {
            Debug.Log(SetupVillageHub());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryPlaceIzbaFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(IzbaFlagPath))
        {
            return;
        }

        File.Delete(IzbaFlagPath);
        try
        {
            Debug.Log(PlaceIzbaInHubScene());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryPlaceWallsFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(WallsFlagPath))
        {
            return;
        }

        File.Delete(WallsFlagPath);
        try
        {
            Scene scene = GetOrCreateHubScene();
            string summary = EnsureHubStopWalls(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(summary);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryPlaceInteriorFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (File.Exists(InteriorFlagPath))
        {
            File.Delete(InteriorFlagPath);
            try
            {
                Scene scene = GetOrCreateHubScene();
                string summary = EnsureHubInterior(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(summary);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            return;
        }

        if (!File.Exists(InteriorArtFlagPath))
        {
            return;
        }

        File.Delete(InteriorArtFlagPath);
        try
        {
            Scene scene = GetOrCreateHubScene();
            string summary = RefreshInteriorBackground(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(summary);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryPlaceNpcFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(NpcFlagPath))
        {
            return;
        }

        File.Delete(NpcFlagPath);
        try
        {
            Scene scene = GetOrCreateHubScene();
            string summary = EnsureVaryagMerchant(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(summary);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void TryRestorePlayerFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(RestorePlayerFlagPath))
        {
            return;
        }

        File.Delete(RestorePlayerFlagPath);
        try
        {
            Debug.Log(RestoreHubPlayerIfMissing());
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static string RestoreHubPlayerIfMissing()
    {
        Scene scene = GetOrCreateHubScene();
        GameObject player = FindPlayer(scene);
        if (player == null)
        {
            // Memory scene dropped the prefab; reload disk YAML instead of duplicating.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            player = FindPlayer(scene);
        }

        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Missing player prefab:\n" + PlayerPrefabPath);
            }

            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = PlayerObjectName;
            player.transform.SetParent(null, true);
            player.transform.position = FallbackPlayerPosition;
            player.transform.rotation = Quaternion.identity;

            if (player.GetComponent<Health>() == null)
            {
                Health health = player.AddComponent<Health>();
                SerializedObject healthSo = new SerializedObject(health);
                SerializedProperty maxHealth = healthSo.FindProperty("maxHealth");
                SerializedProperty destroyOnDeath = healthSo.FindProperty("destroyOnDeath");
                if (maxHealth != null)
                {
                    maxHealth.intValue = 1000;
                }

                if (destroyOnDeath != null)
                {
                    destroyOnDeath.boolValue = false;
                }

                healthSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EnsureCameraFollow(scene, player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Player_HeroKnight is in Hub at " + player.transform.position + ".";
    }

    public static string PlaceIzbaInHubScene()
    {
        CopyIzbaSourceIfNeeded();
        AssetDatabase.Refresh();
        Scene scene = GetOrCreateHubScene();
        string summary = EnsureIzba(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return summary;
    }

    public static string SetupVillageHub()
    {
        EnsureFolder(DestFolder);
        CopySourcesIfNeeded();
        AssetDatabase.Refresh();

        Scene scene = GetOrCreateHubScene();
        string summary = EnsureInScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddHubToBuildSettings();
        AssetDatabase.SaveAssets();
        return summary;
    }

    public static string EnsureInScene(Scene scene)
    {
        EnsureFolder(DestFolder);
        CopySourcesIfNeeded();
        AssetDatabase.Refresh();

        GameObject group = FindOrCreateHubGroup(scene);
        PlaceLayers(group);
        EnsureCamera(scene);
        EnsureGround(group);
        GameObject player = EnsurePlayer(scene);
        EnsureCameraFollow(scene, player);
        EnsureParallax(scene);
        EnsureIzba(scene);
        EnsureHubStopWalls(scene);
        EnsureHubInterior(scene);
        return "Hub scene: village, izba interior, stop walls, player.";
    }

    private static Scene GetOrCreateHubScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded && scene.path == ScenePath)
        {
            return scene;
        }

        if (File.Exists(ToFullPath(ScenePath)))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(Path.GetDirectoryName(ToFullPath(ScenePath)));
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    private static void CopySourcesIfNeeded()
    {
        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Бэкграунд ФОН",
                "Деревня"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Бэкграунд ФОН",
                "Деревня")
        };

        for (int i = 0; i < sourceFolders.Length; i++)
        {
            string desktop = sourceFolders[i];
            CopyIfPresent(Path.Combine(desktop, "ДеревняСлой0.png"), DestFolder + "/VillageLine0_Foreground.png");
            CopyIfPresent(Path.Combine(desktop, "ДеревняСлой1.png"), DestFolder + "/VillageLine1_Near.png");
            CopyIfPresent(Path.Combine(desktop, "ДеревняСлой2.png"), DestFolder + "/VillageLine2_Far.png");
        }

        CopyIzbaSourceIfNeeded();
    }

    private static void CopyIzbaSourceIfNeeded()
    {
        string[] sources =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "ХабДом.png"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "ХабДом.png")
        };

        EnsureFolder("Assets/Art/Backgrounds/Village/Props");
        for (int i = 0; i < sources.Length; i++)
        {
            if (!File.Exists(sources[i]))
            {
                continue;
            }

            string destFull = ToFullPath(IzbaAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destFull));
            File.Copy(sources[i], destFull, true);
            AssetDatabase.ImportAsset(IzbaAssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureIzbaSprite();
            return;
        }
    }

    private static string EnsureIzba(Scene scene)
    {
        CopyIzbaSourceIfNeeded();
        if (File.Exists(ToFullPath(IzbaAssetPath)))
        {
            ConfigureIzbaSprite();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IzbaAssetPath);
        if (sprite == null)
        {
            throw new FileNotFoundException("Izba sprite missing:\n" + IzbaAssetPath);
        }

        GameObject group = FindOrCreateHubGroup(scene);
        Transform existing = group.transform.Find(IzbaObjectName);
        bool created = existing == null;
        GameObject izba = created ? new GameObject(IzbaObjectName) : existing.gameObject;
        izba.name = IzbaObjectName;
        izba.transform.SetParent(group.transform, false);

        if (created)
        {
            GameObject player = FindPlayer(scene);
            float x = player != null ? player.transform.position.x : 0f;
            float y = player != null ? player.transform.position.y + 1.2f : -3.6f;
            izba.transform.position = new Vector3(x, y, 1.2f);
            izba.transform.localRotation = Quaternion.identity;
            izba.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = izba.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = izba.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.sortingOrder = -20;
        renderer.color = Color.white;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        return "HubIzba behind the player (sorting -20).";
    }

    private static string EnsureHubStopWalls(Scene scene)
    {
        BoxCollider2D left = EnsureStopWall(scene, LeftStopWallName, LeftStopWallPosition);
        BoxCollider2D right = EnsureStopWall(scene, RightStopWallName, RightStopWallPosition);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = FindNamed(scene, "Main Camera");
            camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        }

        if (camera != null)
        {
            CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<CameraFollow2D>();
            }

            SerializedObject so = new SerializedObject(follow);
            SerializedProperty wallsProperty = so.FindProperty("cameraStopWalls");
            SerializedProperty clampToSceneWalls = so.FindProperty("clampToSceneWalls");
            if (wallsProperty != null && wallsProperty.isArray)
            {
                wallsProperty.arraySize = 2;
                wallsProperty.GetArrayElementAtIndex(0).objectReferenceValue = left;
                wallsProperty.GetArrayElementAtIndex(1).objectReferenceValue = right;
            }

            if (clampToSceneWalls != null)
            {
                clampToSceneWalls.boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return "Hub stop walls: left " + LeftStopWallPosition + ", right " + RightStopWallPosition + ".";
    }

    private static string EnsureHubInterior(Scene scene)
    {
        CopyInteriorSourcesIfNeeded();
        EnsureFolder(InteriorFolder);
        if (File.Exists(ToFullPath(InteriorSpritePath)))
        {
            ConfigureAsSprite(InteriorSpritePath, 32f, SpriteAlignment.BottomCenter, TextureWrapMode.Clamp);
        }

        if (File.Exists(ToFullPath(TableSpritePath)))
        {
            ConfigureAsSprite(TableSpritePath, 64f, SpriteAlignment.BottomCenter, TextureWrapMode.Clamp);
        }

        Sprite interiorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(InteriorSpritePath);
        Sprite tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TableSpritePath);
        Sprite promptSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/InteractPrompt_F.png");
        if (interiorSprite == null)
        {
            throw new FileNotFoundException("Missing interior sprite:\n" + InteriorSpritePath);
        }

        GameObject rooms = FindNamed(scene, RoomsObjectName);
        if (rooms == null)
        {
            rooms = new GameObject(RoomsObjectName);
            SceneManager.MoveGameObjectToScene(rooms, scene);
        }

        HubRoomTransition2D transition = rooms.GetComponent<HubRoomTransition2D>();
        if (transition == null)
        {
            transition = rooms.AddComponent<HubRoomTransition2D>();
        }

        GameObject interior = FindNamed(scene, InteriorRootName);
        if (interior == null)
        {
            interior = new GameObject(InteriorRootName);
            SceneManager.MoveGameObjectToScene(interior, scene);
            interior.transform.position = new Vector3(80f, 40f, 0f);
            interior.transform.rotation = Quaternion.identity;
            interior.transform.localScale = Vector3.one;
        }

        SpriteRenderer interiorRenderer = EnsureChildSprite(
            interior.transform,
            "IzbaInterior",
            interiorSprite,
            -50,
            new Vector3(0f, 0f, 8f));
        interiorRenderer.drawMode = SpriteDrawMode.Simple;

        if (tableSprite != null)
        {
            EnsureChildSprite(
                interior.transform,
                "IzbaTable",
                tableSprite,
                InteriorPropSortingOrder,
                new Vector3(0.15f, 1.35f, 0.2f));
        }

        EnsureStaticBox(interior.transform, "InteriorFloor", new Vector3(0f, 0.45f, 0f), new Vector2(7.4f, 0.9f), false);
        EnsureStaticBox(interior.transform, "PechkaCollider", new Vector3(2.35f, 2.15f, 0f), new Vector2(2.7f, 2.3f), false);
        EnsureInteriorSideWalls(interior.transform, interiorRenderer);

        HubRoomView2D roomView = interior.GetComponent<HubRoomView2D>();
        if (roomView == null)
        {
            roomView = interior.AddComponent<HubRoomView2D>();
        }

        SerializedObject viewSo = new SerializedObject(roomView);
        SerializedProperty frameProp = viewSo.FindProperty("frameSprite");
        if (frameProp != null)
        {
            frameProp.objectReferenceValue = interiorRenderer;
        }

        viewSo.ApplyModifiedPropertiesWithoutUndo();
        roomView.EditorAssign(interiorRenderer);

        Transform interiorSpawn = EnsureMarker(interior.transform, "InteriorSpawn", new Vector3(0f, 1.85f, 0f));
        Transform exitPoint = EnsureMarker(interior.transform, "InteriorExit", new Vector3(0f, 0.55f, 0f));

        GameObject izba = FindNamedDeep(scene, IzbaObjectName);
        Transform exteriorSpawn = EnsureMarker(
            izba != null ? izba.transform : rooms.transform,
            "ExteriorSpawn",
            izba != null ? new Vector3(0f, 0.7f, 0f) : new Vector3(-31.94f, -4.5f, 0f));

        HubDoorInteract2D enterDoor = (izba != null ? izba : rooms).GetComponent<HubDoorInteract2D>();
        if (enterDoor == null && izba != null)
        {
            enterDoor = izba.AddComponent<HubDoorInteract2D>();
        }

        if (enterDoor != null)
        {
            enterDoor.EditorAssign(transition, interiorSpawn, promptSprite);
        }

        GameObject exitObject = exitPoint.gameObject;
        HubDoorInteract2D exitDoor = exitObject.GetComponent<HubDoorInteract2D>();
        if (exitDoor == null)
        {
            exitDoor = exitObject.AddComponent<HubDoorInteract2D>();
        }

        SerializedObject exitSo = new SerializedObject(exitDoor);
        SerializedProperty distance = exitSo.FindProperty("interactionDistance");
        SerializedProperty promptPos = exitSo.FindProperty("promptLocalPosition");
        if (distance != null)
        {
            distance.floatValue = 1.6f;
        }

        if (promptPos != null)
        {
            promptPos.vector3Value = new Vector3(0f, 1.6f, 0f);
        }

        exitSo.ApplyModifiedPropertiesWithoutUndo();
        exitDoor.EditorAssign(transition, exteriorSpawn, promptSprite);

        transition.EditorAssignInterior(interiorSpawn, roomView);

        SerializedObject enterSo = enterDoor != null ? new SerializedObject(enterDoor) : null;
        if (enterSo != null)
        {
            SerializedProperty enterDistance = enterSo.FindProperty("interactionDistance");
            SerializedProperty enterPrompt = enterSo.FindProperty("promptLocalPosition");
            if (enterDistance != null)
            {
                enterDistance.floatValue = 2.4f;
            }

            if (enterPrompt != null)
            {
                enterPrompt.vector3Value = new Vector3(0f, 4.1f, 0f);
            }

            enterSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EnsureVaryagMerchant(scene);
        return "Hub interior: table behind player, camera framed on лавка, side walls, varyag NPC.";
    }

    private static string RefreshInteriorBackground(Scene scene)
    {
        CopyInteriorSourcesIfNeeded();
        if (File.Exists(ToFullPath(InteriorSpritePath)))
        {
            ConfigureAsSprite(InteriorSpritePath, 32f, SpriteAlignment.BottomCenter, TextureWrapMode.Clamp);
        }

        Sprite interiorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(InteriorSpritePath);
        if (interiorSprite == null)
        {
            throw new FileNotFoundException("Missing interior sprite:\n" + InteriorSpritePath);
        }

        GameObject interior = FindNamed(scene, InteriorRootName);
        if (interior == null)
        {
            throw new System.InvalidOperationException("HubInterior is missing.");
        }

        SpriteRenderer interiorRenderer = EnsureChildSprite(
            interior.transform,
            "IzbaInterior",
            interiorSprite,
            -50,
            new Vector3(0f, 0f, 8f));
        interiorRenderer.drawMode = SpriteDrawMode.Simple;
        EnsureInteriorSideWalls(interior.transform, interiorRenderer);
        return "Hub interior background replaced (688x384), walls and floor width updated.";
    }

    private static string EnsureVaryagMerchant(Scene scene)
    {
        CopyVaryagSourcesIfNeeded();
        EnsureFolder("Assets/Art/Sprites/Characters/NPCs");
        EnsureFolder("Assets/Art/Sprites/Characters/NPCs/VaryagMerchant");
        EnsureFolder(NpcFolder);

        string[] framePaths = Directory.GetFiles(ToFullPath(NpcFolder), "VaryagMerchant_Idle_*.png");
        System.Array.Sort(framePaths, System.StringComparer.OrdinalIgnoreCase);
        if (framePaths.Length == 0)
        {
            throw new FileNotFoundException("Missing Varyag idle frames in:\n" + NpcFolder);
        }

        var frames = new Sprite[framePaths.Length];
        for (int i = 0; i < framePaths.Length; i++)
        {
            string assetPath = NpcFolder + "/" + Path.GetFileName(framePaths[i]);
            ConfigureAsSprite(assetPath, NpcPixelsPerUnit, SpriteAlignment.BottomCenter, TextureWrapMode.Clamp);
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (frames[i] == null)
            {
                throw new FileNotFoundException("Failed to import NPC sprite:\n" + assetPath);
            }
        }

        GameObject interior = FindNamed(scene, InteriorRootName);
        if (interior == null)
        {
            throw new System.InvalidOperationException("HubInterior is missing. Open Hub.unity first.");
        }

        Transform existing = interior.transform.Find(NpcObjectName);
        bool created = existing == null;
        GameObject npc = created ? new GameObject(NpcObjectName) : existing.gameObject;
        npc.name = NpcObjectName;
        npc.transform.SetParent(interior.transform, false);
        if (created)
        {
            npc.transform.localPosition = NpcLocalPosition;
            npc.transform.localRotation = Quaternion.identity;
            npc.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = npc.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = npc.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = frames[0];
        renderer.sortingOrder = NpcSortingOrder;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.maskInteraction = SpriteMaskInteraction.None;

        HubIdleNpc2D idle = npc.GetComponent<HubIdleNpc2D>();
        if (idle == null)
        {
            idle = npc.AddComponent<HubIdleNpc2D>();
        }

        SerializedObject idleSo = new SerializedObject(idle);
        SerializedProperty framesProp = idleSo.FindProperty("idleFrames");
        SerializedProperty rateProp = idleSo.FindProperty("frameRate");
        SerializedProperty pingPongProp = idleSo.FindProperty("pingPong");
        if (framesProp != null && framesProp.isArray)
        {
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }

        if (rateProp != null)
        {
            rateProp.floatValue = NpcIdleFrameRate;
        }

        if (pingPongProp != null)
        {
            pingPongProp.boolValue = true;
        }

        idleSo.ApplyModifiedPropertiesWithoutUndo();
        idle.EditorAssign(frames, NpcIdleFrameRate);

        if (npc.GetComponent<NpcTalk2D>() == null)
        {
            npc.AddComponent<NpcTalk2D>();
        }

        HubMerchantTalk2D talk = npc.GetComponent<HubMerchantTalk2D>();
        if (talk == null)
        {
            talk = npc.AddComponent<HubMerchantTalk2D>();
        }

        Sprite promptSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/InteractPrompt_F.png");
        GameObject rooms = FindNamed(scene, RoomsObjectName);
        HubRoomTransition2D transition = rooms != null
            ? rooms.GetComponent<HubRoomTransition2D>()
            : null;
        GameObject exterior = FindNamedDeep(scene, "ExteriorSpawn");
        talk.EditorAssign(
            transition,
            exterior != null ? exterior.transform : null,
            promptSprite);

        BoxCollider2D box = npc.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Object.DestroyImmediate(box);
        }

        Rigidbody2D body = npc.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            Object.DestroyImmediate(body);
        }

        return "Varyag merchant idle NPC in лавка (behind table, behind player).";
    }

    private static void CopyVaryagSourcesIfNeeded()
    {
        string[] sourceFolders =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Варяг игрок"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Варяг игрок")
        };

        string sourceFolder = null;
        for (int i = 0; i < sourceFolders.Length; i++)
        {
            if (Directory.Exists(sourceFolders[i]))
            {
                sourceFolder = sourceFolders[i];
                break;
            }
        }

        if (sourceFolder == null)
        {
            return;
        }

        EnsureFolder("Assets/Art/Sprites/Characters/NPCs");
        EnsureFolder("Assets/Art/Sprites/Characters/NPCs/VaryagMerchant");
        EnsureFolder(NpcFolder);

        string destFull = ToFullPath(NpcFolder);
        if (Directory.GetFiles(destFull, "VaryagMerchant_Idle_*.png").Length > 0)
        {
            return;
        }

        string[] sourceFiles = Directory.GetFiles(sourceFolder, "*.png");
        System.Array.Sort(sourceFiles, System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destAssetPath = NpcFolder + "/VaryagMerchant_Idle_" + i.ToString("00") + ".png";
            File.Copy(sourceFiles[i], ToFullPath(destAssetPath), true);
            AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceUpdate);
        }
    }

    private static void EnsureInteriorSideWalls(Transform interior, SpriteRenderer interiorRenderer)
    {
        float width = 8f;
        float height = 8f;
        if (interiorRenderer != null && interiorRenderer.sprite != null)
        {
            Vector2 spriteSize = interiorRenderer.sprite.bounds.size;
            width = spriteSize.x;
            height = spriteSize.y;
        }

        const float wallThickness = 0.8f;
        float wallX = (width * 0.5f) + (wallThickness * 0.5f);
        float wallY = height * 0.5f;
        Vector2 wallSize = new Vector2(wallThickness, height);
        EnsureStaticBox(interior, "InteriorWall_Left", new Vector3(-wallX, wallY, 0f), wallSize, true);
        EnsureStaticBox(interior, "InteriorWall_Right", new Vector3(wallX, wallY, 0f), wallSize, true);

        Transform floor = interior.Find("InteriorFloor");
        if (floor == null)
        {
            return;
        }

        BoxCollider2D floorBox = floor.GetComponent<BoxCollider2D>();
        if (floorBox == null)
        {
            return;
        }

        float floorWidth = Mathf.Max(1f, width - wallThickness);
        floorBox.size = new Vector2(floorWidth, floorBox.size.y);
        Vector3 floorPos = floor.localPosition;
        floor.localPosition = new Vector3(0f, floorPos.y, floorPos.z);
    }

    private static void CopyInteriorSourcesIfNeeded()
    {
        string[] interiors =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Лавка\u2026.png"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Лавка\u2026.png"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "лавка.png"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "лавка.png")
        };
        string[] tables =
        {
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                "Персонаж",
                "Стол.png"),
            Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Рабочий стол",
                "Персонаж",
                "Стол.png")
        };

        EnsureFolder(InteriorFolder);
        CopyFirstExisting(interiors, InteriorSpritePath);
        CopyFirstExisting(tables, TableSpritePath);
    }

    private static void CopyFirstExisting(string[] sources, string destAssetPath)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (!File.Exists(sources[i]))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ToFullPath(destAssetPath)));
            File.Copy(sources[i], ToFullPath(destAssetPath), true);
            AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceUpdate);
            return;
        }
    }

    private static SpriteRenderer EnsureChildSprite(
        Transform parent,
        string objectName,
        Sprite sprite,
        int sortingOrder,
        Vector3 localPosition)
    {
        Transform existing = parent.Find(objectName);
        bool created = existing == null;
        GameObject child = created ? new GameObject(objectName) : existing.gameObject;
        child.name = objectName;
        child.transform.SetParent(parent, false);
        if (created)
        {
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Simple;
        return renderer;
    }

    private static void EnsureStaticBox(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector2 size,
        bool overwriteTransform)
    {
        Transform existing = parent.Find(objectName);
        bool created = existing == null;
        GameObject child = created ? new GameObject(objectName) : existing.gameObject;
        child.name = objectName;
        child.transform.SetParent(parent, false);
        if (created || overwriteTransform)
        {
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }

        BoxCollider2D box = child.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = child.AddComponent<BoxCollider2D>();
        }

        if (created || overwriteTransform)
        {
            box.isTrigger = false;
            box.size = size;
            box.offset = Vector2.zero;
        }

        Rigidbody2D body = child.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = child.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
        body.gravityScale = 0f;
    }

    private static Transform EnsureMarker(Transform parent, string objectName, Vector3 localPosition)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject marker = new GameObject(objectName);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one;
        return marker.transform;
    }

    private static GameObject FindNamedDeep(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChildNamed(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildNamed(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = FindChildNamed(current.GetChild(i), objectName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static BoxCollider2D EnsureStopWall(Scene scene, string wallName, Vector3 position)
    {
        GameObject wall = FindNamed(scene, wallName);
        if (wall == null)
        {
            wall = new GameObject(wallName);
            SceneManager.MoveGameObjectToScene(wall, scene);
        }

        wall.transform.SetParent(null, true);
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;

        BoxCollider2D box = wall.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = wall.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = false;
        box.offset = new Vector2(0f, StopWallHeight * 0.5f);
        box.size = new Vector2(StopWallThickness, StopWallHeight);

        Rigidbody2D body = wall.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = wall.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
        body.gravityScale = 0f;
        return box;
    }

    private static void CopyIfPresent(string sourceFullPath, string destAssetPath)
    {
        string destFull = ToFullPath(destAssetPath);
        if (!File.Exists(sourceFullPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destFull));
        File.Copy(sourceFullPath, destFull, true);
        AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureAsSprite(destAssetPath);
    }

    private static void PlaceLayers(GameObject group)
    {
        for (int i = 0; i < Layers.Length; i++)
        {
            LayerSpec spec = Layers[i];
            ConfigureAsSprite(spec.AssetPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.AssetPath);
            if (sprite == null)
            {
                throw new FileNotFoundException("Missing sprite " + spec.AssetPath);
            }

            Transform existing = group.transform.Find(spec.ObjectName);
            GameObject layer = existing != null ? existing.gameObject : new GameObject(spec.ObjectName);
            layer.name = spec.ObjectName;
            layer.transform.SetParent(group.transform, false);
            layer.transform.localPosition = new Vector3(0f, 0f, spec.LocalZ);
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = layer.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = TiledWorldSize;
            renderer.sortingOrder = spec.SortingOrder;
            renderer.color = Color.white;
            renderer.maskInteraction = SpriteMaskInteraction.None;
        }

        group.transform.localPosition = GroupLocalPosition;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
    }

    private static GameObject FindOrCreateHubGroup(Scene scene)
    {
        GameObject background = FindNamed(scene, BackgroundRootName);
        if (background == null)
        {
            background = new GameObject(BackgroundRootName);
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.position = Vector3.zero;
        }

        Transform groupTransform = background.transform.Find(HubGroupName);
        if (groupTransform != null)
        {
            return groupTransform.gameObject;
        }

        GameObject created = new GameObject(HubGroupName);
        created.transform.SetParent(background.transform, false);
        return created;
    }

    private static void EnsureCamera(Scene scene)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = FindNamed(scene, "Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.tag = "MainCamera";
            }

            camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }
        }

        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = HubSkyColor;
        camera.transform.position = new Vector3(
            PlayerSpawnPosition.x,
            PlayerSpawnPosition.y + CameraFollowOffset.y,
            -10f);
        camera.transform.rotation = Quaternion.identity;
    }

    private static GameObject EnsurePlayer(Scene scene)
    {
        GameObject player = FindPlayer(scene);
        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Missing player prefab:\n" + PlayerPrefabPath);
            }

            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        player.name = PlayerObjectName;
        player.transform.SetParent(null, true);
        player.transform.position = PlayerSpawnPosition;
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        if (player.GetComponent<Health>() == null)
        {
            Health health = player.AddComponent<Health>();
            SerializedObject healthSo = new SerializedObject(health);
            SerializedProperty maxHealth = healthSo.FindProperty("maxHealth");
            SerializedProperty destroyOnDeath = healthSo.FindProperty("destroyOnDeath");
            if (maxHealth != null)
            {
                maxHealth.intValue = 1000;
            }

            if (destroyOnDeath != null)
            {
                destroyOnDeath.boolValue = false;
            }

            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return player;
    }

    private static void EnsureCameraFollow(Scene scene, GameObject player)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = FindNamed(scene, "Main Camera");
            camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        }

        if (camera == null || player == null)
        {
            return;
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow2D>();
        }

        SerializedObject so = new SerializedObject(follow);
        SerializedProperty target = so.FindProperty("target");
        SerializedProperty offset = so.FindProperty("offset");
        SerializedProperty smoothTime = so.FindProperty("smoothTime");
        SerializedProperty lockWorldY = so.FindProperty("lockWorldY");
        SerializedProperty clampToSceneWalls = so.FindProperty("clampToSceneWalls");
        if (target != null)
        {
            target.objectReferenceValue = player.transform;
        }

        if (offset != null)
        {
            offset.vector2Value = CameraFollowOffset;
        }

        if (smoothTime != null)
        {
            smoothTime.floatValue = 0.1f;
        }

        if (lockWorldY != null)
        {
            lockWorldY.boolValue = false;
        }

        if (clampToSceneWalls != null)
        {
            clampToSceneWalls.boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindPlayer(Scene scene)
    {
        GameObject named = FindNamed(scene, PlayerObjectName);
        if (named != null)
        {
            return named;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.CompareTag("Player") || root.name == "HeroKnight")
            {
                return root;
            }
        }

        return null;
    }

    private static void EnsureParallax(Scene scene)
    {
        GameObject background = FindNamed(scene, BackgroundRootName);
        if (background == null)
        {
            return;
        }

        SceneBackgroundParallax2D parallax = background.GetComponent<SceneBackgroundParallax2D>();
        if (parallax == null)
        {
            parallax = background.AddComponent<SceneBackgroundParallax2D>();
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(parallax);
        SerializedProperty cameraProperty = so.FindProperty("cameraTransform");
        SerializedProperty affectY = so.FindProperty("affectY");
        if (cameraProperty != null)
        {
            cameraProperty.objectReferenceValue = camera.transform;
        }

        if (affectY != null)
        {
            affectY.boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureGround(GameObject group)
    {
        Transform existing = group.transform.Find("HubGround");
        GameObject ground = existing != null ? existing.gameObject : new GameObject("HubGround");
        ground.name = "HubGround";
        ground.transform.SetParent(group.transform, false);
        ground.transform.localPosition = new Vector3(0f, -4.2f, 0f);
        ground.transform.localRotation = Quaternion.identity;
        ground.transform.localScale = Vector3.one;

        BoxCollider2D box = ground.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = ground.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = false;
        box.size = new Vector2(TiledWorldSize.x, 1f);
        box.offset = Vector2.zero;

        Rigidbody2D body = ground.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = ground.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
        body.gravityScale = 0f;
    }

    private static void AddHubToBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == ScenePath)
            {
                return;
            }
        }

        var next = new EditorBuildSettingsScene[current.Length + 1];
        for (int i = 0; i < current.Length; i++)
        {
            next[i] = current[i];
        }

        next[current.Length] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = next;
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static void ConfigureAsSprite(string assetPath)
    {
        ConfigureAsSprite(assetPath, PixelsPerUnit, SpriteAlignment.Center, TextureWrapMode.Repeat);
    }

    private static void ConfigureIzbaSprite()
    {
        ConfigureAsSprite(IzbaAssetPath, 64f, SpriteAlignment.BottomCenter, TextureWrapMode.Clamp);
    }

    private static void ConfigureAsSprite(
        string assetPath,
        float pixelsPerUnit,
        SpriteAlignment alignment,
        TextureWrapMode wrapMode)
    {
        if (!File.Exists(ToFullPath(assetPath)))
        {
            throw new FileNotFoundException("Village PNG not found:\n" + assetPath);
        }

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer == null)
        {
            throw new InvalidDataException("No TextureImporter for " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = wrapMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)alignment;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = 2048;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
