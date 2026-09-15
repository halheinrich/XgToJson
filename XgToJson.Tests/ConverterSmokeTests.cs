using System.Text.Json;
using BgDataTypes_Lib;
using XgToJson;

namespace XgToJson.Tests;

/// <summary>
/// Primary-path smoke coverage for the conversion engine. Exercises the real
/// wire end to end: an <c>.xg</c> file → <c>ConvertXgToJson_Lib</c> →
/// JSON → file on disk → parsed back to <see cref="BgDecisionData"/>.
///
/// <para>
/// <b>The gating case</b> converts a match synthesized at test time
/// (<see cref="SyntheticXgMatch"/>), so it asserts on every checkout.
/// <b>The real-file case</b> is a local-only extra: it runs the same assertions
/// on the first file of the umbrella's gitignored <c>TestData/</c> corpus —
/// an XG-authored file, which a synthesized match cannot stand in for — and is
/// vacuous on an empty corpus by design, so it gates nothing.
/// </para>
/// </summary>
public class ConverterSmokeTests
{
    [Fact]
    public void ConvertFile_SynthesizedMatch_RoundTripsToDecisionList()
    {
        AssertRoundTripsToDecisionList(sandbox =>
        {
            string inputDir = Path.Combine(sandbox, "input");
            Directory.CreateDirectory(inputDir);
            return SyntheticXgMatch.WriteOne(inputDir);
        });
    }

    [Fact]
    public void ConvertFile_RealCorpusFile_RoundTripsToDecisionList()
    {
        string? input = TestPaths.XgFormatFiles.FirstOrDefault();
        if (input is null)
            return; // Local-only: vacuous on an empty corpus by design (see the class doc).

        AssertRoundTripsToDecisionList(_ => input);
    }

    /// <summary>
    /// The file at the path <paramref name="stageInput"/> returns, given a
    /// fresh <see cref="TestSandbox"/>, converts to a JSON file that parses as a
    /// non-empty array and round-trips back to a <c>List&lt;BgDecisionData&gt;</c>
    /// using the engine's own <see cref="Converter.JsonOptions"/> (the single
    /// source — so this verifies the engine's actual format, not a parallel
    /// copy). Every decision carries a populated <c>Id</c> and <c>Xgid</c>.
    /// </summary>
    private static void AssertRoundTripsToDecisionList(Func<string, string> stageInput)
    {
        TestSandbox.Run(sandbox =>
        {
            string input = stageInput(sandbox);
            string outputDir = Path.Combine(sandbox, "out");
            Directory.CreateDirectory(outputDir);

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
        });
    }
}
