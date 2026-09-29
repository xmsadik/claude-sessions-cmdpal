using System;
using System.Linq;
using ClaudeSessions.Core;
using ClaudeSessions.Notifications;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessions;

public partial class ClaudeSessionsCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly WrappedDockItem _dockBand;
    private readonly SettingsManager _settingsManager = new();
    private readonly SessionStore _store;
    private readonly SessionsPage _page;
    private readonly SessionsSummaryPage _summaryPage;
    private readonly SessionsBand _band;
    private readonly SessionsBand _listBand;
    private readonly SessionTransitionTracker _transitions = new(TimeSpan.FromSeconds(15));
    private readonly SessionNotifier _notifier = new();
    private string _lastLayout;

    public ClaudeSessionsCommandsProvider()
    {
        DisplayName = "Claude Sessions";
        Id = "ClaudeSessions";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

        _store = new SessionStore(() => _settingsManager.RefreshInterval);
        _page = new SessionsPage(_store);

        // Seed synchronously so the band starts with the current sessions, then poll.
        try
        {
            _store.Refresh();
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"Initial session refresh failed: {ex}");
        }

        _commands = [
            new CommandItem(_page)
            {
                Title = DisplayName,
                Subtitle = "Live Claude Code sessions",
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];

        // Command.Id must be non-empty or the host silently drops the band.
        _summaryPage = new SessionsSummaryPage(_store, _settingsManager, _page);
        _band = new SessionsBand(_store, _summaryPage);
        _listBand = new SessionsBand(_store, _page);
        _dockBand = new WrappedDockItem(CurrentDockItems(), "ClaudeSessions.dock.sessions", "Claude Sessions");

        // Compact: the band item updates itself via property setters. Per-session: RaiseItemsChanged
        // does not reach the Dock wrapper, so reassign Items on every change.
        _page.Refreshed += (_, _) =>
        {
            if (!_settingsManager.CompactDock)
            {
                _dockBand.Items = _page.BuildDockItems();
            }
        };

        // Switching the layout setting rebuilds the band items.
        _lastLayout = _settingsManager.DockLayout;
        _settingsManager.Settings.SettingsChanged += (_, _) =>
        {
            try
            {
                if (_settingsManager.DockLayout != _lastLayout)
                {
                    _lastLayout = _settingsManager.DockLayout;
                    _dockBand.Items = CurrentDockItems();
                }
            }
            catch (Exception ex)
            {
                DiagLog.WriteLine($"Dock layout switch failed: {ex}");
            }
        };

        // Baseline the tracker on the seeded snapshot so existing states don't toast at startup.
        _transitions.Observe(_store.Sessions, DateTimeOffset.Now);
        _store.Changed += (_, _) => NotifyTransitions();

        Settings = _settingsManager.Settings;
        _store.Start();
    }

    private void NotifyTransitions()
    {
        // Always observe, even when off, so re-enabling doesn't replay stale transitions.
        var notices = _transitions.Observe(_store.Sessions, DateTimeOffset.Now);
        var mode = _settingsManager.Notifications;
        if (mode == SettingsManager.NotifyOff)
        {
            return;
        }

        _notifier.Notify(mode == SettingsManager.NotifyWaiting
            ? notices.Where(n => n.Kind == SessionNoticeKind.Waiting).ToList()
            : notices);
    }

    private IListItem[] CurrentDockItems() => _settingsManager.DockLayout switch
    {
        SettingsManager.CompactLayout => [_band],
        SettingsManager.PerSessionLayout => _page.BuildDockItems(),
        _ => [_listBand],
    };

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => [_dockBand];

    public override void Dispose()
    {
        _band.Dispose();
        _listBand.Dispose();
        _summaryPage.Dispose();
        _page.Dispose();
        _store.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
