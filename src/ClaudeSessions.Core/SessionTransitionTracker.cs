namespace ClaudeSessions.Core;

public enum SessionNoticeKind
{
    /// <summary>The session started waiting for the user (permission prompt, input, dialog).</summary>
    Waiting,

    /// <summary>The session went from working (busy/shell) to idle after a long enough turn.</summary>
    Finished,
}

public sealed record SessionNotice(SessionNoticeKind Kind, SessionRecord Session, TimeSpan Worked);

/// <summary>
/// Turns successive snapshots into notification-worthy transitions. The first snapshot is the
/// baseline and yields nothing; sessions that appear or disappear yield nothing either.
/// </summary>
public sealed class SessionTransitionTracker
{
    private readonly TimeSpan _minWorked;
    private readonly object _gate = new();
    private Dictionary<int, State>? _states;

    public SessionTransitionTracker(TimeSpan minWorked)
    {
        _minWorked = minWorked;
    }

    public IReadOnlyList<SessionNotice> Observe(IReadOnlyList<SessionRecord> sessions, DateTimeOffset now)
    {
        lock (_gate)
        {
            var baseline = _states is null;
            var previous = _states ?? [];
            var next = new Dictionary<int, State>(sessions.Count);
            var notices = new List<SessionNotice>();

            foreach (var s in sessions)
            {
                if (!previous.TryGetValue(s.Pid, out var old))
                {
                    next[s.Pid] = new State(s.Status, IsWorking(s.Status) ? now : null);
                    continue;
                }

                // Working time spans the whole turn, including any waiting in between; idle ends it.
                var workingSince = s.Status == SessionStatus.Idle ? null : old.WorkingSince ?? (IsWorking(s.Status) ? now : null);
                next[s.Pid] = new State(s.Status, workingSince);

                if (baseline || old.Status == s.Status)
                {
                    continue;
                }

                if (s.Status == SessionStatus.Waiting)
                {
                    notices.Add(new SessionNotice(SessionNoticeKind.Waiting, s, Worked(old, now)));
                }
                else if (s.Status == SessionStatus.Idle && IsWorking(old.Status) && Worked(old, now) >= _minWorked)
                {
                    notices.Add(new SessionNotice(SessionNoticeKind.Finished, s, Worked(old, now)));
                }
            }

            _states = next;
            return notices;
        }
    }

    private static bool IsWorking(SessionStatus status) => status is SessionStatus.Busy or SessionStatus.Shell;

    private static TimeSpan Worked(State old, DateTimeOffset now) =>
        old.WorkingSince is { } since && now > since ? now - since : TimeSpan.Zero;

    private readonly record struct State(SessionStatus Status, DateTimeOffset? WorkingSince);
}
