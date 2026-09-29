using System;
using System.Linq;
using ClaudeSessions.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessions;

/// <summary>
/// The compact Dock flyout: a markdown table of sessions (waiting, working, idle) plus one focus
/// command per session, "Open in palette" and settings. Durations are computed on every
/// GetContent call, so they are current whenever the host re-reads the page.
/// </summary>
internal sealed partial class SessionsSummaryPage : ContentPage, IDisposable
{
    private readonly SessionStore _store;
    private readonly SettingsManager _settings;
    private readonly SessionsPage _listPage;

    public SessionsSummaryPage(SessionStore store, SettingsManager settings, SessionsPage listPage)
    {
        _store = store;
        _settings = settings;
        _listPage = listPage;

        Id = "ClaudeSessions.page.summary";
        Name = "Claude Sessions";
        Title = "Claude Sessions";
        RebuildCommands();
        _store.Changed += OnStoreChanged;
    }

    public void Dispose() => _store.Changed -= OnStoreChanged;

    public override IContent[] GetContent() =>
        [new MarkdownContent(SessionSummary.Markdown(SessionSummary.Sorted(_store.Sessions), DateTimeOffset.Now))];

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        try
        {
            RebuildCommands();
            RaiseItemsChanged();
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"SessionsSummaryPage.OnStoreChanged failed: {ex}");
        }
    }

    private void RebuildCommands()
    {
        // The host labels flyout commands with the command's Name (not the item Title), so name each one.
        var items = SessionSummary.Sorted(_store.Sessions)
            .Select(s => (IContextItem)new CommandContextItem(new FocusSessionCommand(
                s,
                $"{SessionPresentation.Indicator(s.Status)} {SessionPresentation.DisplayName(s)} · {SessionPresentation.FolderName(s.Cwd)}")))
            .ToList();
        items.Add(new CommandContextItem(_listPage) { Title = "Open in palette" });
        items.Add(new CommandContextItem(_settings.Settings.SettingsPage));
        Commands = items.ToArray();
        Icon = new IconInfo(SessionSummary.BandIcon(_store.Sessions));
    }
}
