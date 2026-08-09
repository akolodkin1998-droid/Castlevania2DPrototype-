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
public static class HeroKnightSingleAnimSetupEditor
{
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

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Idle")]
    public static void ApplyIdle()
    {
        ApplyFolder("idle", "HeroKnight_Idle", "frame_*.png", 6, true, true, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Run")]
    public static void ApplyRun()
    {
        // Nested game_ready/frames/run/run (not parent frames/run).
        string nestedRun = Path.Combine(SourceRoot, "run", "run");
        ApplyExternalFolder(
            nestedRun,
            "frame_*.png",
            "HeroKnight_run.png",
            "HeroKnight_Run",
            fps: 10,
            loop: true,
            pixelsPerUnit: PixelsPerUnit,
            chromaKeyGray: false,
            openSpriteEditor: true,
            updatePrefabSprite: false,
            cleanFrameNameOnly: true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Jump")]
    public static void ApplyJump()
    {
        ApplyFolder("jump", "HeroKnight_Jump", "frame_*.png", 8, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Fall")]
    public static void ApplyFall()
    {
        ApplyFolder("fall", "HeroKnight_Fall", "frame_*.png", 8, true, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Hurt")]
    public static void ApplyHurt()
    {
        ApplyFolder("hurt", "HeroKnight_Hurt", "frame_*.png", 9, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Roll")]
    public static void ApplyRoll()
    {
        ApplyFolder("roll", "HeroKnight_Roll", "frame_*.png", 12, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 1")]
    public static void ApplyAttack1()
    {
        ApplyFolder("attack_1", "HeroKnight_Attack1", "frame_*.png", 10, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 2")]
    public static void ApplyAttack2()
    {
        ApplyFolder("attack_2", "HeroKnight_Attack2", "frame_*.png", 10, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Attack 3")]
    public static void ApplyAttack3()
    {
        ApplyFolder("attack_3", "HeroKnight_Attack3", "frame_*.png", 10, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block")]
    public static void ApplyBlock()
    {
        ApplyFolder("block_effect", "HeroKnight_Block", "frame_*.png", 10, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block Overhead")]
    public static void ApplyBlockOverhead()
    {
        ApplyFolder("block_overhead", "HeroKnight_IdleBlock", "frame_*.png", 6, false, false, true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Anim/Block Overhead Walk")]
    public static void ApplyBlockOverheadWalk()
    {
        const string sourceFolder =
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\warrior_animations_game_ready-v5 (1)\1785214083_6a6834832efd6\converted";
        ApplyExternalFolder(
            sourceFolder,
            "frame_*-Photoroom.png",
            "HeroKnight_block_overhead_walk.png",
            "HeroKnight_IdleBlockWalk",
            fps: 10,
            loop: true,
            pixelsPerUnit: 321,
            chromaKeyGray: true,
            openSpriteEditor: true);
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
        ApplyRoll();
        ApplyAttack1();
        ApplyAttack2();
        ApplyAttack3();
        ApplyBlock();
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
        { "Run", "HeroKnight_Run" },
        { "Hurt", "HeroKnight_Hurt" },
        { "Death", "HeroKnight_Death" },
        { "DeathNoBlood", "HeroKnight_DeathNoBlood" },
        { "Jump", "HeroKnight_Jump" },
        { "Fall", "HeroKnight_Fall" },
        { "Block", "HeroKnight_Block" },
        { "Roll", "HeroKnight_Roll" },
        { "Attack1", "HeroKnight_Attack1" },
        { "Attack2", "HeroKnight_Attack2" },
        { "Attack3", "HeroKnight_Attack3" },
        { "Idle Block", "HeroKnight_IdleBlock" },
        { "Idle Block Walk", "HeroKnight_IdleBlockWalk" },
        { "Wall Slide", "HeroKnight_WallSlide" },
        { "Wall Slide Land", "HeroKnight_WallSlideLand" },
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
        importer.maxTextureSize = 8192;

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
