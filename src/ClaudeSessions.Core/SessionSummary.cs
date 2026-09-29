namespace ClaudeSessions.Core;

/// <summary>Pure helpers for the compact Dock band: urgency ordering, counts text, band icon.</summary>
public static class SessionSummary
{
    /// <summary>Lower = more urgent: waiting, then working (busy/shell), then idle.</summary>
    public static int Rank(SessionStatus status) => status switch
    {
        SessionStatus.Waiting => 0,
        SessionStatus.Busy or SessionStatus.Shell => 1,
        _ => 2,
    };

    /// <summary>Waiting, working, idle; within a group the most recent status change first.</summary>
    public static IReadOnlyList<SessionRecord> Sorted(IEnumerable<SessionRecord> sessions) =>
        sessions.OrderBy(s => Rank(s.Status)).ThenByDescending(s => s.StatusUpdatedAt).ToList();

    public static (int Waiting, int Working, int Idle) Counts(IEnumerable<SessionRecord> sessions)
    {
        int waiting = 0, working = 0, idle = 0;
        foreach (var s in sessions)
        {
            switch (Rank(s.Status))
            {
                case 0: waiting++; break;
                case 1: working++; break;
                default: idle++; break;
            }
        }

        return (waiting, working, idle);
    }

    /// <summary>"🟠1 🟢2 ⚪3" (waiting, working, idle - most urgent first); zero groups omitted; "—" when empty.</summary>
    public static string CountsText(IEnumerable<SessionRecord> sessions)
    {
        var (waiting, working, idle) = Counts(sessions);
        var parts = new List<string>();
        if (waiting > 0)
        {
            parts.Add(SessionPresentation.Indicator(SessionStatus.Waiting) + waiting);
        }

        if (working > 0)
        {
            parts.Add(SessionPresentation.Indicator(SessionStatus.Busy) + working);
        }

        if (idle > 0)
        {
            parts.Add(SessionPresentation.Indicator(SessionStatus.Idle) + idle);
        }

        return parts.Count == 0 ? "—" : string.Join(' ', parts);
    }

    /// <summary>Icon of the most urgent state present; idle icon when there are no sessions.</summary>
    public static string BandIcon(IEnumerable<SessionRecord> sessions)
    {
        var (waiting, working, _) = Counts(sessions);
        return SessionPresentation.Indicator(
            waiting > 0 ? SessionStatus.Waiting : working > 0 ? SessionStatus.Busy : SessionStatus.Idle);
    }

    /// <summary>"3 Claude sessions · 1 waiting", "No Claude sessions".</summary>
    public static string Subtitle(IEnumerable<SessionRecord> sessions)
    {
        var list = sessions as IReadOnlyCollection<SessionRecord> ?? sessions.ToList();
        if (list.Count == 0)
        {
            return "No Claude sessions";
        }

        var text = $"{list.Count} Claude session{(list.Count == 1 ? string.Empty : "s")}";
        var (waiting, _, _) = Counts(list);
        return waiting > 0 ? $"{text} · {waiting} waiting" : text;
    }

    /// <summary>Escapes '|' and newlines so a value is safe in a markdown table cell.</summary>
    public static string EscapeCell(string s) =>
        s.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    /// <summary>Markdown table of the given (already sorted) sessions; durations computed against <paramref name="now"/>.</summary>
    public static string Markdown(IReadOnlyList<SessionRecord> sorted, DateTimeOffset now)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Claude Sessions").AppendLine();
        sb.AppendLine(Subtitle(sorted)).AppendLine();
        if (sorted.Count == 0)
        {
            return sb.ToString();
        }

        sb.AppendLine("| | Session | Folder | Status | For |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var s in sorted)
        {
            sb.Append("| ").Append(SessionPresentation.Indicator(s.Status))
              .Append(" | ").Append(EscapeCell(SessionPresentation.DisplayName(s)))
              .Append(" | ").Append(EscapeCell(SessionPresentation.FolderName(s.Cwd)))
              .Append(" | ").Append(EscapeCell(SessionPresentation.StatusText(s)))
              .Append(" | ").Append(SessionPresentation.SinceText(s, now))
              .AppendLine(" |");
        }

        return sb.ToString();
    }
}
