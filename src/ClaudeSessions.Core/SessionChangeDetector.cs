namespace ClaudeSessions.Core;

/// <summary>Decides whether two snapshots differ in anything the UI shows.</summary>
public static class SessionChangeDetector
{
    /// <summary>Compares pid set (order-insensitive), status, waitingFor, name and cwd.</summary>
    public static bool VisiblyEqual(IReadOnlyList<SessionRecord> a, IReadOnlyList<SessionRecord> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        var byPid = new Dictionary<int, SessionRecord>(a.Count);
        foreach (var s in a)
        {
            byPid[s.Pid] = s;
        }

        if (byPid.Count != a.Count)
        {
            return false;
        }

        foreach (var s in b)
        {
            if (!byPid.TryGetValue(s.Pid, out var o)
                || o.Status != s.Status
                || !string.Equals(o.WaitingFor, s.WaitingFor, StringComparison.Ordinal)
                || !string.Equals(o.Name, s.Name, StringComparison.Ordinal)
                || !string.Equals(o.Cwd, s.Cwd, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
