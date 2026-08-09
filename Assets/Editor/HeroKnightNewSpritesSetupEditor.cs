using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reimports the HeroKnight sprite sheet (black keyed to alpha), slices 8×9 @ 128×113,
/// and rewires HeroKnight_*.anim clips to the new frame layout. Preserves texture GUID.
/// </summary>
public static class HeroKnightNewSpritesSetup
{
    private const string TexturePath = "Assets/Art/Sprites/Characters/Player/HeroKnight.png";
    private const string TextureGuid = "ae297123f091bbf4e8f1835eb98fb293";
    private const string AnimsFolder = "Assets/Animations/Player/Animations";
    private const string SourceFallback =
        @"C:\Users\Bensh\.cursor\projects\c-Users-Bensh-Projects-Castlevania2DPrototype\assets\c__Users_Bensh_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_HeroKnight-63f881f9-8d05-4d79-9e2e-6d407876f08b.png";

    private const int SheetWidth = 1024;
    private const int SheetHeight = 1024;
    private const int Columns = 8;
    private const int Rows = 9;
    private const int CellWidth = 128;
    private const int CellHeight = 113;
    private const int PixelsPerUnit = 53;

    [MenuItem("Tools/Castlevania 2D/Setup Hero Knight New Sprites")]
    public static void SetupMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Setup;
            return;
        }

        Setup();
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureTexturePresent();
        KeyOutPureBlackToTransparent(TexturePath);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        ConfigureAndSliceTexture();
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        RewriteAnimationClips();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Hero Knight new sprites ready: " +
            $"{Columns}x{Rows} cells {CellWidth}x{CellHeight}, PPU {PixelsPerUnit}, " +
            $"GUID {TextureGuid}. Anim clips rewired. Focus Prototype and press Play.");
    }

    private static void EnsureTexturePresent()
    {
        string absolute = Path.GetFullPath(TexturePath);
        if (File.Exists(absolute) && new FileInfo(absolute).Length > 10_000)
        {
            return;
        }

        if (!File.Exists(SourceFallback))
        {
            throw new FileNotFoundException(
                "HeroKnight.png missing and source sheet not found.", SourceFallback);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? "Assets");
        File.Copy(SourceFallback, absolute, overwrite: true);
        Debug.Log($"Copied HeroKnight source sheet to {TexturePath}");
    }

    private static void KeyOutPureBlackToTransparent(string assetPath)
    {
        string absolute = Path.GetFullPath(assetPath);
        byte[] bytes = File.ReadAllBytes(absolute);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException($"Failed to load image bytes: {assetPath}");
        }

        Color32[] pixels = texture.GetPixels32();
        int keyed = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.r == 0 && p.g == 0 && p.b == 0)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
                keyed++;
            }
        }

        texture.SetPixels32(pixels);
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        File.WriteAllBytes(absolute, png);
        Debug.Log($"HeroKnight black→alpha: keyed {keyed} pixels.");
    }

    private static void ConfigureAndSliceTexture()
    {
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No TextureImporter at {TexturePath}");
        }

        // Preserve project GUID if meta already carries it.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;

        var metas = new List<SpriteMetaData>(Columns * Rows);
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int index = row * Columns + col;
                int x = col * CellWidth;
                // Unity rect origin is bottom-left; leftover 7px stays at bottom.
                int y = SheetHeight - (row + 1) * CellHeight;
                metas.Add(new SpriteMetaData
                {
                    name = $"HeroKnight_{index}",
                    rect = new Rect(x, y, CellWidth, CellHeight),
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    border = Vector4.zero
                });
            }
        }

        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void RewriteAnimationClips()
    {
        // Row layout (row-major indices):
        // 0–7 walk, 8–15 run, 16–23 attack1, 24–31 attack2,
        // 32–35 idle, 40–41 hurt, 42–45 death, 48–53 block idle,
        // 54–55 block impact, 57–63 roll, 64–65 jump, 66–67 fall, 68–71 climb.
        WriteClip("HeroKnight_Idle", new[] { 32, 33, 34, 35 }, 8, loop: true);
        // Row 0 = walk, row 1 = run (more aggressive); use run row for Move.
        WriteClip("HeroKnight_Run", new[] { 8, 9, 10, 11, 12, 13, 14, 15 }, 12, loop: true);
        WriteClip("HeroKnight_Attack1", Range(16, 23), 12, loop: false);
        WriteClip("HeroKnight_Attack2", Range(24, 31), 12, loop: false);
        WriteClip("HeroKnight_Attack3", Range(24, 31), 12, loop: false);
        WriteClip("HeroKnight_Jump", new[] { 64, 65 }, 10, loop: false);
        WriteClip("HeroKnight_Fall", new[] { 66, 67 }, 10, loop: true);
        WriteClip("HeroKnight_Hurt", new[] { 40, 41 }, 11, loop: false);
        WriteClip("HeroKnight_Death", new[] { 42, 43, 44, 45 }, 10, loop: false);
        WriteClip("HeroKnight_BlockIdle", Range(48, 53), 8, loop: true);
        WriteClip("HeroKnight_IdleBlock", Range(48, 53), 8, loop: true);
        WriteClip("HeroKnight_Block", new[] { 54, 55 }, 12, loop: false);
        WriteClip("HeroKnight_Roll", Range(57, 63), 14, loop: false);
        WriteClip("HeroKnight_LedgeGrab", Range(68, 71), 12, loop: false);
        WriteClip("HeroKnight_WallSlide", new[] { 66, 67 }, 12, loop: false);
    }

    private static int[] Range(int fromInclusive, int toInclusive)
    {
        int count = toInclusive - fromInclusive + 1;
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = fromInclusive + i;
        }

        return result;
    }

    private static void WriteClip(string clipName, int[] spriteIndices, int sampleRate, bool loop)
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

        var keyframes = new ObjectReferenceKeyframe[spriteIndices.Length];
        for (int i = 0; i < spriteIndices.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / (float)sampleRate,
                value = LoadSprite(spriteIndices[i])
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = spriteIndices.Length / (float)sampleRate;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
    }

    private static Sprite LoadSprite(int index)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TexturePath);
        string targetName = $"HeroKnight_{index}";
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite && sprite.name == targetName)
            {
                return sprite;
            }
        }

        throw new InvalidOperationException(
            $"Sprite '{targetName}' not found on {TexturePath}. Reimport the sheet first.");
    }
}
