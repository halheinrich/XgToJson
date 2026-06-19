using System.Diagnostics;

namespace XgToJson.Tests;

/// <summary>
/// Gating smoke over the <em>actual shipped binary</em>: launches the built
/// <c>XgToJson</c> executable as a child process against a real corpus file and
/// asserts it wires the command line through to a <c>0</c> exit with a JSON file
/// on disk. <see cref="CliRunnerTests"/> already exercises the full contract
/// in-process; this one additionally pins that <c>Program.cs</c>'s one-line
/// delegation — args, the <see cref="Console"/> streams, and
/// <see cref="Directory.GetCurrentDirectory"/> — actually reaches
/// <see cref="XgToJson.CliRunner.Run"/> in the real process. The binary and its
/// runtime config are copied into the test output directory by the project
/// reference, so it is located via <see cref="AppContext.BaseDirectory"/>.
/// </summary>
public class CliBinarySmokeTests
{
    [Fact]
    public void Binary_SingleRealFile_OmittedOutputDir_ExitsZeroAndWritesJsonToWorkingDirectory()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // No corpus fixtures present — nothing to convert (tolerated).

        string workingDir = Path.Combine(
            Path.GetTempPath(), "XgToJson.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(workingDir);
        try
        {
            // [outputDir] omitted → the shipped binary must default output to its
            // own working directory (Directory.GetCurrentDirectory()), which we
            // set to an isolated temp dir.
            var psi = BuildStartInfo(workingDir);
            psi.ArgumentList.Add(input);

            using var process = Process.Start(psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(30_000), "binary did not exit within 30s");

            Assert.Equal(0, process.ExitCode);
            Assert.Contains("Wrote", stdout);
            Assert.True(string.IsNullOrEmpty(stderr), $"expected no stderr, got: {stderr}");
            Assert.Single(Directory.GetFiles(workingDir, "*.json"));
        }
        finally
        {
            try { Directory.Delete(workingDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Builds a <see cref="ProcessStartInfo"/> that launches the built XgToJson
    /// binary from the test output directory with <paramref name="workingDir"/>
    /// as its working directory. Prefers the native <c>XgToJson.exe</c> apphost
    /// when present (Windows), falling back to <c>dotnet XgToJson.dll</c>.
    /// </summary>
    private static ProcessStartInfo BuildStartInfo(string workingDir)
    {
        string baseDir = AppContext.BaseDirectory;
        string exe = Path.Combine(baseDir, "XgToJson.exe");
        string dll = Path.Combine(baseDir, "XgToJson.dll");

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (File.Exists(exe))
        {
            psi.FileName = exe;
        }
        else
        {
            psi.FileName = "dotnet";
            psi.ArgumentList.Add(dll);
        }
        return psi;
    }
}
