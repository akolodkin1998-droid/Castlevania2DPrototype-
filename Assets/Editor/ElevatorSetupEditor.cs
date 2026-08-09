using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports elevator sprites and places LiftMechanism in Prototype.
/// Source: Desktop/Персонаж/Подъемник/Новая папка — Лифт_NNNN.png.
/// Stage mapping:
///   idle: Лифт_0001
///   hit1: Лифт_0001→0004 once, hold 0004
///   hit2: Лифт_0004→0008 once, hold 0008
///   hit3: Лифт_0009→0010 once, hold 0010
/// Basket zone: edit BoxCollider2D Offset/Size on MechanismFrame yourself — setup never overwrites it on existing lifts.
/// Frame alignment: custom sprite pivot (428/1280, 559/720) on aligned 1920×1080 canvases
/// (packed Лифт_* registered by shared content bottom-center so MechanismFrame does not hop).
/// Re-running setup on an existing LiftMechanism refreshes sprites/components only — transforms are preserved.
/// </summary>
public static class ElevatorSetupEditor
{
    private const string DestFolder = "Assets/Art/Sprites/Environment/Elevator";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string RootName = "LiftMechanism";
    private const string MechanismName = "MechanismFrame";
    private const string RopeName = "Rope";
    private const string ReferenceName = "AssemblyReference";

    private const string Frame01Asset = DestFolder + "/Elevator_Frame_01.png";
    private const string Frame02Asset = DestFolder + "/Elevator_Frame_02.png";
    private const string Frame03Asset = DestFolder + "/Elevator_Frame_03.png";
    private const string Frame04Asset = DestFolder + "/Elevator_Frame_04.png";
    private const string Frame05Asset = DestFolder + "/Elevator_Frame_05.png";
    private const string Frame06Asset = DestFolder + "/Elevator_Frame_06.png";
    private const string Frame07Asset = DestFolder + "/Elevator_Frame_07.png";
    private const string Frame08Asset = DestFolder + "/Elevator_Frame_08.png";
    private const string Frame09Asset = DestFolder + "/Elevator_Frame_09.png";
    private const string Frame10Asset = DestFolder + "/Elevator_Frame_10.png";
    private const string Frame14Asset = DestFolder + "/Elevator_Frame_14.png";
    private const string ReferenceAsset = DestFolder + "/Elevator_Assembly_Reference.png";
    private const string RopeAsset = DestFolder + "/Elevator_Rope.png";

    private const float PixelsPerUnit = 100f;
    private const float FrameWidthPixels = 1280f;
    private const float FrameHeightPixels = 720f;
    private const float AnchorPixelX = 428f;
    private const float AnchorPixelY = 559f;
    private const float LandingAnimFrameRate = 12f;
    private const float BasketLeftPixelX = 466f;
    private const float BasketRightPixelX = 545f;
    private const float BasketTopPixelY = 310f;
    private const float BasketTriggerWidthMultiplier = 1.5f;
    private const string AlignFramesScript = "Temp/elevator/align_frames.py";
    private const float MechanismVisualScale = 0.375f;
    private const float MechanismFrameLocalScale = 1.33584f;
    private const float AssemblyReferenceLocalScale = 1f;
    private static readonly Vector3 ScenePosition = new Vector3(8f, 0f, 0f);
    private static readonly Vector3 RopeLocalPosition = new Vector3(-1.35f, 3.05f, 0f);
    private static readonly Vector3 ReferenceLocalPosition = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 RopeLocalScale = new Vector3(1f, 4f, 1f);

    [MenuItem("Tools/Castlevania 2D/Setup Elevator Mechanism")]
    public static void SetupElevatorMenu()
    {
        try
        {
            string summary = SetupElevator();
            EditorUtility.DisplayDialog("Elevator Mechanism", summary, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Elevator Mechanism", "Failed:\n" + ex.Message, "OK");
        }
    }

    /// <summary>Batch: -executeMethod ElevatorSetupEditor.SetupFromBatch</summary>
    public static void SetupFromBatch()
    {
        string summary = SetupElevator();
        Debug.Log("[ElevatorSetupEditor] " + summary);
    }

    private static readonly string[] AllElevatorSpriteAssets =
    {
        Frame01Asset,
        Frame02Asset,
        Frame03Asset,
        Frame04Asset,
        Frame05Asset,
        Frame06Asset,
        Frame07Asset,
        Frame08Asset,
        Frame09Asset,
        Frame10Asset,
        Frame14Asset,
        ReferenceAsset,
        RopeAsset,
    };

    private static readonly string[] MechanismFrameAssets =
    {
        Frame01Asset,
        Frame02Asset,
        Frame03Asset,
        Frame04Asset,
        Frame05Asset,
        Frame06Asset,
        Frame07Asset,
        Frame08Asset,
        Frame09Asset,
        Frame10Asset,
        Frame14Asset,
    };

    private static string SetupElevator()
    {
        EnsureFolders();
        var changedAssets = new List<string>();
        string alignSummary = RunAlignFramesScript(changedAssets);
        ReimportAssets(changedAssets);

        Sprite[] frames = new Sprite[MechanismFrameAssets.Length];
        for (int i = 0; i < MechanismFrameAssets.Length; i++)
        {
            frames[i] = ImportSprite(MechanismFrameAssets[i], useMechanismAnchorPivot: true);
        }

        Sprite frame01 = frames[0];
        Sprite frame02 = frames[1];
        Sprite frame03 = frames[2];
        Sprite frame04 = frames[3];
        Sprite frame05 = frames[4];
        Sprite frame06 = frames[5];
        Sprite frame07 = frames[6];
        Sprite frame08 = frames[7];
        Sprite frame09 = frames[8];
        Sprite frame10 = frames[9];
        Sprite frame14 = frames[10];
        Sprite hold10 = frame10 != null ? frame10 : frame14;

        Sprite referenceSprite = ImportSprite(ReferenceAsset, useMechanismAnchorPivot: false);
        Sprite ropeSprite = ImportSprite(RopeAsset, useMechanismAnchorPivot: false);

        if (frame01 == null || frame04 == null || frame08 == null || frame09 == null || hold10 == null)
        {
            throw new InvalidOperationException(
                "Missing required elevator frames (need 01, 04, 08, 09, 10) in " + DestFolder);
        }

        if (frame02 == null || frame03 == null || frame05 == null || frame06 == null || frame07 == null)
        {
            throw new InvalidOperationException(
                "Missing intermediate elevator frames (02–03, 05–07) in " + DestFolder +
                ". Re-run align from Desktop Лифт_0001…0008.");
        }

        bool updatedExisting = PlaceInPrototypeScene(
            frame01,
            frame02,
            frame03,
            frame04,
            frame05,
            frame06,
            frame07,
            frame08,
            frame09,
            hold10,
            referenceSprite,
            ropeSprite);

        return
            $"Sprites: {DestFolder}\n" +
            alignSummary + "\n" +
            $"Alignment anchor (1280×720): ({AnchorPixelX}, {AnchorPixelY})\n" +
            "Basket trigger: edit BoxCollider2D Offset/Size on MechanismFrame (not overwritten by setup).\n" +
            (updatedExisting
                ? $"Updated existing {RootName} — scene transforms + basket collider preserved.\n"
                : $"Created {RootName} at placeholder {ScenePosition}, scale {MechanismVisualScale} (move in scene as needed).\n") +
            "Landings: idle 0001; hit1 0001→0004; hit2 0004→0008; hit3 0009→0010.\n" +
            "Anim arrays: landing1=4, landing2=5, landing3=2 frames @ 12 FPS.";
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Art/Sprites/Environment");
        EnsureFolder(DestFolder);
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
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void CopyAssemblySources(ICollection<string> changedAssets)
    {
        CopyFromSource("Screenshot_4-Photoroom.png", ReferenceAsset, resolveFromFramesFolder: false, changedAssets);
        CopyFromSource("Вепевка-Photoroom.png", RopeAsset, resolveFromFramesFolder: false, changedAssets);
    }

    private static string RunAlignFramesScript(ICollection<string> changedAssets)
    {
        CopyAssemblySources(changedAssets);

        if (HasNumberedFrameSources())
        {
            CopyMechanismFrameSources(changedAssets);
            return "Copied numbered mechanism frames 1–4.png (no align rewrite).";
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string scriptPath = Path.Combine(projectRoot, AlignFramesScript.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(scriptPath))
        {
            CopyMechanismFrameSources(changedAssets);
            return "Align script missing — copied raw mechanism frames when sources exist.";
        }

        string python = ResolvePythonExecutable();
        if (string.IsNullOrEmpty(python))
        {
            CopyMechanismFrameSources(changedAssets);
            return "Python not found — copied raw mechanism frames when sources exist.";
        }

        string sourceDir = ResolveFramesSourceDirectory();
        string arguments = $"\"{scriptPath}\" --dest-only";
        if (!string.IsNullOrEmpty(sourceDir))
        {
            arguments += $" --source \"{sourceDir}\"";
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = python,
            Arguments = arguments,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            CopyMechanismFrameSources(changedAssets);
            return "Failed to start align_frames.py — copied raw mechanism frames when sources exist.";
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Debug.Log("[ElevatorSetupEditor] align_frames.py:\n" + stdout.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Debug.LogWarning("[ElevatorSetupEditor] align_frames.py stderr:\n" + stderr.Trim());
        }

        if (process.ExitCode == 0)
        {
            for (int i = 0; i < AllElevatorSpriteAssets.Length; i++)
            {
                string frameAsset = AllElevatorSpriteAssets[i];
                if (File.Exists(frameAsset.Replace('/', Path.DirectorySeparatorChar)))
                {
                    changedAssets.Add(frameAsset);
                }
            }

            return "Aligned mechanism frames via Temp/elevator/align_frames.py (dest overwritten).";
        }

        CopyMechanismFrameSources(changedAssets);
        return $"align_frames.py exited with code {process.ExitCode}; copied raw mechanism frames when sources exist.";
    }

    private static string ResolvePythonExecutable()
    {
        string[] candidates = { "python", "python3", "py" };

        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = candidates[i],
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    continue;
                }

                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return candidates[i];
                }
            }
            catch (IOException)
            {
            }
        }

        return null;
    }

    private static string ResolveFramesSourceDirectory()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] candidates =
        {
            Path.Combine(desktop, "Персонаж", "Подъемник", "Новая папка"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Подъемник", "Новая папка"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Подъемник\Новая папка",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (Directory.Exists(candidates[i]) && Directory.EnumerateFiles(candidates[i], "*.png").Any())
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static readonly (string[] SourceCandidates, string DestAsset)[] MechanismFrameSourceMap =
    {
        (new[] { "Лифт_0001.png", "1.png", "1-Photoroom-Photoroom.png", "1-Photoroom.png" }, Frame01Asset),
        (new[] { "Лифт_0002.png" }, Frame02Asset),
        (new[] { "Лифт_0003.png" }, Frame03Asset),
        (new[] { "Лифт_0004.png" }, Frame04Asset),
        (new[] { "Лифт_0005.png" }, Frame05Asset),
        (new[] { "Лифт_0006.png" }, Frame06Asset),
        (new[] { "Лифт_0007.png" }, Frame07Asset),
        (new[] { "Лифт_0008.png" }, Frame08Asset),
        (new[] { "Лифт_0009.png", "2.png", "9-Photoroom (2)-Photoroom (1).png", "9-Photoroom (2).png", "9-Photoroom.png" }, Frame09Asset),
        (new[] { "Лифт_0010.png", "3.png", "10-Photoroom-Photoroom.png", "10-Photoroom.png" }, Frame10Asset),
        (new[] { "Лифт_0010.png", "4.png", "14-Photoroom-Photoroom.png", "14-Photoroom.png" }, Frame14Asset),
    };

    private static void CopyMechanismFrameSources(ICollection<string> changedAssets)
    {
        for (int i = 0; i < MechanismFrameSourceMap.Length; i++)
        {
            (string[] sourceCandidates, string destAsset) = MechanismFrameSourceMap[i];
            for (int j = 0; j < sourceCandidates.Length; j++)
            {
                if (CopyFromSource(sourceCandidates[j], destAsset, resolveFromFramesFolder: true, changedAssets))
                {
                    break;
                }
            }
        }
    }

    private static bool HasNumberedFrameSources()
    {
        string framesDir = ResolveFramesSourceDirectory();
        if (string.IsNullOrEmpty(framesDir) || !Directory.Exists(framesDir))
        {
            return false;
        }

        // Лифт_* needs align/upscale onto the shared canvas — do not short-circuit to raw copy.
        if (Directory.EnumerateFiles(framesDir, "*.png")
            .Any(path => Path.GetFileName(path).IndexOf("Лифт_", StringComparison.OrdinalIgnoreCase) >= 0
                         || Path.GetFileName(path).StartsWith("Lift_", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return File.Exists(Path.Combine(framesDir, "1.png"))
               && File.Exists(Path.Combine(framesDir, "2.png"))
               && File.Exists(Path.Combine(framesDir, "3.png"))
               && File.Exists(Path.Combine(framesDir, "4.png"));
    }

    private static bool CopyFromSource(
        string sourceFileName,
        string destAssetPath,
        bool resolveFromFramesFolder,
        ICollection<string> changedAssets)
    {
        string sourcePath = resolveFromFramesFolder
            ? ResolveSourceFile(sourceFileName, "Новая папка")
            : ResolveSourceFile(sourceFileName, null);

        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return false;
        }

        string destFs = destAssetPath.Replace('/', Path.DirectorySeparatorChar);
        Directory.CreateDirectory(Path.GetDirectoryName(destFs) ?? DestFolder);
        File.Copy(sourcePath, destFs, overwrite: true);
        changedAssets.Add(destAssetPath);
        return true;
    }

    private static void ReimportAssets(IEnumerable<string> assetPaths)
    {
        foreach (string assetPath in assetPaths.Distinct())
        {
            if (!File.Exists(assetPath.Replace('/', Path.DirectorySeparatorChar)))
            {
                continue;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }

    private static string ResolveSourceFile(string fileName, string subFolder)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] roots =
        {
            Path.Combine(desktop, "Персонаж", "Подъемник"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Рабочий стол", "Персонаж", "Подъемник"),
            @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Подъемник",
        };

        for (int i = 0; i < roots.Length; i++)
        {
            if (!Directory.Exists(roots[i]))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(subFolder))
            {
                string nestedDir = Path.Combine(roots[i], subFolder);
                string nested = Path.Combine(nestedDir, fileName);
                if (File.Exists(nested))
                {
                    return nested;
                }

                string digitMatch = FindSourceByTrailingDigits(nestedDir, fileName);
                if (!string.IsNullOrEmpty(digitMatch))
                {
                    return digitMatch;
                }
            }

            string direct = Path.Combine(roots[i], fileName);
            if (File.Exists(direct))
            {
                return direct;
            }

            string rootDigitMatch = FindSourceByTrailingDigits(roots[i], fileName);
            if (!string.IsNullOrEmpty(rootDigitMatch))
            {
                return rootDigitMatch;
            }
        }

        return null;
    }

    private static string FindSourceByTrailingDigits(string directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        string stem = Path.GetFileNameWithoutExtension(fileName);
        int digitStart = -1;
        for (int i = stem.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(stem[i]))
            {
                digitStart = i + 1;
                break;
            }

            if (i == 0)
            {
                digitStart = 0;
            }
        }

        if (digitStart < 0 || digitStart >= stem.Length)
        {
            return null;
        }

        string digits = stem.Substring(digitStart);
        if (digits.Length < 3)
        {
            return null;
        }

        foreach (string path in Directory.EnumerateFiles(directory, "*.png"))
        {
            string candidateStem = Path.GetFileNameWithoutExtension(path);
            if (candidateStem.EndsWith(digits, StringComparison.Ordinal)
                && (candidateStem.IndexOf("Лифт", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateStem.IndexOf("Lift", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateStem.EndsWith("_" + digits, StringComparison.Ordinal)))
            {
                return path;
            }
        }

        return null;
    }

    private static Sprite ImportSprite(string assetPath, bool useMechanismAnchorPivot)
    {
        if (!File.Exists(assetPath.Replace('/', Path.DirectorySeparatorChar)))
        {
            return null;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(assetPath, useMechanismAnchorPivot);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void ConfigureImporter(string destPath, bool useMechanismAnchorPivot)
    {
        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        Vector2 pivot;
        SpriteAlignment alignment;
        if (useMechanismAnchorPivot)
        {
            pivot = new Vector2(AnchorPixelX / FrameWidthPixels, AnchorPixelY / FrameHeightPixels);
            alignment = SpriteAlignment.Custom;
        }
        else
        {
            pivot = new Vector2(0.5f, 0f);
            alignment = SpriteAlignment.BottomCenter;
        }

        importer.spritePivot = pivot;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)alignment;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void ComputeBasketTrigger(out Vector2 offset, out Vector2 size)
    {
        float centerPixelX = (BasketLeftPixelX + BasketRightPixelX) * 0.5f;
        float widthPixels = BasketRightPixelX - BasketLeftPixelX;

        offset = new Vector2(
            (centerPixelX - AnchorPixelX) / PixelsPerUnit,
            (BasketTopPixelY - AnchorPixelY) / PixelsPerUnit);
        size = new Vector2(widthPixels / PixelsPerUnit * BasketTriggerWidthMultiplier, 0.35f);
    }

    private readonly struct LocalTransformSnapshot
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public LocalTransformSnapshot(Transform transform)
        {
            Position = transform.localPosition;
            Rotation = transform.localRotation;
            Scale = transform.localScale;
        }

        public void Apply(Transform transform)
        {
            transform.localPosition = Position;
            transform.localRotation = Rotation;
            transform.localScale = Scale;
        }
    }

    private static bool PlaceInPrototypeScene(
        Sprite frame01,
        Sprite frame02,
        Sprite frame03,
        Sprite frame04,
        Sprite frame05,
        Sprite frame06,
        Sprite frame07,
        Sprite frame08,
        Sprite frame09,
        Sprite frame10,
        Sprite referenceSprite,
        Sprite ropeSprite)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            UpdateExistingLiftMechanism(
                existingRoot,
                frame01, frame02, frame03, frame04,
                frame05, frame06, frame07, frame08,
                frame09, frame10,
                referenceSprite,
                ropeSprite);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        CreateNewLiftMechanism(
            frame01, frame02, frame03, frame04,
            frame05, frame06, frame07, frame08,
            frame09, frame10,
            referenceSprite,
            ropeSprite);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return false;
    }

    private static void UpdateExistingLiftMechanism(
        GameObject root,
        Sprite frame01,
        Sprite frame02,
        Sprite frame03,
        Sprite frame04,
        Sprite frame05,
        Sprite frame06,
        Sprite frame07,
        Sprite frame08,
        Sprite frame09,
        Sprite frame10,
        Sprite referenceSprite,
        Sprite ropeSprite)
    {
        LocalTransformSnapshot rootTransform = new LocalTransformSnapshot(root.transform);

        Transform mechanismTransform = root.transform.Find(MechanismName);
        LocalTransformSnapshot? mechanismTransformSnapshot = mechanismTransform != null
            ? new LocalTransformSnapshot(mechanismTransform)
            : (LocalTransformSnapshot?)null;

        Transform ropeTransform = root.transform.Find(RopeName);
        LocalTransformSnapshot? ropeTransformSnapshot = ropeTransform != null
            ? new LocalTransformSnapshot(ropeTransform)
            : (LocalTransformSnapshot?)null;

        Transform referenceTransform = root.transform.Find(ReferenceName);
        LocalTransformSnapshot? referenceTransformSnapshot = referenceTransform != null
            ? new LocalTransformSnapshot(referenceTransform)
            : (LocalTransformSnapshot?)null;

        GameObject mechanism = mechanismTransform != null
            ? mechanismTransform.gameObject
            : CreateMechanismFrame(root.transform, isNew: true);
        // Preserve authored BoxCollider2D Offset/Size on existing MechanismFrame.
        ConfigureMechanismFrame(
            mechanism,
            frame01, frame02, frame03, frame04,
            frame05, frame06, frame07, frame08,
            frame09, frame10,
            applyDefaultBasketCollider: false);

        if (mechanismTransformSnapshot.HasValue)
        {
            mechanismTransformSnapshot.Value.Apply(mechanism.transform);
        }

        rootTransform.Apply(root.transform);

        if (ropeSprite != null)
        {
            GameObject rope = ropeTransform != null
                ? ropeTransform.gameObject
                : CreateRope(root.transform, isNew: true);
            ConfigureRope(rope, ropeSprite);

            if (ropeTransformSnapshot.HasValue)
            {
                ropeTransformSnapshot.Value.Apply(rope.transform);
            }
        }

        if (referenceSprite != null)
        {
            GameObject reference = referenceTransform != null
                ? referenceTransform.gameObject
                : CreateAssemblyReference(root.transform, isNew: true);
            ConfigureAssemblyReference(reference, referenceSprite);

            if (referenceTransformSnapshot.HasValue)
            {
                referenceTransformSnapshot.Value.Apply(reference.transform);
            }
        }
    }

    private static void CreateNewLiftMechanism(
        Sprite frame01,
        Sprite frame02,
        Sprite frame03,
        Sprite frame04,
        Sprite frame05,
        Sprite frame06,
        Sprite frame07,
        Sprite frame08,
        Sprite frame09,
        Sprite frame10,
        Sprite referenceSprite,
        Sprite ropeSprite)
    {
        GameObject root = new GameObject(RootName);
        root.transform.position = ScenePosition;
        root.transform.localScale = new Vector3(MechanismVisualScale, MechanismVisualScale, 1f);

        GameObject mechanism = CreateMechanismFrame(root.transform, isNew: true);
        ConfigureMechanismFrame(
            mechanism,
            frame01, frame02, frame03, frame04,
            frame05, frame06, frame07, frame08,
            frame09, frame10,
            applyDefaultBasketCollider: true);

        if (ropeSprite != null)
        {
            GameObject rope = CreateRope(root.transform, isNew: true);
            ConfigureRope(rope, ropeSprite);
        }

        if (referenceSprite != null)
        {
            GameObject reference = CreateAssemblyReference(root.transform, isNew: true);
            ConfigureAssemblyReference(reference, referenceSprite);
        }
    }

    private static GameObject CreateMechanismFrame(Transform parent, bool isNew)
    {
        GameObject mechanism = new GameObject(MechanismName);
        mechanism.transform.SetParent(parent, false);
        mechanism.transform.localPosition = Vector3.zero;
        if (isNew)
        {
            mechanism.transform.localScale = new Vector3(
                MechanismFrameLocalScale,
                MechanismFrameLocalScale,
                1f);
        }

        return mechanism;
    }

    private static void ConfigureMechanismFrame(
        GameObject mechanism,
        Sprite frame01,
        Sprite frame02,
        Sprite frame03,
        Sprite frame04,
        Sprite frame05,
        Sprite frame06,
        Sprite frame07,
        Sprite frame08,
        Sprite frame09,
        Sprite frame10,
        bool applyDefaultBasketCollider)
    {
        var mechanismRenderer = mechanism.GetComponent<SpriteRenderer>();
        if (mechanismRenderer == null)
        {
            mechanismRenderer = mechanism.AddComponent<SpriteRenderer>();
        }

        mechanismRenderer.sprite = frame01;
        mechanismRenderer.sortingOrder = 1;

        var mechanismLogic = mechanism.GetComponent<ElevatorMechanism2D>();
        if (mechanismLogic == null)
        {
            mechanismLogic = mechanism.AddComponent<ElevatorMechanism2D>();
        }

        var box = mechanism.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = mechanism.AddComponent<BoxCollider2D>();
            applyDefaultBasketCollider = true;
        }

        box.isTrigger = true;
        if (applyDefaultBasketCollider)
        {
            Vector2 basketOffset;
            Vector2 basketSize;
            ComputeBasketTrigger(out basketOffset, out basketSize);
            box.offset = basketOffset;
            box.size = new Vector2(Mathf.Abs(basketSize.x), Mathf.Abs(basketSize.y));
        }

        SerializedObject mechanismSo = new SerializedObject(mechanismLogic);
        SerializedProperty frameSpritesProp = RequireProperty(mechanismSo, "frameSprites");
        // Hold sprites after each stage completes: 0001 / 0004 / 0008 / 0010.
        frameSpritesProp.arraySize = 4;
        frameSpritesProp.GetArrayElementAtIndex(0).objectReferenceValue = frame01;
        frameSpritesProp.GetArrayElementAtIndex(1).objectReferenceValue = frame04;
        frameSpritesProp.GetArrayElementAtIndex(2).objectReferenceValue = frame08;
        frameSpritesProp.GetArrayElementAtIndex(3).objectReferenceValue = frame10;

        Sprite[] hit1Anim = { frame01, frame02, frame03, frame04 };
        Sprite[] hit2Anim = { frame04, frame05, frame06, frame07, frame08 };
        Sprite[] hit3Anim = { frame09, frame10 };
        AssignAnimArray(RequireProperty(mechanismSo, "landing1AnimFrames"), hit1Anim);
        AssignAnimArray(RequireProperty(mechanismSo, "landing2AnimFrames"), hit2Anim);
        AssignAnimArray(RequireProperty(mechanismSo, "landing3AnimFrames"), hit3Anim);
        RequireProperty(mechanismSo, "landingAnimFrameRate").floatValue = LandingAnimFrameRate;

        Transform liftRoot = mechanism.transform.parent;
        if (liftRoot != null)
        {
            RequireProperty(mechanismSo, "ropeTransform").objectReferenceValue = liftRoot.Find(RopeName);
            RequireProperty(mechanismSo, "assemblyReferenceTransform").objectReferenceValue =
                liftRoot.Find(ReferenceName);
        }

        RequireProperty(mechanismSo, "targetAssemblyWorldY").floatValue = -38.5f;
        RequireProperty(mechanismSo, "descentStepWorldY").floatValue = 2f;
        mechanismSo.ApplyModifiedProperties();

        // Dual-path: also set via public API so arrays cannot stay empty if SO apply is skipped.
        mechanismLogic.ConfigureLandingAnimations(hit1Anim, hit2Anim, hit3Anim, LandingAnimFrameRate);
        EditorUtility.SetDirty(mechanismLogic);
        EditorUtility.SetDirty(mechanism);
    }

    private static SerializedProperty RequireProperty(SerializedObject so, string propertyName)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"ElevatorMechanism2D missing serialized field '{propertyName}'. Recompile scripts and re-run Setup.");
        }

        return property;
    }

    private static void AssignAnimArray(SerializedProperty arrayProperty, params Sprite[] frames)
    {
        if (arrayProperty == null)
        {
            throw new InvalidOperationException("landing*AnimFrames property is null — cannot wire hit animations.");
        }

        arrayProperty.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"Landing anim frame[{i}] is null while wiring '{arrayProperty.propertyPath}'.");
            }

            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }

    private static GameObject CreateRope(Transform parent, bool isNew)
    {
        GameObject rope = new GameObject(RopeName);
        rope.transform.SetParent(parent, false);
        rope.transform.localPosition = RopeLocalPosition;
        if (isNew)
        {
            rope.transform.localScale = RopeLocalScale;
        }

        return rope;
    }

    private static void ConfigureRope(GameObject rope, Sprite ropeSprite)
    {
        var ropeRenderer = rope.GetComponent<SpriteRenderer>();
        if (ropeRenderer == null)
        {
            ropeRenderer = rope.AddComponent<SpriteRenderer>();
        }

        ropeRenderer.sprite = ropeSprite;
        ropeRenderer.sortingOrder = 2;
    }

    private static GameObject CreateAssemblyReference(Transform parent, bool isNew)
    {
        GameObject reference = new GameObject(ReferenceName);
        reference.transform.SetParent(parent, false);
        reference.transform.localPosition = ReferenceLocalPosition;
        if (isNew)
        {
            reference.transform.localScale = new Vector3(
                AssemblyReferenceLocalScale,
                AssemblyReferenceLocalScale,
                1f);
        }

        return reference;
    }

    private static void ConfigureAssemblyReference(GameObject reference, Sprite referenceSprite)
    {
        var referenceRenderer = reference.GetComponent<SpriteRenderer>();
        if (referenceRenderer == null)
        {
            referenceRenderer = reference.AddComponent<SpriteRenderer>();
        }

        referenceRenderer.sprite = referenceSprite;
        referenceRenderer.sortingOrder = 0;
        referenceRenderer.color = new Color(1f, 1f, 1f, 1f);

        var platform = reference.GetComponent<DecorOneWayPlatform2D>();
        if (platform == null)
        {
            platform = reference.AddComponent<DecorOneWayPlatform2D>();
        }

        platform.Apply();
    }
}
