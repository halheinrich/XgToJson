using XgToJson;

namespace XgToJson.Tests;

/// <summary>
/// Exe-level integration coverage for <see cref="CliRunner.Run"/> — the CLI's
/// full contract: argument-count validation, the output-directory existence
/// guard, single-file vs directory dispatch, the current-directory default for
/// omitted <c>[outputDir]</c>, and the exit-code matrix (<c>0</c>/<c>1</c>/<c>2</c>).
/// Each case runs in-process against the real <c>ConvertXgToJson_Lib</c> wire
/// with captured stdout/stderr and an isolated temp sandbox, so the working
/// directory is injected rather than mutated process-globally (which would race
/// under xUnit's parallel execution). The success and batch cases are
/// fixture-agnostic: they pull from the shared corpus and no-op when it is
/// empty, mirroring <see cref="ConverterSmokeTests"/>.
/// </summary>
public class CliRunnerTests
{
    // ------------------------------------------------------------------ //
    //  Argument-count validation → exit 1 (usage)
    // ------------------------------------------------------------------ //

    [Fact]
    public void Run_NoArgs_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            var (exit, stdout, stderr) = Run([], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("Usage:", stderr);
            Assert.Empty(stdout);
        });
    }

    [Fact]
    public void Run_TooManyArgs_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            var (exit, _, stderr) = Run(["a", "b", "c"], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("Usage:", stderr);
        });
    }

    // ------------------------------------------------------------------ //
    //  Output-directory existence guard → exit 1 (usage)
    // ------------------------------------------------------------------ //

    [Fact]
    public void Run_OutputDirDoesNotExist_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            string missing = Path.Combine(sandbox, "no-such-dir");
            string input = Path.Combine(sandbox, "whatever.xg"); // never reached

            var (exit, _, stderr) = Run([input, missing], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("Output directory does not exist", stderr);
        });
    }

    [Fact]
    public void Run_OutputDirIsAFileNotADirectory_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            string fileAsOutputDir = Path.Combine(sandbox, "out.xgp");
            File.WriteAllText(fileAsOutputDir, "not a directory");
            string input = Path.Combine(sandbox, "whatever.xg"); // never reached

            var (exit, _, stderr) = Run([input, fileAsOutputDir], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("Output directory does not exist", stderr);
        });
    }

    // ------------------------------------------------------------------ //
    //  Input-path validation → exit 1 (usage)
    // ------------------------------------------------------------------ //

    [Fact]
    public void Run_InputPathNotFound_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            string missing = Path.Combine(sandbox, "ghost.xg");

            var (exit, _, stderr) = Run([missing], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("Input path not found", stderr);
        });
    }

    [Fact]
    public void Run_SingleFileNotXgFormat_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            string textFile = Path.Combine(sandbox, "notes.txt");
            File.WriteAllText(textFile, "plain text, not an XG file");

            var (exit, _, stderr) = Run([textFile], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("is not an XG-format file", stderr);
        });
    }

    [Fact]
    public void Run_DirectoryWithNoXgFiles_ReturnsUsageError()
    {
        InSandbox(sandbox =>
        {
            string inputDir = Path.Combine(sandbox, "input");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "readme.txt"), "no xg here");

            var (exit, _, stderr) = Run([inputDir], sandbox);

            Assert.Equal(1, exit);
            Assert.Contains("No XG-format files", stderr);
        });
    }

    // ------------------------------------------------------------------ //
    //  Successful conversion → exit 0
    // ------------------------------------------------------------------ //

    [Fact]
    public void Run_SingleRealFile_ExplicitOutputDir_WritesJsonThere()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // No corpus fixtures present — nothing to convert (tolerated).

        InSandbox(sandbox =>
        {
            string outputDir = Path.Combine(sandbox, "out");
            Directory.CreateDirectory(outputDir);
            string currentDirectory = Path.Combine(sandbox, "cwd");
            Directory.CreateDirectory(currentDirectory);

            var (exit, stdout, stderr) = Run([input, outputDir], currentDirectory);

            Assert.Equal(0, exit);
            Assert.Contains("Wrote", stdout);
            Assert.Empty(stderr);
            // Output lands in the supplied directory, not the working directory.
            Assert.Single(Directory.GetFiles(outputDir, "*.json"));
            Assert.Empty(Directory.GetFiles(currentDirectory, "*.json"));
        });
    }

    [Fact]
    public void Run_SingleRealFile_OmittedOutputDir_WritesJsonToCurrentDirectory()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // No corpus fixtures present — nothing to convert (tolerated).

        InSandbox(sandbox =>
        {
            // [outputDir] omitted → output must land in the injected working
            // directory (the CWD-default branch).
            var (exit, stdout, stderr) = Run([input], sandbox);

            Assert.Equal(0, exit);
            Assert.Contains("Wrote", stdout);
            Assert.Empty(stderr);
            Assert.Single(Directory.GetFiles(sandbox, "*.json"));
        });
    }

    [Fact]
    public void Run_DirectoryOfRealFiles_WritesOneJsonPerInput()
    {
        var sources = TestPaths.XgFormatFiles.Take(3).ToList();
        if (sources.Count == 0)
            return; // No corpus fixtures present — nothing to convert (tolerated).

        InSandbox(sandbox =>
        {
            string inputDir = Path.Combine(sandbox, "input");
            Directory.CreateDirectory(inputDir);
            foreach (string src in sources)
                File.Copy(src, Path.Combine(inputDir, Path.GetFileName(src)));
            string outputDir = Path.Combine(sandbox, "out");
            Directory.CreateDirectory(outputDir);

            var (exit, stdout, stderr) = Run([inputDir, outputDir], sandbox);

            Assert.Equal(0, exit);
            Assert.Empty(stderr);
            // One JSON output per input file (ResolveBatch guarantees unique names).
            Assert.Equal(sources.Count, Directory.GetFiles(outputDir, "*.json").Length);
            Assert.Equal(sources.Count, CountOccurrences(stdout, "Wrote"));
        });
    }

    // ------------------------------------------------------------------ //
    //  Conversion failure → exit 2
    // ------------------------------------------------------------------ //

    [Fact]
    public void Run_SingleMalformedXgFile_ReturnsConversionFailure()
    {
        InSandbox(sandbox =>
        {
            // A .xg extension passes the (extension-only) format guard but garbage
            // contents throw in ReadFile → the conversion catch → exit 2. Synthesized
            // inline so the test is hermetic and deterministic (no corpus dependency).
            string malformed = Path.Combine(sandbox, "corrupt.xg");
            File.WriteAllText(malformed, "this is not a valid XG file");

            var (exit, _, stderr) = Run([malformed], sandbox);

            // Exit 2 (not 1) proves it reached the conversion catch, not the format guard.
            Assert.Equal(2, exit);
            Assert.Contains("FAILED", stderr);
            Assert.DoesNotContain("is not an XG-format file", stderr);
        });
    }

    [Fact]
    public void Run_DirectoryWithGoodAndMalformedFiles_ReturnsFailureButWritesGoodOutputs()
    {
        string? good = TestPaths.XgFormatFiles.FirstOrDefault();
        if (good is null)
            return; // No corpus fixtures present — nothing to convert (tolerated).

        InSandbox(sandbox =>
        {
            string inputDir = Path.Combine(sandbox, "input");
            Directory.CreateDirectory(inputDir);
            File.Copy(good, Path.Combine(inputDir, Path.GetFileName(good)));
            File.WriteAllText(Path.Combine(inputDir, "corrupt.xg"), "not a valid XG file");
            string outputDir = Path.Combine(sandbox, "out");
            Directory.CreateDirectory(outputDir);

            var (exit, stdout, stderr) = Run([inputDir, outputDir], sandbox);

            // One file failed → exit 2, but the good file is still written
            // (directory mode collects per-file failures, never aborts the batch).
            Assert.Equal(2, exit);
            Assert.Contains("FAILED", stderr);
            Assert.Contains("Wrote", stdout);
            Assert.Single(Directory.GetFiles(outputDir, "*.json"));
        });
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Invokes <see cref="CliRunner.Run"/> with captured output streams and
    /// returns the exit code together with everything written to stdout/stderr.
    /// </summary>
    private static (int Exit, string Stdout, string Stderr) Run(string[] args, string currentDirectory)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        int exit = CliRunner.Run(args, outWriter, errWriter, currentDirectory);
        return (exit, outWriter.ToString(), errWriter.ToString());
    }

    /// <summary>
    /// Runs <paramref name="body"/> against a freshly created, uniquely named
    /// temp directory, deleting it afterwards (best effort), so each case is
    /// isolated from every other and from ambient state.
    /// </summary>
    private static void InSandbox(Action<string> body)
    {
        string sandbox = Path.Combine(
            Path.GetTempPath(), "XgToJson.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(sandbox);
        try
        {
            body(sandbox);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
