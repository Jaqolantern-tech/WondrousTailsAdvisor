using System;

namespace TailsAdvisor.Solver;

public enum Verdict
{
    Shuffle,
    Hold,
    TooFewStickers,
    TooManyStickers,
    NotEnoughPoints,
    Complete,
}

/// <summary>Everything the window needs for one board, computed off the UI thread.</summary>
public sealed class Analysis
{
    public required int Mask { get; init; }
    public required int Stickers { get; init; }
    public required int Points { get; init; }
    public required int UsablePoints { get; init; }
    public required Goal Goal { get; init; }
    public required int LinesDone { get; init; }
    public required int LinesOneAway { get; init; }

    /// <summary>At least 1/2/3 lines if you never shuffle again.</summary>
    public required double[] NoShuffleOdds { get; init; }

    /// <summary>At least 1/2/3 lines if you hold now and follow the plan afterwards.</summary>
    public required double[] HoldOdds { get; init; }

    /// <summary>At least 1/2/3 lines if you shuffle now and follow the plan afterwards. Null when shuffling is not possible.</summary>
    public required double[]? ShuffleOdds { get; init; }

    public required double HoldValue { get; init; }
    public required double? ShuffleValue { get; init; }
    public required Verdict Verdict { get; init; }

    /// <summary>Per square: goal value under the plan if the next sticker lands there. NaN for filled squares.</summary>
    public required double[] CellValue { get; init; }

    /// <summary>Per square: at least 1/2/3 lines under the plan if the next sticker lands there. Null for filled squares.</summary>
    public required double[]?[] CellOdds { get; init; }
}

public static class Advisor
{
    public static Analysis Analyze(ShufflePolicy policy, int mask, int points, int reservePoints)
    {
        var stickers = TailsMath.Count(mask);
        var usable = Math.Clamp(points - reservePoints, 0, TailsMath.MaxSecondChancePoints);

        // Boards with more than 9 stickers cannot exist; clamp defensively.
        if (stickers > TailsMath.MaxStickers) stickers = TailsMath.MaxStickers;

        var ns = TailsMath.NoShuffleDist.AsSpan(mask * 4, 4);
        var noShuffle = TailsMath.AtLeast(ns[0], ns[1], ns[2], ns[3]);

        var hold = policy.HoldNow(mask, usable);
        double[]? shuffleOdds = null;
        double? shuffleValue = null;

        if (TailsMath.CanShuffleAt(stickers) && usable >= TailsMath.ShuffleCost)
        {
            var s = policy.ShuffleNow(mask, usable);
            shuffleValue = s.Value;
            shuffleOdds = TailsMath.AtLeast(s.Dist[0], s.Dist[1], s.Dist[2], s.Dist[3]);
        }

        Verdict verdict;
        if (stickers >= TailsMath.MaxStickers) verdict = Verdict.Complete;
        else if (stickers < TailsMath.MinShuffleStickers) verdict = Verdict.TooFewStickers;
        else if (stickers > TailsMath.MaxShuffleStickers) verdict = Verdict.TooManyStickers;
        else if (usable < TailsMath.ShuffleCost) verdict = Verdict.NotEnoughPoints;
        else verdict = policy.ShouldShuffle(mask, usable) ? Verdict.Shuffle : Verdict.Hold;

        var cellValue = new double[TailsMath.Cells];
        var cellOdds = new double[]?[TailsMath.Cells];
        for (var bit = 0; bit < TailsMath.Cells; bit++)
        {
            if ((mask & (1 << bit)) != 0 || stickers >= TailsMath.MaxStickers)
            {
                cellValue[bit] = double.NaN;
                continue;
            }

            var child = mask | (1 << bit);
            cellValue[bit] = policy.Value(child, usable);
            var d = policy.Dist(child, usable);
            cellOdds[bit] = TailsMath.AtLeast(d[0], d[1], d[2], d[3]);
        }

        return new Analysis
        {
            Mask = mask,
            Stickers = stickers,
            Points = points,
            UsablePoints = usable,
            Goal = policy.Goal,
            LinesDone = TailsMath.CompletedLines(mask),
            LinesOneAway = TailsMath.LinesOneAway(mask),
            NoShuffleOdds = noShuffle,
            HoldOdds = TailsMath.AtLeast(hold.Dist[0], hold.Dist[1], hold.Dist[2], hold.Dist[3]),
            ShuffleOdds = shuffleOdds,
            HoldValue = hold.Value,
            ShuffleValue = shuffleValue,
            Verdict = verdict,
            CellValue = cellValue,
            CellOdds = cellOdds,
        };
    }
}
