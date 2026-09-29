namespace ClaudeSessions.Core;

/// <summary>Compares terminal tab titles while ignoring Claude Code's animated spinner prefix.</summary>
public static class TabTitle
{
    /// <summary>
    /// Drops the leading spinner/status glyph run (e.g. "◑ ", "✳ ", "⠂ ") and surrounding whitespace,
    /// so "◑ Fix the build" and "✳ Fix the build" compare equal.
    /// </summary>
    public static string Normalize(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        var span = title.AsSpan();
        var start = 0;
        while (start < span.Length && !char.IsLetterOrDigit(span[start]))
        {
            start++;
        }

        return span[start..].Trim().ToString();
    }
}
