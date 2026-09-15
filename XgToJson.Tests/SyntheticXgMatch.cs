using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgToJson.Tests;

/// <summary>
/// The gating tests' input: short matches built in memory through
/// <see cref="XgFileBuilder"/> and written as <c>.xg</c> files into the
/// calling test's own temp directory at run time
/// (<c>halheinrich/backgammon#227</c>).
///
/// <para>
/// <b>Why it is synthesized.</b> The umbrella's <c>TestData/</c> corpus is
/// gitignored, so a test that reads its input from there returns without
/// asserting on any checkout that lacks the corpus — it cannot gate. A match
/// generated here is present on every checkout, commits no binary, and
/// carries no real player's name: the players are invented in this file.
/// </para>
///
/// <para>
/// <b>What each match holds.</b> An opening checker play by each side with a
/// cube decision between them, every one analysed — so the converter emits
/// a non-empty decision list covering both decision kinds, not merely a
/// file. <see cref="XgFileBuilder"/> output is byte-deterministic (its
/// documented contract: no timestamps, no ids), so every run stages the same
/// bytes.
/// </para>
/// </summary>
internal static class SyntheticXgMatch
{
    /// <summary>
    /// The players. Invented here, in tracked source, which is what keeps
    /// every staged file free of real names.
    /// </summary>
    private const string Player1 = "Player One";

    /// <inheritdoc cref="Player1"/>
    private const string Player2 = "Player Two";

    /// <summary>
    /// Length of the first match staged by one call. Match <c>i</c> of a call
    /// is <c>FirstMatchLength + i</c> points long, which is what makes the
    /// matches of a multi-file call distinct matches rather than copies of one.
    /// </summary>
    private const int FirstMatchLength = 5;

    /// <summary>The opening play, recorded for <see cref="XgPlayer.Player1"/>: 31 as 8/5 6/5.</summary>
    private static readonly (DiceRoll Dice, Play Play) OpeningPlay =
        (new DiceRoll(3, 1), Of(new Move(8, 5), new Move(6, 5)));

    /// <summary>The reply, recorded for <see cref="XgPlayer.Player2"/>: 65 as 24/18/13.</summary>
    private static readonly (DiceRoll Dice, Play Play) Reply =
        (new DiceRoll(6, 5), Of(new Move(24, 18), new Move(18, 13)));

    /// <summary>
    /// The cubeful equities <see cref="XgPlayer.Player2"/>'s cube decision is
    /// analysed with, from the doubler's perspective — no double is right, as
    /// it is this early. Values only: no test reads the answer they imply.
    /// </summary>
    private static readonly XgCubeEquities CubeEquities =
        new(NoDouble: 0.05, DoubleTake: 0.02, DoubleDrop: 1.0);

    /// <summary>
    /// Writes one synthesized match into <paramref name="directory"/> and
    /// returns its path.
    /// </summary>
    /// <param name="directory">An existing directory; the test's own temp sandbox.</param>
    internal static string WriteOne(string directory) => WriteMany(directory, count: 1)[0];

    /// <summary>
    /// Writes <paramref name="count"/> distinct synthesized matches into
    /// <paramref name="directory"/>, each under its own name, and returns
    /// their paths in the order written.
    /// </summary>
    /// <param name="directory">An existing directory; the test's own temp sandbox.</param>
    /// <param name="count">How many matches to write; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 1.</exception>
    internal static IReadOnlyList<string> WriteMany(string directory, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var paths = new string[count];
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(directory, $"synthetic-match-{i + 1}.xg");
            File.WriteAllBytes(path, Bytes(FirstMatchLength + i));
            paths[i] = path;
        }
        return paths;
    }

    /// <summary>A <paramref name="matchLength"/>-point match holding one game, as XG binary bytes.</summary>
    private static byte[] Bytes(int matchLength)
    {
        var builder = XgFileBuilder.ForMatch(matchLength, Player1, Player2);
        builder.AddGame()
            .Play(XgPlayer.Player1, OpeningPlay.Dice, OpeningPlay.Play)
            .CubeDecision(XgPlayer.Player2, CubeEquities, doublerAction: CubeAction.NoDouble)
            .Play(XgPlayer.Player2, Reply.Dice, Reply.Play);
        return XgFileWriter.ToBytes(builder.Build());
    }

    /// <summary>A <see cref="Play"/> of the given moves; the type is built up, not constructed.</summary>
    private static Play Of(params Move[] moves)
    {
        var play = new Play();
        foreach (var move in moves) play.Add(move);
        return play;
    }
}
