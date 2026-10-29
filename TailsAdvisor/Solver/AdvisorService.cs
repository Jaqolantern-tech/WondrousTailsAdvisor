using System;
using System.Threading;
using System.Threading.Tasks;

namespace TailsAdvisor.Solver;

/// <summary>
/// Builds the shuffle plan on a background thread and keeps the latest analysis ready for the UI.
/// Solving one goal takes a fraction of a second; the result is cached until the goal changes.
/// </summary>
public sealed class AdvisorService : IDisposable
{
    private readonly object gate = new();
    private ShufflePolicy? policy;
    private Task? building;
    private Goal buildingGoal;
    private (int Mask, int Points, Goal Goal)? lastKey;
    private volatile Analysis? latest;
    private int disposed;

    public Analysis? Latest => latest;

    public bool IsBusy
    {
        get
        {
            lock (gate) return building is { IsCompleted: false };
        }
    }

    /// <summary>Call whenever the board or settings may have changed. Cheap when nothing changed.</summary>
    public void Request(int mask, int points, Goal goal)
    {
        if (Volatile.Read(ref disposed) != 0) return;

        var key = (mask, points, goal);
        lock (gate)
        {
            if (lastKey.HasValue && lastKey.Value.Equals(key) && latest != null) return;

            if (policy != null && policy.Goal == goal)
            {
                lastKey = key;
                latest = Advisor.Analyze(policy, mask, points);
                return;
            }

            if (building is { IsCompleted: false } && buildingGoal == goal) return;

            buildingGoal = goal;
            building = Task.Run(() =>
            {
                var built = new ShufflePolicy(goal);
                lock (gate)
                {
                    if (Volatile.Read(ref disposed) != 0) return;
                    policy = built;
                    lastKey = null; // force a fresh analysis on the next request
                }
            });
        }
    }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);
}
