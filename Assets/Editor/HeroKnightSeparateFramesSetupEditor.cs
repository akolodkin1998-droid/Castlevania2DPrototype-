using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Imports HeroKnight separate_frames folders, builds one strip texture per animation,
/// aligns feet to the bottom of each cell, rewires clips, opens Sprite Editor.
/// </summary>
public static class HeroKnightSeparateFramesSetupEditor
{
    private const string SourceRoot =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\separate_frames\separate_frames";

    private const string OutputFolder = "Assets/Art/Sprites/Characters/Player/HeroKnightAnim";
    private const string AnimsFolder = "Assets/Animations/Player/Animations";
    private const string PrototypeScene = "Assets/Scenes/Prototype.unity";
    private const string FlagPath = "Temp/setup_heroknight_separate_frames.flag";
    private const string WallSlideOnlyFlagPath = "Temp/setup_heroknight_wall_slide_only.flag";

    private const int PixelsPerUnit = 240;
    private const int CellWidth = 528;
    private const int CellHeight = 520;

    private static readonly AnimMapping[] Mappings =
    {
        new("idle", "HeroKnight_Idle", 8, true),
        new("run", "HeroKnight_Run", 12, true),
        new("attack_1", "HeroKnight_Attack1", 12, false),
        new("attack_2", "HeroKnight_Attack2", 12, false),
        new("attack_3", "HeroKnight_Attack3", 12, false),
        new("block_effect", "HeroKnight_Block", 12, false),
        new("block_overhead", "HeroKnight_IdleBlock", 8, true),
        new("block_overhead", "HeroKnight_BlockNoEffect", 12, false),
        new("block_overhead", "HeroKnight_BlockIdle", 8, true),
        new("death_blood", "HeroKnight_Death", 10, false),
        new("death_blood", "HeroKnight_DeathNoBlood", 10, false),
        new("fall", "HeroKnight_Fall", 10, true),
        new("hurt", "HeroKnight_Hurt", 11, false),
        new("jump", "HeroKnight_Jump", 10, false),
        new("ledge_grab", "HeroKnight_LedgeGrab", 12, false),
        new("roll", "HeroKnight_Roll", 14, false),
        // Wall slide: PPU 126.98 (~5% larger than prior 133.33). Loop while sliding.
        new("wall_slide", "HeroKnight_WallSlide", 8, true),
    };

    private const float WallSlidePixelsPerUnit = 126.98f;
    // Pivot near bottom-center; 0.56 = slight left of previous 0.52 bias.
    private static readonly Vector2 WallSlidePivot = new Vector2(0.56f, 0f);

    static HeroKnightSeparateFramesSetupEditor()
    {
        EditorApplication.delayCall += TryApplyFromFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Separate Frames")]
    public static void ApplyMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ApplyMenu;
            return;
        }

        Apply(openSpriteEditor: true);
    }

    [MenuItem("Tools/Castlevania 2D/Apply HeroKnight Separate Frames/Wall Slide Only")]
    public static void ApplyWallSlideOnlyMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ApplyWallSlideOnlyMenu;
            return;
        }

        ApplyWallSlideOnly(openSpriteEditor: true);
    }

    public static void ApplyWallSlideOnlyFromBatch()
    {
        try
        {
            ApplyWallSlideOnly(openSpriteEditor: false);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorApplication.Exit(1);
        }
    }

    public static void ApplyFromBatch()
    {
        try
        {
            Apply(openSpriteEditor: false);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void TryApplyFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string wallSlideFlag = Path.Combine(GetProjectRoot(), WallSlideOnlyFlagPath);
        if (File.Exists(wallSlideFlag))
        {
            File.Delete(wallSlideFlag);
            ApplyWallSlideOnly(openSpriteEditor: true);
            return;
        }

        string flagPath = Path.Combine(GetProjectRoot(), FlagPath);
        if (!File.Exists(flagPath))
        {
            return;
        }

        File.Delete(flagPath);
        Apply(openSpriteEditor: true);
    }

    public static void Apply(bool openSpriteEditor = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!Directory.Exists(SourceRoot))
        {
            throw new DirectoryNotFoundException("Source folder not found: " + SourceRoot);
        }

        Directory.CreateDirectory(Path.GetFullPath(OutputFolder));

        var builtStrips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimMapping mapping in Mappings)
        {
            if (builtStrips.ContainsKey(mapping.FolderName))
            {
                continue;
            }

            string sourceFolder = Path.Combine(SourceRoot, mapping.FolderName);
            if (!Directory.Exists(sourceFolder))
            {
                throw new DirectoryNotFoundException("Missing animation folder: " + sourceFolder);
            }

            string[] framePaths = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (framePaths.Length == 0)
            {
                throw new InvalidOperationException("No PNG frames in " + sourceFolder);
            }

            string assetPath = $"{OutputFolder}/HeroKnight_{mapping.FolderName}.png";
            BuildStripTexture(framePaths, assetPath);
            if (mapping.FolderName == "wall_slide")
            {
                ConfigureStripImporter(assetPath, framePaths.Length, WallSlidePixelsPerUnit, WallSlidePivot);
            }
            else
            {
                ConfigureStripImporter(assetPath, framePaths.Length, PixelsPerUnit);
            }

            builtStrips[mapping.FolderName] = assetPath;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (AnimMapping mapping in Mappings)
        {
            string stripPath = builtStrips[mapping.FolderName];
            Sprite[] sprites = LoadStripSprites(stripPath);
            WriteClip(mapping.ClipName, sprites, mapping.Fps, mapping.Loop);
            if (mapping.FolderName == "wall_slide")
            {
                WriteWallSlideLandClip(sprites, mapping.Fps);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (File.Exists(Path.GetFullPath(PrototypeScene)))
        {
            EditorSceneManager.OpenScene(PrototypeScene);
        }

        if (openSpriteEditor && builtStrips.Count > 0)
        {
            string firstStrip = builtStrips.Values.First();
            OpenSpriteEditor(firstStrip);
        }

        Debug.Log(
            $"HeroKnight separate_frames applied: {builtStrips.Count} strips, " +
            $"cell {CellWidth}x{CellHeight}, PPU {PixelsPerUnit} (wall_slide PPU {WallSlidePixelsPerUnit}, " +
            $"pivot {WallSlidePivot.x:0.##},{WallSlidePivot.y:0.##}), feet bottom-aligned. " +
            "Sprite Editor opened on idle strip — tweak pivots there if needed.");

        HeroKnightSingleAnimSetupEditor.RepairAnimatorWiring();
    }

    public static void ApplyWallSlideOnly(bool openSpriteEditor = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AnimMapping mapping = Mappings.First(m => m.FolderName == "wall_slide");
        string sourceFolder = Path.Combine(SourceRoot, mapping.FolderName);
        if (!Directory.Exists(sourceFolder))
        {
            throw new DirectoryNotFoundException("Missing animation folder: " + sourceFolder);
        }

        string[] framePaths = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (framePaths.Length == 0)
        {
            throw new InvalidOperationException("No PNG frames in " + sourceFolder);
        }

        Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
        string assetPath = $"{OutputFolder}/HeroKnight_{mapping.FolderName}.png";
        BuildStripTexture(framePaths, assetPath);
        ConfigureStripImporter(assetPath, framePaths.Length, WallSlidePixelsPerUnit, WallSlidePivot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Sprite[] sprites = LoadStripSprites(assetPath);
        WriteClip(mapping.ClipName, sprites, mapping.Fps, mapping.Loop);
        WriteWallSlideLandClip(sprites, mapping.Fps);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openSpriteEditor)
        {
            OpenSpriteEditor(assetPath);
        }

        Debug.Log(
            $"HeroKnight wall_slide only: {sprites.Length} frames → {assetPath}, " +
            $"cell {CellWidth}x{CellHeight}, PPU {WallSlidePixelsPerUnit} (vs hero PPU {PixelsPerUnit}), " +
            $"custom pivot ({WallSlidePivot.x:0.##}, {WallSlidePivot.y:0.##}), " +
            "WallSlide + WallSlideLand clips updated.");

        HeroKnightSingleAnimSetupEditor.RepairAnimatorWiring();
    }

    private static void WriteWallSlideLandClip(Sprite[] wallSlideSprites, int sampleRate)
    {
        if (wallSlideSprites == null || wallSlideSprites.Length == 0)
        {
            return;
        }

        int landCount = Math.Min(2, wallSlideSprites.Length);
        var landSprites = new Sprite[landCount];
        Array.Copy(wallSlideSprites, wallSlideSprites.Length - landCount, landSprites, 0, landCount);
        WriteClip("HeroKnight_WallSlideLand", landSprites, sampleRate, loop: false);
    }

    private static void BuildStripTexture(string[] framePaths, string assetPath)
    {
        int frameCount = framePaths.Length;
        var strip = new Texture2D(CellWidth * frameCount, CellHeight, TextureFormat.RGBA32, false);
        var clear = Enumerable.Repeat(new Color32(0, 0, 0, 0), strip.width * strip.height).ToArray();
        strip.SetPixels32(clear);

        for (int i = 0; i < frameCount; i++)
        {
            byte[] bytes = File.ReadAllBytes(framePaths[i]);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(source);
                throw new InvalidOperationException("Failed to load frame: " + framePaths[i]);
            }

            if (!TryGetAlphaBounds(source, out int minX, out int minY, out int maxX, out int maxY))
            {
                UnityEngine.Object.DestroyImmediate(source);
                continue;
            }

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;
            Color[] cropped = source.GetPixels(minX, minY, cropW, cropH);
            int destX = (i * CellWidth) + ((CellWidth - cropW) / 2);
            int destY = 0;
            strip.SetPixels(destX, destY, cropW, cropH, cropped);
            UnityEngine.Object.DestroyImmediate(source);
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

    private static void ConfigureStripImporter(
        string assetPath,
        int frameCount,
        float pixelsPerUnit = PixelsPerUnit,
        Vector2? pivotOverride = null)
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
        importer.wrapMode = TextureWrapMode.Clamp;

        Vector2 pivot = pivotOverride ?? new Vector2(0.5f, 0f);
        int alignment = pivotOverride.HasValue
            ? (int)SpriteAlignment.Custom
            : (int)SpriteAlignment.BottomCenter;

        var metas = new List<SpriteMetaData>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            metas.Add(new SpriteMetaData
            {
                name = $"frame_{i:000}",
                rect = new Rect(i * CellWidth, 0f, CellWidth, CellHeight),
                alignment = alignment,
                pivot = pivot,
                border = Vector4.zero
            });
        }

        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadStripSprites(string assetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return assets
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void WriteClip(string clipName, Sprite[] sprites, int sampleRate, bool loop)
    {
        if (sprites == null || sprites.Length == 0)
        {
            throw new InvalidOperationException($"No sprites for clip {clipName}");
        }

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
        settings.stopTime = sprites.Length / (float)sampleRate;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void OpenSpriteEditor(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Selection.activeObject = texture != null ? texture : importer;
        EditorGUIUtility.PingObject(Selection.activeObject);

        Type spriteEditorWindowType = Type.GetType(
            "UnityEditor.U2D.Sprites.SpriteEditorWindow, Unity.2D.Sprite.Editor");
        MethodInfo showMethod = spriteEditorWindowType?.GetMethod(
            "ShowSpriteEditorWindow",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(TextureImporter) },
            null);
        showMethod?.Invoke(null, new object[] { importer });
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private readonly struct AnimMapping
    {
        public readonly string FolderName;
        public readonly string ClipName;
        public readonly int Fps;
        public readonly bool Loop;

        public AnimMapping(string folderName, string clipName, int fps, bool loop)
        {
            FolderName = folderName;
            ClipName = clipName;
            Fps = fps;
            Loop = loop;
        }
    }
}
