using ClaudeSessions.Core;
using Xunit;

namespace ClaudeSessions.Tests;

public class SessionTransitionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static SessionRecord S(int pid, SessionStatus st) =>
        new(pid, "id", @"C:\a\proj", 0, 0, "interactive", "n", st, st == SessionStatus.Waiting ? "permission prompt" : null, 0);

    private static SessionTransitionTracker Tracker() => new(TimeSpan.FromSeconds(15));

    [Fact]
    public void First_snapshot_is_baseline()
    {
        Assert.Empty(Tracker().Observe([S(1, SessionStatus.Waiting)], T0));
    }

    [Fact]
    public void Entering_waiting_notifies()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Busy)], T0);
        var n = Assert.Single(t.Observe([S(1, SessionStatus.Waiting)], T0.AddSeconds(2)));
        Assert.Equal(SessionNoticeKind.Waiting, n.Kind);
        Assert.Equal(1, n.Session.Pid);
    }

    [Fact]
    public void Staying_in_same_status_does_not_repeat()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Busy)], T0);
        t.Observe([S(1, SessionStatus.Waiting)], T0.AddSeconds(1));
        Assert.Empty(t.Observe([S(1, SessionStatus.Waiting)], T0.AddSeconds(2)));
    }

    [Fact]
    public void Long_turn_finishing_notifies_short_turn_does_not()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Idle), S(2, SessionStatus.Idle)], T0);
        t.Observe([S(1, SessionStatus.Busy), S(2, SessionStatus.Idle)], T0.AddSeconds(1));
        t.Observe([S(1, SessionStatus.Busy), S(2, SessionStatus.Busy)], T0.AddSeconds(20));
        var notices = t.Observe([S(1, SessionStatus.Idle), S(2, SessionStatus.Idle)], T0.AddSeconds(25));

        var n = Assert.Single(notices);
        Assert.Equal(SessionNoticeKind.Finished, n.Kind);
        Assert.Equal(1, n.Session.Pid);
        Assert.Equal(TimeSpan.FromSeconds(24), n.Worked);
    }

    [Fact]
    public void Turn_duration_spans_waiting_and_shell()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Idle)], T0);
        t.Observe([S(1, SessionStatus.Busy)], T0.AddSeconds(1));
        t.Observe([S(1, SessionStatus.Waiting)], T0.AddSeconds(5));
        t.Observe([S(1, SessionStatus.Shell)], T0.AddSeconds(10));
        t.Observe([S(1, SessionStatus.Busy)], T0.AddSeconds(12));
        var n = Assert.Single(t.Observe([S(1, SessionStatus.Idle)], T0.AddSeconds(17)));
        Assert.Equal(TimeSpan.FromSeconds(16), n.Worked);
    }

    [Fact]
    public void New_and_closed_sessions_do_not_notify()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Busy)], T0);
        Assert.Empty(t.Observe([S(2, SessionStatus.Waiting)], T0.AddSeconds(30)));
    }

    [Fact]
    public void Waiting_then_idle_is_not_finished()
    {
        var t = Tracker();
        t.Observe([S(1, SessionStatus.Busy)], T0);
        t.Observe([S(1, SessionStatus.Waiting)], T0.AddSeconds(20));
        Assert.Empty(t.Observe([S(1, SessionStatus.Idle)], T0.AddSeconds(40)));
    }
}
