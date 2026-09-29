using System;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessions;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private const string DefaultRefreshSeconds = "2";
    public const string CompactListLayout = "compactList";
    public const string CompactLayout = "compact";
    public const string PerSessionLayout = "perSession";
    public const string NotifyOff = "off";
    public const string NotifyWaiting = "waiting";
    public const string NotifyWaitingFinished = "waitingFinished";

    private static readonly string _namespace = "ClaudeSessions";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ChoiceSetSetting _refreshInterval = new(
        Namespaced(nameof(RefreshInterval)),
        "Refresh interval",
        "How often to re-read Claude Code session files",
        [
            new ChoiceSetSetting.Choice("1 second", "1"),
            new ChoiceSetSetting.Choice("2 seconds", DefaultRefreshSeconds),
            new ChoiceSetSetting.Choice("5 seconds", "5"),
            new ChoiceSetSetting.Choice("10 seconds", "10"),
        ]);

    private readonly ChoiceSetSetting _dockLayout = new(
        Namespaced(nameof(DockLayout)),
        "Dock layout",
        "How sessions appear in the Dock",
        [
            new ChoiceSetSetting.Choice("Compact: one item, opens the session list", CompactListLayout),
            new ChoiceSetSetting.Choice("Compact: one item, opens a summary table", CompactLayout),
            new ChoiceSetSetting.Choice("One icon per session", PerSessionLayout),
        ]);

    private readonly ChoiceSetSetting _notifications = new(
        Namespaced(nameof(Notifications)),
        "Notifications",
        "Show a Windows notification when a session changes state",
        [
            new ChoiceSetSetting.Choice("Waiting for me + finished a long turn", NotifyWaitingFinished),
            new ChoiceSetSetting.Choice("Only when waiting for me", NotifyWaiting),
            new ChoiceSetSetting.Choice("Off", NotifyOff),
        ]);

    public TimeSpan RefreshInterval
    {
        get
        {
            var raw = _refreshInterval.Value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(int.Parse(DefaultRefreshSeconds, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>True for both single-item layouts; false for one icon per session.</summary>
    public bool CompactDock => DockLayout != PerSessionLayout;

    /// <summary>One of the layout constants; anything unknown falls back to <see cref="CompactListLayout"/>.</summary>
    public string DockLayout => _dockLayout.Value is CompactLayout or PerSessionLayout ? _dockLayout.Value : CompactListLayout;

    /// <summary>One of the Notify constants; anything unknown falls back to <see cref="NotifyWaitingFinished"/>.</summary>
    public string Notifications => _notifications.Value is NotifyOff or NotifyWaiting ? _notifications.Value : NotifyWaitingFinished;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("ClaudeSessions");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_dockLayout);
        Settings.Add(_refreshInterval);
        Settings.Add(_notifications);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            try
            {
                SaveSettings();
            }
            catch (Exception)
            {
                // Persisting failed (locked/readonly settings.json); in-memory values are still correct.
            }
        };
    }
}
