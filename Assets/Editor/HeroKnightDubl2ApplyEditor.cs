using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Applies Дубль2 frames into HeroKnight.png (keeps .meta GUID + 10×9 / 100×55 slices),
/// reimports, opens Prototype. Does not rewrite animation clips, Roll shrink, or Boss_Flower.
/// </summary>
public static class HeroKnightDubl2ApplyEditor
{
    private const string TexturePath = "Assets/Art/Sprites/Characters/Player/HeroKnight.png";
    private const string TextureGuid = "ae297123f091bbf4e8f1835eb98fb293";
    private const string PrototypeScene = "Assets/Scenes/Prototype.unity";
    private const string AssemblerRelative =
        @"Temp\heroknight_redraw\assemble_dubl2_heroknight_sheet.py";

    private const string Dubl2FramesDefault =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Дубль2";

    [MenuItem("Tools/Castlevania 2D/Apply Дубль2 HeroKnight")]
    public static void ApplyMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Apply;
            return;
        }

        Apply();
    }

    /// <summary>BatchMode entry: -executeMethod HeroKnightDubl2ApplyEditor.ApplyFromBatch</summary>
    public static void ApplyFromBatch()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorApplication.Exit(1);
        }
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assembler = Path.Combine(projectRoot, AssemblerRelative);
        if (!File.Exists(assembler))
        {
            throw new FileNotFoundException("Assembler script missing.", assembler);
        }

        if (!Directory.Exists(Dubl2FramesDefault))
        {
            throw new DirectoryNotFoundException(
                "Дубль2 folder not found: " + Dubl2FramesDefault);
        }

        RunPythonAssembler(assembler);

        string metaPath = TexturePath + ".meta";
        if (!File.Exists(Path.GetFullPath(metaPath)))
        {
            throw new FileNotFoundException("HeroKnight.png.meta missing — GUID would be lost.");
        }

        string guidOnDisk = AssetDatabase.AssetPathToGUID(TexturePath);
        if (!string.IsNullOrEmpty(guidOnDisk) && guidOnDisk != TextureGuid)
        {
            Debug.LogWarning(
                $"HeroKnight GUID is {guidOnDisk}, expected etalon {TextureGuid}. Keeping meta as-is.");
        }

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (File.Exists(Path.GetFullPath(PrototypeScene)))
        {
            EditorSceneManager.OpenScene(PrototypeScene);
        }

        Debug.Log(
            "Дубль2 HeroKnight applied: sheet replaced (meta/GUID preserved), " +
            "90 frames 10×9 @ 100×55, no Roll shrink. " +
            "Play Mode: move/attack/roll on Player_HeroKnight.");
    }

    private static void RunPythonAssembler(string assemblerPath)
    {
        string python = ResolvePython();
        string args = python.Equals("py", StringComparison.OrdinalIgnoreCase)
            ? $"-3 \"{assemblerPath}\""
            : $"\"{assemblerPath}\"";
        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(assemblerPath) ?? ""
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Python for Дубль2 assemble.");
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
                $"Дубль2 assemble failed (exit {process.ExitCode}): {stderr}\n{stdout}");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Debug.LogWarning(stderr.Trim());
        }
    }

    private static string ResolvePython()
    {
        string[] candidates =
        {
            "py",
            "python",
            @"C:\Users\Bensh\AppData\Local\Programs\Python\Python311\python.exe",
            @"C:\Users\Bensh\AppData\Local\Programs\Python\Python312\python.exe",
            @"C:\Python311\python.exe"
        };

        foreach (string candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = candidate == "py" ? "-3 -c \"import PIL\"" : "-c \"import PIL\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null)
                {
                    continue;
                }

                p.WaitForExit(8000);
                if (p.ExitCode == 0)
                {
                    return candidate == "py" ? "py" : candidate;
                }
            }
            catch
            {
                // try next
            }
        }

        throw new InvalidOperationException(
            "Python with Pillow (PIL) not found. Install pillow or run assemble_dubl2_heroknight_sheet.py first.");
    }
}
