using System.Diagnostics;

namespace XgToJson.Tests;

/// <summary>
/// Smoke over the <em>actual shipped binary</em>: launches the built
/// <c>XgToJson</c> executable as a child process on one <c>.xg</c> file and
/// asserts it wires the command line through to a <c>0</c> exit with a JSON file
/// on disk. <see cref="CliRunnerTests"/> already exercises the full contract
/// in-process; this one additionally pins that <c>Program.cs</c>'s one-line
/// delegation — args, the <see cref="Console"/> streams, and
/// <see cref="Directory.GetCurrentDirectory"/> — actually reaches
/// <see cref="XgToJson.CliRunner.Run"/> in the real process. The binary and its
/// runtime config are copied into the test output directory by the project
/// reference, so it is located via <see cref="AppContext.BaseDirectory"/>.
///
/// <para>
/// <b>The gating case</b> runs the binary on a match synthesized at test time
/// (<see cref="SyntheticXgMatch"/>), so it asserts on every checkout.
/// <b>The real-file case</b> is a local-only extra: it runs the same assertions
/// on the first file of the umbrella's gitignored <c>TestData/</c> corpus —
/// an XG-authored file, which a synthesized match cannot stand in for — and is
/// vacuous on an empty corpus by design, so it gates nothing.
/// </para>
/// </summary>
public class CliBinarySmokeTests
{
    [Fact]
    public void Binary_SynthesizedMatch_OmittedOutputDir_ExitsZeroAndWritesJsonToWorkingDirectory()
    {
        AssertBinaryWritesJsonToWorkingDirectory(sandbox =>
        {
            string inputDir = Path.Combine(sandbox, "input");
            Directory.CreateDirectory(inputDir);
            return SyntheticXgMatch.WriteOne(inputDir);
        });
    }

    [Fact]
    public void Binary_RealCorpusFile_OmittedOutputDir_ExitsZeroAndWritesJsonToWorkingDirectory()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // Local-only: vacuous on an empty corpus by design (see the class doc).

        AssertBinaryWritesJsonToWorkingDirectory(_ => input);
    }

    /// <summary>
    /// Runs the built binary with one argument — the path
    /// <paramref name="stageInput"/> returns, given a fresh temp sandbox — and
    /// asserts a <c>0</c> exit, a "Wrote" line, no stderr, and exactly one JSON
    /// file in the binary's working directory (a subdirectory of the sandbox
    /// holding nothing else), deleting the sandbox afterwards (best effort).
    /// </summary>
    private static void AssertBinaryWritesJsonToWorkingDirectory(Func<string, string> stageInput)
    {
        string sandbox = Path.Combine(
            Path.GetTempPath(), "XgToJson.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(sandbox);
        try
        {
            string input = stageInput(sandbox);
            string workingDir = Path.Combine(sandbox, "cwd");
            Directory.CreateDirectory(workingDir);

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
            try { Directory.Delete(sandbox, recursive: true); }
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
