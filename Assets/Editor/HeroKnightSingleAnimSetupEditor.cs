using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// HeroKnight import from game_ready/frames into Unity Assets.
/// Does NOT import from the OneDrive hash folder 1785214083_6a6834832efd6.
/// </summary>
[InitializeOnLoad]
public static class HeroKnightSingleAnimSetupEditor
{
    private const string Attack1FlagPath = "Temp/setup_new_hero_attack1.flag";
    private const string SourceRoot =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\warrior_animations_game_ready-v5 (1)\game_ready\frames";

    private const string OutputFolder = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim";
    private const string AnimsFolder = "Assets/Animations/Player/Animations";
    private const string ControllerPath = "Assets/Animations/Player/Animations/HeroKnight_AnimController.controller";
    private const string PrefabPath = "Assets/Animations/Player/Demo/HeroKnight.prefab";

    private const int CellWidth = 784;
    private const int CellHeight = 736;
    private const int PixelsPerUnit = 240;

    private static readonly Vector2 CanvasPivot = new Vector2(342f / CellWidth, (CellHeight - 654f) / CellHeight);

    static HeroKnightSingleAnimSetupEditor()
    {
        EditorApplication.delayCall += TryApplyAttack1FromFlag;
        EditorApplication.update += PollAttack1Flag;
    }

    private static void PollAttack1Flag()
    {
        if (!File.Exists(Attack1FlagPath)
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TryApplyAttack1FromFlag();
    }

    private static void TryApplyAttack1FromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Attack1FlagPath))
        {
            return;
        }

        File.Delete(Attack1FlagPath);
        try
        {
            ApplyNewCharacterAttack1(openSpriteEditor: false);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_new_hero_attack1_result.txt", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_new_hero_attack1_result.txt", "FAIL\n" + exception);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Idle")]
    public static void ApplyIdle()
    {
        ApplyNewCharacterIdle(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterIdle(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_idle.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Idle assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_idle.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Idle", sprites, 6, true);
        RepairAnimatorWiring();
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Run")]
    public static void ApplyRun()
    {
        ApplyNewCharacterWalk(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterWalk(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_walk.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Walk assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_run.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        int[] startIndices = { 0, 1, 2, 3 };
        int[] loopIndices =
        {
            4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22
        };
        WriteClip("HeroKnight_RunStart", PickSprites(sprites, startIndices), 36, false);
        WriteClip("HeroKnight_Run", PickSprites(sprites, loopIndices), 12, true);
        RepairAnimatorWiring();
    }

    private static Sprite[] PickSprites(Sprite[] sprites, int[] indices)
    {
        var picked = new Sprite[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            picked[i] = sprites[indices[i]];
        }

        return picked;
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Jump")]
    public static void ApplyJump()
    {
        ApplyNewCharacterJump(openSpriteEditor: true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Fall")]
    public static void ApplyFall()
    {
        ApplyNewCharacterJump(openSpriteEditor: false);
    }

    public static void ApplyNewCharacterJump(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_jump.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Jump assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_jump.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        int[] startIndices = { 0, 1, 2, 3, 4 };
        int[] jumpIndices = { 5, 6 };
        int[] fallIndices = { 7, 8, 9 };
        int[] landIndices = { 10, 11, 12, 13, 14, 15 };
        WriteClip("HeroKnight_JumpStart", PickSprites(sprites, startIndices), 36, false);
        WriteClip("HeroKnight_Jump", PickSprites(sprites, jumpIndices), 8, false);
        WriteClip("HeroKnight_Fall", PickSprites(sprites, fallIndices), 8, false);
        WriteClip("HeroKnight_JumpLand", PickSprites(sprites, landIndices), 36, false);
        RepairAnimatorWiring();
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Hurt")]
    public static void ApplyHurt()
    {
        ApplyNewCharacterHurt(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterHurt(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_hurt.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Hurt assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_hurt.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Hurt", sprites, 12, false);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Death")]
    public static void ApplyDeath()
    {
        ApplyNewCharacterDeath(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterDeath(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_death.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Death assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_death_blood.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Death", sprites, 10, false);
        WriteClip("HeroKnight_DeathNoBlood", sprites, 10, false);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Roll")]
    public static void ApplyRoll()
    {
        ApplyNewCharacterRoll(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterRoll(bool openSpriteEditor)
    {
        const string rollAssembler = @"Temp\assemble_new_hero_roll.py";
        const string slideAssembler = @"Temp\assemble_new_hero_roll_slide.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string rollAssemblerPath = Path.Combine(projectRoot, rollAssembler);
        string slideAssemblerPath = Path.Combine(projectRoot, slideAssembler);
        if (!File.Exists(rollAssemblerPath))
        {
            throw new FileNotFoundException("Roll assembler missing.", rollAssemblerPath);
        }

        if (!File.Exists(slideAssemblerPath))
        {
            throw new FileNotFoundException("Roll slide assembler missing.", slideAssemblerPath);
        }

        RunPython(rollAssemblerPath);
        const string rollStripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_roll.png";
        AssetDatabase.ImportAsset(rollStripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] rollSprites = LoadStripSprites(rollStripPath);
        WriteClip("HeroKnight_Roll", rollSprites, 12, false);

        RunPython(slideAssemblerPath);
        const string slideStripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_roll_slide.png";
        AssetDatabase.ImportAsset(slideStripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] slideSprites = LoadStripSprites(slideStripPath);
        int[] startIndices = { 0, 1, 2, 3, 4, 5, 6, 7 };
        int[] loopIndices = { 8, 9 };
        WriteClip("HeroKnight_RollSlide", PickSprites(slideSprites, startIndices), 12, false);
        WriteClip("HeroKnight_RollSlideLoop", PickSprites(slideSprites, loopIndices), 12, true);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(slideStripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 1")]
    public static void ApplyAttack1()
    {
        ApplyNewCharacterAttack1(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterAttack1FromBatch()
    {
        ApplyNewCharacterAttack1(openSpriteEditor: false);
    }

    public static void ApplyNewCharacterAttack1(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_attack1.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Attack 1 assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_attack_1.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Attack1", sprites, 20, false);
        RepairAnimatorWiring();
    }

    private static void RunPython(string assemblerPath)
    {
        string python = File.Exists(@"C:\Users\Bensh\AppData\Local\Programs\Python\Python311\python.exe")
            ? @"C:\Users\Bensh\AppData\Local\Programs\Python\Python311\python.exe"
            : "python";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = python,
            Arguments = "\"" + assemblerPath + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projectRoot
        };
        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Python for Attack 1.");
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Debug.Log(stdout.Trim());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Attack 1 assemble failed: " + stderr + "\n" + stdout);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 2")]
    public static void ApplyAttack2()
    {
        ApplyNewCharacterAttack2(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterAttack2(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_attack2.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Attack 2 assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_attack_2.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Attack2", sprites, 20, false);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 3")]
    public static void ApplyAttack3()
    {
        ApplyNewCharacterAttack3(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterAttack3(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_attack3.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Attack 3 assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_attack_3.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Attack3", sprites, 20, false);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Climb")]
    public static void ApplyClimb()
    {
        ApplyNewCharacterClimb(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterClimb(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_climb.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Climb assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_climb.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Climb", sprites, 12, true);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block")]
    public static void ApplyBlock()
    {
        ApplyNewCharacterBlock(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterBlock(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_block.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Block assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_block_effect.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_Block", sprites, 12, false);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block Walk")]
    public static void ApplyBlockWalk()
    {
        ApplyNewCharacterBlockWalk(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterBlockWalk(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_block_walk.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Block walk assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_block_walk.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        int[] startIndices = { 0, 1, 2 };
        int[] loopIndices = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        WriteClip("HeroKnight_BlockWalkStart", PickSprites(sprites, startIndices), 12, false);
        WriteClip("HeroKnight_BlockWalk", PickSprites(sprites, loopIndices), 12, true);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block Overhead")]
    public static void ApplyBlockOverhead()
    {
        ApplyNewCharacterBlockOverhead(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterBlockOverhead(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_block_overhead.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Overhead block assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_block_overhead.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        WriteClip("HeroKnight_IdleBlock", sprites, 12, false);

        Sprite[] reversed = new Sprite[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            reversed[i] = sprites[sprites.Length - 1 - i];
        }

        WriteClip("HeroKnight_IdleBlockRelease", reversed, 12, false);
        RepairAnimatorWiring();
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block Overhead Walk")]
    public static void ApplyBlockOverheadWalk()
    {
        ApplyNewCharacterBlockOverheadWalk(openSpriteEditor: true);
    }

    public static void ApplyNewCharacterBlockOverheadWalk(bool openSpriteEditor)
    {
        const string assembler = @"Temp\assemble_new_hero_block_overhead_walk.py";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assemblerPath = Path.Combine(projectRoot, assembler);
        if (!File.Exists(assemblerPath))
        {
            throw new FileNotFoundException("Overhead block walk assembler missing.", assemblerPath);
        }

        RunPython(assemblerPath);

        string stripPath = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim/HeroKnight_block_overhead_walk.png";
        AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate);
        Sprite[] sprites = LoadStripSprites(stripPath);
        int[] startIndices = { 0, 1, 2, 3, 4, 5 };
        int[] loopIndices = { 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        WriteClip("HeroKnight_IdleBlockWalkStart", PickSprites(sprites, startIndices), 12, false);
        WriteClip("HeroKnight_IdleBlockWalk", PickSprites(sprites, loopIndices), 12, true);
        RepairAnimatorWiring();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(stripPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Apply All (Global Cell)")]
    public static void ApplyAll()
    {
        Debug.Log(
            $"HeroKnight source: {SourceRoot}. " +
            $"Cell {CellWidth}x{CellHeight}, pivot ({CanvasPivot.x:F6},{CanvasPivot.y:F6}), PPU {PixelsPerUnit}.");
        ApplyIdle();
        ApplyRun();
        ApplyJump();
        ApplyFall();
        ApplyHurt();
        ApplyDeath();
        ApplyRoll();
        ApplyAttack1();
        ApplyAttack2();
        ApplyAttack3();
        ApplyBlock();
        ApplyBlockWalk();
        ApplyBlockOverhead();
        ApplyBlockOverheadWalk();
        RepairAnimatorWiring();
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Force Reimport Strips")]
    public static void ForceReimportStrips()
    {
        if (!Directory.Exists(OutputFolder))
        {
            Debug.LogWarning($"HeroKnight strip folder not found: {OutputFolder}");
            return;
        }

        string[] stripPaths = Directory.GetFiles(OutputFolder, "HeroKnight_*.png", SearchOption.TopDirectoryOnly);
        foreach (string stripPath in stripPaths)
        {
            AssetDatabase.ImportAsset(stripPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        string[] clipPaths = Directory.GetFiles(AnimsFolder, "HeroKnight_*.anim", SearchOption.TopDirectoryOnly);
        foreach (string clipPath in clipPaths)
        {
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Force-reimported {stripPaths.Length} HeroKnight strips, {clipPaths.Length} clips, and prefab.");
    }

    private static readonly Dictionary<string, string> StateToClipName = new Dictionary<string, string>
    {
        { "Idle", "HeroKnight_Idle" },
        { "RunStart", "HeroKnight_RunStart" },
        { "Run", "HeroKnight_Run" },
        { "Hurt", "HeroKnight_Hurt" },
        { "Death", "HeroKnight_Death" },
        { "DeathNoBlood", "HeroKnight_DeathNoBlood" },
        { "JumpStart", "HeroKnight_JumpStart" },
        { "Jump", "HeroKnight_Jump" },
        { "Fall", "HeroKnight_Fall" },
        { "JumpLand", "HeroKnight_JumpLand" },
        { "Block", "HeroKnight_Block" },
        { "Front Block Walk Start", "HeroKnight_BlockWalkStart" },
        { "Front Block Walk", "HeroKnight_BlockWalk" },
        { "Roll", "HeroKnight_Roll" },
        { "Roll Slide", "HeroKnight_RollSlide" },
        { "Roll Slide Loop", "HeroKnight_RollSlideLoop" },
        { "Attack1", "HeroKnight_Attack1" },
        { "Attack2", "HeroKnight_Attack2" },
        { "Attack3", "HeroKnight_Attack3" },
        { "Idle Block", "HeroKnight_IdleBlock" },
        { "Idle Block Release", "HeroKnight_IdleBlockRelease" },
        { "Idle Block Walk Start", "HeroKnight_IdleBlockWalkStart" },
        { "Idle Block Walk", "HeroKnight_IdleBlockWalk" },
        { "Wall Slide", "HeroKnight_WallSlide" },
        { "Wall Slide Land", "HeroKnight_WallSlideLand" },
        { "Climb", "HeroKnight_Climb" },
    };

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Repair Animator Wiring")]
    public static void RepairAnimatorWiring()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"HeroKnight AnimatorController not found: {ControllerPath}");
            return;
        }

        var log = new List<string>();
        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length > 0)
        {
            layers[0].defaultWeight = 1f;
            controller.layers = layers;
            log.Add("Base Layer defaultWeight set to 1.");
        }

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            RewireStateMachine(layer.stateMachine, log);
        }

        ReserializeAnimationClips(log);
        EnsurePrefabAnimatorWired(controller, log);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("HeroKnight animator repair complete:\n- " + string.Join("\n- ", log));
    }

    public static void ApplyFolder(
        string folderName,
        string clipName,
        string framePattern,
        int fps,
        bool loop,
        bool updatePrefabSprite,
        bool openSpriteEditor)
    {
        string sourceFolder = Path.Combine(SourceRoot, folderName);
        ApplyExternalFolder(
            sourceFolder,
            framePattern,
            $"HeroKnight_{folderName}.png",
            clipName,
            fps,
            loop,
            PixelsPerUnit,
            chromaKeyGray: false,
            openSpriteEditor: openSpriteEditor,
            updatePrefabSprite: updatePrefabSprite,
            cleanFrameNameOnly: true);
    }

    private static readonly Regex CleanFrameNameRegex = new Regex(
        @"^frame_\d+\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void ApplyExternalFolder(
        string sourceFolder,
        string framePattern,
        string stripFileName,
        string clipName,
        int fps,
        bool loop,
        int pixelsPerUnit,
        bool chromaKeyGray,
        bool openSpriteEditor,
        bool updatePrefabSprite = false,
        bool cleanFrameNameOnly = false)
    {
        if (!Directory.Exists(sourceFolder))
        {
            throw new DirectoryNotFoundException("Source folder not found: " + sourceFolder);
        }

        IEnumerable<string> frameQuery = Directory.GetFiles(sourceFolder, framePattern, SearchOption.TopDirectoryOnly);
        if (cleanFrameNameOnly)
        {
            frameQuery = frameQuery.Where(path => CleanFrameNameRegex.IsMatch(Path.GetFileName(path)));
        }

        string[] framePaths = frameQuery
            .OrderBy(path => path, Comparer<string>.Create(CompareNaturalFileNames))
            .ToArray();
        if (framePaths.Length == 0)
        {
            throw new InvalidOperationException($"No frames matching '{framePattern}' in {sourceFolder}");
        }

        Texture2D[] frames = new Texture2D[framePaths.Length];
        for (int i = 0; i < framePaths.Length; i++)
        {
            frames[i] = LoadCell(framePaths[i], chromaKeyGray);
        }

        string assetPath = $"{OutputFolder}/{stripFileName}";
        BuildStrip(frames, assetPath);
        foreach (Texture2D frame in frames)
        {
            UnityEngine.Object.DestroyImmediate(frame);
        }

        ConfigureStripImporter(assetPath, frames.Length, pixelsPerUnit);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Sprite[] sprites = LoadStripSprites(assetPath);
        WriteClip(clipName, sprites, fps, loop);

        if (updatePrefabSprite && sprites.Length > 0)
        {
            UpdatePrefabDefaultSprite(sprites[0]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openSpriteEditor)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        Debug.Log(
            $"HeroKnight '{clipName}': {sprites.Length} frames, " +
            $"cell {CellWidth}x{CellHeight}, pivot ({CanvasPivot.x:F6},{CanvasPivot.y:F6}), PPU {pixelsPerUnit}. " +
            $"Source: {sourceFolder}");
    }

    private static Texture2D LoadCell(string path, bool chromaKeyGray = false)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(bytes))
        {
            UnityEngine.Object.DestroyImmediate(source);
            return new Texture2D(CellWidth, CellHeight, TextureFormat.RGBA32, false);
        }

        if (chromaKeyGray)
        {
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a > 0 &&
                    Mathf.Abs(pixel.r - 33) <= 8 &&
                    Mathf.Abs(pixel.g - 33) <= 8 &&
                    Mathf.Abs(pixel.b - 33) <= 8)
                {
                    pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 0);
                }
            }

            source.SetPixels32(pixels);
            source.Apply();
        }

        if (source.width == CellWidth && source.height == CellHeight)
        {
            return source;
        }

        var canvas = new Texture2D(CellWidth, CellHeight, TextureFormat.RGBA32, false);
        var clear = Enumerable.Repeat(new Color32(0, 0, 0, 0), CellWidth * CellHeight).ToArray();
        canvas.SetPixels32(clear);

        if (!TryGetAlphaBounds(source, out int minX, out int minY, out int maxX, out int maxY))
        {
            UnityEngine.Object.DestroyImmediate(source);
            return canvas;
        }

        int cropW = maxX - minX + 1;
        int cropH = maxY - minY + 1;
        Color[] cropped = source.GetPixels(minX, minY, cropW, cropH);
        int destX = (CellWidth - cropW) / 2;
        int destY = 0;
        canvas.SetPixels(destX, destY, cropW, cropH, cropped);
        canvas.Apply();
        UnityEngine.Object.DestroyImmediate(source);
        return canvas;
    }

    private static void BuildStrip(Texture2D[] frameTextures, string assetPath)
    {
        var strip = new Texture2D(CellWidth * frameTextures.Length, CellHeight, TextureFormat.RGBA32, false);
        var clear = Enumerable.Repeat(new Color32(0, 0, 0, 0), strip.width * strip.height).ToArray();
        strip.SetPixels32(clear);

        for (int i = 0; i < frameTextures.Length; i++)
        {
            Color[] pixels = frameTextures[i].GetPixels(0, 0, CellWidth, CellHeight);
            strip.SetPixels(i * CellWidth, 0, CellWidth, CellHeight, pixels);
        }

        strip.Apply();
        string absolute = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? OutputFolder);
        File.WriteAllBytes(absolute, strip.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(strip);
    }

    private static bool TryGetAlphaBounds(Texture2D texture, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = texture.width;
        minY = texture.height;
        maxX = -1;
        maxY = -1;

        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[(y * width) + x].a <= 16)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return maxX >= 0 && maxY >= 0;
    }

    private static void ConfigureStripImporter(string assetPath, int frameCount, int pixelsPerUnit = PixelsPerUnit)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("No importer for " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 16384;

        var metas = new SpriteMetaData[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            metas[i] = new SpriteMetaData
            {
                name = $"frame_{i:000}",
                rect = new Rect(i * CellWidth, 0f, CellWidth, CellHeight),
                alignment = (int)SpriteAlignment.Custom,
                pivot = CanvasPivot,
                border = Vector4.zero
            };
        }

        importer.spritesheet = metas;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadStripSprites(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void WriteClip(string clipName, Sprite[] sprites, int sampleRate, bool loop)
    {
        string path = $"{AnimsFolder}/{clipName}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = sampleRate;
        clip.ClearCurves();

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / (float)sampleRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = sprites.Length > 0 ? (sprites.Length - 1) / (float)sampleRate : 0f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void UpdatePrefabDefaultSprite(Sprite sprite)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return;
        }

        var renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        EditorUtility.SetDirty(prefab);
    }

    private static void RewireStateMachine(AnimatorStateMachine stateMachine, List<string> log)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state == null || !StateToClipName.TryGetValue(child.state.name, out string clipName))
            {
                continue;
            }

            string clipPath = $"{AnimsFolder}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                log.Add($"Missing clip for state '{child.state.name}': {clipPath}");
                continue;
            }

            child.state.motion = clip;
            log.Add($"Rewired state '{child.state.name}' -> {clipName}");
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            if (childMachine.stateMachine != null)
            {
                RewireStateMachine(childMachine.stateMachine, log);
            }
        }
    }

    private static void ReserializeAnimationClips(List<string> log)
    {
        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        string[] clipPaths = Directory.GetFiles(AnimsFolder, "HeroKnight_*.anim", SearchOption.TopDirectoryOnly);
        foreach (string clipPath in clipPaths)
        {
            string assetPath = clipPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                int assetsIndex = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                if (assetsIndex >= 0)
                {
                    assetPath = assetPath.Substring(assetsIndex);
                }
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                continue;
            }

            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0)
            {
                continue;
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            EditorUtility.SetDirty(clip);
            log.Add($"Reserialized clip curves: {Path.GetFileName(assetPath)}");
        }
    }

    private static void EnsurePrefabAnimatorWired(AnimatorController controller, List<string> log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            log.Add($"Prefab missing: {PrefabPath}");
            return;
        }

        var animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            log.Add("Prefab has no Animator.");
            return;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(prefab);
            log.Add("Prefab AnimatorController rewired.");
        }
    }

    private static int CompareNaturalFileNames(string left, string right)
    {
        string leftName = Path.GetFileName(left);
        string rightName = Path.GetFileName(right);
        var leftParts = Regex.Split(leftName, @"(\d+)").Where(part => part.Length > 0).ToArray();
        var rightParts = Regex.Split(rightName, @"(\d+)").Where(part => part.Length > 0).ToArray();
        int count = Math.Min(leftParts.Length, rightParts.Length);

        for (int i = 0; i < count; i++)
        {
            bool leftIsNumber = int.TryParse(leftParts[i], out int leftNumber);
            bool rightIsNumber = int.TryParse(rightParts[i], out int rightNumber);
            if (leftIsNumber && rightIsNumber)
            {
                int compare = leftNumber.CompareTo(rightNumber);
                if (compare != 0)
                {
                    return compare;
                }

                // Tie-break so frame_0003 sorts before frame_003.
                compare = string.Compare(leftParts[i], rightParts[i], StringComparison.Ordinal);
                if (compare != 0)
                {
                    return compare;
                }

                continue;
            }

            int textCompare = string.Compare(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase);
            if (textCompare != 0)
            {
                return textCompare;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
