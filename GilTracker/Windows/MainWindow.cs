using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GilTracker.Tracking;

namespace GilTracker.Windows;

/// <summary>
/// A merchant's ledger: the same parchment paper as Khloe's journal, gold coin accents,
/// a mint card for what came in and a peach card for what went out.
/// </summary>
public sealed class MainWindow : Window
{
    private static readonly Vector4 Paper = Hex(0xF7EDD6);
    private static readonly Vector4 PaperDeep = Hex(0xEADBB9);
    private static readonly Vector4 PaperHover = Hex(0xE2CC9F);
    private static readonly Vector4 PaperPress = Hex(0xDBC28F);
    private static readonly Vector4 SlotEdge = Hex(0xCDAE78);
    private static readonly Vector4 Cover = Hex(0xD8BC86);
    private static readonly Vector4 CoverEdge = Hex(0xB88E55);
    private static readonly Vector4 Ink = Hex(0x4A3222);
    private static readonly Vector4 InkSoft = Hex(0x86694F);
    private static readonly Vector4 GoldInk = Hex(0x8F6210);
    private static readonly Vector4 Gold = Hex(0xD9A54A);
    private static readonly Vector4 GoldLight = Hex(0xF2D28A);
    private static readonly Vector4 GoldDeep = Hex(0xB07E22);
    private static readonly Vector4 Teal = Hex(0x2E7D72);
    private static readonly Vector4 Mint = Hex(0xDDF1EC);
    private static readonly Vector4 MintEdge = Hex(0x8CC3B8);
    private static readonly Vector4 Coral = Hex(0xC4502F);
    private static readonly Vector4 Peach = Hex(0xFBE4D8);
    private static readonly Vector4 PeachEdge = Hex(0xE3A58E);
    private static readonly Vector4 White = Hex(0xFFFFFF);

    private static readonly (Period Period, string Label)[] Periods =
    [
        (Period.Session, "Session"),
        (Period.Today, "Today"),
        (Period.Week, "7 days"),
        (Period.AllTime, "All time"),
    ];

    private const float BaseWidth = 360f;
    private const int ChartDays = 14;

    private readonly Plugin plugin;
    private int pushedColors;

    public MainWindow(Plugin plugin)
        : base("###GilTrackerMain",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
    }

    private static float S => ImGuiHelpers.GlobalScale;
    private static float ContentWidth => BaseWidth * S;

    // Window chrome

    public override void PreDraw()
    {
        pushedColors = 0;
        PushColor(ImGuiCol.WindowBg, Paper);
        PushColor(ImGuiCol.PopupBg, Paper);
        PushColor(ImGuiCol.TitleBg, Cover);
        PushColor(ImGuiCol.TitleBgActive, Cover);
        PushColor(ImGuiCol.TitleBgCollapsed, Cover);
        PushColor(ImGuiCol.Border, CoverEdge);
        PushColor(ImGuiCol.Text, Ink);
        PushColor(ImGuiCol.TextDisabled, InkSoft);
        PushColor(ImGuiCol.Button, PaperDeep);
        PushColor(ImGuiCol.ButtonHovered, PaperHover);
        PushColor(ImGuiCol.ButtonActive, PaperPress);
        PushColor(ImGuiCol.Separator, SlotEdge);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f * S);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f * S);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * S);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f * S);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(pushedColors);
        pushedColors = 0;
    }

    private void PushColor(ImGuiCol col, Vector4 value)
    {
        ImGui.PushStyleColor(col, value);
        pushedColors++;
    }

    // Layout

    public override void Draw()
    {
        var tracker = plugin.Tracker;
        var config = plugin.Configuration;

        DrawHeader(tracker.Ledger?.Name);

        if (tracker.Ledger == null)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ContentWidth);
            ImGui.TextWrapped("Log in and your gil will start counting here.");
            ImGui.PopTextWrapPos();
            ImGui.Dummy(new Vector2(ContentWidth, 0));
            return;
        }

        DrawPeriodTabs(config);
        ImGui.Dummy(new Vector2(0, 2f * S));

        var totals = tracker.TotalsFor(config.Period);
        DrawBigNumber(totals, config.Period, tracker.SessionStartUtc);
        ImGui.Dummy(new Vector2(0, 2f * S));

        Card("Where it came from", Mint, MintEdge, Teal, totals.Earned, Classifier.EarnedLabel,
            "Nothing yet. Go make some gil!");
        ImGui.Dummy(new Vector2(0, 2f * S));
        Card("Where it went", Peach, PeachEdge, Coral, totals.Spent, Classifier.SpentLabel,
            "Nothing spent. Your wallet thanks you.");
        ImGui.Dummy(new Vector2(0, 4f * S));

        DrawChart(tracker.Ledger);
        DrawWallet(tracker);

        if (config.ShowRecent) DrawRecent(tracker.Ledger);
    }

    private void DrawHeader(string? name)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineH = 24f * S;
        var pos = ImGui.GetCursorScreenPos();
        DrawCoin(dl, pos + new Vector2(10f * S, lineH / 2f), 10f * S);
        ImGui.Dummy(new Vector2(24f * S, lineH));
        ImGui.SameLine(0, 6f * S);

        using (plugin.TitleFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, GoldInk))
            ImGui.TextUnformatted("Gil Tracker");

        if (!string.IsNullOrEmpty(name))
        {
            var w = ImGui.CalcTextSize(name).X;
            ImGui.SameLine(ContentWidth - w + ImGui.GetStyle().WindowPadding.X);
            using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
                ImGui.TextUnformatted(name);
        }

        DottedRule();
    }

    private void DrawPeriodTabs(Configuration config)
    {
        var dl = ImGui.GetWindowDrawList();
        var gap = 6f * S;
        var size = new Vector2((ContentWidth - 3 * gap) / 4f, 28f * S);

        for (var i = 0; i < Periods.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, gap);
            var (period, label) = Periods[i];

            var pos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton($"##period{i}", size) && config.Period != period)
            {
                config.Period = period;
                config.Save();
            }

            var selected = config.Period == period;
            var hovered = ImGui.IsItemHovered();
            var bg = selected ? GoldDeep : hovered ? PaperHover : PaperDeep;

            dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(bg), size.Y / 2f);
            if (!selected) dl.AddRect(pos, pos + size, ImGui.GetColorU32(SlotEdge), size.Y / 2f, ImDrawFlags.None, 1f * S);

            var text = ImGui.CalcTextSize(label);
            dl.AddText(pos + (size - text) / 2f, ImGui.GetColorU32(selected ? White : Ink), label);
        }
    }

    private void DrawBigNumber(Totals totals, Period period, DateTime sessionStartUtc)
    {
        var net = totals.Net;
        using (plugin.BigFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, net >= 0 ? Teal : Coral))
            ImGui.TextUnformatted(net >= 0 ? $"Up {Gil(net)} gil" : $"Down {Gil(-net)} gil");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Exactly {Exact(Math.Abs(net))} gil {(net >= 0 ? "up" : "down")}\nEarned {Exact(totals.EarnedTotal)}, spent {Exact(totals.SpentTotal)}");

        using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
        {
            ImGui.TextUnformatted($"Earned {Gil(totals.EarnedTotal)}   Spent {Gil(totals.SpentTotal)}");

            if (period == Period.Session)
            {
                var hours = (DateTime.UtcNow - sessionStartUtc).TotalHours;
                ImGui.TextUnformatted(hours >= 1.0 / 6.0
                    ? (net >= 0
                        ? $"About {Gil((long)(net / hours))} gil an hour this session"
                        : $"Spending about {Gil((long)(-net / hours))} gil an hour this session")
                    : "Rate shows up after 10 minutes of play");
            }
        }
    }

    // Cards

    private static void Card(string title, Vector4 bg, Vector4 edge, Vector4 accent,
        Dictionary<Source, long> amounts, Func<Source, string> label, string empty)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = 12f * S;
        var cw = ContentWidth;
        var start = ImGui.GetCursorScreenPos();
        var localLeft = ImGui.GetCursorPosX();
        var innerRight = localLeft + cw - pad;

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(start + new Vector2(0, pad));
        ImGui.Indent(pad);

        using (ImRaii.PushColor(ImGuiCol.Text, accent))
            ImGui.TextUnformatted(title);

        var rows = amounts.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ToList();
        if (rows.Count == 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
                ImGui.TextUnformatted(empty);
        }
        else
        {
            var max = rows[0].Value;
            var total = rows.Sum(kv => kv.Value);
            foreach (var (source, amount) in rows)
            {
                ImGui.TextUnformatted(label(source));
                var text = Gil(amount);
                var w = ImGui.CalcTextSize(text).X;
                ImGui.SameLine(innerRight - w);
                ImGui.TextUnformatted(text);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{Exact(amount)} gil, {(double)amount / total:P0} of this list");

                var barPos = ImGui.GetCursorScreenPos();
                var barW = cw - pad * 2;
                var barH = 4f * S;
                dl.AddRectFilled(barPos, barPos + new Vector2(barW, barH), ImGui.GetColorU32(edge with { W = 0.35f }), barH / 2f);
                dl.AddRectFilled(barPos, barPos + new Vector2(MathF.Max(barH, barW * amount / max), barH), ImGui.GetColorU32(accent), barH / 2f);
                ImGui.Dummy(new Vector2(barW, barH));
            }
        }

        ImGui.Unindent(pad);
        var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y + pad;
        var cardMax = new Vector2(start.X + cw, bottom);

        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(start, cardMax, ImGui.GetColorU32(bg), 12f * S);
        dl.AddRect(start, cardMax, ImGui.GetColorU32(edge), 12f * S, ImDrawFlags.None, 1.5f * S);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(start.X, bottom));
        ImGui.Dummy(new Vector2(cw, 0));
    }

    // Chart: net gil per day, teal above the line, coral below

    private static void DrawChart(Ledger ledger)
    {
        var values = ledger.DailyNet(DateTime.Now, ChartDays);
        var peak = Math.Max(1, values.Select(v => Math.Abs(v)).DefaultIfEmpty(0).Max());

        using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
            ImGui.TextUnformatted("Last 14 days");

        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var height = 64f * S;
        var cw = ContentWidth;
        var mid = start.Y + height / 2f;
        var slot = cw / ChartDays;
        var barW = slot * 0.6f;

        dl.AddRectFilled(start, start + new Vector2(cw, height), ImGui.GetColorU32(PaperDeep with { W = 0.6f }), 8f * S);
        dl.AddLine(new Vector2(start.X + 4f * S, mid), new Vector2(start.X + cw - 4f * S, mid), ImGui.GetColorU32(SlotEdge), 1f * S);

        for (var i = 0; i < ChartDays; i++)
        {
            var v = values[i];
            if (v == 0) continue;
            var h = MathF.Max(2f * S, (float)((double)Math.Abs(v) / peak) * (height / 2f - 6f * S));
            var x = start.X + slot * i + (slot - barW) / 2f;
            var col = ImGui.GetColorU32(v > 0 ? Teal : Coral);
            if (v > 0)
                dl.AddRectFilled(new Vector2(x, mid - h), new Vector2(x + barW, mid), col, 2f * S);
            else
                dl.AddRectFilled(new Vector2(x, mid), new Vector2(x + barW, mid + h), col, 2f * S);
        }

        // Today's slot gets a little gold marker.
        var todayX = start.X + slot * (ChartDays - 1) + slot / 2f;
        dl.AddCircleFilled(new Vector2(todayX, start.Y + height - 5f * S), 2.5f * S, ImGui.GetColorU32(Gold));

        ImGui.Dummy(new Vector2(cw, height));

        var mouse = ImGui.GetMousePos();
        if (ImGui.IsItemHovered())
        {
            var i = Math.Clamp((int)((mouse.X - start.X) / slot), 0, ChartDays - 1);
            var date = DateTime.Now.Date.AddDays(-(ChartDays - 1 - i));
            var v = values[i];
            ImGui.SetTooltip(v == 0 ? $"{date:ddd MMM d}: even" : v > 0 ? $"{date:ddd MMM d}: up {Gil(v)} gil" : $"{date:ddd MMM d}: down {Gil(-v)} gil");
        }
    }

    // Wallet line and recent feed

    private void DrawWallet(Tracker tracker)
    {
        DottedRule();
        var config = plugin.Configuration;

        using (ImRaii.PushColor(ImGuiCol.Text, GoldInk))
            ImGui.TextUnformatted($"On hand: {Gil(tracker.CurrentGil ?? 0)}");

        using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ContentWidth);
            ImGui.TextWrapped(tracker.HasSeenRetainers
                ? $"Retainers: {Gil(tracker.RetainerGil)} as of your last bell visit. Market sales count when you check your retainers."
                : "Visit a summoning bell once so your market board sales can be counted.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0, 2f * S));
        using (ImRaii.PushColor(ImGuiCol.Text, GoldDeep))
            ImGui.TextUnformatted(config.ShowRecent ? "Hide recent" : "Show recent");
        if (ImGui.IsItemHovered())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), max, ImGui.GetColorU32(GoldDeep), 1f * S);
        }
        if (ImGui.IsItemClicked())
        {
            config.ShowRecent = !config.ShowRecent;
            config.Save();
        }
    }

    private static void DrawRecent(Ledger ledger)
    {
        if (ledger.Recent.Count == 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
                ImGui.TextUnformatted("No gil changes yet.");
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var e in ledger.Recent.Take(12))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
                ImGui.TextUnformatted(Ago(now - e.Utc));
            ImGui.SameLine(70f * S);
            ImGui.TextUnformatted(e.Amount > 0 ? Classifier.EarnedLabel(e.Source) : Classifier.SpentLabel(e.Source));
            var text = e.Amount > 0 ? "+" + Gil(e.Amount) : Gil(-e.Amount);
            var w = ImGui.CalcTextSize(text).X;
            ImGui.SameLine(ContentWidth - w + ImGui.GetStyle().WindowPadding.X);
            using (ImRaii.PushColor(ImGuiCol.Text, e.Amount > 0 ? Teal : Coral))
                ImGui.TextUnformatted(text);
        }
    }

    // Helpers

    private static void DottedRule()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var y = pos.Y + 4f * S;
        var col = ImGui.GetColorU32(SlotEdge);
        for (var x = 2f * S; x < ContentWidth; x += 7f * S)
            dl.AddCircleFilled(new Vector2(pos.X + x, y), 1.3f * S, col);
        ImGui.Dummy(new Vector2(ContentWidth, 8f * S));
    }

    private static void DrawCoin(ImDrawListPtr dl, Vector2 c, float r)
    {
        dl.AddCircleFilled(c, r, ImGui.GetColorU32(GoldDeep));
        dl.AddCircleFilled(c, r * 0.82f, ImGui.GetColorU32(Gold));
        dl.AddCircleFilled(c + new Vector2(-r * 0.3f, -r * 0.3f), r * 0.22f, ImGui.GetColorU32(GoldLight));
        dl.AddCircle(c, r * 0.55f, ImGui.GetColorU32(GoldDeep), 0, 1.2f * S);
    }

    /// <summary>Short form for display: 98,500 then 345K, 2.45M, 12.3M, 1.23B. Rounds down so it never overstates.</summary>
    private static string Gil(long v)
    {
        var sign = v < 0 ? "-" : string.Empty;
        var a = Math.Abs((double)v);
        if (a >= 1e9) return sign + Floor(a / 1e9, 2) + "B";
        if (a >= 1e7) return sign + Floor(a / 1e6, 1) + "M";
        if (a >= 1e6) return sign + Floor(a / 1e6, 2) + "M";
        if (a >= 1e5) return sign + Floor(a / 1e3, 0) + "K";
        return Exact(v);
    }

    private static string Floor(double x, int decimals)
    {
        var f = Math.Pow(10, decimals);
        return (Math.Floor(x * f) / f).ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), CultureInfo.InvariantCulture);
    }

    private static string Exact(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    private static string Ago(TimeSpan t)
    {
        if (t.TotalMinutes < 1) return "now";
        if (t.TotalHours < 1) return $"{(int)t.TotalMinutes}m ago";
        if (t.TotalDays < 1) return $"{(int)t.TotalHours}h ago";
        return $"{(int)t.TotalDays}d ago";
    }

    private static Vector4 Hex(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        1f);
}
