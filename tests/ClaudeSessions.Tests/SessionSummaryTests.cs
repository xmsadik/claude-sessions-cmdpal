using ClaudeSessions.Core;
using Xunit;

namespace ClaudeSessions.Tests;

public class SessionSummaryTests
{
    private static SessionRecord S(int pid, SessionStatus st, long updated = 0, string name = "n", string cwd = @"C:\a\proj") =>
        new(pid, "id", cwd, 0, 0, "interactive", name, st, null, updated);

    [Fact]
    public void Sorts_waiting_working_idle_then_newest_first()
    {
        var sorted = SessionSummary.Sorted([
            S(1, SessionStatus.Idle, 500),
            S(2, SessionStatus.Busy, 100),
            S(3, SessionStatus.Waiting, 50),
            S(4, SessionStatus.Shell, 200),
            S(5, SessionStatus.Idle, 900)]);
        Assert.Equal([3, 4, 2, 5, 1], sorted.Select(s => s.Pid));
    }

    [Fact]
    public void Counts_text_omits_zero_groups()
    {
        Assert.Equal("🟠1 🟢2 ⚪3", SessionSummary.CountsText([
            S(1, SessionStatus.Busy), S(2, SessionStatus.Shell), S(3, SessionStatus.Waiting),
            S(4, SessionStatus.Idle), S(5, SessionStatus.Idle), S(6, SessionStatus.Idle)]));
        Assert.Equal("⚪1", SessionSummary.CountsText([S(1, SessionStatus.Idle)]));
        Assert.Equal("—", SessionSummary.CountsText([]));
    }

    [Fact]
    public void Band_icon_is_most_urgent()
    {
        Assert.Equal("🟠", SessionSummary.BandIcon([S(1, SessionStatus.Busy), S(2, SessionStatus.Waiting)]));
        Assert.Equal("🟢", SessionSummary.BandIcon([S(1, SessionStatus.Idle), S(2, SessionStatus.Shell)]));
        Assert.Equal("⚪", SessionSummary.BandIcon([S(1, SessionStatus.Idle)]));
        Assert.Equal("⚪", SessionSummary.BandIcon([]));
    }

    [Fact]
    public void Subtitle_pluralizes_and_counts_waiting()
    {
        Assert.Equal("No Claude sessions", SessionSummary.Subtitle([]));
        Assert.Equal("1 Claude session", SessionSummary.Subtitle([S(1, SessionStatus.Idle)]));
        Assert.Equal("3 Claude sessions · 1 waiting",
            SessionSummary.Subtitle([S(1, SessionStatus.Idle), S(2, SessionStatus.Waiting), S(3, SessionStatus.Busy)]));
    }

    [Fact]
    public void Markdown_has_rows_current_durations_and_escapes_pipes()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(10_000_000);
        var started = now.AddMinutes(-5).ToUnixTimeMilliseconds();
        var s = S(1, SessionStatus.Busy, started, "a|b");
        Assert.Contains("| 🟢 | a\\|b | proj | Working | 5 min |", SessionSummary.Markdown([s], now));
        Assert.Contains("| 9 min |", SessionSummary.Markdown([s], now.AddMinutes(4)));
    }
}
