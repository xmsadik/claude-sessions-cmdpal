namespace ClaudeSessions.Core;

/// <summary>Claude Code's per-session status. Unknown values map to <see cref="Idle"/>.</summary>
public enum SessionStatus
{
    Idle,
    Busy,
    Shell,
    Waiting,
}

/// <summary>One parsed <c>~/.claude/sessions/&lt;pid&gt;.json</c> file.</summary>
public sealed record SessionRecord(
    int Pid,
    string SessionId,
    string Cwd,
    long StartedAt,
    long ProcStart,
    string Kind,
    string Name,
    SessionStatus Status,
    string? WaitingFor,
    long StatusUpdatedAt);
