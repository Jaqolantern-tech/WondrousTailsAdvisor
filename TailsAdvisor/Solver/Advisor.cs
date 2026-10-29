using System;

namespace TailsAdvisor.Solver;

/// <summary>The four things the advisor can tell you.</summary>
public enum Verdict
{
    KeepStamping,
    ShakeItUp,
    GrabPoints,
    OffToKhloe,
}

/// <summary>Why the verdict was given; picks the explanation line.</summary>
public enum Reason
{
    TooEarly,
    GoalOutOfReach,
    GoodBoard,
    Locked,
    ShuffleHelps,
    NeedPoints,
    Full,
}

/// <summary>Everything the window needs for one board.</summary>
public sealed class Analysis
{
    public required int Mask { get; init; }
    public required int Stickers { get; init; }
    public required int Points { get; init; }
    public required Goal Goal { get; init; }
    public required Verdict Verdict { get; init; }
    public required Reason Reason { get; init; }
    public required int LinesDone { get; init; }

    /// <summary>Points still needed to afford a shuffle (GrabPoints only).</summary>
    public required int PointsNeeded { get; init; }

    /// <summary>Chance of the goal if you keep this board.</summary>
    public required double KeepChance { get; init; }

    /// <summary>Chance of the goal if you use your shuffles now. Null when a shuffle is not possible.</summary>
    public required double? ShuffleChance { get; init; }

    /// <summary>At least 1/2/3 lines if you keep this board (for "Show the math").</summary>
    public required double[] KeepOdds { get; init; }

    /// <summary>At least 1/2/3 lines if you shuffle now (for "Show the math"). Null when not possible.</summary>
    public required double[]? ShuffleOdds { get; init; }
}

public static class Advisor
{
    public static Analysis Analyze(ShufflePolicy policy, int mask, int points)
    {
        var goal = policy.Goal;
        var k = Math.Min(TailsMath.Count(mask), TailsMath.MaxStickers);
        points = Math.Clamp(points, 0, TailsMath.MaxSecondChancePoints);

        var ns = TailsMath.NoShuffleDist.AsSpan(mask * 4, 4);
        var keepOdds = TailsMath.AtLeast(ns[0], ns[1], ns[2], ns[3]);
        var keepChance = GoalChance(goal, keepOdds);

        // At 7 stickers, judge the shuffle as if you could afford one, so we can say
        // "grab more points" when it would help.
        double? shuffleChance = null;
        double[]? shuffleOdds = null;
        var budget = Math.Max(points, TailsMath.ShuffleCost);
        if (TailsMath.CanShuffleAt(k))
        {
            var s = policy.ShuffleNow(mask, budget);
            shuffleOdds = TailsMath.AtLeast(s.Dist[0], s.Dist[1], s.Dist[2], s.Dist[3]);
            shuffleChance = GoalChance(goal, shuffleOdds);
        }

        Verdict verdict;
        Reason reason;
        var needed = 0;

        if (k >= TailsMath.MaxStickers)
        {
            verdict = Verdict.OffToKhloe;
            reason = Reason.Full;
        }
        else if (k > TailsMath.MaxShuffleStickers)
        {
            verdict = Verdict.KeepStamping;
            reason = Reason.Locked;
        }
        else if (k == TailsMath.MaxShuffleStickers && policy.ShouldShuffle(mask, budget))
        {
            if (points >= TailsMath.ShuffleCost)
            {
                verdict = Verdict.ShakeItUp;
                reason = Reason.ShuffleHelps;
            }
            else
            {
                verdict = Verdict.GrabPoints;
                reason = Reason.NeedPoints;
                needed = TailsMath.ShuffleCost - points;
            }
        }
        else if (k == TailsMath.MaxShuffleStickers)
        {
            verdict = Verdict.KeepStamping;
            reason = Reason.GoodBoard;
        }
        else
        {
            // Before 7 the optimal plan never shuffles: a shuffle at 7 rerolls everything anyway.
            verdict = Verdict.KeepStamping;
            reason = keepChance <= 0 ? Reason.GoalOutOfReach : Reason.TooEarly;
        }

        // Before 7, "shuffle now" odds are not a real option worth showing.
        if (k < TailsMath.MaxShuffleStickers)
        {
            shuffleOdds = null;
            shuffleChance = null;
        }

        return new Analysis
        {
            Mask = mask,
            Stickers = k,
            Points = points,
            Goal = goal,
            Verdict = verdict,
            Reason = reason,
            LinesDone = TailsMath.CompletedLines(mask),
            PointsNeeded = needed,
            KeepChance = keepChance,
            ShuffleChance = shuffleChance,
            KeepOdds = keepOdds,
            ShuffleOdds = shuffleOdds,
        };
    }

    private static double GoalChance(Goal goal, double[] atLeast) => goal switch
    {
        Goal.TwoLines => atLeast[1],
        Goal.ThreeLines => atLeast[2],
        _ => atLeast[0],
    };
}
