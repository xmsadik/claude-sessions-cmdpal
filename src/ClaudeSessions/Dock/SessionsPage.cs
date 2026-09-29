using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeSessions.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessions;

/// <summary>
/// The "Claude Sessions" ListPage in the palette, and the item factory for the Dock band. The Dock
/// band is a separate WrappedDockItem whose Items must be reassigned on change (RaiseItemsChanged
/// does not reach it), so <see cref="Refreshed"/> tells the provider to do that.
/// </summary>
internal sealed partial class SessionsPage : ListPage, IDisposable
{
    private readonly SessionStore _store;

    public SessionsPage(SessionStore store)
    {
        _store = store;
        _store.Changed += OnStoreChanged;

        Id = "ClaudeSessions.page.sessions";
        Title = "Claude Sessions";
        Name = "Claude Sessions";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        PlaceholderText = "Focus a Claude Code session";
    }

    /// <summary>Raised after the store changed, in addition to RaiseItemsChanged.</summary>
    public event EventHandler? Refreshed;

    public void Dispose() => _store.Changed -= OnStoreChanged;

    public override IListItem[] GetItems()
    {
        var now = DateTimeOffset.Now;
        return SessionSummary.Sorted(_store.Sessions)
            .Select(s => (IListItem)new ListItem(new FocusSessionCommand(s))
            {
                Title = SessionPresentation.DisplayName(s),
                Subtitle = SessionPresentation.Detail(s, now),
                Icon = new IconInfo(SessionPresentation.Indicator(s.Status)),
            })
            .ToArray();
    }

    /// <summary>Icon-only items for the Dock band: title (tooltip) = "name · folder · status" (no duration: it goes stale).</summary>
    public IListItem[] BuildDockItems()
    {
        var list = new List<IListItem>();
        foreach (var s in _store.Sessions)
        {
            list.Add(new ListItem(new FocusSessionCommand(s))
            {
                Title = SessionPresentation.DockTitle(s),
                Icon = new IconInfo(SessionPresentation.Indicator(s.Status)),
            });
        }

        return list.ToArray();
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        try
        {
            RaiseItemsChanged();
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Must never crash the host.
            DiagLog.WriteLine($"SessionsPage.OnStoreChanged failed: {ex}");
        }
    }
}
