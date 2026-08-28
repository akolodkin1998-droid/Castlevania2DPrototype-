#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Imports the dungeon and loading sheets into separate sprite PNG and Tile asset folders.
/// Underground-only: Tools → Castlevania 2D → Import Underground Tiles
/// or Temp/import_underground_tiles.flag. Never edits scenes.
/// </summary>
[InitializeOnLoad]
public static class DungeonTilesImportEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Import Dungeon Tiles";
    private const string TextureFolder = "Assets/Art/Tiles/Dungeon";
    private const string UndergroundFlagPath = "Temp/import_underground_tiles.flag";
    private const string UndergroundResultPath = "Temp/import_underground_tiles_result.txt";

    static DungeonTilesImportEditor()
    {
        EditorApplication.delayCall += TryImportUndergroundFromFlag;
        EditorApplication.update += PollUndergroundFlag;
    }

    private static void PollUndergroundFlag()
    {
        if (!File.Exists(UndergroundFlagPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TryImportUndergroundFromFlag();
    }

    private static void TryImportUndergroundFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(UndergroundFlagPath))
        {
            return;
        }

        File.Delete(UndergroundFlagPath);
        try
        {
            int created = ImportUnderground();
            string summary = "Imported underground tiles only. Tile assets: " + created +
                             ". Scene was not modified.";
            Debug.Log(summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(UndergroundResultPath, "OK\n" + summary);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(UndergroundResultPath, "FAIL\n" + exception);
        }
    }

    private static readonly SheetDefinition[] Sheets =
    {
        new(
            sourcePath: @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\подземелье.png",
            textureAssetPath: TextureFolder + "/Dungeon_Tiles.png",
            outputFolderName: "Dungeon",
            sheetWidth: 64,
            sheetHeight: 64,
            cellSize: 16),
        new(
            sourcePath: @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\загрузка.png",
            textureAssetPath: TextureFolder + "/Loading_Tiles.png",
            outputFolderName: "Loading",
            sheetWidth: 256,
            sheetHeight: 256,
            cellSize: 32,
            alwaysExportTiles: true),
        new(
            sourcePath: @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Бэкграунд ФОН\Подъземье\тайлы.png",
            textureAssetPath: TextureFolder + "/Underground_Tiles.png",
            outputFolderName: "Underground",
            sheetWidth: 256,
            sheetHeight: 256,
            cellSize: 32,
            alwaysExportTiles: true,
            fallbackSourcePaths: new[]
            {
                @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Подъземье.png"
            })
    };

    // Intentionally menu-only: automatic imports can invalidate Tile Palette targets mid-drag.
    [MenuItem(MenuPath)]
    public static void ImportMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Import;
            return;
        }

        Import();
    }

    [MenuItem("Tools/Castlevania 2D/Import Underground Tiles")]
    public static void ImportUndergroundMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => ImportUnderground();
            return;
        }

        ImportUnderground();
    }

    public static void Import()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolders();

        foreach (SheetDefinition sheet in Sheets)
        {
            ImportSheet(sheet);
        }

        AssetDatabase.SaveAssets();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TextureFolder + "/Tiles");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    public static int ImportUnderground()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return 0;
        }

        EnsureFolders();
        SheetDefinition underground = GetSheet("Underground");
        int created = ImportSheet(underground);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Art", "Tiles");
        EnsureFolder("Assets/Art/Tiles", "Dungeon");
        EnsureFolder(TextureFolder, "Sliced");
        EnsureFolder(TextureFolder, "Tiles");
    }

    private static SheetDefinition GetSheet(string outputFolderName)
    {
        for (int i = 0; i < Sheets.Length; i++)
        {
            if (Sheets[i].OutputFolderName == outputFolderName)
            {
                return Sheets[i];
            }
        }

        throw new InvalidOperationException("No tile sheet named " + outputFolderName + ".");
    }

    private static int ImportSheet(SheetDefinition sheet)
    {
        EnsureFolder(TextureFolder + "/Sliced", sheet.OutputFolderName);
        EnsureFolder(TextureFolder + "/Tiles", sheet.OutputFolderName);

        CopySourceTexture(sheet);
        ConfigureAndSliceTexture(sheet, isReadable: true);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.TextureAssetPath);
        if (texture == null || texture.width != sheet.SheetWidth || texture.height != sheet.SheetHeight)
        {
            throw new InvalidOperationException(
                $"{sheet.OutputFolderName} tiles must be {sheet.SheetWidth}x{sheet.SheetHeight}; imported texture is " +
                $"{texture?.width}x{texture?.height}.");
        }

        // GetPixels32 returns pixels in bottom-left row order. Reuse this single
        // read for both transparency detection and PNG cell extraction.
        Color32[] sourcePixels = texture.GetPixels32();
        HashSet<string> nonEmptySpriteNames = GetNonEmptySpriteNames(sourcePixels, sheet);
        ExportIndividualTilePngs(sourcePixels, nonEmptySpriteNames, sheet);

        // Pixel access is only needed while generating the separate PNG files.
        ConfigureAndSliceTexture(sheet, isReadable: false);

        Sprite[] sprites = LoadSlicedSprites(sheet);
        if (sprites.Length != sheet.CellCount)
        {
            throw new InvalidOperationException(
                $"DungeonTilesImportEditor: expected {sheet.CellCount} {sheet.OutputFolderName} sprites, found {sprites.Length}.");
        }

        int created = CreateOrUpdateTiles(sprites, nonEmptySpriteNames, sheet);
        Debug.Log(
            $"{sheet.OutputFolderName} tiles ready: {sprites.Length} sliced sprites / {created} Tile assets, " +
            $"cell {sheet.CellSize}x{sheet.CellSize}, grid {sheet.Columns}x{sheet.Rows}. " +
            $"Texture: {sheet.TextureAssetPath}. PNG tiles: {sheet.SlicedPngFolder}. Tiles: {sheet.TilesFolder}.");
        return created;
    }

    private static void CopySourceTexture(SheetDefinition sheet)
    {
        string absoluteDestination = Path.GetFullPath(sheet.TextureAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteDestination)!);

        string sourcePath = ResolveSourcePath(sheet);
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, absoluteDestination, overwrite: true);
        }
        else if (!File.Exists(absoluteDestination))
        {
            throw new FileNotFoundException(
                $"{sheet.OutputFolderName} source PNG not found and project copy is missing.", sheet.SourcePath);
        }
        else
        {
            Debug.LogWarning(
                $"{sheet.OutputFolderName} source missing at {sheet.SourcePath}; using existing {sheet.TextureAssetPath}.");
        }

        AssetDatabase.ImportAsset(sheet.TextureAssetPath, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureAndSliceTexture(SheetDefinition sheet, bool isReadable)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.TextureAssetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No TextureImporter at {sheet.TextureAssetPath}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = sheet.CellSize;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = isReadable;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePivot = new Vector2(0.5f, 0.5f);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Multiple;
        settings.spritePixelsPerUnit = sheet.CellSize;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.filterMode = FilterMode.Point;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        importer.SetTextureSettings(settings);

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(platform);

        var metadata = new List<SpriteMetaData>(sheet.CellCount);
        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int column = 0; column < sheet.Columns; column++)
            {
                int index = row * sheet.Columns + column;
                metadata.Add(new SpriteMetaData
                {
                    name = sheet.GetSpriteName(index),
                    rect = new Rect(
                        column * sheet.CellSize,
                        sheet.SheetHeight - (row + 1) * sheet.CellSize,
                        sheet.CellSize,
                        sheet.CellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                });
            }
        }

        importer.spritesheet = metadata.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static HashSet<string> GetNonEmptySpriteNames(Color32[] pixels, SheetDefinition sheet)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int column = 0; column < sheet.Columns; column++)
            {
                if (CellHasVisiblePixels(pixels, column, row, sheet))
                {
                    names.Add(sheet.GetSpriteName(row * sheet.Columns + column));
                }
            }
        }

        return names;
    }

    private static void ExportIndividualTilePngs(
        Color32[] sourcePixels,
        HashSet<string> nonEmptySpriteNames,
        SheetDefinition sheet)
    {
        string absoluteFolder = Path.GetFullPath(sheet.SlicedPngFolder);
        Directory.CreateDirectory(absoluteFolder);

        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int column = 0; column < sheet.Columns; column++)
            {
                string spriteName = sheet.GetSpriteName(row * sheet.Columns + column);
                string filePath = Path.Combine(absoluteFolder, spriteName + ".png");

                if (!sheet.AlwaysExportTiles && !nonEmptySpriteNames.Contains(spriteName))
                {
                    continue;
                }

                var tileTexture = new Texture2D(sheet.CellSize, sheet.CellSize, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                tileTexture.SetPixels32(CopyCellPixels(sourcePixels, column, row, sheet));
                tileTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(filePath, tileTexture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tileTexture);
            }
        }

        AssetDatabase.Refresh();
        if (sheet.AlwaysExportTiles)
        {
            for (int index = 0; index < sheet.CellCount; index++)
            {
                string spriteName = sheet.GetSpriteName(index);
                ConfigureIndividualTileTexture($"{sheet.SlicedPngFolder}/{spriteName}.png", sheet.CellSize);
            }

            return;
        }

        foreach (string spriteName in nonEmptySpriteNames)
        {
            ConfigureIndividualTileTexture($"{sheet.SlicedPngFolder}/{spriteName}.png", sheet.CellSize);
        }
    }

    private static Color32[] CopyCellPixels(
        Color32[] sourcePixels,
        int column,
        int topDownRow,
        SheetDefinition sheet)
    {
        int cellSize = sheet.CellSize;
        int sourceX = column * cellSize;
        int sourceY = sheet.SheetHeight - (topDownRow + 1) * cellSize;
        var cellPixels = new Color32[cellSize * cellSize];

        // Texture2D pixel arrays are row-major from the bottom-left. sourceY is
        // already the bottom edge of this top-down sheet-grid cell, so copying
        // rows in ascending order preserves the tile orientation and alpha.
        for (int y = 0; y < cellSize; y++)
        {
            Array.Copy(
                sourcePixels,
                (sourceY + y) * sheet.SheetWidth + sourceX,
                cellPixels,
                y * cellSize,
                cellSize);
        }

        return cellPixels;
    }

    private static void ConfigureIndividualTileTexture(string assetPath, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No TextureImporter at {assetPath}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
    }

    private static bool CellHasVisiblePixels(
        Color32[] pixels,
        int column,
        int topDownRow,
        SheetDefinition sheet)
    {
        int startX = column * sheet.CellSize;
        int startY = sheet.SheetHeight - (topDownRow + 1) * sheet.CellSize;
        for (int y = startY; y < startY + sheet.CellSize; y++)
        {
            for (int x = startX; x < startX + sheet.CellSize; x++)
            {
                if (pixels[y * sheet.SheetWidth + x].a != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Sprite[] LoadSlicedSprites(SheetDefinition sheet)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheet.TextureAssetPath);
        var sprites = new List<Sprite>(sheet.CellCount);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return sprites.ToArray();
    }

    private static int CreateOrUpdateTiles(
        IEnumerable<Sprite> sprites,
        HashSet<string> nonEmptySpriteNames,
        SheetDefinition sheet)
    {
        int count = 0;
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null || (!sheet.AlwaysExportTiles && !nonEmptySpriteNames.Contains(sprite.name)))
            {
                continue;
            }

            string tilePath = $"{sheet.TilesFolder}/{sprite.name}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Sprite;
            EditorUtility.SetDirty(tile);
            count++;
        }

        return count;
    }

    private sealed class SheetDefinition
    {
        public SheetDefinition(
            string sourcePath,
            string textureAssetPath,
            string outputFolderName,
            int sheetWidth,
            int sheetHeight,
            int cellSize,
            bool alwaysExportTiles = false,
            string[] fallbackSourcePaths = null)
        {
            if (sheetWidth % cellSize != 0 || sheetHeight % cellSize != 0)
            {
                throw new ArgumentException("Sheet dimensions must be divisible by the cell size.");
            }

            SourcePath = sourcePath;
            TextureAssetPath = textureAssetPath;
            OutputFolderName = outputFolderName;
            SheetWidth = sheetWidth;
            SheetHeight = sheetHeight;
            CellSize = cellSize;
            AlwaysExportTiles = alwaysExportTiles;
            FallbackSourcePaths = fallbackSourcePaths ?? Array.Empty<string>();
        }

        public string SourcePath { get; }
        public string[] FallbackSourcePaths { get; }
        public string TextureAssetPath { get; }
        public string OutputFolderName { get; }
        public int SheetWidth { get; }
        public int SheetHeight { get; }
        public int CellSize { get; }
        public int Columns => SheetWidth / CellSize;
        public int Rows => SheetHeight / CellSize;
        public int CellCount => Columns * Rows;
        public bool AlwaysExportTiles { get; }
        public string SpriteNamePrefix => OutputFolderName + "_Tile_";
        public string SlicedPngFolder => TextureFolder + "/Sliced/" + OutputFolderName;
        public string TilesFolder => TextureFolder + "/Tiles/" + OutputFolderName;

        public string GetSpriteName(int index)
        {
            return $"{SpriteNamePrefix}{index:D2}";
        }
    }

    private static string ResolveSourcePath(SheetDefinition sheet)
    {
        if (File.Exists(sheet.SourcePath))
        {
            return sheet.SourcePath;
        }

        for (int i = 0; i < sheet.FallbackSourcePaths.Length; i++)
        {
            string fallback = sheet.FallbackSourcePaths[i];
            if (File.Exists(fallback))
            {
                return fallback;
            }
        }

        return sheet.SourcePath;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
