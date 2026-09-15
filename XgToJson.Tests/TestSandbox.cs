namespace XgToJson.Tests;

/// <summary>
/// The test project's one isolated temp directory: created fresh and uniquely
/// named for a single test, and removed after it. Every test that touches the
/// filesystem stages its inputs and receives its outputs here, so no case sees
/// another's files or any ambient state — and nothing depends on the process
/// working directory, which xUnit's parallel execution would race on.
/// </summary>
internal static class TestSandbox
{
    /// <summary>
    /// Runs <paramref name="body"/> against a freshly created, uniquely named
    /// temp directory, deleting it afterwards (best effort — a cleanup failure
    /// never masks the test's own outcome).
    /// </summary>
    /// <param name="body">The test body; receives the sandbox's full path.</param>
    internal static void Run(Action<string> body)
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
}
