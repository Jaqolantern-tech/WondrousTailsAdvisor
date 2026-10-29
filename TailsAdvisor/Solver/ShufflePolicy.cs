using System;

namespace TailsAdvisor.Solver;

public enum Goal
{
    AnyLine = 0,
    TwoLines = 1,
    ThreeLines = 2,
}

/// <summary>
/// Optimal shuffle plan for one goal, solved for every board and every point budget from 0 to 9.
///
/// At each board you may Hold (wait for the next random sticker) or Shuffle (if 3 to 7 stickers and
/// at least 2 usable points), which turns the board into a uniformly random board with the same number
/// of stickers. Values are computed bottom up, so "Shuffle" already accounts for shuffling again later.
/// </summary>
public sealed class ShufflePolicy
{
    private const double Eps = 1e-9;

    public Goal Goal { get; }
    public int MaxPoints { get; }

    // value[p][mask]: best achievable goal value with p usable points.
    private readonly double[][] value;
    // dist[p][mask * 4 + lines]: exact outcome distribution when following the plan.
    private readonly double[][] dist;
    // Average over all boards with k stickers (what a shuffle hands you).
    private readonly double[][] shuffleValue;
    private readonly double[][] shuffleDist;

    public ShufflePolicy(Goal goal, int maxPoints = TailsMath.MaxSecondChancePoints)
    {
        Goal = goal;
        MaxPoints = Math.Clamp(maxPoints, 0, TailsMath.MaxSecondChancePoints);

        value = new double[MaxPoints + 1][];
        dist = new double[MaxPoints + 1][];
        shuffleValue = new double[MaxPoints + 1][];
        shuffleDist = new double[MaxPoints + 1][];

        for (var p = 0; p <= MaxPoints; p++) Solve(p);
    }

    public static double Reward(Goal goal, int lines) => goal switch
    {
        Goal.TwoLines => lines >= 2 ? 1 : 0,
        Goal.ThreeLines => lines >= 3 ? 1 : 0,
        _ => lines >= 1 ? 1 : 0,
    };

    /// <summary>Goal value of a finished outcome distribution (exactly 0,1,2,3 lines).</summary>
    public double ValueOf(ReadOnlySpan<double> d) =>
        d[0] * Reward(Goal, 0) + d[1] * Reward(Goal, 1) + d[2] * Reward(Goal, 2) + d[3] * Reward(Goal, 3);

    public double Value(int mask, int points) => value[Clamp(points)][mask];

    public ReadOnlySpan<double> Dist(int mask, int points) => dist[Clamp(points)].AsSpan(mask * 4, 4);

    public bool ShuffleAllowed(int mask, int points) =>
        TailsMath.CanShuffleAt(TailsMath.Count(mask)) && Clamp(points) >= TailsMath.ShuffleCost;

    /// <summary>Value and distribution if you shuffle right now, then keep following the plan.</summary>
    public (double Value, double[] Dist) ShuffleNow(int mask, int points)
    {
        var k = TailsMath.Count(mask);
        var q = Clamp(points) - TailsMath.ShuffleCost;
        return (shuffleValue[q][k], shuffleDist[q].AsSpan(k * 4, 4).ToArray());
    }

    /// <summary>Value and distribution if you hold now (take the next random sticker), then follow the plan.</summary>
    public (double Value, double[] Dist) HoldNow(int mask, int points)
    {
        var p = Clamp(points);
        var k = TailsMath.Count(mask);
        var d = new double[4];
        if (k >= TailsMath.MaxStickers)
        {
            var lines = Math.Min(TailsMath.CompletedLines(mask), 3);
            d[lines] = 1;
            return (Reward(Goal, lines), d);
        }

        var open = TailsMath.Cells - k;
        var v = 0.0;
        for (var bit = 0; bit < TailsMath.Cells; bit++)
        {
            if ((mask & (1 << bit)) != 0) continue;
            var child = mask | (1 << bit);
            v += value[p][child] / open;
            for (var i = 0; i < 4; i++) d[i] += dist[p][child * 4 + i] / open;
        }
        return (v, d);
    }

    /// <summary>True when shuffling now beats holding under this plan.</summary>
    public bool ShouldShuffle(int mask, int points) =>
        ShuffleAllowed(mask, points) && ShuffleNow(mask, points).Value > HoldNow(mask, points).Value + Eps;

    private int Clamp(int points) => Math.Clamp(points, 0, MaxPoints);

    private void Solve(int p)
    {
        var v = value[p] = new double[1 << TailsMath.Cells];
        var d = dist[p] = new double[(1 << TailsMath.Cells) * 4];
        var sv = shuffleValue[p] = new double[TailsMath.MaxStickers + 1];
        var sd = shuffleDist[p] = new double[(TailsMath.MaxStickers + 1) * 4];

        for (var k = TailsMath.MaxStickers; k >= 0; k--)
        {
            var canShuffle = TailsMath.CanShuffleAt(k) && p >= TailsMath.ShuffleCost;
            var q = p - TailsMath.ShuffleCost;

            foreach (var mask in TailsMath.MasksByCount[k])
            {
                var o = mask * 4;
                if (k == TailsMath.MaxStickers)
                {
                    var lines = Math.Min(TailsMath.CompletedLines(mask), 3);
                    v[mask] = Reward(Goal, lines);
                    d[o + lines] = 1;
                    continue;
                }

                // Hold: average over the next random sticker.
                var open = TailsMath.Cells - k;
                double hold = 0, h0 = 0, h1 = 0, h2 = 0, h3 = 0;
                for (var bit = 0; bit < TailsMath.Cells; bit++)
                {
                    if ((mask & (1 << bit)) != 0) continue;
                    var child = mask | (1 << bit);
                    var co = child * 4;
                    hold += v[child];
                    h0 += d[co];
                    h1 += d[co + 1];
                    h2 += d[co + 2];
                    h3 += d[co + 3];
                }
                hold /= open;

                if (canShuffle && shuffleValue[q][k] > hold + Eps)
                {
                    v[mask] = shuffleValue[q][k];
                    for (var i = 0; i < 4; i++) d[o + i] = shuffleDist[q][k * 4 + i];
                }
                else
                {
                    v[mask] = hold;
                    d[o] = h0 / open;
                    d[o + 1] = h1 / open;
                    d[o + 2] = h2 / open;
                    d[o + 3] = h3 / open;
                }
            }

            // What a shuffle at k stickers is worth with p points left afterwards.
            var masks = TailsMath.MasksByCount[k];
            foreach (var mask in masks)
            {
                sv[k] += v[mask];
                for (var i = 0; i < 4; i++) sd[k * 4 + i] += d[mask * 4 + i];
            }
            sv[k] /= masks.Length;
            for (var i = 0; i < 4; i++) sd[k * 4 + i] /= masks.Length;
        }
    }
}
