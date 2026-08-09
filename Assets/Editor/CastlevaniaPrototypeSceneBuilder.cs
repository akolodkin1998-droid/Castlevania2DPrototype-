using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Castlevania2D.Player;
using Castlevania2D.UI;

public static class CastlevaniaPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string HeroKnightPrefabPath = "Assets/Animations/Player/Demo/HeroKnight.prefab";
    private const string GroundTilePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_0.asset";
    private const string WallTilePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_148.asset";
    private const string LeftWallTilePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_131.asset";
    private const string WallBodyTilePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_133.asset";
    private const string EnemyControllerPath = "Assets/Animations/Enemies/EvilWizardRuntime.controller";
    private const string EnemyGetHitClipPath = "Assets/Animations/Enemies/EvilWizard_GetHit.anim";
    private const string EnemyDeathClipPath = "Assets/Animations/Enemies/EvilWizard_Death.anim";
    private const int PlayerHealth = 150;
    private const int PlayerAttackDamage = 25;
    private const int EnemyHealth = 75;
    private const int EnemyProjectileDamage = 10;
    private const float PlayerSpawnX = -6f;
    private const float EnemySpawnX = 3f;
    private const float SpawnSurfacePadding = 0.05f;
    private const int GroundLevelY = 0;
    private const int PlatformLevelY = 3;
    private const int SceneMinX = -12;
    private const int SceneMaxX = 12;
    private const int LeftWallX = -12;
    private const int RightWallX = 13;
    private const int WallMinY = -1;
    private const int WallMaxY = 4;
    private const int RightWallExtraHeight = 1;
    private const int FormerCenterPlatformTileCount = 7;
    private const float SidePlatformScale = 2.5f;
    private const int SidePlatformTileCount = 3;
    private const int SidePlatformLeftMinX = -9;
    private const int SidePlatformLeftMaxX = -7;
    private const int RightCenterPlatformMinX = 7;
    private const int RightCenterPlatformMaxX = 11;

    [MenuItem("Tools/Castlevania 2D/Delete Prototype Scene")]
    public static void DeletePrototypeSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => DeletePrototypeScene();
            return;
        }

        DeletePrototypeScene();
    }

    public static void DeletePrototypeSceneBatch()
    {
        DeletePrototypeScene(closeEditor: true);
    }

    public static void CreatePrototypeSceneBatch()
    {
        CreatePrototypeScene();
        EditorApplication.Exit(0);
    }

    private static void DeletePrototypeScene(bool closeEditor = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        if (AssetDatabase.DeleteAsset(ScenePath))
        {
            Debug.Log($"Deleted {ScenePath}");
        }
        else if (!File.Exists(ScenePath))
        {
            Debug.Log($"Prototype scene is already removed from disk: {ScenePath}");
        }
        else
        {
            Debug.LogWarning($"Could not delete {ScenePath}. Close the scene in Unity and try again.");
        }

        AssetDatabase.Refresh();

        if (closeEditor)
        {
            EditorApplication.Exit(0);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Create Prototype Scene")]
    [MenuItem("GameObject/Castlevania 2D/Create Prototype Scene", false, 0)]
    [MenuItem("Assets/Create/Castlevania 2D/Prototype Scene")]
    public static void CreatePrototypeSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => CreatePrototypeScene();
            Debug.Log("Play Mode stopped. Prototype scene creation will continue in Edit Mode.");
            return;
        }

        CreatePrototypeScene();
    }

    private static void CreatePrototypeScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets", "Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        List<PlatformSurface> platformSurfaces = CreatePrototypePlatforms();

        GameObject player = CreatePlayer(platformSurfaces);
        CreateEnemy(player.transform, platformSurfaces);
        CreateCamera(player.transform);
        CreateHud(player);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
        AssetDatabase.Refresh();

        try
        {
            FlowerBossSceneSetup.EnsureFlowerBossIdlePreviewInScene();
            PrototypeSceneSnapshotRestore.RestorePrototypeSceneSnapshot();
            EditorSceneManager.SaveOpenScenes();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Flower Boss idle preview setup skipped: {exception.Message}");
        }

        Debug.Log($"Created prototype scene with snapshot background at {ScenePath}");
    }

    private static GameObject CreatePlayer(IReadOnlyList<PlatformSurface> platformSurfaces)
    {
        GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroKnightPrefabPath);

        if (heroPrefab == null)
        {
            throw new FileNotFoundException($"HeroKnight prefab was not found at {HeroKnightPrefabPath}");
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);
        player.name = "Player_HeroKnight";
        ConfigureCharacterSlopeCollider(player);
        PositionCharacterOnPlatform(player, platformSurfaces, PlayerSpawnX);

        TryAssignTag(player, "Player");
        ConfigurePlayerStats(player);
        ConfigurePlayerAttackHitbox(player);

        return player;
    }

    private static void CreateEnemy(Transform playerTarget, IReadOnlyList<PlatformSurface> platformSurfaces)
    {
        AnimatorController enemyController = CreateEnemyAnimatorController();

        GameObject enemy = new GameObject("Enemy_EvilWizard");
        enemy.transform.position = new Vector3(EnemySpawnX, 0f, 0f);
        enemy.transform.localScale = new Vector3(2f, 2f, 1f);

        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadFirstSprite("Assets/Art/Sprites/Characters/Enemies/Evil Wizard 3/Sprites/Idle.png");

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;

        CapsuleCollider2D collider = enemy.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.8f, 1.6f);
        collider.offset = new Vector2(0f, 0.55f);
        collider.direction = CapsuleDirection2D.Vertical;
        PositionCharacterOnPlatform(enemy, platformSurfaces, EnemySpawnX);

        Animator animator = enemy.AddComponent<Animator>();
        animator.runtimeAnimatorController = enemyController;

        Type healthType = FindType("Castlevania2D.Health.Health");
        Component health = AddOrGetComponent(enemy, healthType);
        if (health != null)
        {
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHealth").intValue = EnemyHealth;
            serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        Type enemyAiType = FindType("Castlevania2D.Enemies.SimpleHostileEnemy2D");
        Component enemyAi = AddOrGetComponent(enemy, enemyAiType);
        if (enemyAi != null)
        {
            SerializedObject serializedEnemyAi = new SerializedObject(enemyAi);
            serializedEnemyAi.FindProperty("target").objectReferenceValue = playerTarget;
            serializedEnemyAi.FindProperty("attackRange").floatValue = 6f;
            serializedEnemyAi.FindProperty("attackCooldown").floatValue = 1f;
            serializedEnemyAi.FindProperty("projectileSprite").objectReferenceValue = LoadFirstSprite("Assets/Art/Sprites/Characters/Enemies/Evil Wizard 3/Sprites/Projectile/Moving.png");
            serializedEnemyAi.FindProperty("projectileDamage").intValue = EnemyProjectileDamage;
            serializedEnemyAi.FindProperty("projectileSpeed").floatValue = 6f;
            AssignSpriteArray(
                serializedEnemyAi,
                "deathSprites",
                LoadSprites("Assets/Art/Sprites/Characters/Enemies/Evil Wizard 3/Sprites/Death.png"));
            serializedEnemyAi.FindProperty("deathFrameRate").floatValue = 12f;
            serializedEnemyAi.FindProperty("destroyAfterDeathDelay").floatValue = 1.5f;
            serializedEnemyAi.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static AnimatorController CreateEnemyAnimatorController()
    {
        EnsureFolder("Assets", "Animations");
        EnsureFolder("Assets/Animations", "Enemies");
        CreateSpriteAnimationClip("Assets/Art/Sprites/Characters/Enemies/Evil Wizard 3/Sprites/Get hit.png", EnemyGetHitClipPath, 12f);
        CreateSpriteAnimationClip("Assets/Art/Sprites/Characters/Enemies/Evil Wizard 3/Sprites/Death.png", EnemyDeathClipPath, 12f);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
        }

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = AddState(stateMachine, "Idle", "Assets/Animations/Enemies/Idle.anim", new Vector3(200f, 0f, 0f));
        AnimatorState walk = AddState(stateMachine, "Walk", "Assets/Animations/Enemies/Walk.anim", new Vector3(420f, 0f, 0f));
        AnimatorState attack = AddState(stateMachine, "Attack", "Assets/Animations/Enemies/Attack.anim", new Vector3(420f, 120f, 0f));
        AnimatorState hurt = AddState(stateMachine, "Hurt", EnemyGetHitClipPath, new Vector3(420f, 240f, 0f));
        AnimatorState death = AddState(stateMachine, "Death", EnemyDeathClipPath, new Vector3(420f, 360f, 0f));

        stateMachine.defaultState = idle;

        AnimatorStateTransition idleToWalk = idle.AddTransition(walk);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.05f;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

        AnimatorStateTransition walkToIdle = walk.AddTransition(idle);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.05f;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

        AddAnyStateTriggerTransition(stateMachine, attack, "Attack");
        AddAnyStateTriggerTransition(stateMachine, hurt, "Hurt");
        AddAnyStateTriggerTransition(stateMachine, death, "Death");
        AddExitTransition(attack, idle);
        AddExitTransition(hurt, idle);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    private static void CreateSpriteAnimationClip(string spriteSheetPath, string clipPath, float sampleRate)
    {
        Sprite[] sprites = LoadSprites(spriteSheetPath);

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"Could not create animation clip because no sprites were found in {spriteSheetPath}.");
            return;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = sampleRate;
        clip.wrapMode = WrapMode.Once;

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / sampleRate,
                value = sprites[i]
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string stateName, string clipPath, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = LoadAnimationClip(clipPath);
        return state;
    }

    private static AnimationClip LoadAnimationClip(string clipPath)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        if (clip != null)
        {
            return clip;
        }

        Debug.LogWarning($"Animation clip was not found at {clipPath}. Falling back to enemy Idle animation.");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Idle.anim");
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string triggerName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddExitTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.05f;
    }

    private static Sprite LoadFirstSprite(string spriteSheetPath)
    {
        Sprite[] sprites = LoadSprites(spriteSheetPath);

        if (sprites.Length > 0)
        {
            return sprites[0];
        }

        Debug.LogWarning($"No sprites were found in {spriteSheetPath}.");
        return null;
    }

    private static Sprite[] LoadSprites(string spriteSheetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((left, right) => GetSpriteFrameIndex(left.name).CompareTo(GetSpriteFrameIndex(right.name)));
        return sprites.ToArray();
    }

    private static int GetSpriteFrameIndex(string spriteName)
    {
        int separatorIndex = spriteName.LastIndexOf('_');

        if (separatorIndex < 0 || separatorIndex == spriteName.Length - 1)
        {
            return 0;
        }

        string indexText = spriteName.Substring(separatorIndex + 1);
        return int.TryParse(indexText, out int index) ? index : 0;
    }

    private static void AssignSpriteArray(SerializedObject serializedObject, string propertyName, Sprite[] sprites)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = sprites.Length;

        for (int i = 0; i < sprites.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static void ConfigurePlayerStats(GameObject player)
    {
        Type healthType = FindType("Castlevania2D.Health.Health");
        Component health = AddOrGetComponent(player, healthType);

        if (health == null)
        {
            Debug.LogWarning("Could not add player Health component.");
            return;
        }

        SerializedObject serializedHealth = new SerializedObject(health);
        serializedHealth.FindProperty("maxHealth").intValue = PlayerHealth;
        serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
        serializedHealth.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePlayerAttackHitbox(GameObject player)
    {
        Type hitboxType = FindType("Castlevania2D.Combat.Hitbox2D");
        Type heroKnightType = FindType("HeroKnight");

        if (hitboxType == null || heroKnightType == null)
        {
            Debug.LogWarning("Could not configure player attack hitbox because required types were not found.");
            return;
        }

        GameObject attackHitboxObject = new GameObject("AttackHitbox");
        attackHitboxObject.transform.SetParent(player.transform, false);
        // Align melee hitbox with CapsuleCollider2D body (offset 0,0.662 / size 0.73x1.2),
        // then extend forward reach by +0.75 total (size.x +0.75, local X +0.375 so Hitbox2D facing flip keeps rear edge).
        attackHitboxObject.transform.localPosition = new Vector3(0.375f, 0.662f, 0f);

        BoxCollider2D collider = attackHitboxObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.offset = Vector2.zero;
        collider.size = new Vector2(1.48f, 1.2f);

        Component hitbox = attackHitboxObject.AddComponent(hitboxType);
        SerializedObject serializedHitbox = new SerializedObject(hitbox);
        serializedHitbox.FindProperty("damage").intValue = PlayerAttackDamage;
        serializedHitbox.ApplyModifiedPropertiesWithoutUndo();

        Component heroKnight = player.GetComponent(heroKnightType);

        if (heroKnight == null)
        {
            Debug.LogWarning("HeroKnight component was not found on the player prefab instance.");
            return;
        }

        SerializedObject serializedHero = new SerializedObject(heroKnight);
        serializedHero.FindProperty("m_attackDamage").intValue = PlayerAttackDamage;
        serializedHero.FindProperty("m_attackHitbox").objectReferenceValue = hitbox;
        serializedHero.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCharacterSlopeCollider(GameObject character)
    {
        BoxCollider2D boxCollider = character.GetComponent<BoxCollider2D>();
        if (boxCollider == null || boxCollider.isTrigger)
        {
            return;
        }

        Vector2 size = boxCollider.size;
        Vector2 offset = boxCollider.offset;
        UnityEngine.Object.DestroyImmediate(boxCollider);

        CapsuleCollider2D capsuleCollider = character.AddComponent<CapsuleCollider2D>();
        capsuleCollider.size = size;
        capsuleCollider.offset = offset;
        capsuleCollider.direction = CapsuleDirection2D.Vertical;
    }

    private static void PositionCharacterOnPlatform(
        GameObject character,
        IReadOnlyList<PlatformSurface> platformSurfaces,
        float desiredX)
    {
        if (platformSurfaces == null || platformSurfaces.Count == 0)
        {
            character.transform.position = new Vector3(desiredX, 2.5f, character.transform.position.z);
            return;
        }

        PlatformSurface surface = FindBestSpawnSurface(platformSurfaces, desiredX);
        character.transform.position = new Vector3(desiredX, surface.TopY + 2f, character.transform.position.z);

        Collider2D bodyCollider = GetMainBodyCollider(character);
        float bottomOffset = bodyCollider != null ? bodyCollider.bounds.min.y - character.transform.position.y : 0f;
        character.transform.position = new Vector3(
            desiredX,
            surface.TopY - bottomOffset + SpawnSurfacePadding,
            character.transform.position.z);

        Rigidbody2D body = character.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private static PlatformSurface FindBestSpawnSurface(IReadOnlyList<PlatformSurface> platformSurfaces, float desiredX)
    {
        bool foundContainingSurface = false;
        PlatformSurface best = platformSurfaces[0];

        for (int i = 0; i < platformSurfaces.Count; i++)
        {
            PlatformSurface surface = platformSurfaces[i];

            if (surface.ContainsX(desiredX))
            {
                if (!foundContainingSurface || surface.TopY < best.TopY)
                {
                    best = surface;
                    foundContainingSurface = true;
                }

                continue;
            }

            if (foundContainingSurface)
            {
                continue;
            }

            float currentDistance = Mathf.Abs(surface.CenterX - desiredX);
            float bestDistance = Mathf.Abs(best.CenterX - desiredX);
            if (currentDistance < bestDistance)
            {
                best = surface;
            }
        }

        return best;
    }

    private static Collider2D GetMainBodyCollider(GameObject character)
    {
        Collider2D[] colliders = character.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        return null;
    }

    private readonly struct PlatformSurface
    {
        public PlatformSurface(float minX, float maxX, float topY)
        {
            MinX = minX;
            MaxX = maxX;
            TopY = topY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float TopY { get; }
        public float CenterX => (MinX + MaxX) * 0.5f;

        public bool ContainsX(float x)
        {
            return x >= MinX && x <= MaxX;
        }
    }

    private static List<PlatformSurface> CreatePrototypePlatforms()
    {
        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>(GroundTilePath);
        Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(WallTilePath) ?? groundTile;
        Tile leftWallTile = AssetDatabase.LoadAssetAtPath<Tile>(LeftWallTilePath) ?? wallTile;
        Tile wallBodyTile = AssetDatabase.LoadAssetAtPath<Tile>(WallBodyTilePath) ?? wallTile;

        if (groundTile == null)
        {
            Debug.LogWarning($"Ground tile was not found at {GroundTilePath}. Creating collider-only platforms.");
            return CreateColliderPlatforms(null, wallTile != null ? wallTile.sprite : null);
        }

        GameObject gridObject = new GameObject("Platform_Grid");
        Grid grid = gridObject.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        Tilemap groundTilemap = CreateTilemapLayer(gridObject.transform, "Ground");
        FillTileRun(groundTilemap, groundTile, SceneMinX, SceneMaxX, GroundLevelY - 1);
        ConfigureTilemapAsSolidPlatform(groundTilemap);

        Tilemap leftWallTilemap = CreateTilemapLayer(gridObject.transform, "LeftWall");
        FillTileColumn(leftWallTilemap, leftWallTile, LeftWallX, WallMinY, WallMaxY);
        ConfigureTilemapAsSolidPlatform(leftWallTilemap);

        Tilemap rightWallTilemap = CreateTilemapLayer(gridObject.transform, "RightWall");
        FillTileColumn(rightWallTilemap, wallBodyTile ?? wallTile, RightWallX, WallMinY, WallMaxY + RightWallExtraHeight);
        ConfigureRightWallColumn(rightWallTilemap);

        Tilemap oneWayTilemap = CreateTilemapLayer(gridObject.transform, "OneWayPlatforms");
        FillTileRun(oneWayTilemap, wallTile, SidePlatformLeftMinX, SidePlatformLeftMaxX, PlatformLevelY - 1);
        ConfigureTilemapAsOneWayPlatform(oneWayTilemap);

        Tilemap rightCenterPlatformTilemap = CreateTilemapLayer(gridObject.transform, "RightCenterPlatform");
        FillTileRun(rightCenterPlatformTilemap, wallTile, RightCenterPlatformMinX, RightCenterPlatformMaxX, PlatformLevelY - 1);
        ConfigureTilemapAsOneWayPlatform(rightCenterPlatformTilemap);

        List<PlatformSurface> surfaces = new List<PlatformSurface>();
        CollectPlatformSurfacesFromTilemap(groundTilemap, surfaces);
        CollectPlatformSurfacesFromTilemap(oneWayTilemap, surfaces);
        CollectPlatformSurfacesFromTilemap(rightCenterPlatformTilemap, surfaces);

        if (surfaces.Count == 0)
        {
            UnityEngine.Object.DestroyImmediate(gridObject);
            return CreateColliderPlatforms(groundTile.sprite, wallTile.sprite);
        }

        return surfaces;
    }

    private static Tilemap CreateTilemapLayer(Transform parent, string name)
    {
        GameObject tilemapObject = new GameObject(name);
        tilemapObject.transform.SetParent(parent, false);

        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 0;

        return tilemap;
    }

    private static List<PlatformSurface> CreateColliderPlatforms(Sprite groundSprite, Sprite elevatedSprite)
    {
        List<PlatformSurface> surfaces = new List<PlatformSurface>();

        float groundWidth = SceneMaxX - SceneMinX + 1f;
        float groundCenterX = (SceneMinX + SceneMaxX) * 0.5f;
        CreateColliderPlatform(
            "Ground",
            new Vector3(groundCenterX, GroundLevelY - 0.5f, 0f),
            new Vector2(groundWidth, 1f),
            groundSprite,
            surfaces);

        float sidePlatformWidth = FormerCenterPlatformTileCount / SidePlatformScale;
        float sidePlatformCenterY = PlatformLevelY - 0.5f;
        Sprite sidePlatformSprite = elevatedSprite != null ? elevatedSprite : groundSprite;

        float leftPlatformCenterX = (SidePlatformLeftMinX + SidePlatformLeftMaxX) * 0.5f;
        CreateOneWayColliderPlatform(
            "Platform_OneWay_Left",
            new Vector3(leftPlatformCenterX, sidePlatformCenterY, 0f),
            new Vector2(sidePlatformWidth, 1f),
            sidePlatformSprite,
            surfaces);

        float rightCenterPlatformWidth = RightCenterPlatformMaxX - RightCenterPlatformMinX + 1f;
        float rightCenterPlatformCenterX = (RightCenterPlatformMinX + RightCenterPlatformMaxX) * 0.5f;
        CreateOneWayColliderPlatform(
            "RightCenterPlatform",
            new Vector3(rightCenterPlatformCenterX, sidePlatformCenterY, 0f),
            new Vector2(rightCenterPlatformWidth, 1f),
            sidePlatformSprite,
            surfaces);

        float leftWallHeight = WallMaxY - WallMinY + 1f;
        float leftWallCenterY = WallMinY + leftWallHeight * 0.5f - 0.5f;
        CreateColliderPlatform(
            "LeftWall",
            new Vector3(LeftWallX, leftWallCenterY, 0f),
            new Vector2(1f, leftWallHeight),
            sidePlatformSprite,
            surfaces,
            registerSurface: false);

        int rightWallMaxY = WallMaxY + RightWallExtraHeight;
        float rightWallHeight = rightWallMaxY - WallMinY + 1f;
        float rightWallCenterY = WallMinY + rightWallHeight * 0.5f - 0.5f;
        CreateColliderPlatform(
            "RightWall",
            new Vector3(RightWallX, rightWallCenterY, 0f),
            new Vector2(1f, rightWallHeight),
            sidePlatformSprite,
            surfaces,
            registerSurface: false);

        return surfaces;
    }

    private static void CreateColliderPlatform(
        string name,
        Vector3 position,
        Vector2 size,
        Sprite platformSprite,
        List<PlatformSurface> surfaces,
        bool registerSurface = true)
    {
        GameObject platform = new GameObject(name);
        platform.transform.position = position;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = size;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = platformSprite;
        renderer.sortingOrder = 0;

        if (!registerSurface)
        {
            return;
        }

        float halfWidth = size.x * 0.5f;
        float topY = position.y + size.y * 0.5f;
        surfaces.Add(new PlatformSurface(position.x - halfWidth, position.x + halfWidth, topY));
    }

    private static void CreateChildColliderPlatform(
        Transform parent,
        string name,
        Vector3 position,
        Vector2 size,
        Sprite platformSprite)
    {
        GameObject platform = new GameObject(name);
        platform.transform.SetParent(parent, false);
        platform.transform.position = position;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = size;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = platformSprite;
        renderer.sortingOrder = 0;
    }

    private static void CreateOneWayColliderPlatform(
        string name,
        Vector3 position,
        Vector2 size,
        Sprite platformSprite,
        List<PlatformSurface> surfaces)
    {
        GameObject platform = new GameObject(name);
        platform.transform.position = position;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.usedByEffector = true;

        PlatformEffector2D effector = platform.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useOneWayGrouping = false;
        effector.surfaceArc = 160f;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = platformSprite;
        renderer.sortingOrder = 0;

        float halfWidth = size.x * 0.5f;
        float topY = position.y + size.y * 0.5f;
        surfaces.Add(new PlatformSurface(position.x - halfWidth, position.x + halfWidth, topY));
    }

    private static void FillTileRun(Tilemap tilemap, Tile tile, int minX, int maxX, int y)
    {
        if (tile == null)
        {
            return;
        }

        for (int x = minX; x <= maxX; x++)
        {
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private static void FillTileColumn(Tilemap tilemap, Tile tile, int x, int minY, int maxY)
    {
        if (tile == null)
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private static void FillTileColumnWithBase(Tilemap tilemap, Tile baseTile, Tile bodyTile, int x, int minY, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            Tile tile = y == minY ? baseTile : bodyTile;
            if (tile == null)
            {
                continue;
            }

            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private static void ConfigureRightWallColumn(Tilemap tilemap)
    {
        Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = tilemap.gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        ConfigureWallColumnGridCollider(tilemap);
    }

    private static void ConfigureWallColumnGridCollider(Tilemap tilemap)
    {
        Type wallColliderType = FindType("Castlevania2D.Level.WallColumnGridCollider2D");
        if (wallColliderType == null)
        {
            return;
        }

        Component wallCollider = tilemap.GetComponent(wallColliderType);
        if (wallCollider == null)
        {
            wallCollider = tilemap.gameObject.AddComponent(wallColliderType);
        }

        wallColliderType.GetMethod("Rebuild")?.Invoke(wallCollider, null);
    }

    private static void ConfigureTilemapAsSolidPlatform(Tilemap tilemap)
    {
        PlatformEffector2D tilemapEffector = tilemap.GetComponent<PlatformEffector2D>();
        if (tilemapEffector != null)
        {
            UnityEngine.Object.DestroyImmediate(tilemapEffector);
        }

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
        {
            tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = tilemap.gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        CompositeCollider2D compositeCollider = tilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
        {
            compositeCollider = tilemap.gameObject.AddComponent<CompositeCollider2D>();
        }

        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

        tilemapCollider.usedByComposite = true;
        tilemapCollider.usedByEffector = false;
    }

    private static void ConfigureTilemapAsOneWayPlatform(Tilemap tilemap)
    {
        CompositeCollider2D compositeCollider = tilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider != null)
        {
            UnityEngine.Object.DestroyImmediate(compositeCollider);
        }

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
        {
            tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        tilemapCollider.usedByComposite = false;
        tilemapCollider.usedByEffector = true;

        Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = tilemap.gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        PlatformEffector2D effector = tilemap.GetComponent<PlatformEffector2D>();
        if (effector == null)
        {
            effector = tilemap.gameObject.AddComponent<PlatformEffector2D>();
        }

        effector.useOneWay = true;
        effector.useOneWayGrouping = false;
        effector.surfaceArc = 160f;
    }

    private static void CollectPlatformSurfacesFromTilemap(
        Tilemap tilemap,
        List<PlatformSurface> surfaces)
    {
        BoundsInt bounds = tilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            int runStartX = 0;
            int runLength = 0;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                bool isPlatformTop = tilemap.HasTile(cell) && !tilemap.HasTile(new Vector3Int(x, y + 1, 0));

                if (isPlatformTop)
                {
                    if (runLength == 0)
                    {
                        runStartX = x;
                    }

                    runLength++;
                    continue;
                }

                AddPlatformSurfaceRun(tilemap, surfaces, y, runStartX, runLength);
                runLength = 0;
            }

            AddPlatformSurfaceRun(tilemap, surfaces, y, runStartX, runLength);
        }
    }

    private static void AddPlatformSurfaceRun(
        Tilemap tilemap,
        List<PlatformSurface> surfaces,
        int y,
        int runStartX,
        int runLength)
    {
        if (runLength <= 0)
        {
            return;
        }

        Grid grid = tilemap.layoutGrid;
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
        Vector3Int firstCell = new Vector3Int(runStartX, y, 0);
        Vector3Int lastCell = new Vector3Int(runStartX + runLength - 1, y, 0);
        Vector3 firstCenter = tilemap.GetCellCenterWorld(firstCell);
        Vector3 lastCenter = tilemap.GetCellCenterWorld(lastCell);
        float topY = firstCenter.y + cellSize.y * 0.5f;
        float width = runLength * cellSize.x;
        float centerX = (firstCenter.x + lastCenter.x) * 0.5f;

        surfaces.Add(new PlatformSurface(centerX - width * 0.5f, centerX + width * 0.5f, topY));
    }

    private static void CreateCamera(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindSceneCamera(SceneManager.GetActiveScene());
        }

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.5f, -10f);

            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        }
        else
        {
            TryAssignTag(camera.gameObject, "MainCamera");
            camera.orthographic = true;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        }

        Type arenaCameraType = FindType("Castlevania2D.Player.ArenaFixedCamera2D");
        Type cameraFollowType = FindType("Castlevania2D.Player.CameraFollow2D");

        // Prefer follow locked to the player; keep ArenaFixed disabled unless explicitly set up elsewhere.
        if (arenaCameraType != null && AddOrGetComponent(camera.gameObject, arenaCameraType) is Behaviour arenaBehaviour)
        {
            arenaBehaviour.enabled = false;
        }

        if (cameraFollowType != null && AddOrGetComponent(camera.gameObject, cameraFollowType) is Behaviour followBehaviour)
        {
            SerializedObject so = new SerializedObject(followBehaviour);
            SerializedProperty targetProp = so.FindProperty("target");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = target;
            }

            // Positive Y ≈ 0.75 * orthoSize keeps player in mid lower quarter (~12.5% from bottom).
            const float followOffsetY = 3.54f;

            SerializedProperty offsetProp = so.FindProperty("offset");
            if (offsetProp != null)
            {
                offsetProp.vector2Value = new Vector2(0f, followOffsetY);
            }

            SerializedProperty smoothTimeProp = so.FindProperty("smoothTime");
            if (smoothTimeProp != null)
            {
                smoothTimeProp.floatValue = 0.1f;
            }

            SerializedProperty lockY = so.FindProperty("lockWorldY");
            if (lockY != null)
            {
                lockY.boolValue = false;
            }

            SerializedProperty clampWalls = so.FindProperty("clampToSceneWalls");
            if (clampWalls != null)
            {
                clampWalls.boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            followBehaviour.enabled = true;
        }

        camera.orthographic = true;
        camera.orthographicSize = 4.725f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        if (target != null)
        {
            camera.transform.position = new Vector3(target.position.x, target.position.y + 3.54f, -10f);
        }
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Camera camera = rootObject.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                return camera;
            }
        }

        return null;
    }

    private static void CreateHud(GameObject player)
    {
        GameObject canvasObject = new GameObject("HUD_Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasObject.AddComponent<HudGameViewportFit>();

        Image healthFill = CreateBar(canvasObject.transform, "HealthBar", new Vector2(24f, -24f), new Color(0.75f, 0.05f, 0.05f));
        Image blockFill = CreateBar(canvasObject.transform, "BlockDurabilityBar", new Vector2(24f, -58f), new Color(0.15f, 0.55f, 1f));

        Type hudType = FindType("Castlevania2D.UI.PlayerHudView");
        Type healthType = FindType("Castlevania2D.Health.Health");
        Type heroKnightType = FindType("HeroKnight");

        if (hudType == null || healthType == null || heroKnightType == null)
        {
            Debug.LogWarning("Could not configure HUD because required types were not found.");
            return;
        }

        Component hud = canvasObject.AddComponent(hudType);
        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("health").objectReferenceValue = player.GetComponent(healthType);
        serializedHud.FindProperty("blockDurabilitySource").objectReferenceValue = player.GetComponent(heroKnightType);
        serializedHud.FindProperty("healthFill").objectReferenceValue = healthFill;
        serializedHud.FindProperty("blockFill").objectReferenceValue = blockFill;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image CreateBar(Transform parent, string name, Vector2 anchoredPosition, Color fillColor)
    {
        GameObject backgroundObject = new GameObject(name);
        backgroundObject.transform.SetParent(parent, false);

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 1f);
        backgroundRect.anchorMax = new Vector2(0f, 1f);
        backgroundRect.pivot = new Vector2(0f, 1f);
        backgroundRect.anchoredPosition = anchoredPosition;
        backgroundRect.sizeDelta = new Vector2(260f, 22f);

        Image background = backgroundObject.AddComponent<Image>();
        background.sprite = GetUiWhiteSprite();
        background.type = Image.Type.Simple;
        background.color = new Color(0.05f, 0.05f, 0.06f, 0.85f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Image fill = fillObject.AddComponent<Image>();
        fill.sprite = GetUiWhiteSprite();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        return fill;
    }

    private static Sprite uiWhiteSprite;

    private static Sprite GetUiWhiteSprite()
    {
        if (uiWhiteSprite != null)
        {
            return uiWhiteSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        uiWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        uiWhiteSprite.name = "UiWhiteSprite";

        return uiWhiteSprite;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void TryAssignTag(GameObject gameObject, string tag)
    {
        try
        {
            gameObject.tag = tag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{tag}' does not exist. The object was left untagged.");
        }
    }

    private static Component AddOrGetComponent(GameObject gameObject, Type componentType)
    {
        if (componentType == null)
        {
            return null;
        }

        Component component = gameObject.GetComponent(componentType);
        return component != null ? component : gameObject.AddComponent(componentType);
    }

    private static Type FindType(string fullName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);

            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
