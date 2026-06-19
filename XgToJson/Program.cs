using ConvertXgToJson_Lib;
using XgToJson;

// XgToJson — convert XG-format (.xg/.xgp) files to JSON decision records.
//
//   XgToJson <input> [outputDir]
//
//     <input>     a .xg/.xgp file, or a directory of them (top-level only).
//     [outputDir] an existing directory to write .json output into;
//                 defaults to the current directory.
//
// Exit codes: 0 = success · 1 = usage/argument error · 2 = conversion failure.

const int exitSuccess = 0;
const int exitUsage = 1;
const int exitConversionFailure = 2;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine(UsageText());
    return exitUsage;
}

string inputPath = args[0];
string? outputDirArg = args.Length == 2 ? args[1] : null;

// A supplied output directory must already exist. We do not create it, so a
// typo or a file path (e.g. "out.xgp") fails loudly rather than silently
// spawning a folder of that name. Omitted → the current working directory,
// which always exists. One check, shared by both modes.
if (outputDirArg is not null && !Directory.Exists(outputDirArg))
{
    Console.Error.WriteLine(
        $"Output directory does not exist or is not a directory: '{outputDirArg}'.");
    return exitUsage;
}
string outputDir = outputDirArg ?? Directory.GetCurrentDirectory();

// Directory input → batch mode: convert every XG-format file within it.
if (Directory.Exists(inputPath))
{
    DirectoryConversionResult result = Converter.ConvertDirectory(inputPath, outputDir);

    foreach (string written in result.Written)
        Console.WriteLine($"Wrote {written}");
    foreach (ConversionFailure failure in result.Failed)
        Console.Error.WriteLine($"FAILED {failure.InputPath}: {failure.Error}");

    if (result.Written.Count == 0 && result.Failed.Count == 0)
    {
        Console.Error.WriteLine(
            $"No XG-format files ({AcceptedFormats()}) found in '{inputPath}'.");
        return exitUsage;
    }

    return result.Failed.Count == 0 ? exitSuccess : exitConversionFailure;
}

// Single-file input.
if (File.Exists(inputPath))
{
    if (!XgFileReader.IsXgFormatFile(inputPath))
    {
        Console.Error.WriteLine(
            $"'{inputPath}' is not an XG-format file (expected {AcceptedFormats()}).");
        return exitUsage;
    }

    try
    {
        string outputPath = Converter.ConvertFile(inputPath, outputDir);
        Console.WriteLine($"Wrote {outputPath}");
        return exitSuccess;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAILED {inputPath}: {ex.Message}");
        return exitConversionFailure;
    }
}

Console.Error.WriteLine($"Input path not found: '{inputPath}'.");
return exitUsage;

// Accepted-format list, read from the producer's single source rather than
// re-hardcoding ".xg/.xgp" here.
static string AcceptedFormats() => string.Join(", ", XgFileReader.XgFormatExtensions);

static string UsageText() =>
    $"""
    XgToJson — convert XG-format ({AcceptedFormats()}) files to JSON decision records.

    Usage:
      XgToJson <input> [outputDir]

      <input>     a {AcceptedFormats()} file, or a directory of them (top-level only).
      [outputDir] an existing directory to write .json output into;
                  defaults to the current directory.

    Exit codes: 0 = success, 1 = usage/argument error, 2 = conversion failure.
    """;
