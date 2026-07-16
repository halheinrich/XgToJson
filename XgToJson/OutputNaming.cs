namespace XgToJson;

/// <summary>
/// Pure output-filename resolution: maps each XG-format input file to its
/// <c>.json</c> output filename, disambiguating the case where two inputs
/// (<c>foo.xg</c> and <c>foo.xgp</c>) would otherwise collide on a single
/// <c>foo.json</c>. This is the output-naming rule and is deliberately
/// separate from the input-discovery rule (which lives in
/// <c>XgFileReader.EnumerateXgFormatFiles</c>): one decides which files are
/// read, the other decides what the written files are called.
/// </summary>
internal static class OutputNaming
{
    /// <summary>
    /// The output filename for a single input, ignoring any collision: the
    /// input's bare filename with its extension replaced by <c>.json</c>
    /// (<c>match.xg</c> → <c>match.json</c>). The base naming rule;
    /// <see cref="ResolveBatch"/> layers collision disambiguation on top and
    /// derives its base candidate from here rather than re-encoding it.
    /// </summary>
    /// <param name="inputPath">An input file path or name.</param>
    /// <returns>The output filename only (no directory component).</returns>
    public static string JsonFileNameFor(string inputPath)
        => Path.ChangeExtension(Path.GetFileName(inputPath), ".json");

    /// <summary>
    /// Resolves output filenames for a batch of inputs, disambiguating
    /// collisions. When two or more inputs map to the same base
    /// <see cref="JsonFileNameFor"/> result (e.g. <c>foo.xg</c> and
    /// <c>foo.xgp</c> both → <c>foo.json</c>), every member of that colliding
    /// group instead retains its full source filename plus <c>.json</c>
    /// (<c>foo.xg.json</c>, <c>foo.xgp.json</c>); an input whose base name is
    /// unique keeps the clean <c>foo.json</c>. Deterministic given the input
    /// set; never yields two identical output names, so no input silently
    /// overwrites another's output.
    /// </summary>
    /// <param name="inputPaths">Input file paths (typically one directory's
    /// worth, so bare filenames are unique within the batch).</param>
    /// <returns>A map from each input path to its resolved output filename
    /// (filename only, no directory component).</returns>
    public static IReadOnlyDictionary<string, string> ResolveBatch(IEnumerable<string> inputPaths)
    {
        // Group by the base candidate name case-insensitively: the target file
        // system (Windows) is case-insensitive, so "Foo.json" and "foo.json"
        // would collide on disk even though the strings differ.
        var byBaseName = inputPaths.GroupBy(
            JsonFileNameFor,
            StringComparer.OrdinalIgnoreCase);

        var resolved = new Dictionary<string, string>();
        foreach (var group in byBaseName)
        {
            bool collides = group.Count() > 1;
            foreach (string input in group)
            {
                // Collision → keep the whole source name (foo.xg → foo.xg.json),
                // which is unique because filenames within a directory are.
                resolved[input] = collides
                    ? Path.GetFileName(input) + ".json"
                    : group.Key;
            }
        }
        return resolved;
    }
}
