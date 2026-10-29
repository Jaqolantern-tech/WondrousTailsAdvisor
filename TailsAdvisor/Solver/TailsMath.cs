using System.Collections.Generic;
using System.Numerics;

namespace TailsAdvisor.Solver;

/// <summary>
/// Board geometry and the "no more shuffles" odds for every possible journal.
/// A board is a 16 bit mask, bit index = row * 4 + column (row major, top left is bit 0).
/// The game fills 9 stickers, each one dropped on a uniformly random open square.
/// </summary>
public static class TailsMath
{
    public const int Cells = 16;
    public const int MaxStickers = 9;
    public const int MinShuffleStickers = 3;
    public const int MaxShuffleStickers = 7;
    public const int ShuffleCost = 2;
    public const int MaxSecondChancePoints = 9;

    /// <summary>The 10 scoring lines: 4 rows, 4 columns, 2 diagonals.</summary>
    public static readonly int[] LineMasks = BuildLineMasks();

    /// <summary>Masks grouped by sticker count (0 to 9).</summary>
    public static readonly int[][] MasksByCount = BuildMasksByCount();

    /// <summary>
    /// For every mask with 9 or fewer stickers: probability of finishing with exactly 0, 1, 2, 3 lines
    /// if you never shuffle again. Stored flat: [mask * 4 + lines].
    /// </summary>
    public static readonly double[] NoShuffleDist = BuildNoShuffleDist();

    /// <summary>
    /// Exact outcome distribution right after a shuffle with k stickers and no further shuffles
    /// (average over every possible k sticker board). Stored flat: [k * 4 + lines].
    /// </summary>
    public static readonly double[] ShuffleOnceDist = BuildShuffleOnceDist();

    public static int Count(int mask) => BitOperations.PopCount((uint)mask);

    public static bool CanShuffleAt(int stickers) =>
        stickers >= MinShuffleStickers && stickers <= MaxShuffleStickers;

    public static int CompletedLines(int mask)
    {
        var n = 0;
        foreach (var line in LineMasks)
            if ((mask & line) == line) n++;
        return n;
    }

    /// <summary>Lines that are missing exactly one sticker.</summary>
    public static int LinesOneAway(int mask)
    {
        var n = 0;
        foreach (var line in LineMasks)
            if (Count(mask & line) == 3) n++;
        return n;
    }

    /// <summary>Converts an exact distribution (0,1,2,3 lines) into "at least 1/2/3 lines" odds.</summary>
    public static double[] AtLeast(double d0, double d1, double d2, double d3) =>
        [d1 + d2 + d3, d2 + d3, d3];

    private static int[] BuildLineMasks()
    {
        var lines = new List<int>();
        for (var r = 0; r < 4; r++)
        {
            var m = 0;
            for (var c = 0; c < 4; c++) m |= 1 << (r * 4 + c);
            lines.Add(m);
        }
        for (var c = 0; c < 4; c++)
        {
            var m = 0;
            for (var r = 0; r < 4; r++) m |= 1 << (r * 4 + c);
            lines.Add(m);
        }
        int d1 = 0, d2 = 0;
        for (var i = 0; i < 4; i++)
        {
            d1 |= 1 << (i * 4 + i);
            d2 |= 1 << (i * 4 + (3 - i));
        }
        lines.Add(d1);
        lines.Add(d2);
        return lines.ToArray();
    }

    private static int[][] BuildMasksByCount()
    {
        var buckets = new List<int>[MaxStickers + 1];
        for (var k = 0; k <= MaxStickers; k++) buckets[k] = new List<int>();
        for (var mask = 0; mask < 1 << Cells; mask++)
        {
            var k = Count(mask);
            if (k <= MaxStickers) buckets[k].Add(mask);
        }
        var result = new int[MaxStickers + 1][];
        for (var k = 0; k <= MaxStickers; k++) result[k] = buckets[k].ToArray();
        return result;
    }

    private static double[] BuildNoShuffleDist()
    {
        var dist = new double[(1 << Cells) * 4];
        for (var k = MaxStickers; k >= 0; k--)
        {
            foreach (var mask in MasksByCount[k])
            {
                var o = mask * 4;
                if (k == MaxStickers)
                {
                    var lines = CompletedLines(mask);
                    dist[o + (lines > 3 ? 3 : lines)] = 1.0;
                    continue;
                }

                var open = Cells - k;
                for (var bit = 0; bit < Cells; bit++)
                {
                    if ((mask & (1 << bit)) != 0) continue;
                    var child = (mask | (1 << bit)) * 4;
                    for (var i = 0; i < 4; i++) dist[o + i] += dist[child + i] / open;
                }
            }
        }
        return dist;
    }

    private static double[] BuildShuffleOnceDist()
    {
        var avg = new double[(MaxStickers + 1) * 4];
        for (var k = 0; k <= MaxStickers; k++)
        {
            var masks = MasksByCount[k];
            foreach (var mask in masks)
                for (var i = 0; i < 4; i++) avg[k * 4 + i] += NoShuffleDist[mask * 4 + i];
            for (var i = 0; i < 4; i++) avg[k * 4 + i] /= masks.Length;
        }
        return avg;
    }
}
