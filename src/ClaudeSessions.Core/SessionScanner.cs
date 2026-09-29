namespace ClaudeSessions.Core;

/// <summary>
/// Reads the sessions directory. Separated from the timer so it is testable: liveness is injected.
/// <c>cache</c> holds the last good record per file so a half-written JSON keeps the previous
/// value for that file until the next round.
/// </summary>
public static class SessionScanner
{
    public static IReadOnlyList<SessionRecord> Scan(
        string directory,
        Dictionary<string, SessionRecord> cache,
        Func<SessionRecord, bool> isAlive)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                seen.Add(path);
                if (TryRead(path, out var text) && SessionParser.TryParse(text, out var rec) && rec is not null)
                {
                    cache[path] = rec;
                }

                // else: unreadable or partially written -> keep whatever the cache holds for this file.
            }
        }

        foreach (var stale in cache.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            cache.Remove(stale);
        }

        return cache.Values
            .Where(r => string.Equals(r.Kind, "interactive", StringComparison.Ordinal) && isAlive(r))
            .OrderBy(r => r.StartedAt)
            .ThenBy(r => r.Pid)
            .ToList();
    }

    private static bool TryRead(string path, out string text)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            text = sr.ReadToEnd();
            return true;
        }
        catch (IOException)
        {
            text = string.Empty;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            text = string.Empty;
            return false;
        }
    }
}
