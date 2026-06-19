# XgToJson — Subproject Instructions

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 console application + xUnit test project. Visual Studio 2026, Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgToJson\XgToJson.slnx`

## Repo

https://github.com/halheinrich/XgToJson — branch `main`.

## Depends on

- **ConvertXgToJson_Lib** — `XgFileReader.ReadFile` (parse), `XgDecisionIterator.IterateDiagramRequests` (decision extraction), and the file-discovery surface `XgFileReader.IsXgFormatFile` / `EnumerateXgFormatFiles` / `XgFormatExtensions`. Transitively brings **BgDataTypes_Lib** (`BgDecisionData` and its type-level JSON converters — the serialized shape) and **BgMoveGen**.

## Directory tree

```
XgToJson/
├── XgToJson.slnx
├── INSTRUCTIONS.md
├── XgToJson/
│   ├── XgToJson.csproj          console exe (OutputType=Exe)
│   ├── Program.cs               thin entry — arg parse → dispatch, exit codes
│   ├── Converter.cs             the conversion engine + DirectoryConversionResult
│   └── OutputNaming.cs          output-filename + collision rules (pure)
└── XgToJson.Tests/
    ├── XgToJson.Tests.csproj    xUnit
    ├── TestPaths.cs             locates the shared TestData corpus
    ├── ConverterSmokeTests.cs   primary-path wire smoke (file → JSON → file)
    └── OutputNamingTests.cs     naming + collision unit tests (no corpus)
```

## Architecture

**Thin `Main`, testable engine.** `Program.cs` is top-level statements: parse 1–2 args, dispatch to `Converter`, map the outcome to an exit code. All real work lives in `Converter` and `OutputNaming`, which the tests target directly — no test drives `Main`.

**The engine (`Converter`).** One private worker, `Convert(inputPath, outputPath)`, is the single place the `ReadFile → IterateDiagramRequests → Serialize → WriteAllText` sequence lives; both single-file and directory modes route through it. It emits **every** decision (checker and cube) as a JSON array of `BgDecisionData` — no filtering; the downstream consumer thresholds. `IterateDiagramRequests` is always passed `Path.GetFileName(inputPath)` as `sourceFile` (it stamps `DecisionId` and throws on null).

**Serialization single-source (`Converter.JsonOptions`).** One public `JsonSerializerOptions { WriteIndented = true }`, consumed by both the engine's serialize call and the smoke test's deserialize call. No option-level converters are registered: every converter `BgDecisionData` needs is a type-level `[JsonConverter]` in `BgDataTypes_Lib`, so plain options round-trip losslessly. `WriteIndented` is this exe's own presentation choice, not a producer concern — which is why the options live here and are not pushed into the producer.

**Output naming (`OutputNaming`).** `JsonFileNameFor` is the base rule (`match.xg → match.json`). `ResolveBatch` derives its candidate from `JsonFileNameFor` and layers collision disambiguation: when `foo.xg` and `foo.xgp` both map to `foo.json`, both retain their full source name (`foo.xg.json`, `foo.xgp.json`) while unique names stay clean. This is the **output** rule and is deliberately separate from the **discovery** rule (`XgFileReader.EnumerateXgFormatFiles`).

**Directory mode.** `ConvertDirectory` enumerates via `EnumerateXgFormatFiles` (top-level only, no recursion), resolves all names up front, then converts each — collecting per-file failures into `DirectoryConversionResult` rather than throwing, so one bad file does not abort the batch. `Program` does all console I/O and exit-code mapping; `Converter` stays I/O-policy-free (returns results, never writes to the console).

## Public API

This is a console executable; its contract is the CLI.

```
XgToJson <input> [outputDir]
```

- **`<input>` (required).** A `.xg`/`.xgp` file, or a directory. A file with any other extension is rejected (`IsXgFormatFile`). A directory is processed top-level only — every `.xg`/`.xgp` in it, one JSON output per input file.
- **`[outputDir]` (optional).** Omitted → JSON is written to the current working directory (one default shared by both modes). Given → must be an **existing** directory; a non-existent or non-directory path is a usage error (exit `1`), not silently created — this guards against a typo'd or file-looking path spawning a bogus folder.
- **Output name.** `match.xg → match.json`. Collision (`foo.xg` + `foo.xgp` in one batch) → both keep their source extension (`foo.xg.json`, `foo.xgp.json`); never a silent overwrite.
- **Exit codes.** `0` success · `1` usage/argument error (wrong arg count, non-XG file, input not found, output dir not an existing directory, directory with no XG files) · `2` conversion failure (the single file threw, or ≥1 file failed in directory mode — successful files in the batch are still written).

The testable seams behind the CLI:

```csharp
public static class Converter
{
    public static JsonSerializerOptions JsonOptions { get; }            // the output-format single source
    public static string ConvertFile(string inputPath, string outputDir);        // → output path
    public static DirectoryConversionResult ConvertDirectory(string inputDir, string outputDir);
}

public static class OutputNaming
{
    public static string JsonFileNameFor(string inputPath);                       // match.xg → match.json
    public static IReadOnlyDictionary<string, string> ResolveBatch(IEnumerable<string> inputPaths);
}
```

## Pitfalls

- **`sourceFile` must be the bare name.** `IterateDiagramRequests` throws if `sourceFile` is null and uses it to stamp `DecisionId`; always pass `Path.GetFileName(inputPath)`, never the full path (the ID would embed the directory).
- **Don't register JSON converters here.** `BgDecisionData` carries its converters as type-level attributes; registering option-level converters in `JsonOptions` would mask a future dropped attribute that the producer's `BgDecisionDataSerializationTests` is designed to catch. Keep `JsonOptions` bare.
- **Don't re-derive the accepted-extension set.** Input validation, discovery, and usage text all read `XgFileReader.IsXgFormatFile` / `EnumerateXgFormatFiles` / `XgFormatExtensions`. The collision rule in `OutputNaming` is the one piece that is genuinely separate — it is an output-naming decision, not a discovery decision.
- **Directory mode never throws on a bad file.** Failures are collected and reported; a batch can exit `2` while still having written good files. Check `DirectoryConversionResult.Failed`, not just the return.
- **Collision disambiguation is batch-scoped.** A lone `foo.xg` produces `foo.json`; adding a `foo.xgp` later changes *both* outputs to the `.xg.json`/`.xgp.json` form. Output names are a function of the whole input set, not each file in isolation.

## Subproject-internal next steps

None pending.
