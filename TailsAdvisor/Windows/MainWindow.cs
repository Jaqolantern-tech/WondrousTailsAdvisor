using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TailsAdvisor.Game;
using TailsAdvisor.Solver;

namespace TailsAdvisor.Windows;

public sealed class MainWindow : Window
{
    private static readonly Vector4 Gold = new(0.86f, 0.66f, 0.18f, 1f);
    private static readonly Vector4 GoldHover = new(0.93f, 0.74f, 0.26f, 1f);
    private static readonly Vector4 Ink = new(0.08f, 0.06f, 0.02f, 1f);
    private static readonly Vector4 Bad = new(0.58f, 0.20f, 0.20f, 1f);
    private static readonly Vector4 Mid = new(0.62f, 0.52f, 0.18f, 1f);
    private static readonly Vector4 Good = new(0.20f, 0.56f, 0.28f, 1f);
    private static readonly Vector4 Neutral = new(0.30f, 0.32f, 0.36f, 1f);
    private static readonly Vector4 ShuffleBlue = new(0.40f, 0.72f, 1.00f, 1f);
    private static readonly Vector4 HoldGreen = new(0.45f, 0.90f, 0.50f, 1f);
    private static readonly Vector4 Muted = new(0.70f, 0.70f, 0.72f, 1f);

    private static readonly string[] GoalNames =
    [
        "At least one line",
        "Two or more lines",
        "Three lines (jackpot)",
        "Most lines on average",
    ];

    private readonly Plugin plugin;

    private bool plannerMode;
    private int plannerMask;
    private int plannerPoints = 4;

    public MainWindow(Plugin plugin)
        : base("Wondrous Tails Advisor###TailsAdvisorMain", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    public override void Draw()
    {
        var config = plugin.Configuration;
        var journal = plugin.Journal;

        DrawModeToggle();

        int mask, points;
        if (plannerMode)
        {
            mask = plannerMask;
            points = plannerPoints;
            DrawPlannerControls();
        }
        else
        {
            if (journal is null)
            {
                ImGui.TextWrapped("Log in to read your journal. You can still use Planner mode.");
                DrawSettings();
                return;
            }

            if (!journal.Value.HasJournal)
            {
                ImGui.TextWrapped("You do not have a Wondrous Tails journal right now. Pick one up from Khloe Aliapoh in Idyllshire.");
                ImGui.TextDisabled($"Second Chance points: {journal.Value.SecondChancePoints}");
                DrawSettings();
                return;
            }

            mask = journal.Value.Mask;
            points = journal.Value.SecondChancePoints;
        }

        plugin.Advisor.Request(mask, points, config.ReservePoints, config.Goal);
        var analysis = plugin.Advisor.Latest;

        if (analysis == null || analysis.Mask != mask || analysis.Goal != config.Goal ||
            analysis.Points != points)
        {
            ImGui.TextDisabled("Working out the best plan...");
            DrawSettings();
            return;
        }

        DrawStatus(analysis, plannerMode ? null : journal);
        ImGui.Spacing();
        DrawVerdict(analysis, config.Goal);
        ImGui.Spacing();
        DrawOddsTable(analysis);
        ImGui.Spacing();
        DrawGrid(analysis, config);
        DrawLegend(config.Goal);
        ImGui.Spacing();
        DrawSettings();
    }

    // Mode and planner

    private void DrawModeToggle()
    {
        if (ImGui.RadioButton("My journal", !plannerMode)) plannerMode = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Planner", plannerMode))
        {
            if (!plannerMode && plugin.Journal is { HasJournal: true } j)
            {
                plannerMask = j.Mask;
                plannerPoints = j.SecondChancePoints;
            }
            plannerMode = true;
        }
        ImGui.Separator();
    }

    private void DrawPlannerControls()
    {
        ImGui.TextWrapped("Click squares to add or remove stickers and test any board.");
        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        var p = plannerPoints;
        if (ImGui.SliderInt("Second Chance points##planner", ref p, 0, TailsMath.MaxSecondChancePoints))
            plannerPoints = p;

        if (ImGui.Button("Clear board")) plannerMask = 0;
        ImGui.SameLine();
        using (ImRaii.Disabled(plugin.Journal is not { HasJournal: true }))
        {
            if (ImGui.Button("Copy my journal") && plugin.Journal is { HasJournal: true } j)
            {
                plannerMask = j.Mask;
                plannerPoints = j.SecondChancePoints;
            }
        }
        ImGui.Spacing();
    }

    // Status and verdict

    private static void DrawStatus(Analysis a, JournalSnapshot? journal)
    {
        ImGui.Text($"Stickers: {a.Stickers} of {TailsMath.MaxStickers}");
        ImGui.SameLine(170 * ImGuiHelpers.GlobalScale);
        var reserved = a.Points - a.UsablePoints;
        ImGui.Text(reserved > 0
            ? $"Second Chance: {a.Points} ({reserved} reserved)"
            : $"Second Chance: {a.Points}");

        ImGui.TextColored(Muted, a.LinesDone > 0
            ? $"Lines complete: {a.LinesDone}"
            : $"Lines one sticker away: {a.LinesOneAway}");

        if (journal is { } j)
        {
            if (j.StickersWaiting > 0)
            {
                ImGui.TextColored(Gold, j.StickersWaiting == 1
                    ? "1 finished objective is waiting for its sticker."
                    : $"{j.StickersWaiting} finished objectives are waiting for their stickers.");
            }

            if (j.Expired)
                ImGui.TextColored(Bad, "This journal has expired.");
            else if (j.ExpiresUtc is { } exp)
                ImGui.TextDisabled($"Turn in by {exp.ToLocalTime():ddd MMM d, h:mm tt}");
        }
    }

    private static void DrawVerdict(Analysis a, Goal goal)
    {
        var goalName = GoalNames[(int)goal].ToLowerInvariant();
        switch (a.Verdict)
        {
            case Verdict.Shuffle:
                Banner("SHUFFLE NOW", ShuffleBlue);
                ImGui.TextWrapped(
                    $"Spend {TailsMath.ShuffleCost} Second Chance points. Your odds for {goalName} go from " +
                    $"{GoalValue(goal, a.HoldValue)} to {GoalValue(goal, a.ShuffleValue ?? 0)}.");
                break;

            case Verdict.Hold:
                Banner("HOLD", HoldGreen);
                ImGui.TextWrapped(
                    $"Keep your stickers. Holding gives {GoalValue(goal, a.HoldValue)} for {goalName}; " +
                    $"shuffling now gives {GoalValue(goal, a.ShuffleValue ?? 0)}.");
                break;

            case Verdict.TooFewStickers:
                Banner("KEEP GOING", HoldGreen);
                ImGui.TextWrapped($"Shuffle unlocks at {TailsMath.MinShuffleStickers} stickers. Nothing to decide yet.");
                break;

            case Verdict.TooManyStickers:
                Banner("FINISH IT OUT", HoldGreen);
                ImGui.TextWrapped($"Shuffle locks after {TailsMath.MaxShuffleStickers} stickers. Complete your last objective.");
                break;

            case Verdict.NotEnoughPoints:
                Banner("HOLD", HoldGreen);
                ImGui.TextWrapped(a.Points >= TailsMath.ShuffleCost
                    ? "Shuffling would dip into the points you reserved for Retry."
                    : $"You need {TailsMath.ShuffleCost} Second Chance points to shuffle.");
                break;

            case Verdict.Complete:
                Banner("COMPLETE", Gold);
                ImGui.TextWrapped("All nine stickers placed. Turn the journal in to Khloe.");
                break;
        }
    }

    private static void Banner(string text, Vector4 color)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.SetWindowFontScale(1.6f);
            ImGui.Text(text);
            ImGui.SetWindowFontScale(1f);
        }
    }

    private static void DrawOddsTable(Analysis a)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg;
        using var table = ImRaii.Table("##odds", 4, flags);
        if (!table.Success) return;

        ImGui.TableSetupColumn("Chance of");
        ImGui.TableSetupColumn("1+ line");
        ImGui.TableSetupColumn("2+ lines");
        ImGui.TableSetupColumn("3 lines");
        ImGui.TableHeadersRow();

        OddsRow("No more shuffles", a.NoShuffleOdds);
        if (a.Stickers < TailsMath.MaxStickers)
        {
            OddsRow("Hold, follow plan", a.HoldOdds);
            if (a.ShuffleOdds != null) OddsRow("Shuffle, follow plan", a.ShuffleOdds);
        }
    }

    private static void OddsRow(string label, double[] odds)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(label);
        for (var i = 0; i < 3; i++)
        {
            ImGui.TableNextColumn();
            ImGui.Text(Pct(odds[i]));
        }
    }

    // Grid

    private void DrawGrid(Analysis a, Configuration config)
    {
        var size = new Vector2(64, 48) * ImGuiHelpers.GlobalScale;

        // Range of values across open squares, for the heat colors.
        double min = double.MaxValue, max = double.MinValue;
        foreach (var v in a.CellValue)
        {
            if (double.IsNaN(v)) continue;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }

        ImGui.TextDisabled(plannerMode
            ? "Planner board"
            : "If the next sticker lands here (game picks the square at random):");

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4) * ImGuiHelpers.GlobalScale))
        {
            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    if (col > 0) ImGui.SameLine();
                    DrawCell(a, config, row * 4 + col, size, min, max);
                }
            }
        }
    }

    private void DrawCell(Analysis a, Configuration config, int bit, Vector2 size, double min, double max)
    {
        var placed = (a.Mask & (1 << bit)) != 0;
        var value = a.CellValue[bit];

        Vector4 bg;
        string label;
        if (placed)
        {
            bg = Gold;
            label = "Sticker";
        }
        else if (double.IsNaN(value))
        {
            bg = Neutral;
            label = string.Empty;
        }
        else
        {
            var t = max - min < 1e-9 ? 0.5f : (float)((value - min) / (max - min));
            bg = t < 0.5f ? Vector4.Lerp(Bad, Mid, t * 2f) : Vector4.Lerp(Mid, Good, (t - 0.5f) * 2f);
            label = config.ShowSquareOdds ? GoalValue(config.Goal, value) : string.Empty;
        }

        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Button, bg))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, placed ? GoldHover : bg * 1.15f))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, bg))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink, placed))
        {
            clicked = ImGui.Button($"{label}##cell{bit}", size);
        }

        if (ImGui.IsItemHovered())
        {
            if (placed)
                ImGui.SetTooltip(plannerMode ? "Sticker placed. Click to remove." : "Sticker placed.");
            else if (a.CellOdds[bit] is { } o)
                ImGui.SetTooltip(
                    $"If the next sticker lands here:\n1+ line: {Pct(o[0])}\n2+ lines: {Pct(o[1])}\n3 lines: {Pct(o[2])}" +
                    (plannerMode ? "\n\nClick to place a sticker." : string.Empty));
        }

        if (clicked && plannerMode)
        {
            if (placed) plannerMask &= ~(1 << bit);
            else if (TailsMath.Count(plannerMask) < TailsMath.MaxStickers) plannerMask |= 1 << bit;
        }
    }

    private static void DrawLegend(Goal goal)
    {
        ImGui.TextDisabled(goal == Goal.MostLines
            ? "Squares show expected lines. Green is a lucky landing, red is an unlucky one."
            : "Squares show your goal odds. Green is a lucky landing, red is an unlucky one.");
    }

    // Settings

    private void DrawSettings()
    {
        var config = plugin.Configuration;
        if (!ImGui.CollapsingHeader("Settings")) return;

        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("Goal", GoalNames[(int)config.Goal]))
        {
            if (combo.Success)
            {
                for (var i = 0; i < GoalNames.Length; i++)
                {
                    if (ImGui.Selectable(GoalNames[i], (int)config.Goal == i))
                    {
                        config.Goal = (Goal)i;
                        config.Save();
                    }
                }
            }
        }

        ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
        var reserve = config.ReservePoints;
        if (ImGui.SliderInt("Keep for Retry", ref reserve, 0, TailsMath.MaxSecondChancePoints))
        {
            config.ReservePoints = reserve;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Second Chance points the advisor will never spend on a shuffle.");

        var showOdds = config.ShowSquareOdds;
        if (ImGui.Checkbox("Show odds on squares", ref showOdds))
        {
            config.ShowSquareOdds = showOdds;
            config.Save();
        }

        var openWith = config.OpenWithJournal;
        if (ImGui.Checkbox("Open when the journal opens", ref openWith))
        {
            config.OpenWithJournal = openWith;
            config.Save();
        }

        var closeWith = config.CloseWithJournal;
        if (ImGui.Checkbox("Close when the journal closes", ref closeWith))
        {
            config.CloseWithJournal = closeWith;
            config.Save();
        }

        ImGui.TextDisabled("The plan assumes you earn no new Second Chance points this week.");
    }

    // Formatting

    private static string Pct(double p)
    {
        if (p <= 0) return "0%";
        if (p >= 0.9995) return "100%";
        if (p < 0.001) return "<0.1%";
        return $"{p * 100:0.0}%";
    }

    private static string GoalValue(Goal goal, double v) =>
        goal == Goal.MostLines ? $"{v:0.00} lines" : Pct(v);
}
