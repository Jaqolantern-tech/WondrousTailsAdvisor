using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GilTracker.Tracking;

/// <summary>Where gil came from or went. Each source can appear on both sides.</summary>
public enum Source
{
    MarketBoard,
    Vendor,
    Duties,
    Quests,
    Trades,
    Mail,
    Repairs,
    Teleports,
    Other,
    Fates,
    TreasureMaps,
    Housing,
}

public enum Period
{
    Session,
    Today,
    Week,
    AllTime,
}

public sealed class GilEvent
{
    public DateTime Utc { get; set; }
    public long Amount { get; set; }
    public Source Source { get; set; }
}

/// <summary>Earned and spent gil for one bucket of time, by source. Amounts are always positive.</summary>
public sealed class Totals
{
    public Dictionary<Source, long> Earned { get; set; } = new();
    public Dictionary<Source, long> Spent { get; set; } = new();

    [JsonIgnore]
    public long EarnedTotal => Earned.Values.Sum();
    [JsonIgnore]
    public long SpentTotal => Spent.Values.Sum();
    [JsonIgnore]
    public long Net => EarnedTotal - SpentTotal;

    public void Add(long amount, Source source)
    {
        if (amount == 0) return;
        var map = amount > 0 ? Earned : Spent;
        map[source] = map.GetValueOrDefault(source) + Math.Abs(amount);
    }

    public void AddAll(Totals other)
    {
        foreach (var (k, v) in other.Earned) Earned[k] = Earned.GetValueOrDefault(k) + v;
        foreach (var (k, v) in other.Spent) Spent[k] = Spent.GetValueOrDefault(k) + v;
    }
}

/// <summary>One character's gil history, saved to disk between sessions.</summary>
public sealed class Ledger
{
    public const int RecentCap = 40;

    public int Version { get; set; } = 1;
    public ulong ContentId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Last gil each retainer was seen holding, by retainer id.</summary>
    public Dictionary<ulong, long> RetainerGil { get; set; } = new();

    /// <summary>Daily totals keyed by local date, yyyy-MM-dd.</summary>
    public Dictionary<string, Totals> Days { get; set; } = new();

    /// <summary>Newest first.</summary>
    public List<GilEvent> Recent { get; set; } = new();

    public static string DayKey(DateTime local) => local.ToString("yyyy-MM-dd");

    public void Record(long amount, Source source, DateTime utc)
    {
        if (amount == 0) return;
        var key = DayKey(utc.ToLocalTime());
        if (!Days.TryGetValue(key, out var day)) Days[key] = day = new Totals();
        day.Add(amount, source);

        Recent.Insert(0, new GilEvent { Utc = utc, Amount = amount, Source = source });
        if (Recent.Count > RecentCap) Recent.RemoveRange(RecentCap, Recent.Count - RecentCap);
    }

    /// <summary>Totals over the last <paramref name="days"/> local days, today included. 0 means all time.</summary>
    public Totals Sum(DateTime nowLocal, int days)
    {
        var result = new Totals();
        if (days <= 0)
        {
            foreach (var t in Days.Values) result.AddAll(t);
            return result;
        }

        for (var i = 0; i < days; i++)
        {
            if (Days.TryGetValue(DayKey(nowLocal.Date.AddDays(-i)), out var t)) result.AddAll(t);
        }
        return result;
    }

    /// <summary>Net gil per day for the last <paramref name="days"/> days, oldest first.</summary>
    public long[] DailyNet(DateTime nowLocal, int days)
    {
        var result = new long[days];
        for (var i = 0; i < days; i++)
        {
            var date = nowLocal.Date.AddDays(-(days - 1 - i));
            result[i] = Days.TryGetValue(DayKey(date), out var t) ? t.Net : 0;
        }
        return result;
    }
}
