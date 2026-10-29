using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TailsAdvisor.Game;
using TailsAdvisor.Solver;
using TailsAdvisor.Windows;

namespace TailsAdvisor;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Command = "/wta";
    private const string CommandLong = "/tailsadvisor";
    private const string JournalAddon = "WeeklyBingo";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public Configuration Configuration { get; }

    /// <summary>The game's Jupiter typeface, for the journal title and the verdict.</summary>
    public IFontHandle TitleFont { get; }
    public IFontHandle HeadlineFont { get; }
    public AdvisorService Advisor { get; } = new();

    /// <summary>Latest journal read from the game. Null when logged out.</summary>
    public JournalSnapshot? Journal { get; private set; }

    private readonly WindowSystem windowSystem = new("TailsAdvisor");
    private readonly MainWindow mainWindow;
    private DateTime nextPoll = DateTime.MinValue;
    private bool openedByJournal;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

        TitleFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter20));
        HeadlineFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        var help = new CommandInfo(OnCommand) { HelpMessage = "Open the Wondrous Tails Advisor." };
        CommandManager.AddHandler(Command, help);
        CommandManager.AddHandler(CommandLong, new CommandInfo(OnCommand) { HelpMessage = "Open the Wondrous Tails Advisor.", ShowInHelp = false });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, JournalAddon, OnJournalOpened);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, JournalAddon, OnJournalClosed);
    }

    public void Dispose()
    {
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, JournalAddon, OnJournalOpened);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, JournalAddon, OnJournalClosed);
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(Command);
        CommandManager.RemoveHandler(CommandLong);
        Advisor.Dispose();
        TitleFont.Dispose();
        HeadlineFont.Dispose();
    }

    public void ToggleMainUi() => mainWindow.Toggle();

    /// <summary>Forces a fresh read on the next framework tick.</summary>
    public void RefreshNow() => nextPoll = DateTime.MinValue;

    private void OnCommand(string command, string args) => mainWindow.Toggle();

    private void OnFrameworkUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < nextPoll) return;
        nextPoll = now + PollInterval;

        if (!ClientState.IsLoggedIn)
        {
            Journal = null;
            return;
        }

        try
        {
            Journal = JournalReader.Read();
        }
        catch (Exception ex)
        {
            Journal = null;
            Log.Warning(ex, "Could not read the Wondrous Tails journal.");
        }
    }

    private void OnJournalOpened(AddonEvent type, AddonArgs args)
    {
        RefreshNow();
        if (!Configuration.OpenWithJournal || mainWindow.IsOpen) return;
        mainWindow.IsOpen = true;
        openedByJournal = true;
    }

    private void OnJournalClosed(AddonEvent type, AddonArgs args)
    {
        RefreshNow();
        if (openedByJournal && Configuration.CloseWithJournal) mainWindow.IsOpen = false;
        openedByJournal = false;
    }
}
