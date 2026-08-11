#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Castlevania2D.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class LavaTilesSetupEditor
{
    private const string SourceFolder =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Лава";
    private const string LavaFolder = "Assets/Art/Tiles/Lava";
    private const string FramesFolder = LavaFolder + "/Frames";
    private const string TilePath = LavaFolder + "/Lava_Animated.asset";
    private const string PrefabPath = "Assets/Prefabs/Environment/Lava_Tilemap.prefab";
    private const float PixelsPerUnit = 32f;
    private const float FramesPerSecond = 10f;

    static LavaTilesSetupEditor()
    {
        EditorApplication.delayCall += EnsureAssetsExist;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Lava Tiles")]
    public static void SetupMenu()
    {
        try
        {
            string result = Setup();
            EditorUtility.DisplayDialog("Lava Tiles", result, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Lava Tiles", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static string Setup()
    {
        EnsureFolder(LavaFolder);
        EnsureFolder(FramesFolder);
        EnsureFolder("Assets/Prefabs/Environment");

        CopySourceFrames();
        Sprite[] frames = ImportFrames();
        if (frames.Length == 0)
        {
            throw new InvalidOperationException(
                "No lava frames found in " + SourceFolder + " or " + FramesFolder);
        }

        LavaAnimatedTile tile = CreateOrUpdateAnimatedTile(frames);
        CreateOrUpdatePrefab(tile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = tile;
        EditorGUIUtility.PingObject(tile);

        return
            $"Lava ready: {frames.Length} animation frames at {FramesPerSecond} FPS.\n" +
            $"Animated Tile: {TilePath}\n" +
            $"Drag-ready prefab: {PrefabPath}\n\n" +
            "For painting: drag Lava_Animated into Window → 2D → Tile Palette.";
    }

    private static void EnsureAssetsExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !RequiresSetup())
        {
            return;
        }

        try
        {
            Debug.Log(Setup());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool RequiresSetup()
    {
        LavaAnimatedTile tile = AssetDatabase.LoadAssetAtPath<LavaAnimatedTile>(TilePath);
        if (tile == null || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            return true;
        }

        int sourceFrameCount = Directory.Exists(SourceFolder)
            ? Directory.GetFiles(SourceFolder, "*.png").Length
            : 0;
        int importedFrameCount = Directory.Exists(ToFullPath(FramesFolder))
            ? Directory.GetFiles(ToFullPath(FramesFolder), "Lava_*.png").Length
            : 0;

        var serializedTile = new SerializedObject(tile);
        SerializedProperty framesProperty = serializedTile.FindProperty("frames");
        return sourceFrameCount > 0 &&
               (sourceFrameCount != importedFrameCount ||
                framesProperty == null ||
                framesProperty.arraySize != sourceFrameCount);
    }

    private static void CopySourceFrames()
    {
        if (!Directory.Exists(SourceFolder))
        {
            return;
        }

        string[] sourceFiles = Directory.GetFiles(SourceFolder, "*.png");
        Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
        string destinationFolder = ToFullPath(FramesFolder);
        Directory.CreateDirectory(destinationFolder);

        string[] previousFrames = Directory.GetFiles(destinationFolder, "Lava_*.png");
        for (int i = 0; i < previousFrames.Length; i++)
        {
            string assetPath = FramesFolder + "/" + Path.GetFileName(previousFrames[i]);
            AssetDatabase.DeleteAsset(assetPath);
        }

        for (int i = 0; i < sourceFiles.Length; i++)
        {
            string destination = Path.Combine(destinationFolder, $"Lava_{i + 1:D4}.png");
            File.Copy(sourceFiles[i], destination, overwrite: true);
        }

        AssetDatabase.Refresh();
    }

    private static Sprite[] ImportFrames()
    {
        string absoluteFramesFolder = ToFullPath(FramesFolder);
        if (!Directory.Exists(absoluteFramesFolder))
        {
            return Array.Empty<Sprite>();
        }

        string[] frameFiles = Directory.GetFiles(absoluteFramesFolder, "Lava_*.png");
        Array.Sort(frameFiles, StringComparer.OrdinalIgnoreCase);
        var frames = new List<Sprite>(frameFiles.Length);

        for (int i = 0; i < frameFiles.Length; i++)
        {
            string assetPath = FramesFolder + "/" + Path.GetFileName(frameFiles[i]);
            ConfigureFrameImporter(assetPath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                frames.Add(sprite);
            }
        }

        return frames.ToArray();
    }

    private static void ConfigureFrameImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer == null)
        {
            throw new InvalidOperationException("No TextureImporter for " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static LavaAnimatedTile CreateOrUpdateAnimatedTile(Sprite[] frames)
    {
        LavaAnimatedTile tile = AssetDatabase.LoadAssetAtPath<LavaAnimatedTile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<LavaAnimatedTile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }

        var serializedTile = new SerializedObject(tile);
        SerializedProperty framesProperty = serializedTile.FindProperty("frames");
        framesProperty.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        serializedTile.FindProperty("framesPerSecond").floatValue = FramesPerSecond;
        serializedTile.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void CreateOrUpdatePrefab(LavaAnimatedTile tile)
    {
        var root = new GameObject("Lava_Tilemap");
        root.AddComponent<Grid>();

        var tilemapObject = new GameObject("Lava");
        tilemapObject.transform.SetParent(root.transform, worldPositionStays: false);
        var tilemap = tilemapObject.AddComponent<Tilemap>();
        var renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 2;
        tilemap.SetTile(Vector3Int.zero, tile);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
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
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
