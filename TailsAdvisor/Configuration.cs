using System;
using Dalamud.Configuration;
using TailsAdvisor.Solver;

namespace TailsAdvisor;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>What the shuffle plan tries to maximize.</summary>
    public Goal Goal { get; set; } = Goal.AnyLine;

    /// <summary>Second Chance points the advisor will never spend, so you keep them for Retry.</summary>
    public int ReservePoints { get; set; } = 0;

    /// <summary>Open the advisor whenever the in game journal opens.</summary>
    public bool OpenWithJournal { get; set; } = true;

    /// <summary>Close the advisor when the journal closes, if the journal opened it.</summary>
    public bool CloseWithJournal { get; set; } = true;

    /// <summary>Print the odds on each open square.</summary>
    public bool ShowSquareOdds { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
