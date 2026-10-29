using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using Lumina.Excel.Sheets;

namespace GilTracker.Tracking;

/// <summary>
/// Watches your gil and your retainers' gil, works out where each change came from,
/// and keeps a per character ledger on disk. Read only: it never touches the game.
/// </summary>
public sealed unsafe class Tracker : IDisposable
{
    // Windows that tell us what a gil change was for.
    private const string RetainerBank = "Bank";
    private const string CompanyChest = "FreeCompanyChest";
    private const string TradeWindow = "Trade";
    private const string MarketSearch = "ItemSearch";
    private const string MarketResults = "ItemSearchResult";
    private const string RepairWindow = "Repair";
    private const string ShopWindow = "Shop";
    private const string GearShop = "InclusionShop";
    private const string QuestReward = "JournalResult";
    private const string MailWindow = "LetterViewer";
    private static readonly string[] HousingWindows = ["HousingSignBoard", "MansionSelectRoom"];
    private static readonly string[] RetainerWindows =
        ["RetainerList", "InventoryRetainer", "InventoryRetainerLarge", "RetainerSellList", "RetainerSell"];

    private static readonly string[] Watched =
    [
        RetainerBank, CompanyChest, TradeWindow, MarketSearch, MarketResults,
        RepairWindow, ShopWindow, GearShop, QuestReward, MailWindow, .. RetainerWindows, .. HousingWindows,
    ];

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan WindowGrace = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TeleportGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FateGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CofferGrace = TimeSpan.FromSeconds(10);

    // ContentType row for treasure map dungeons (The Aquapolis, The Lost Canals of Uznair and friends).
    private const uint TreasureHuntContentType = 9;
    private static readonly TimeSpan LoginSettle = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly ICondition condition;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly ITargetManager targets;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly string folder;

    private readonly Dictionary<string, DateTime> windowSeen = new();
    private readonly HashSet<string> windowOpen = new();

    private DateTime nextPoll = DateTime.MinValue;
    private DateTime nextSave = DateTime.MinValue;
    private DateTime settleUntil = DateTime.MinValue;
    private DateTime teleportSeen = DateTime.MinValue;
    private DateTime fateSeen = DateTime.MinValue;
    private DateTime cofferSeen = DateTime.MinValue;
    private uint treasureCheckedTerritory = uint.MaxValue;
    private bool inTreasureDuty;
    private long? lastGil;
    private bool dirty;

    public Tracker(IClientState clientState, IPlayerState playerState, ICondition condition,
        IAddonLifecycle addonLifecycle, ITargetManager targets, IDataManager data, IPluginLog log, string folder)
    {
        this.targets = targets;
        this.data = data;
        this.clientState = clientState;
        this.playerState = playerState;
        this.condition = condition;
        this.addonLifecycle = addonLifecycle;
        this.log = log;
        this.folder = folder;

        addonLifecycle.RegisterListener(AddonEvent.PostSetup, Watched, OnWindowOpened);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, Watched, OnWindowClosed);
    }

    /// <summary>The logged in character's ledger, or null when logged out.</summary>
    public Ledger? Ledger { get; private set; }

    /// <summary>Totals since you logged in (or since the plugin loaded).</summary>
    public Totals Session { get; private set; } = new();

    public DateTime SessionStartUtc { get; private set; } = DateTime.UtcNow;

    public long? CurrentGil => lastGil;

    /// <summary>Gil your retainers held the last time you visited a summoning bell.</summary>
    public long RetainerGil => Ledger?.RetainerGil.Values.Sum() ?? 0;

    public bool HasSeenRetainers => Ledger is { RetainerGil.Count: > 0 };

    public Totals TotalsFor(Period period)
    {
        if (Ledger == null) return new Totals();
        var now = DateTime.Now;
        return period switch
        {
            Period.Session => Session,
            Period.Today => Ledger.Sum(now, 1),
            Period.Week => Ledger.Sum(now, 7),
            _ => Ledger.Sum(now, 0),
        };
    }

    public void Tick()
    {
        var now = DateTime.UtcNow;
        if (now < nextPoll) return;
        nextPoll = now + PollInterval;

        try
        {
            Poll(now);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Gil Tracker could not read gil this tick.");
        }

        if (dirty && now >= nextSave) Save();
    }

    private void Poll(DateTime now)
    {
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || playerState.ContentId == 0)
        {
            if (Ledger != null)
            {
                Save();
                Ledger = null;
            }
            lastGil = null;
            return;
        }

        var contentId = playerState.ContentId;
        if (Ledger == null || Ledger.ContentId != contentId)
        {
            if (Ledger != null) Save();
            Ledger = Load(contentId);
            Ledger.Name = playerState.CharacterName;
            Session = new Totals();
            SessionStartUtc = now;
            lastGil = null;
            settleUntil = now + LoginSettle;
        }

        var loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        if (loading || condition[ConditionFlag.Casting]) teleportSeen = now;

        var fates = FateManager.Instance();
        if (fates != null && fates->CurrentFate != null) fateSeen = now;

        var target = targets.Target;
        if (target != null && target.Name.TextValue.Contains("Coffer", StringComparison.OrdinalIgnoreCase)) cofferSeen = now;

        UpdateTreasureDuty();

        // Inventory can read as empty while the game is still loading; wait it out.
        if (now < settleUntil || loading) return;

        var ctx = Context(now);

        var inventory = InventoryManager.Instance();
        if (inventory != null)
        {
            long gil = inventory->GetGil();
            if (lastGil is { } previous && gil != previous)
            {
                var delta = gil - previous;
                if (Classifier.ForPlayer(delta, ctx) is { } source) Record(delta, source, now);
            }
            lastGil = gil;
        }

        var retainers = RetainerManager.Instance();
        if (retainers != null && retainers->IsReady)
        {
            var count = retainers->GetRetainerCount();
            for (uint i = 0; i < count; i++)
            {
                var r = retainers->GetRetainerBySortedIndex(i);
                if (r == null || r->RetainerId == 0) continue;

                long held = r->Gil;
                if (Ledger.RetainerGil.TryGetValue(r->RetainerId, out var before))
                {
                    if (held == before) continue;
                    if (Classifier.ForRetainer(held - before, ctx) is { } source) Record(held - before, source, now);
                }

                Ledger.RetainerGil[r->RetainerId] = held;
                dirty = true;
            }
        }
    }

    private GilContext Context(DateTime now) => new(
        RetainerBank: Active(RetainerBank, now),
        CompanyChest: Active(CompanyChest, now),
        Trade: Active(TradeWindow, now),
        MarketBoard: Active(MarketSearch, now) || Active(MarketResults, now),
        Repair: Active(RepairWindow, now),
        Shop: Active(ShopWindow, now) || Active(GearShop, now),
        RetainerMenu: RetainerWindows.Any(w => Active(w, now)),
        Quest: Active(QuestReward, now),
        Mail: Active(MailWindow, now),
        Teleporting: now - teleportSeen < TeleportGrace,
        InDuty: condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56],
        TreasureDuty: inTreasureDuty,
        Fate: now - fateSeen < FateGrace,
        TreasureCoffer: now - cofferSeen < CofferGrace,
        Housing: HousingWindows.Any(w => Active(w, now)));

    private void UpdateTreasureDuty()
    {
        var territory = clientState.TerritoryType;
        if (territory == treasureCheckedTerritory) return;
        treasureCheckedTerritory = territory;
        inTreasureDuty = false;

        if (data.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var row))
        {
            var cfc = row.ContentFinderCondition;
            inTreasureDuty = cfc.RowId != 0 && cfc.IsValid && cfc.Value.ContentType.RowId == TreasureHuntContentType;
        }
    }

    private bool Active(string window, DateTime now) =>
        windowOpen.Contains(window) || (windowSeen.TryGetValue(window, out var closed) && now - closed < WindowGrace);

    private void Record(long amount, Source source, DateTime now)
    {
        Ledger!.Record(amount, source, now);
        Session.Add(amount, source);
        dirty = true;
    }

    private void OnWindowOpened(AddonEvent type, AddonArgs args)
    {
        windowOpen.Add(args.AddonName);
        windowSeen[args.AddonName] = DateTime.UtcNow;
    }

    private void OnWindowClosed(AddonEvent type, AddonArgs args)
    {
        windowOpen.Remove(args.AddonName);
        windowSeen[args.AddonName] = DateTime.UtcNow;
    }

    // Saving and loading

    private string PathFor(ulong contentId) => Path.Combine(folder, $"ledger-{contentId:X16}.json");

    private Ledger Load(ulong contentId)
    {
        var path = PathFor(contentId);
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<Ledger>(File.ReadAllText(path), Json);
                if (loaded != null)
                {
                    loaded.ContentId = contentId;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Gil Tracker could not read its saved history; keeping a backup and starting fresh.");
            try { File.Copy(path, path + ".bak", true); } catch { /* best effort */ }
        }

        return new Ledger { ContentId = contentId };
    }

    public void Save()
    {
        nextSave = DateTime.UtcNow + SaveInterval;
        if (Ledger == null || !dirty) return;

        try
        {
            Directory.CreateDirectory(folder);
            var path = PathFor(Ledger.ContentId);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Ledger, Json));
            File.Move(temp, path, true);
            dirty = false;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Gil Tracker could not save its history.");
        }
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, Watched, OnWindowOpened);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, Watched, OnWindowClosed);
        Save();
    }
}
