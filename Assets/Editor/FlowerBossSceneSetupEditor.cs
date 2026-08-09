using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Castlevania2D.Enemies;
using Castlevania2D.Player;

public static class FlowerBossSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string SpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Sprites";
    private const string IdleSpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Idle";
    private const string AnimationsFolder = "Assets/Animations/Enemies/FlowerBoss";
    private const string ControllerPath = "Assets/Animations/Enemies/FlowerBoss/FlowerBossRuntime.controller";
    private const string IdleClipPath = "Assets/Animations/Enemies/FlowerBoss/FlowerBoss_Idle.anim";
    private const string HurtClipPath = "Assets/Animations/Enemies/FlowerBoss/FlowerBoss_Hurt.anim";
    private const string AttackClipPath = "Assets/Animations/Enemies/FlowerBoss/FlowerBoss_Attack.anim";
    private const string DeathClipPath = "Assets/Animations/Enemies/FlowerBoss/FlowerBoss_Death.anim";
    private const string BossObjectName = "Boss_Flower";
    private const string GrassCoverObjectName = "GrassCover";
    private const string GrassSpriteTexturePath = "Assets/Art/Backgrounds/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";
    private const string GrassSpriteName = "TX Village Props - Grass 02";
    private const float BossPreviewScale = 0.375f;
    private const float BossSpawnX = 9f;
    private const int BossHealth = 300;
    private const int BossProjectileDamage = 15;
    private const float SpawnSurfacePadding = 0.05f;
    private const float IdlePreviewFrameRate = 12f;
    private const float IdlePreviewBossY = 1.32f;

    [MenuItem("Tools/Castlevania 2D/Setup Flower Boss Idle Preview")]
    public static void SetupFlowerBossIdlePreviewMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += SetupFlowerBossIdlePreview;
            return;
        }

        SetupFlowerBossIdlePreview();
    }

    public static void SetupFlowerBossIdlePreviewBatch()
    {
        SetupFlowerBossIdlePreview();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void EnsureFlowerBossIdlePreviewInScene()
    {
        SetupFlowerBossIdlePreview();
    }

    [MenuItem("Tools/Castlevania 2D/Add Flower Boss To Scene")]
    public static void AddFlowerBossToSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += AddFlowerBossToScene;
            return;
        }

        AddFlowerBossToScene();
    }

    public static void AddFlowerBossToSceneBatch()
    {
        AddFlowerBossToScene();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static void AddFlowerBossToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        ConfigureFlowerBossSpriteImports();
        AnimatorController controller = CreateFlowerBossAnimatorController();
        Sprite[] allSprites = LoadFlowerBossSprites();

        if (allSprites.Length == 0)
        {
            throw new InvalidOperationException("No Flower Boss sprites were found. Check the Sprites folder import settings.");
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Transform playerTarget = FindPlayerTransform(scene);
        if (playerTarget == null)
        {
            throw new InvalidOperationException("Player transform was not found in the prototype scene.");
        }

        GameObject boss = GameObject.Find(BossObjectName);
        if (boss == null)
        {
            boss = new GameObject(BossObjectName);
        }

        ConfigureBossComponents(boss, playerTarget, controller, allSprites);
        PositionBossOnPlatform(boss, BossSpawnX);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Flower Boss added to scene at x={BossSpawnX}.");
    }

    private static void SetupFlowerBossIdlePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolder("Assets/Art/Sprites/Characters/Enemies", "Flower Boss");
        EnsureFolder("Assets/Art/Sprites/Characters/Enemies/Flower Boss", "Idle");
        EnsureFolder("Assets", "Animations");
        EnsureFolder("Assets/Animations", "Enemies");
        EnsureFolder("Assets/Animations/Enemies", "FlowerBoss");

        ConfigureSpriteImports(IdleSpritesFolder);
        Sprite[] idleSprites = LoadSpritesFromFolder(IdleSpritesFolder, "FlowerBoss_Idle_");

        if (idleSprites.Length == 0)
        {
            throw new InvalidOperationException($"No idle sprites found in {IdleSpritesFolder}.");
        }

        CreateSpriteAnimationClip(idleSprites, IdleClipPath, IdlePreviewFrameRate, true);
        AnimatorController controller = CreateIdleOnlyAnimatorController();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject boss = GameObject.Find(BossObjectName) ?? new GameObject(BossObjectName);
        ConfigureBossPreviewComponents(boss, controller, idleSprites[0]);
        boss.transform.position = new Vector3(0f, IdlePreviewBossY, 0f);

        ConfigureCameraForBossPreview(boss.transform);
        DisableHostileEnemiesInScene();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Flower Boss idle preview ready: {idleSprites.Length} frames, centered at x=0.");
    }

    private static void ConfigureFlowerBossSpriteImports()
    {
        ConfigureSpriteImports(SpritesFolder);
        ConfigureSpriteImports(IdleSpritesFolder);
    }

    private static void ConfigureSpriteImports(string folder)
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.SaveAndReimport();
        }
    }

    private static AnimatorController CreateFlowerBossAnimatorController()
    {
        EnsureFolder("Assets", "Animations");
        EnsureFolder("Assets/Animations", "Enemies");
        EnsureFolder("Assets/Animations/Enemies", "FlowerBoss");

        Sprite[] sprites = LoadFlowerBossSprites();
        Sprite[] idleSprites = SliceSprites(sprites, 0, 3);
        Sprite[] hurtSprites = SliceSprites(sprites, 3, 3);
        Sprite[] attackSprites = SliceSprites(sprites, 6, 3);
        Sprite[] deathSprites = SliceSprites(sprites, 7, 2);

        CreateSpriteAnimationClip(idleSprites, IdleClipPath, 6f, true);
        CreateSpriteAnimationClip(hurtSprites, HurtClipPath, 10f, false);
        CreateSpriteAnimationClip(attackSprites, AttackClipPath, 8f, false);
        CreateSpriteAnimationClip(deathSprites, DeathClipPath, 6f, false);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = AddState(stateMachine, "Idle", IdleClipPath, new Vector3(220f, 0f, 0f));
        AnimatorState hurt = AddState(stateMachine, "Hurt", HurtClipPath, new Vector3(420f, 120f, 0f));
        AnimatorState attack = AddState(stateMachine, "Attack", AttackClipPath, new Vector3(420f, 240f, 0f));
        AnimatorState death = AddState(stateMachine, "Death", DeathClipPath, new Vector3(420f, 360f, 0f));

        stateMachine.defaultState = idle;
        AddAnyStateTriggerTransition(stateMachine, hurt, "Hurt");
        AddAnyStateTriggerTransition(stateMachine, attack, "Attack");
        AddAnyStateTriggerTransition(stateMachine, death, "Death");
        AddExitTransition(hurt, idle);
        AddExitTransition(attack, idle);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    private static AnimatorController CreateIdleOnlyAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = new AnimatorControllerParameter[0];

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = AddState(stateMachine, "Idle", IdleClipPath, new Vector3(220f, 0f, 0f));
        stateMachine.defaultState = idle;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    private static void ConfigureBossPreviewComponents(GameObject boss, AnimatorController controller, Sprite firstSprite)
    {
        boss.transform.localScale = new Vector3(BossPreviewScale, BossPreviewScale, 1f);

        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(boss);
        renderer.sprite = firstSprite;
        renderer.sortingOrder = 2;

        Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(boss);
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;

        Collider2D collider = boss.GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Animator animator = GetOrAddComponent<Animator>(boss);
        animator.runtimeAnimatorController = controller;

        if (boss.GetComponent(FindType("Castlevania2D.Enemies.SimpleHostileEnemy2D")) is Behaviour enemyAi)
        {
            enemyAi.enabled = false;
        }

        if (boss.GetComponent(FindType("Castlevania2D.Health.Health")) is Behaviour health)
        {
            health.enabled = false;
        }

        ConfigureBossGrassCover(boss.transform);
    }

    private static void ConfigureBossGrassCover(Transform bossTransform)
    {
        Sprite grassSprite = LoadGrass02Sprite();
        if (grassSprite == null)
        {
            Debug.LogWarning("Grass 02 sprite was not found. Boss grass cover was skipped.");
            return;
        }

        Transform coverRoot = bossTransform.Find(GrassCoverObjectName);
        if (coverRoot == null)
        {
            GameObject coverObject = new GameObject(GrassCoverObjectName);
            coverRoot = coverObject.transform;
            coverRoot.SetParent(bossTransform, false);
        }

        ClearChildren(coverRoot);

        const int grassTileCount = 9;
        float inverseBossScale = 1f / BossPreviewScale;
        float tileSpacing = grassSprite.bounds.size.x / BossPreviewScale;
        float rowYOffset = grassSprite.bounds.size.y * 0.35f / BossPreviewScale;
        float startX = -((grassTileCount - 1) * tileSpacing) * 0.5f;

        for (int row = 0; row < 2; row++)
        {
            for (int i = 0; i < grassTileCount; i++)
            {
                GameObject grassObject = new GameObject($"Grass02_{row}_{i:00}");
                Transform grassTransform = grassObject.transform;
                grassTransform.SetParent(coverRoot, false);
                grassTransform.localPosition = new Vector3(startX + i * tileSpacing, rowYOffset * row, 0f);
                grassTransform.localScale = new Vector3(inverseBossScale, inverseBossScale, 1f);

                SpriteRenderer grassRenderer = grassObject.AddComponent<SpriteRenderer>();
                grassRenderer.sprite = grassSprite;
                grassRenderer.sortingOrder = 3;
                grassRenderer.flipX = (i + row) % 2 == 1;
            }
        }
    }

    private static Sprite LoadGrass02Sprite()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(GrassSpriteTexturePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == GrassSpriteName)
            {
                return sprite;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void ConfigureCameraForBossPreview(Transform bossTransform)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            return;
        }

        if (camera.GetComponent(FindType("Castlevania2D.Player.CameraFollow2D")) is Behaviour follow)
        {
            follow.enabled = false;
        }

        if (camera.GetComponent<ArenaFixedCamera2D>() is not ArenaFixedCamera2D arenaCamera)
        {
            arenaCamera = camera.gameObject.AddComponent<ArenaFixedCamera2D>();
        }

        arenaCamera.enabled = true;
        arenaCamera.ApplyFixedFraming();
    }

    private static void DisableHostileEnemiesInScene()
    {
        Type enemyAiType = FindType("Castlevania2D.Enemies.SimpleHostileEnemy2D");
        if (enemyAiType == null)
        {
            return;
        }

        UnityEngine.Object[] enemies = UnityEngine.Object.FindObjectsByType(enemyAiType, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] is not Behaviour behaviour)
            {
                continue;
            }

            if (behaviour.gameObject.name == BossObjectName)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private static void ConfigureBossComponents(
        GameObject boss,
        Transform playerTarget,
        AnimatorController controller,
        Sprite[] allSprites)
    {
        boss.transform.localScale = Vector3.one;

        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(boss);
        renderer.sprite = allSprites[0];
        renderer.sortingOrder = 1;

        Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(boss);
        body.freezeRotation = true;
        body.gravityScale = 1f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        CapsuleCollider2D existingCapsule = boss.GetComponent<CapsuleCollider2D>();
        if (existingCapsule != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCapsule);
        }

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(boss);
        FlowerBossHitboxFit2D hitboxFit = GetOrAddComponent<FlowerBossHitboxFit2D>(boss);
        SerializedObject serializedHitbox = new SerializedObject(hitboxFit);
        SerializedProperty sizeProp = serializedHitbox.FindProperty("hitboxSize");
        if (sizeProp != null)
        {
            sizeProp.vector2Value = new Vector2(5f, 7f);
            serializedHitbox.FindProperty("hitboxOffset").vector2Value = Vector2.zero;
            serializedHitbox.FindProperty("isTrigger").boolValue = true;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();
        }

        hitboxFit.ApplyFit();

        Animator animator = GetOrAddComponent<Animator>(boss);
        animator.runtimeAnimatorController = controller;

        Type healthType = FindType("Castlevania2D.Health.Health");
        Component health = GetOrAddComponent(boss, healthType);
        if (health != null)
        {
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHealth").intValue = BossHealth;
            serializedHealth.FindProperty("destroyOnDeath").boolValue = false;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        Type enemyAiType = FindType("Castlevania2D.Enemies.SimpleHostileEnemy2D");
        Component enemyAi = GetOrAddComponent(boss, enemyAiType);
        if (enemyAi != null)
        {
            SerializedObject serializedEnemyAi = new SerializedObject(enemyAi);
            serializedEnemyAi.FindProperty("target").objectReferenceValue = playerTarget;
            serializedEnemyAi.FindProperty("moveSpeed").floatValue = 0f;
            serializedEnemyAi.FindProperty("chaseRange").floatValue = 12f;
            serializedEnemyAi.FindProperty("attackRange").floatValue = 8f;
            serializedEnemyAi.FindProperty("attackCooldown").floatValue = 2f;
            serializedEnemyAi.FindProperty("projectileSprite").objectReferenceValue = allSprites[Mathf.Min(8, allSprites.Length - 1)];
            serializedEnemyAi.FindProperty("projectileDamage").intValue = BossProjectileDamage;
            serializedEnemyAi.FindProperty("projectileSpeed").floatValue = 5f;
            serializedEnemyAi.FindProperty("projectileSpawnOffset").vector2Value = new Vector2(0f, 1.8f);
            AssignSpriteArray(serializedEnemyAi, "deathSprites", SliceSprites(allSprites, 7, 2));
            serializedEnemyAi.FindProperty("deathFrameRate").floatValue = 8f;
            serializedEnemyAi.FindProperty("destroyAfterDeathDelay").floatValue = 2f;
            serializedEnemyAi.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void PositionBossOnPlatform(GameObject boss, float desiredX)
    {
        float platformTopY = 3f;
        Collider2D bodyCollider = boss.GetComponent<Collider2D>();
        float bottomOffset = bodyCollider != null
            ? bodyCollider.bounds.min.y - boss.transform.position.y
            : 0f;

        boss.transform.position = new Vector3(
            desiredX,
            platformTopY - bottomOffset + SpawnSurfacePadding,
            0f);

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private static bool SceneContainsFlowerBoss()
    {
        if (!File.Exists(ScenePath))
        {
            return false;
        }

        string sceneText = File.ReadAllText(ScenePath);
        return sceneText.Contains($"m_Name: {BossObjectName}");
    }

    private static Transform FindPlayerTransform(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name.Contains("Player"))
            {
                return rootObject.transform;
            }
        }

        GameObject player = GameObject.Find("Player_HeroKnight");
        return player != null ? player.transform : null;
    }

    private static Sprite[] LoadFlowerBossSprites()
    {
        return LoadSpritesFromFolder(SpritesFolder, "FlowerBoss_");
    }

    private static Sprite[] LoadSpritesFromFolder(string folder, string namePrefix)
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        List<(int index, Sprite sprite)> sprites = new List<(int, Sprite)>();

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                continue;
            }

            sprites.Add((GetSpriteFrameIndex(sprite.name), sprite));
        }

        sprites.Sort((left, right) => left.index.CompareTo(right.index));
        Sprite[] result = new Sprite[sprites.Count];

        for (int i = 0; i < sprites.Count; i++)
        {
            result[i] = sprites[i].sprite;
        }

        return result;
    }

    private static Sprite[] SliceSprites(Sprite[] sprites, int startIndex, int count)
    {
        if (sprites.Length == 0)
        {
            return Array.Empty<Sprite>();
        }

        int safeCount = Mathf.Min(count, sprites.Length - startIndex);
        if (safeCount <= 0)
        {
            return new[] { sprites[Mathf.Clamp(startIndex, 0, sprites.Length - 1)] };
        }

        Sprite[] slice = new Sprite[safeCount];
        Array.Copy(sprites, startIndex, slice, 0, safeCount);
        return slice;
    }

    private static void CreateSpriteAnimationClip(Sprite[] sprites, string clipPath, float sampleRate, bool loop)
    {
        if (sprites.Length == 0)
        {
            Debug.LogWarning($"Could not create animation clip at {clipPath} because no sprites were provided.");
            return;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = sampleRate;
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

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

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
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
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        return state;
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Component GetOrAddComponent(GameObject gameObject, Type componentType)
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
