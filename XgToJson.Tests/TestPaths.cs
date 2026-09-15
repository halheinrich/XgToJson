using ConvertXgToJson_Lib;

namespace XgToJson.Tests;

/// <summary>
/// Locates the shared <c>backgammon/TestData</c> corpus relative to the test
/// assembly, mirroring <c>ConvertXgToJson_Lib.Tests.TestPaths</c>. Its readers
/// are local-only real-file cases: fixture-agnostic, they take whatever
/// <c>.xg</c>/<c>.xgp</c> files happen to be present and are vacuous on an empty
/// corpus by design. Nothing gating reads it — the corpus is gitignored, so
/// gating tests synthesize their input through <see cref="SyntheticXgMatch"/>.
/// </summary>
internal static class TestPaths
{
    private static readonly string _root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\TestData"));

    public static string XgDir => Path.Combine(_root, "xg");
    public static string XgpDir => Path.Combine(_root, "xgp");

    /// <summary>
    /// Every <c>.xg</c>/<c>.xgp</c> fixture in the shared corpus (across both
    /// the <c>xg</c> and <c>xgp</c> subdirectories), discovered via the same
    /// producer helper the exe uses. Empty when no corpus is present.
    /// </summary>
    public static IEnumerable<string> XgFormatFiles
    {
        get
        {
            foreach (string dir in new[] { XgDir, XgpDir })
            {
                if (!Directory.Exists(dir))
                    continue;
                foreach (string path in XgFileReader.EnumerateXgFormatFiles(dir))
                    yield return path;
            }
        }
    }
}
