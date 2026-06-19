using System.Text.Json;
using BgDataTypes_Lib;
using XgToJson;

namespace XgToJson.Tests;

/// <summary>
/// Primary-path smoke coverage for the conversion engine. Exercises the real
/// wire end to end: a genuine corpus file → <c>ConvertXgToJson_Lib</c> →
/// JSON → file on disk → parsed back to <see cref="BgDecisionData"/>.
/// </summary>
public class ConverterSmokeTests
{
    /// <summary>
    /// A real <c>.xg</c>/<c>.xgp</c> file converts to a JSON file that parses
    /// as a non-empty array and round-trips back to a
    /// <c>List&lt;BgDecisionData&gt;</c> using the engine's own
    /// <see cref="Converter.JsonOptions"/> (the single source — so this verifies
    /// the engine's actual format, not a parallel copy). Every decision carries
    /// a populated <c>Id</c> and <c>Xgid</c>. Fixture-agnostic: picks the first
    /// available corpus file and no-ops if the corpus is empty.
    /// </summary>
    [Fact]
    public void ConvertFile_RealCorpusFile_RoundTripsToDecisionList()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // No corpus fixtures present — nothing to smoke (tolerated).

        string outputDir = Path.Combine(
            Path.GetTempPath(), "XgToJson.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(outputDir);
        try
        {
            string outputPath = Converter.ConvertFile(input, outputDir);

            Assert.True(File.Exists(outputPath));
            Assert.Equal(".json", Path.GetExtension(outputPath));

            string json = File.ReadAllText(outputPath);
            var decisions = JsonSerializer.Deserialize<List<BgDecisionData>>(
                json, Converter.JsonOptions);

            Assert.NotNull(decisions);
            Assert.NotEmpty(decisions);
            Assert.All(decisions, d =>
            {
                Assert.NotNull(d.Id);
                Assert.False(string.IsNullOrEmpty(d.Xgid),
                    "every decision should carry a populated Xgid");
            });
        }
        finally
        {
            try { Directory.Delete(outputDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
