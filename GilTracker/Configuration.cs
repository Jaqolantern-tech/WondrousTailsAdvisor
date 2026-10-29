using System;
using Dalamud.Configuration;
using GilTracker.Tracking;

namespace GilTracker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Which time range the window shows.</summary>
    public Period Period { get; set; } = Period.Session;

    /// <summary>Show the list of recent gil changes.</summary>
    public bool ShowRecent { get; set; } = false;

    /// <summary>Window color theme, by name.</summary>
    public string Theme { get; set; } = global::GilTracker.Theme.DefaultName;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
