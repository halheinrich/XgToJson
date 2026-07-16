using System.Text.Json;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgToJson;

/// <summary>
/// The conversion engine: reads an XG-format file, extracts every decision
/// (checker <em>and</em> cube) as <see cref="BgDecisionData"/>, and writes them
/// as a JSON array. No filtering — every analysed decision is emitted; the
/// downstream consumer applies its own thresholds.
/// </summary>
internal static class Converter
{
    /// <summary>
    /// The single source of truth for XgToJson's output JSON format. Bare
    /// options plus <see cref="JsonSerializerOptions.WriteIndented"/>: every
    /// converter <see cref="BgDecisionData"/> needs (<c>DecisionId</c>,
    /// <c>CubeOwner</c>, <c>Play</c>, enums) is a type-level
    /// <c>[JsonConverter]</c> in <c>BgDataTypes_Lib</c>, so no option-level
    /// converter registration is required — registering one here would mask a
    /// dropped type attribute. <c>WriteIndented</c> is this exe's own
    /// presentation choice (human-readable output), not a producer concern.
    /// Both <see cref="ConvertFile"/> and the round-trip smoke test consume
    /// this instance, so the test verifies the engine's actual format rather
    /// than a parallel copy. Frozen at initialization: sharing one mutable
    /// instance would let any consumer (a test, a future caller) flip an option
    /// and silently redefine the format for everyone.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        // populateMissingResolver: true installs the default reflection-based
        // resolver that Serialize would otherwise attach on first use; the
        // parameterless overload throws when TypeInfoResolver is still null.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>
    /// Converts one XG-format file to a JSON file written into
    /// <paramref name="outputDir"/> under its base <see cref="OutputNaming"/>
    /// name (single-file mode has no batch, so no collision disambiguation).
    /// </summary>
    /// <param name="inputPath">Path to a <c>.xg</c>/<c>.xgp</c> file.</param>
    /// <param name="outputDir">Directory to write the <c>.json</c> file into;
    /// must already exist.</param>
    /// <returns>The full path of the JSON file written.</returns>
    public static string ConvertFile(string inputPath, string outputDir)
    {
        string outputPath = Path.Combine(outputDir, OutputNaming.JsonFileNameFor(inputPath));
        Convert(inputPath, outputPath);
        return outputPath;
    }

    /// <summary>
    /// Converts every XG-format file in <paramref name="inputDir"/> (top-level
    /// only) to a JSON file in <paramref name="outputDir"/>, naming outputs via
    /// <see cref="OutputNaming.ResolveBatch"/> so colliding <c>.xg</c>/<c>.xgp</c>
    /// pairs do not overwrite one another. Per-file failures are collected, not
    /// thrown, so one bad file does not abort the batch.
    /// </summary>
    /// <param name="inputDir">Directory to scan for <c>.xg</c>/<c>.xgp</c> files.</param>
    /// <param name="outputDir">Directory to write <c>.json</c> files into; must
    /// already exist.</param>
    /// <returns>The per-file outcome of the batch.</returns>
    public static DirectoryConversionResult ConvertDirectory(string inputDir, string outputDir)
    {
        var inputs = XgFileReader.EnumerateXgFormatFiles(inputDir).ToList();
        var outputNames = OutputNaming.ResolveBatch(inputs);

        var written = new List<string>();
        var failed = new List<ConversionFailure>();
        foreach (string input in inputs)
        {
            string outputPath = Path.Combine(outputDir, outputNames[input]);
            try
            {
                Convert(input, outputPath);
                written.Add(outputPath);
            }
            catch (Exception ex)
            {
                failed.Add(new ConversionFailure(input, ex.Message));
            }
        }
        return new DirectoryConversionResult(written, failed);
    }

    /// <summary>
    /// The conversion worker — read → iterate every decision → serialize →
    /// write. The single place that sequence lives; both single-file and
    /// directory modes route through it.
    /// </summary>
    internal static void Convert(string inputPath, string outputPath)
    {
        var file = XgFileReader.ReadFile(inputPath);

        // IterateDiagramRequests stamps each DecisionId with the source file's
        // bare name and throws if it is null, so always pass the file name.
        var decisions = XgDecisionIterator
            .IterateDiagramRequests(file, Path.GetFileName(inputPath))
            .ToList();

        string json = JsonSerializer.Serialize(decisions, JsonOptions);
        File.WriteAllText(outputPath, json);
    }
}

/// <summary>One input file that failed to convert, with the failure message.</summary>
/// <param name="InputPath">The input file that failed.</param>
/// <param name="Error">The exception message describing the failure.</param>
internal readonly record struct ConversionFailure(string InputPath, string Error);

/// <summary>
/// Outcome of a <see cref="Converter.ConvertDirectory"/> batch:
/// <see cref="Written"/> lists every JSON file produced, <see cref="Failed"/>
/// every input that errored. <see cref="Failed"/> empty ⇔ full success.
/// </summary>
/// <param name="Written">Full paths of the JSON files written.</param>
/// <param name="Failed">Inputs that failed, with their error messages.</param>
internal sealed record DirectoryConversionResult(
    IReadOnlyList<string> Written,
    IReadOnlyList<ConversionFailure> Failed);
