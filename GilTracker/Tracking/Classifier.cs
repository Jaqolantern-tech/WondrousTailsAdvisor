namespace GilTracker.Tracking;

/// <summary>What the game was doing when your gil changed.</summary>
public readonly record struct GilContext(
    bool RetainerBank,
    bool CompanyChest,
    bool Trade,
    bool MarketBoard,
    bool Repair,
    bool Shop,
    bool Quest,
    bool Mail,
    bool Teleporting,
    bool InDuty);

public static class Classifier
{
    /// <summary>
    /// Picks the source for a change in your own gil. Returns null for moves that are not
    /// really earning or spending, like handing gil to a retainer or the company chest.
    /// </summary>
    public static Source? ForPlayer(long delta, GilContext c)
    {
        if (delta == 0) return null;
        if (c.RetainerBank || c.CompanyChest) return null;
        if (c.Trade) return Source.Trades;
        if (c.MarketBoard) return Source.MarketBoard;
        if (c.Repair) return Source.Repairs;
        if (c.Shop) return Source.Vendor;
        if (c.Quest) return Source.Quests;
        if (c.Mail) return Source.Mail;
        if (c.Teleporting && delta < 0) return Source.Teleports;
        if (c.InDuty) return Source.Duties;
        return Source.Other;
    }

    /// <summary>
    /// A retainer's gil only goes up when its market listings sell, and only goes down
    /// when you withdraw it, so increases are sales and everything else is a transfer.
    /// </summary>
    public static Source? ForRetainer(long delta, GilContext c)
    {
        if (delta <= 0 || c.RetainerBank) return null;
        return Source.MarketBoard;
    }

    public static string EarnedLabel(Source s) => s switch
    {
        Source.MarketBoard => "Market board sales",
        Source.Vendor => "Sold to vendors",
        Source.Duties => "Duties and loot",
        Source.Quests => "Quests and leves",
        Source.Trades => "Trades",
        Source.Mail => "Mail",
        Source.Repairs => "Repairs",
        Source.Teleports => "Teleports",
        _ => "Everything else",
    };

    public static string SpentLabel(Source s) => s switch
    {
        Source.MarketBoard => "Market board buys",
        Source.Vendor => "Vendor shopping",
        Source.Duties => "Duties",
        Source.Quests => "Quests",
        Source.Trades => "Trades",
        Source.Mail => "Mail",
        Source.Repairs => "Repairs",
        Source.Teleports => "Teleports",
        _ => "Everything else",
    };
}
