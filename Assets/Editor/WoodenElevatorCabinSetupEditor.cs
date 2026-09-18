using System;
using System.IO;
using Castlevania2D.Level;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the wooden elevator cabin (Лифт_0002) and the front beam (Лифт_0001).
/// Player walks behind the beam into the cabin. Menu / Temp/setup_wooden_elevator.flag.
/// </summary>
[InitializeOnLoad]
public static class WoodenElevatorCabinSetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Environment/WoodenElevator";
    private const string CabinAsset = DestFolder + "/ElevatorCabin.png";
    private const string BeamAsset = DestFolder + "/ElevatorCabin_Beam.png";
    private const string PrefabFolder = "Assets/Prefabs/Environment";
    private const string PrefabPath = PrefabFolder + "/WoodenElevatorCabin.prefab";
    private const string SetupFlagPath = "Temp/setup_wooden_elevator.flag";
    private const string RootName = "WoodenElevatorCabin";
    private const string CabinName = "Cabin";
    private const string BeamName = "FrontBeam";
    private const string FloorName = "Floor";
    private const string LeftWallName = "LeftWall";
    private const string CeilingName = "Ceiling";
    private const string InteriorName = "Interior";
    private const string WinchName = "Winch";
    private const string RopeName = "HangingRope";
    private const string RopeHookName = "RopeHook";
    private const string WinchFolder = DestFolder + "/Winch";
    private const string RopeAsset = DestFolder + "/ElevatorCabin_Rope.png";
    private const string GearBoxName = "GearBox";
    private const string GearBoxAsset = DestFolder + "/ElevatorGearBox.png";
    private static readonly Vector3 GearBoxLocalPosition = new Vector3(-1.72f, 8f, 0f);
    private const int GearBoxSortingOrder = 3;
    private const int WinchSortingOrder = 2;
    private const float WinchFrameRate = 10f;
    private static readonly Vector3 WinchLocalPosition = new Vector3(0f, 8f, 0f);
    private const float PixelsPerUnit = 100f;
    private const float TextureWidth = 688f;
    private const int BeamSortingOrder = 6;
    private const int CabinSpriteOrder = 1;
    private static readonly Vector3 CabinScale = new Vector3(1.4f, 1.4f, 1.41f);
    private static readonly Vector3 FallbackPosition = new Vector3(22f, -69.5f, 0f);

    static WoodenElevatorCabinSetupEditor()
    {
        EditorApplication.delayCall += TrySetupFromFlag;
        EditorApplication.update += PollSetupFlag;
    }

    [MenuItem("Tools/Castlevania 2D/Setup Wooden Elevator Cabin")]
    public static void SetupFromMenu()
    {
        try
        {
            EditorUtility.DisplayDialog("Wooden Elevator", SetupWoodenElevatorCabin(), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Wooden Elevator", "Failed:\n" + exception.Message, "OK");
        }
    }

    public static void SetupFromBatch()
    {
        string summary = SetupWoodenElevatorCabin();
        Debug.Log("[WoodenElevatorCabinSetupEditor] " + summary);
    }

    private static void PollSetupFlag()
    {
        if (!File.Exists(SetupFlagPath)
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TrySetupFromFlag();
    }

    private static void TrySetupFromFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SetupFlagPath))
        {
            return;
        }

        File.Delete(SetupFlagPath);
        try
        {
            string summary = SetupWoodenElevatorCabin();
            Debug.Log("[WoodenElevatorCabinSetupEditor] " + summary);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_wooden_elevator_result.txt", "OK\n" + summary);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/setup_wooden_elevator_result.txt", "FAIL\n" + exception);
        }
    }

    public static string SetupWoodenElevatorCabin()
    {
        EnsureFolder("Assets/Art/Sprites/Environment");
        EnsureFolder(DestFolder);
        EnsureFolder(WinchFolder);
        EnsureFolder(PrefabFolder);
        CopySources();
        CopyWinchSources();
        CopyRopeSource();
        CopyGearBoxSource();

        Sprite cabinSprite = ImportSprite(CabinAsset);
        Sprite beamSprite = ImportSprite(BeamAsset);
        Sprite[] winchFrames = ImportWinchFrames();
        Sprite ropeSprite = ImportSprite(RopeAsset, SpriteMeshType.FullRect);
        Sprite gearBoxSprite = ImportSprite(GearBoxAsset);
        if (cabinSprite == null || beamSprite == null)
        {
            throw new InvalidOperationException(
                "Missing cabin sprites. Expected Лифт_0001.png and Лифт_0002.png in Desktop/Персонаж/Лифт.");
        }

        if (winchFrames == null || winchFrames.Length == 0)
        {
            throw new InvalidOperationException(
                "Missing winch frames. Expected PNGs in Desktop/Персонаж/Лифт/Катушка.");
        }

        bool created = !File.Exists(PrefabPath.Replace('/', Path.DirectorySeparatorChar));
        GameObject root = created
            ? new GameObject(RootName)
            : PrefabUtility.LoadPrefabContents(PrefabPath);
        if (created)
        {
            root.transform.position = FallbackPosition;
            root.transform.localScale = CabinScale;
        }

        try
        {
            ConfigureSpriteChild(root.transform, CabinName, cabinSprite, CabinSpriteOrder);
            ConfigureSpriteChild(root.transform, BeamName, beamSprite, BeamSortingOrder);
            ConfigureBoxChild(
                root.transform,
                FloorName,
                PixelBoxOffset(86f, 601f, 41f, 54f),
                PixelBoxSize(86f, 601f, 41f, 54f),
                isTrigger: false);
            ConfigureBoxChild(
                root.transform,
                LeftWallName,
                PixelBoxOffset(86f, 128f, 54f, 290f),
                PixelBoxSize(86f, 128f, 54f, 290f),
                isTrigger: false);
            ConfigureBoxChild(
                root.transform,
                CeilingName,
                PixelBoxOffset(86f, 601f, 278f, 296f),
                PixelBoxSize(86f, 601f, 278f, 296f),
                isTrigger: false);
            GameObject interior = ConfigureBoxChild(
                root.transform,
                InteriorName,
                PixelBoxOffset(128f, 560f, 54f, 278f),
                PixelBoxSize(128f, 560f, 54f, 278f),
                isTrigger: true);
            WireCabinLogic(root.transform, interior);
            ConfigureWinch(root.transform, winchFrames);
            ConfigureGearBox(root.transform, gearBoxSprite);
            ConfigureRope(root.transform, ropeSprite);
            WireElevator(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            return created
                ? $"Created prefab {PrefabPath}. Scene was not modified."
                : $"Updated prefab {PrefabPath}. Scene was not modified.";
        }
        finally
        {
            if (created)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            else
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void CopySources()
    {
        string sourceFolder = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            return;
        }

        CopySourceFile(sourceFolder, "0001", BeamAsset);
        CopySourceFile(sourceFolder, "0002", CabinAsset);
    }

    private static void CopySourceFile(string sourceFolder, string digits, string destAsset)
    {
        string[] files = Directory.GetFiles(sourceFolder, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            string stem = Path.GetFileNameWithoutExtension(files[i]);
            if (!stem.EndsWith(digits, StringComparison.Ordinal))
            {
                continue;
            }

            File.Copy(files[i], destAsset.Replace('/', Path.DirectorySeparatorChar), true);
            return;
        }
    }

    private static string ResolveSourceFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Лифт"),
            Path.Combine(userProfile, "OneDrive", "Рабочий стол", "Персонаж", "Лифт"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (Directory.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static Sprite ImportSprite(
        string assetPath,
        SpriteMeshType meshType = SpriteMeshType.Tight)
    {
        if (!File.Exists(assetPath.Replace('/', Path.DirectorySeparatorChar)))
        {
            return null;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = meshType;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void ConfigureSpriteChild(
        Transform parent,
        string childName,
        Sprite sprite,
        int sortingOrder)
    {
        Transform child = parent.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }

    private static GameObject ConfigureBoxChild(
        Transform parent,
        string childName,
        Vector2 offset,
        Vector2 size,
        bool isTrigger)
    {
        Transform child = parent.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
        }

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = go.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = isTrigger;
        box.offset = offset;
        box.size = size;
        return go;
    }

    private static void WireCabinLogic(Transform root, GameObject interior)
    {
        WoodenElevatorCabin2D logic = interior.GetComponent<WoodenElevatorCabin2D>();
        if (logic == null)
        {
            logic = interior.AddComponent<WoodenElevatorCabin2D>();
        }

        Transform cabin = root.Find(CabinName);
        Transform beam = root.Find(BeamName);
        SerializedObject serialized = new SerializedObject(logic);
        serialized.FindProperty("cabinRenderer").objectReferenceValue =
            cabin != null ? cabin.GetComponent<SpriteRenderer>() : null;
        serialized.FindProperty("beamRenderer").objectReferenceValue =
            beam != null ? beam.GetComponent<SpriteRenderer>() : null;
        serialized.FindProperty("cabinSortingOrder").intValue = CabinSpriteOrder;
        serialized.FindProperty("beamSortingOrder").intValue = BeamSortingOrder;
        serialized.FindProperty("playerInsideSortingOrder").intValue = 3;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyWinchSources()
    {
        string sourceFolder = ResolveWinchSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            return;
        }

        string[] files = Directory.GetFiles(sourceFolder, "*.png");
        System.Array.Sort(files);
        for (int i = 0; i < files.Length; i++)
        {
            string destAsset = $"{WinchFolder}/Winch_{i + 1:D3}.png";
            File.Copy(files[i], destAsset.Replace('/', Path.DirectorySeparatorChar), true);
        }
    }

    private static string ResolveWinchSourceFolder()
    {
        string liftRoot = ResolveSourceFolder();
        if (string.IsNullOrEmpty(liftRoot))
        {
            return null;
        }

        string nested = Path.Combine(liftRoot, "Катушка");
        return Directory.Exists(nested) ? nested : null;
    }

    private static Sprite[] ImportWinchFrames()
    {
        var frames = new System.Collections.Generic.List<Sprite>();
        for (int i = 1; i <= 9; i++)
        {
            Sprite sprite = ImportSprite($"{WinchFolder}/Winch_{i:D3}.png");
            if (sprite != null)
            {
                frames.Add(sprite);
            }
        }

        return frames.ToArray();
    }

    private static void CopyRopeSource()
    {
        string liftRoot = ResolveSourceFolder();
        if (string.IsNullOrEmpty(liftRoot))
        {
            return;
        }

        string sourcePath = Path.Combine(liftRoot, "Веревка_0010.png");
        if (!File.Exists(sourcePath))
        {
            string[] matches = Directory.GetFiles(liftRoot, "*.png");
            for (int i = 0; i < matches.Length; i++)
            {
                if (Path.GetFileNameWithoutExtension(matches[i]).EndsWith("0010", StringComparison.Ordinal))
                {
                    sourcePath = matches[i];
                    break;
                }
            }
        }

        if (!File.Exists(sourcePath))
        {
            return;
        }

        string destFs = RopeAsset.Replace('/', Path.DirectorySeparatorChar);
        File.Copy(sourcePath, destFs, true);
        CropPngToOpaque(destFs);
    }

    private static void CropPngToOpaque(string pngPath)
    {
        string python = "python";
        string arguments =
            "-c \"from PIL import Image; im=Image.open(r'''" + pngPath +
            "''').convert('RGBA'); b=im.getbbox(); im.crop(b).save(r'''" + pngPath + "''')\"";
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = python,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (IOException)
        {
        }
    }

    private static void ConfigureRope(Transform root, Sprite ropeSprite)
    {
        if (ropeSprite == null)
        {
            return;
        }

        Transform cabin = root.Find(CabinName);
        Transform winch = root.Find(WinchName);
        if (cabin == null || winch == null)
        {
            return;
        }

        Transform hook = cabin.Find(RopeHookName);
        GameObject hookGo = hook != null ? hook.gameObject : new GameObject(RopeHookName);
        if (hook == null)
        {
            hookGo.transform.SetParent(cabin, false);
            hookGo.transform.localPosition = new Vector3(0f, 3.12f, 0f);
            hookGo.transform.localScale = Vector3.one;
        }

        Transform rope = root.Find(RopeName);
        GameObject ropeGo = rope != null ? rope.gameObject : new GameObject(RopeName);
        if (rope == null)
        {
            ropeGo.transform.SetParent(root, false);
            ropeGo.transform.localPosition = Vector3.zero;
            ropeGo.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = ropeGo.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = ropeGo.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = ropeSprite;
        renderer.sortingOrder = CabinSpriteOrder;
        renderer.drawMode = SpriteDrawMode.Tiled;

        WoodenElevatorRope2D ropeLogic = ropeGo.GetComponent<WoodenElevatorRope2D>();
        if (ropeLogic == null)
        {
            ropeLogic = ropeGo.AddComponent<WoodenElevatorRope2D>();
        }

        SerializedObject serialized = new SerializedObject(ropeLogic);
        serialized.FindProperty("topAnchor").objectReferenceValue = winch;
        serialized.FindProperty("bottomAnchor").objectReferenceValue = hookGo.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ropeLogic.Stretch();
    }

    private static void CopyGearBoxSource()
    {
        string sourcePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "Короб.png");
        if (!File.Exists(sourcePath))
        {
            return;
        }

        File.Copy(sourcePath, GearBoxAsset.Replace('/', Path.DirectorySeparatorChar), true);
    }

    private static void ConfigureGearBox(Transform parent, Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Transform child = parent.Find(GearBoxName);
        bool created = child == null;
        GameObject go = created ? new GameObject(GearBoxName) : child.gameObject;
        if (created)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = GearBoxLocalPosition;
            go.transform.localScale = Vector3.one;
        }

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = GearBoxSortingOrder;
    }

    private static void ConfigureWinch(Transform parent, Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        Transform child = parent.Find(WinchName);
        bool created = child == null;
        GameObject go = created ? new GameObject(WinchName) : child.gameObject;
        if (created)
        {
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
        }

        if (created || Mathf.Abs(go.transform.localPosition.y - 3.55f) < 0.02f)
        {
            go.transform.localPosition = WinchLocalPosition;
        }

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = frames[0];
        renderer.sortingOrder = WinchSortingOrder;

        WoodenElevatorWinch2D winch = go.GetComponent<WoodenElevatorWinch2D>();
        if (winch == null)
        {
            winch = go.AddComponent<WoodenElevatorWinch2D>();
        }

        winch.EditorAssignFrames(frames, WinchFrameRate);
    }

    private static void WireElevator(Transform root)
    {
        WoodenElevator2D elevator = root.GetComponent<WoodenElevator2D>();
        if (elevator == null)
        {
            elevator = root.gameObject.AddComponent<WoodenElevator2D>();
        }

        Transform winchTransform = root.Find(WinchName);
        SerializedObject serialized = new SerializedObject(elevator);
        serialized.FindProperty("winch").objectReferenceValue =
            winchTransform != null ? winchTransform.GetComponent<WoodenElevatorWinch2D>() : null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector2 PixelBoxOffset(float minX, float maxX, float minYFromBottom, float maxYFromBottom)
    {
        float centerX = ((minX + maxX) * 0.5f / PixelsPerUnit) - (TextureWidth * 0.5f / PixelsPerUnit);
        float centerY = (minYFromBottom + maxYFromBottom) * 0.5f / PixelsPerUnit;
        return new Vector2(centerX, centerY);
    }

    private static Vector2 PixelBoxSize(float minX, float maxX, float minYFromBottom, float maxYFromBottom)
    {
        return new Vector2((maxX - minX) / PixelsPerUnit, (maxYFromBottom - minYFromBottom) / PixelsPerUnit);
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
