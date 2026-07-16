using ConvertXgToJson_Lib;

namespace XgToJson;

/// <summary>
/// The CLI's behavioural core: argument validation, single-file vs directory
/// dispatch, the output-directory existence guard, the current-directory
/// default, and exit-code mapping. <see cref="Run"/> holds the entire
/// user-facing contract so it can be exercised in-process by the test suite;
/// <c>Program.cs</c> is a one-line shim that supplies the process-global
/// ambient dependencies (<see cref="Console"/> streams and the working
/// directory).
/// </summary>
/// <remarks>
/// The ambient dependencies are injected rather than read inline so the matrix
/// is deterministically testable: <c>currentDirectory</c> in
/// particular lets the CWD-default branch be verified against a temp directory
/// without mutating process-global <see cref="Directory.GetCurrentDirectory"/>
/// (which would race under xUnit's parallel test execution). Kept
/// <c>internal</c> — this exe has no library surface at all: the CLI itself is
/// the public contract, and every type behind it (<see cref="Converter"/> and
/// <see cref="OutputNaming"/>, the engine seams) is likewise <c>internal</c>,
/// reachable from the test assembly only through <c>InternalsVisibleTo</c>.
/// </remarks>
internal static class CliRunner
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 1;
    private const int ExitConversionFailure = 2;

    /// <summary>
    /// Runs the conversion CLI over <paramref name="args"/>, writing normal
    /// output to <paramref name="out"/> and diagnostics to
    /// <paramref name="error"/>, and returns the process exit code
    /// (<c>0</c> success · <c>1</c> usage/argument error · <c>2</c> conversion
    /// failure).
    /// </summary>
    /// <param name="args">The command-line arguments: <c>&lt;input&gt;</c> and an
    /// optional <c>[outputDir]</c>.</param>
    /// <param name="out">Destination for success lines (<c>Wrote …</c>).</param>
    /// <param name="error">Destination for usage text and failure diagnostics.</param>
    /// <param name="currentDirectory">The working directory used as the output
    /// directory when <c>[outputDir]</c> is omitted.</param>
    public static int Run(string[] args, TextWriter @out, TextWriter error, string currentDirectory)
    {
        if (args.Length is < 1 or > 2)
        {
            error.WriteLine(UsageText());
            return ExitUsage;
        }

        string inputPath = args[0];
        string? outputDirArg = args.Length == 2 ? args[1] : null;

        // A supplied output directory must already exist. We do not create it, so a
        // typo or a file path (e.g. "out.xgp") fails loudly rather than silently
        // spawning a folder of that name. Omitted → the current working directory,
        // which always exists. One check, shared by both modes.
        if (outputDirArg is not null && !Directory.Exists(outputDirArg))
        {
            error.WriteLine(
                $"Output directory does not exist or is not a directory: '{outputDirArg}'.");
            return ExitUsage;
        }
        string outputDir = outputDirArg ?? currentDirectory;

        // Directory input → batch mode: convert every XG-format file within it.
        if (Directory.Exists(inputPath))
        {
            DirectoryConversionResult result = Converter.ConvertDirectory(inputPath, outputDir);

            foreach (string written in result.Written)
                @out.WriteLine($"Wrote {written}");
            foreach (ConversionFailure failure in result.Failed)
                error.WriteLine($"FAILED {failure.InputPath}: {failure.Error}");

            if (result.Written.Count == 0 && result.Failed.Count == 0)
            {
                error.WriteLine(
                    $"No XG-format files ({AcceptedFormats()}) found in '{inputPath}'.");
                return ExitUsage;
            }

            return result.Failed.Count == 0 ? ExitSuccess : ExitConversionFailure;
        }

        // Single-file input.
        if (File.Exists(inputPath))
        {
            if (!XgFileReader.IsXgFormatFile(inputPath))
            {
                error.WriteLine(
                    $"'{inputPath}' is not an XG-format file (expected {AcceptedFormats()}).");
                return ExitUsage;
            }

            try
            {
                string outputPath = Converter.ConvertFile(inputPath, outputDir);
                @out.WriteLine($"Wrote {outputPath}");
                return ExitSuccess;
            }
            catch (Exception ex)
            {
                error.WriteLine($"FAILED {inputPath}: {ex.Message}");
                return ExitConversionFailure;
            }
        }

        error.WriteLine($"Input path not found: '{inputPath}'.");
        return ExitUsage;
    }

    // Accepted-format list, read from the producer's single source rather than
    // re-hardcoding ".xg/.xgp" here.
    private static string AcceptedFormats() => string.Join(", ", XgFileReader.XgFormatExtensions);

    private static string UsageText() =>
        $"""
        XgToJson — convert XG-format ({AcceptedFormats()}) files to JSON decision records.

        Usage:
          XgToJson <input> [outputDir]

          <input>     a {AcceptedFormats()} file, or a directory of them (top-level only).
          [outputDir] an existing directory to write .json output into;
                      defaults to the current directory.

        Exit codes: 0 = success, 1 = usage/argument error, 2 = conversion failure.
        """;
}
