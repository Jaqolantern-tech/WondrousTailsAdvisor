using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TailsAdvisor.Game;
using TailsAdvisor.Solver;

namespace TailsAdvisor.Windows;

/// <summary>
/// The advisor window, styled after Khloe's sticker journal: parchment paper, a coral title,
/// tan sticker slots, round orange stickers, and a mint card like the journal's reward list.
/// </summary>
public sealed class MainWindow : Window
{
    // Journal palette
    private static Theme Th = Theme.All[0];

    private static Vector4 Paper => Theme.V(Th.Background);
    private static Vector4 PaperDeep => Theme.V(Th.Surface);
    private static Vector4 PaperHover => Theme.V(Th.SurfaceHover);
    private static Vector4 PaperPress => Theme.V(Th.SurfacePress);
    private static Vector4 SlotEdge => Theme.V(Th.Line);
    private static Vector4 Cover => Theme.V(Th.Header);
    private static Vector4 CoverEdge => Theme.V(Th.Border);
    private static Vector4 Ink => Theme.V(Th.Text);
    private static Vector4 InkSoft => Theme.V(Th.TextSoft);
    private static Vector4 Coral => Theme.V(Th.Accent);
    private static Vector4 Sticker => Theme.V(Th.Sticker);
    private static Vector4 StickerRim => Theme.V(Th.StickerRim);
    private static Vector4 Mint => Theme.V(Th.Card);
    private static Vector4 MintEdge => Theme.V(Th.CardEdge);
    private static Vector4 MintDeep => Theme.V(Th.CardDeep);
    private static Vector4 Teal => Theme.V(Th.Good);
    private static Vector4 Amber => Theme.V(Th.Warn);
    private static Vector4 Gold => Theme.V(Th.Star);
    private static Vector4 White => Theme.V(Th.OnAccent);

    private static readonly string[] GoalTabs = ["Any line", "2 lines", "Jackpot"];

    private const float BaseWidth = 340f;

    private readonly Plugin plugin;
    private int pushedColors;
    private int pushedVars;

    public MainWindow(Plugin plugin)
        : base("###TailsAdvisorMain",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
    }

    private static float S => ImGuiHelpers.GlobalScale;
    private static float ContentWidth => BaseWidth * S;

    // Window chrome

    public override void PreDraw()
    {
        Th = Theme.Get(plugin.Configuration.Theme);
        pushedColors = 0;
        PushColor(ImGuiCol.WindowBg, Paper);
        PushColor(ImGuiCol.PopupBg, Paper);
        PushColor(ImGuiCol.TitleBg, Cover);
        PushColor(ImGuiCol.TitleBgActive, Cover);
        PushColor(ImGuiCol.TitleBgCollapsed, Cover);
        PushColor(ImGuiCol.Border, CoverEdge);
        PushColor(ImGuiCol.Text, Ink);
        PushColor(ImGuiCol.TextDisabled, InkSoft);
        PushColor(ImGuiCol.FrameBg, PaperDeep);
        PushColor(ImGuiCol.FrameBgHovered, PaperHover);
        PushColor(ImGuiCol.FrameBgActive, PaperPress);
        PushColor(ImGuiCol.CheckMark, Coral);
        PushColor(ImGuiCol.Button, PaperDeep);
        PushColor(ImGuiCol.ButtonHovered, PaperHover);
        PushColor(ImGuiCol.ButtonActive, PaperPress);
        PushColor(ImGuiCol.Separator, SlotEdge);
        PushColor(ImGuiCol.Header, PaperDeep);
        PushColor(ImGuiCol.HeaderHovered, PaperHover);
        PushColor(ImGuiCol.HeaderActive, PaperPress);

        pushedVars = 0;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f * S);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2f * S);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * S);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f * S);
        pushedVars = 4;
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(pushedVars);
        ImGui.PopStyleColor(pushedColors);
        pushedVars = 0;
        pushedColors = 0;
    }

    private void PushColor(ImGuiCol col, Vector4 value)
    {
        ImGui.PushStyleColor(col, value);
        pushedColors++;
    }

    // Main layout

    public override void Draw()
    {
        var config = plugin.Configuration;
        var journal = plugin.Journal;

        DrawHeader(journal);

        if (journal is null)
        {
            DrawNote("Hello there!", "Log in to see your journal.");
            return;
        }

        var j = journal.Value;
        if (!j.HasJournal)
        {
            DrawNote("No journal yet!", "Pick one up from Khloe Aliapoh in Idyllshire.");
            return;
        }

        if (j.Expired)
        {
            DrawNote("This one's past due!", "Khloe has a fresh journal waiting for you in Idyllshire.");
            return;
        }

        DrawGoalTabs(config);
        ImGui.Dummy(new Vector2(0, 2f * S));

        plugin.Advisor.Request(j.Mask, j.SecondChancePoints, config.Goal);
        var a = plugin.Advisor.Latest;
        if (a == null || a.Mask != j.Mask || a.Goal != config.Goal || a.Points != Math.Min(j.SecondChancePoints, TailsMath.MaxSecondChancePoints))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
                ImGui.TextUnformatted("Reading the stars...");
            ImGui.Dummy(new Vector2(ContentWidth, 0));
            return;
        }

        DrawVerdict(a);
        ImGui.Dummy(new Vector2(0, 2f * S));
        DrawFooter(a, config);

        if (config.ShowMath) DrawMath(a, config);
    }

    private void DrawHeader(JournalSnapshot? journal)
    {
        var dl = ImGui.GetWindowDrawList();

        using (plugin.TitleFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.V(Th.Title)))
            ImGui.TextUnformatted("Wondrous Tails");

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var cy = (min.Y + max.Y) / 2f;
        DrawStar(dl, new Vector2(max.X + 14f * S, cy - 3f * S), 7f * S, Gold);
        DrawStar(dl, new Vector2(max.X + 27f * S, cy + 4f * S), 4f * S, Theme.V(Th.StarLight));

        if (journal is { HasJournal: true, Expired: false, ExpiresUtc: { } exp })
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.V(Th.Title)))
                ImGui.TextUnformatted($"Deadline: {exp.ToLocalTime():ddd MMM d, h:mm tt}");
        }

        DottedRule();
    }

    private void DrawGoalTabs(Configuration config)
    {
        var dl = ImGui.GetWindowDrawList();
        var gap = 6f * S;
        var size = new Vector2((ContentWidth - 2 * gap) / 3f, 30f * S);

        for (var i = 0; i < GoalTabs.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, gap);

            var pos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton($"##goal{i}", size) && (int)config.Goal != i)
            {
                config.Goal = (Goal)i;
                config.Save();
            }

            var selected = (int)config.Goal == i;
            var hovered = ImGui.IsItemHovered();
            var bg = selected ? Coral : hovered ? PaperHover : PaperDeep;
            var fg = selected ? White : Ink;

            dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(bg), size.Y / 2f);
            if (!selected) dl.AddRect(pos, pos + size, ImGui.GetColorU32(SlotEdge), size.Y / 2f, ImDrawFlags.None, 1f * S);

            var text = ImGui.CalcTextSize(GoalTabs[i]);
            dl.AddText(pos + (size - text) / 2f, ImGui.GetColorU32(fg), GoalTabs[i]);

            if (hovered)
            {
                ImGui.SetTooltip(i switch
                {
                    0 => "Aim for at least one line.",
                    1 => "Aim for two lines or more.",
                    _ => "Aim for all three lines. A long shot!",
                });
            }
        }
    }

    // Verdict card

    private void DrawVerdict(Analysis a)
    {
        var (headline, color, why) = Words(a);
        var showTip = a.Verdict == Verdict.GrabPoints;

        var dl = ImGui.GetWindowDrawList();
        var pad = 14f * S;
        var cw = ContentWidth;
        var start = ImGui.GetCursorScreenPos();
        var localLeft = ImGui.GetCursorPosX();

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(start + new Vector2(0, pad));
        ImGui.Indent(pad);
        ImGui.PushTextWrapPos(localLeft + cw - pad);

        using (plugin.HeadlineFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextWrapped(headline);

        ImGui.Dummy(new Vector2(0, 2f * S));
        ImGui.TextWrapped(why);

        if (showTip)
        {
            ImGui.Dummy(new Vector2(0, 4f * S));
            var tipPad = 10f * S;
            var tipStart = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(tipStart + new Vector2(0, tipPad));
            ImGui.Indent(tipPad);
            ImGui.PushTextWrapPos(localLeft + cw - pad - tipPad);

            using (ImRaii.PushColor(ImGuiCol.Text, Teal))
                ImGui.TextUnformatted("How to get points");
            TipLine("Run any duty with a party member who is new to it. Each one earns a point.");
            TipLine("Don't claim sticker 8 yet. Shuffling locks once it's placed.");

            ImGui.PopTextWrapPos();
            ImGui.Unindent(tipPad);

            var tipEnd = new Vector2(start.X + cw - pad, ImGui.GetCursorScreenPos().Y + tipPad - ImGui.GetStyle().ItemSpacing.Y);
            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(tipStart, tipEnd, ImGui.GetColorU32(MintDeep), 8f * S);
            dl.ChannelsSetCurrent(1);
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, tipEnd.Y + ImGui.GetStyle().ItemSpacing.Y));
        }

        ImGui.PopTextWrapPos();
        ImGui.Unindent(pad);

        var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y + pad;
        var cardMax = new Vector2(start.X + cw, bottom);

        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(start, cardMax, ImGui.GetColorU32(Mint), 12f * S);
        dl.AddRect(start, cardMax, ImGui.GetColorU32(MintEdge), 12f * S, ImDrawFlags.None, 1.5f * S);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(start.X, bottom));
        ImGui.Dummy(new Vector2(cw, 0));
    }

    private static void TipLine(string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        dl.AddCircleFilled(pos + new Vector2(4f * S, lineH / 2f), 2.5f * S, ImGui.GetColorU32(Sticker));
        ImGui.Indent(12f * S);
        ImGui.TextWrapped(text);
        ImGui.Unindent(12f * S);
    }

    // Footer: sticker mirror plus counts

    private void DrawFooter(Analysis a, Configuration config)
    {
        var dl = ImGui.GetWindowDrawList();
        var cell = 20f * S;
        var gap = 4f * S;
        var gridSize = new Vector2(cell * 4 + gap * 3, cell * 4 + gap * 3);

        var origin = ImGui.GetCursorScreenPos();
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var min = origin + new Vector2(col * (cell + gap), row * (cell + gap));
                var placed = (a.Mask & (1 << (row * 4 + col))) != 0;
                DrawSlot(dl, min, cell, placed);
            }
        }
        ImGui.Dummy(gridSize);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Your journal, as the advisor sees it.");

        ImGui.SameLine(0, 16f * S);
        ImGui.BeginGroup();

        ImGui.TextUnformatted($"Seals: {a.Stickers} / {TailsMath.MaxStickers}");
        using (ImRaii.PushColor(ImGuiCol.Text, Teal))
            ImGui.TextUnformatted($"Second Chance: {a.Points}");
        if (a.LinesDone > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Coral))
                ImGui.TextUnformatted(a.LinesDone == 1 ? "1 line done!" : $"{a.LinesDone} lines done!");
        }

        ImGui.Dummy(new Vector2(0, 2f * S));
        using (ImRaii.PushColor(ImGuiCol.Text, Coral))
            ImGui.TextUnformatted(config.ShowMath ? "Hide the math" : "Show the math");
        if (ImGui.IsItemHovered())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            dl.AddLine(new Vector2(min.X, max.Y), max, ImGui.GetColorU32(Coral), 1f * S);
        }
        if (ImGui.IsItemClicked())
        {
            config.ShowMath = !config.ShowMath;
            config.Save();
        }

        ImGui.Dummy(new Vector2(0, 2f * S));
        DrawThemePicker(config);

        ImGui.EndGroup();
    }

    private static void DrawSlot(ImDrawListPtr dl, Vector2 min, float size, bool placed)
    {
        var max = min + new Vector2(size, size);
        var round = size * 0.32f;
        dl.AddRectFilled(min, max, ImGui.GetColorU32(PaperDeep), round);
        dl.AddRect(min, max, ImGui.GetColorU32(SlotEdge), round, ImDrawFlags.None, 1f * S);

        if (!placed) return;

        var c = (min + max) / 2f;
        var r = size * 0.47f;
        dl.AddCircleFilled(c, r, ImGui.GetColorU32(StickerRim));
        dl.AddCircleFilled(c, r * 0.80f, ImGui.GetColorU32(Sticker));
        DrawStar(dl, c, r * 0.48f, ImGui.GetColorU32(StickerRim));
    }

    // Math drawer

    private static void DrawMath(Analysis a, Configuration config)
    {
        DottedRule();

        var c1 = 140f * S;
        var c2 = 205f * S;
        var c3 = 270f * S;

        using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
        {
            ImGui.TextUnformatted("Chance of");
            ImGui.SameLine(c1);
            ImGui.TextUnformatted("1+ line");
            ImGui.SameLine(c2);
            ImGui.TextUnformatted("2+ lines");
            ImGui.SameLine(c3);
            ImGui.TextUnformatted("3 lines");
        }

        OddsRow("Keep this board", a.KeepOdds, c1, c2, c3, Ink);
        if (a.ShuffleOdds != null) OddsRow("Use your shuffles", a.ShuffleOdds, c1, c2, c3, Coral);

        using (ImRaii.PushColor(ImGuiCol.Text, InkSoft))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ContentWidth);
            ImGui.TextWrapped(a.Stickers < TailsMath.MaxShuffleStickers
                ? "Shuffle odds appear at 7 stickers, the only point where shuffling pays off."
                : "Shuffle odds assume you shuffle again if the new board is still poor.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0, 2f * S));
        var open = config.OpenWithJournal;
        if (ImGui.Checkbox("Open with the journal", ref open))
        {
            config.OpenWithJournal = open;
            config.Save();
        }

        var close = config.CloseWithJournal;
        if (ImGui.Checkbox("Close with the journal", ref close))
        {
            config.CloseWithJournal = close;
            config.Save();
        }
    }

    private static void OddsRow(string label, double[] odds, float c1, float c2, float c3, Vector4 color)
    {
        ImGui.TextUnformatted(label);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.SameLine(c1);
            ImGui.TextUnformatted(Pct(odds[0]));
            ImGui.SameLine(c2);
            ImGui.TextUnformatted(Pct(odds[1]));
            ImGui.SameLine(c3);
            ImGui.TextUnformatted(Pct(odds[2]));
        }
    }

    // A simple card for the no journal cases

    private void DrawNote(string headline, string body)
    {
        using (plugin.HeadlineFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Teal))
            ImGui.TextUnformatted(headline);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ContentWidth);
        ImGui.TextWrapped(body);
        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(ContentWidth, 0));
    }

    // Words

    private static (string Headline, Vector4 Color, string Why) Words(Analysis a)
    {
        var goal = a.Goal switch
        {
            Goal.TwoLines => "2 lines",
            Goal.ThreeLines => "3 lines",
            _ => "a line",
        };
        var keep = Pct(a.KeepChance);
        var shuffle = Pct(a.ShuffleChance ?? 0);

        switch (a.Verdict)
        {
            case Verdict.ShakeItUp:
                return ("Shake it up!", Coral,
                    $"Your stickers could be better placed. Shuffling takes your chance of {goal} from {keep} to {shuffle}. Each shuffle costs 2 Second Chance points."
                    + LongShot(a) + BreaksLine(a));

            case Verdict.GrabPoints:
                return (a.PointsNeeded == 1 ? "Grab 1 more point!" : $"Grab {a.PointsNeeded} more points!", Amber,
                    $"A shuffle would take your chance of {goal} from {keep} to {shuffle}, but it costs 2 Second Chance points and you have {a.Points}."
                    + LongShot(a) + BreaksLine(a));

            case Verdict.OffToKhloe:
                return ("Off to Khloe!", Coral, a.LinesDone switch
                {
                    0 => "Your journal is full. Go pick up your prize in Idyllshire!",
                    1 => "Your journal is full with 1 line. Go pick up your prize in Idyllshire!",
                    _ => $"Your journal is full with {a.LinesDone} lines. Go pick up your prize in Idyllshire!",
                });
        }

        // Keep stamping
        var why = a.Reason switch
        {
            Reason.GoalOutOfReach =>
                $"This board can't make {goal} anymore, but don't worry. A shuffle at 7 stickers resets everything, so save your points for then.",
            Reason.GoodBoard => a.KeepChance >= 0.9995
                ? $"You've already locked in {goal}. Don't touch a thing!"
                : $"Your board looks great. You have a {keep} chance of {goal}, better than a shuffle would give you. Don't touch it!",
            Reason.Locked => a.KeepChance >= 0.9995
                ? $"You've locked in {goal}! Grab your last sticker and head to Khloe."
                : a.KeepChance <= 0
                    ? "Shuffling is locked now, so this board is set. Finish strong!"
                    : $"Shuffling is locked now. You have a {keep} chance of {goal}. Finish strong!",
            _ => a.Stickers < TailsMath.MinShuffleStickers
                ? "Shuffles unlock at 3 stickers, but they only pay off at 7. Go clear some duties!"
                : "Save your points for now. Shuffles only pay off at 7 stickers, so check back then.",
        };
        return ("Keep stamping!", Teal, why);
    }

    private static string LongShot(Analysis a) =>
        a.ShuffleChance is < 0.02
            ? " It's a long shot either way, so only chase it if you're feeling lucky."
            : string.Empty;

    private static string BreaksLine(Analysis a) =>
        a.LinesDone > 0
            ? " Heads up: this breaks up the line you already have."
            : string.Empty;

    // Theme picker

    private void DrawThemePicker(Configuration config)
    {
        ImGui.SetNextItemWidth(140f * S);
        using (var combo = ImRaii.Combo("##theme", Th.Name))
        {
            if (combo.Success)
            {
                foreach (var t in Theme.All)
                {
                    if (ImGui.Selectable(t.Name, t.Name == Th.Name))
                    {
                        config.Theme = t.Name;
                        config.Save();
                    }
                }
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Theme");
    }

    // Drawing helpers

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

    private static void DrawStar(ImDrawListPtr dl, Vector2 center, float radius, Vector4 color) =>
        DrawStar(dl, center, radius, ImGui.GetColorU32(color));

    private static void DrawStar(ImDrawListPtr dl, Vector2 center, float radius, uint color)
    {
        var inner = radius * 0.45f;
        for (var i = 0; i < 5; i++)
        {
            var a = (-90f + 72f * i) * MathF.PI / 180f;
            var l = a - 36f * MathF.PI / 180f;
            var r = a + 36f * MathF.PI / 180f;
            var tip = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            var left = center + new Vector2(MathF.Cos(l), MathF.Sin(l)) * inner;
            var right = center + new Vector2(MathF.Cos(r), MathF.Sin(r)) * inner;
            dl.AddTriangleFilled(center, left, tip, color);
            dl.AddTriangleFilled(center, tip, right, color);
        }
    }

    private static string Pct(double p)
    {
        if (p <= 0) return "0%";
        if (p >= 0.9995) return "100%";
        if (p < 0.01) return $"{p * 100:0.0}%";
        if (p >= 0.995) return "99%"; // never round a maybe up to a sure thing
        return $"{p * 100:0}%";
    }

}
