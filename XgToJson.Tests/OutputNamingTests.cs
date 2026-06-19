using XgToJson;

namespace XgToJson.Tests;

/// <summary>
/// Unit tests for <see cref="OutputNaming"/> — the output-filename and
/// collision-disambiguation rules. No corpus required.
/// </summary>
public class OutputNamingTests
{
    [Fact]
    public void JsonFileNameFor_ReplacesExtensionWithJson()
    {
        Assert.Equal("match.json", OutputNaming.JsonFileNameFor(@"C:\games\match.xg"));
        Assert.Equal("match.json", OutputNaming.JsonFileNameFor("match.xgp"));
    }

    [Fact]
    public void ResolveBatch_NonCollidingInputs_KeepCleanNames()
    {
        var inputs = new[] { @"d\alpha.xg", @"d\beta.xgp" };

        var map = OutputNaming.ResolveBatch(inputs);

        Assert.Equal("alpha.json", map[@"d\alpha.xg"]);
        Assert.Equal("beta.json", map[@"d\beta.xgp"]);
    }

    [Fact]
    public void ResolveBatch_XgAndXgpCollision_RetainsSourceExtension()
    {
        var inputs = new[] { @"d\foo.xg", @"d\foo.xgp", @"d\bar.xg" };

        var map = OutputNaming.ResolveBatch(inputs);

        // foo.xg and foo.xgp both map to foo.json → disambiguate by retaining
        // the full source filename.
        Assert.Equal("foo.xg.json", map[@"d\foo.xg"]);
        Assert.Equal("foo.xgp.json", map[@"d\foo.xgp"]);
        // bar.xg is unique → clean name.
        Assert.Equal("bar.json", map[@"d\bar.xg"]);
    }

    [Fact]
    public void ResolveBatch_NeverProducesDuplicateOutputNames()
    {
        var inputs = new[] { @"d\foo.xg", @"d\foo.xgp", @"d\bar.xg", @"d\baz.xgp" };

        var names = OutputNaming.ResolveBatch(inputs).Values;

        Assert.Equal(
            names.Count(),
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
