using System;
using Dalamud.Configuration;
using TailsAdvisor.Solver;

namespace TailsAdvisor;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    /// <summary>What you're chasing this week.</summary>
    public Goal Goal { get; set; } = Goal.AnyLine;

    /// <summary>Expand the full odds under the verdict.</summary>
    public bool ShowMath { get; set; } = false;

    /// <summary>Open the advisor whenever the in game journal opens.</summary>
    public bool OpenWithJournal { get; set; } = true;

    /// <summary>Close the advisor when the journal closes, if the journal opened it.</summary>
    public bool CloseWithJournal { get; set; } = true;

    /// <summary>Fixes values saved by older versions.</summary>
    public void Migrate()
    {
        if (!Enum.IsDefined(Goal)) Goal = Goal.AnyLine;
        Version = 2;
    }

    /// <summary>Window color theme, by name.</summary>
    public string Theme { get; set; } = global::TailsAdvisor.Theme.DefaultName;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
