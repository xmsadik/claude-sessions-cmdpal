namespace ClaudeSessions.Core;

/// <summary>Pure text/format helpers for the tooltip and subtitle strings.</summary>
public static class SessionPresentation
{
    public static string StatusText(SessionRecord s) => s.Status switch
    {
        SessionStatus.Busy => "Working",
        SessionStatus.Shell => "Working (shell command)",
        SessionStatus.Waiting => string.IsNullOrWhiteSpace(s.WaitingFor) ? "Waiting" : $"Waiting: {s.WaitingFor}",
        _ => "Idle",
    };

    /// <summary>Emoji indicator: green busy/shell, orange waiting, white idle.</summary>
    public static string Indicator(SessionStatus status) => status switch
    {
        SessionStatus.Busy or SessionStatus.Shell => "\U0001F7E2",
        SessionStatus.Waiting => "\U0001F7E0",
        _ => "⚪",
    };

    public static string FolderName(string cwd)
    {
        var trimmed = cwd.TrimEnd('\\', '/');
        if (trimmed.Length == 0)
        {
            return cwd;
        }

        var i = trimmed.LastIndexOfAny(['\\', '/']);
        var last = i < 0 ? trimmed : trimmed[(i + 1)..];

        // A drive root such as "C:" has no folder name of its own.
        return last.EndsWith(':') ? trimmed + "\\" : last;
    }

    public static string Duration(TimeSpan d)
    {
        if (d < TimeSpan.Zero)
        {
            d = TimeSpan.Zero;
        }

        if (d.TotalSeconds < 60)
        {
            return "<1 min";
        }

        if (d.TotalMinutes < 60)
        {
            return $"{(int)d.TotalMinutes} min";
        }

        if (d.TotalHours < 24)
        {
            return $"{(int)d.TotalHours} h {d.Minutes} min";
        }

        return $"{(int)d.TotalDays} d {d.Hours} h";
    }

    public static string DisplayName(SessionRecord s) =>
        string.IsNullOrWhiteSpace(s.Name) ? $"pid {s.Pid}" : s.Name;

    /// <summary>Time in the current status as text; empty when the timestamp is unknown (0).</summary>
    public static string SinceText(SessionRecord s, DateTimeOffset now) =>
        s.StatusUpdatedAt > 0 ? Duration(now - DateTimeOffset.FromUnixTimeMilliseconds(s.StatusUpdatedAt)) : string.Empty;

    /// <summary>"folder · status · duration" (the part after the name); duration omitted when unknown.</summary>
    public static string Detail(SessionRecord s, DateTimeOffset now)
    {
        var since = SinceText(s, now);
        var text = $"{FolderName(s.Cwd)} · {StatusText(s)}";
        return since.Length == 0 ? text : $"{text} · {since}";
    }

    /// <summary>Static label for the Dock: "name · folder · status" (no duration, it would go stale).</summary>
    public static string DockTitle(SessionRecord s) => $"{DisplayName(s)} · {FolderName(s.Cwd)} · {StatusText(s)}";

    /// <summary>Full tooltip: "name · folder · status · duration".</summary>
    public static string Tooltip(SessionRecord s, DateTimeOffset now) => $"{DisplayName(s)} · {Detail(s, now)}";
}
