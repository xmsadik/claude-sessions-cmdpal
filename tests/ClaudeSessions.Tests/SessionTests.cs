using ClaudeSessions.Core;
using Xunit;

namespace ClaudeSessions.Tests;

public class SessionParserTests
{
    private const string Sample =
        """{"pid":17560,"sessionId":"3575e38f-x","cwd":"C:\\Users\\user","startedAt":1790685571772,"procStart":"134351591685115485","version":"2.1.284","kind":"interactive","entrypoint":"cli","name":"user-80","nameSource":"derived","status":"busy","updatedAt":1790685928330,"statusUpdatedAt":1790685928330}""";

    [Fact]
    public void Parses_full_record()
    {
        Assert.True(SessionParser.TryParse(Sample, out var r));
        Assert.NotNull(r);
        Assert.Equal(17560, r!.Pid);
        Assert.Equal(@"C:\Users\user", r.Cwd);
        Assert.Equal(134351591685115485L, r.ProcStart);
        Assert.Equal("interactive", r.Kind);
        Assert.Equal("user-80", r.Name);
        Assert.Equal(SessionStatus.Busy, r.Status);
        Assert.Equal(1790685928330L, r.StatusUpdatedAt);
        Assert.Null(r.WaitingFor);
    }

    [Theory]
    [InlineData("busy", SessionStatus.Busy)]
    [InlineData("shell", SessionStatus.Shell)]
    [InlineData("idle", SessionStatus.Idle)]
    [InlineData("waiting", SessionStatus.Waiting)]
    [InlineData("somethingNew", SessionStatus.Idle)]
    [InlineData(null, SessionStatus.Idle)]
    public void Maps_status(string? raw, SessionStatus expected) =>
        Assert.Equal(expected, SessionParser.ParseStatus(raw));

    [Fact]
    public void Reads_waitingFor()
    {
        var json = """{"pid":5,"status":"waiting","waitingFor":"permission prompt","procStart":"1","kind":"interactive"}""";
        Assert.True(SessionParser.TryParse(json, out var r));
        Assert.Equal(SessionStatus.Waiting, r!.Status);
        Assert.Equal("permission prompt", r.WaitingFor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"pid\":12,\"stat")]
    [InlineData("[1,2]")]
    [InlineData("{\"name\":\"no pid\"}")]
    [InlineData("{\"pid\":0}")]
    public void Rejects_partial_or_invalid(string json) =>
        Assert.False(SessionParser.TryParse(json, out _));

    [Fact]
    public void Accepts_numeric_procStart()
    {
        Assert.True(SessionParser.TryParse("""{"pid":9,"procStart":42}""", out var r));
        Assert.Equal(42L, r!.ProcStart);
    }
}

public class SessionChangeDetectorTests
{
    private static SessionRecord Rec(int pid, SessionStatus st = SessionStatus.Idle, string name = "n", string? wf = null, long updated = 1) =>
        new(pid, "sid", @"C:\x", 1, 1, "interactive", name, st, wf, updated);

    [Fact]
    public void Same_is_equal_and_order_insensitive() =>
        Assert.True(SessionChangeDetector.VisiblyEqual([Rec(1), Rec(2)], [Rec(2), Rec(1)]));

    [Fact]
    public void Timestamp_only_change_is_not_visible() =>
        Assert.True(SessionChangeDetector.VisiblyEqual([Rec(1, updated: 1)], [Rec(1, updated: 999)]));

    [Fact]
    public void Pid_set_change_is_visible()
    {
        Assert.False(SessionChangeDetector.VisiblyEqual([Rec(1)], [Rec(1), Rec(2)]));
        Assert.False(SessionChangeDetector.VisiblyEqual([Rec(1)], [Rec(2)]));
    }

    [Fact]
    public void Status_waitingFor_and_name_changes_are_visible()
    {
        Assert.False(SessionChangeDetector.VisiblyEqual([Rec(1)], [Rec(1, SessionStatus.Busy)]));
        Assert.False(SessionChangeDetector.VisiblyEqual([Rec(1, SessionStatus.Waiting, wf: "a")], [Rec(1, SessionStatus.Waiting, wf: "b")]));
        Assert.False(SessionChangeDetector.VisiblyEqual([Rec(1)], [Rec(1, name: "other")]));
    }
}

public class SessionPresentationTests
{
    private static SessionRecord Rec(SessionStatus st, string? wf = null) =>
        new(7, "sid", @"C:\Users\user\proj", 1, 1, "interactive", "my-session", st, wf, 0);

    [Fact]
    public void Status_text()
    {
        Assert.Equal("Working", SessionPresentation.StatusText(Rec(SessionStatus.Busy)));
        Assert.Equal("Idle", SessionPresentation.StatusText(Rec(SessionStatus.Idle)));
        Assert.Equal("Waiting: permission prompt", SessionPresentation.StatusText(Rec(SessionStatus.Waiting, "permission prompt")));
        Assert.Equal("Waiting", SessionPresentation.StatusText(Rec(SessionStatus.Waiting)));
    }

    [Fact]
    public void Indicator_per_status()
    {
        Assert.Equal(SessionPresentation.Indicator(SessionStatus.Busy), SessionPresentation.Indicator(SessionStatus.Shell));
        Assert.NotEqual(SessionPresentation.Indicator(SessionStatus.Busy), SessionPresentation.Indicator(SessionStatus.Waiting));
        Assert.NotEqual(SessionPresentation.Indicator(SessionStatus.Waiting), SessionPresentation.Indicator(SessionStatus.Idle));
    }

    [Theory]
    [InlineData(@"C:\Users\user\proj", "proj")]
    [InlineData(@"C:\Users\user\proj\", "proj")]
    [InlineData("/home/x/y", "y")]
    [InlineData(@"C:\", @"C:\")]
    public void Folder_name(string cwd, string expected) =>
        Assert.Equal(expected, SessionPresentation.FolderName(cwd));

    [Theory]
    [InlineData(5, "<1 min")]
    [InlineData(120, "2 min")]
    [InlineData(3 * 3600 + 5 * 60, "3 h 5 min")]
    [InlineData(2 * 86400 + 3 * 3600, "2 d 3 h")]
    public void Duration_format(int seconds, string expected) =>
        Assert.Equal(expected, SessionPresentation.Duration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Detail_omits_duration_when_timestamp_unknown()
    {
        var rec = Rec(SessionStatus.Busy) with { StatusUpdatedAt = 0 };
        Assert.Equal("proj · Working", SessionPresentation.Detail(rec, DateTimeOffset.UnixEpoch));
        Assert.Equal(string.Empty, SessionPresentation.SinceText(rec, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void DockTitle_has_no_duration()
    {
        var rec = Rec(SessionStatus.Busy) with { StatusUpdatedAt = 8 * 60_000 };
        Assert.Equal("my-session · proj · Working", SessionPresentation.DockTitle(rec));
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("9223372036854775807")]
    [InlineData("253402300800000")]
    public void Parser_treats_out_of_range_timestamps_as_zero(string ms)
    {
        var json = $$"""{"pid":1,"statusUpdatedAt":{{ms}},"updatedAt":{{ms}},"startedAt":{{ms}}}""";
        Assert.True(SessionParser.TryParse(json, out var r));
        Assert.Equal(0, r!.StatusUpdatedAt);
        Assert.Equal(0, r.StartedAt);
    }

    [Fact]
    public void Detector_sees_cwd_change()
    {
        var a = Rec(SessionStatus.Busy);
        Assert.False(SessionChangeDetector.VisiblyEqual([a], [a with { Cwd = "other" }]));
        Assert.True(SessionChangeDetector.VisiblyEqual([a], [a with { }]));
    }

    [Fact]
    public void Tooltip_has_all_parts()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(10 * 60_000);
        var rec = Rec(SessionStatus.Busy) with { StatusUpdatedAt = 8 * 60_000 };
        Assert.Equal("my-session \u00B7 proj \u00B7 Working \u00B7 2 min", SessionPresentation.Tooltip(rec, now));
    }
}

public class SessionScannerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cs-tests-" + Guid.NewGuid().ToString("N"));

    public SessionScannerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, true);
        GC.SuppressFinalize(this);
    }

    private void Write(string name, string json) => File.WriteAllText(Path.Combine(_dir, name), json);

    [Fact]
    public void Filters_kind_and_liveness_and_ignores_key_files()
    {
        Write("1.json", """{"pid":1,"kind":"interactive","status":"busy","startedAt":2}""");
        Write("2.json", """{"pid":2,"kind":"bg","status":"busy","startedAt":1}""");
        Write("3.json", """{"pid":3,"kind":"interactive","status":"idle","startedAt":3}""");
        Write("1.key", "secret");
        var cache = new Dictionary<string, SessionRecord>();

        var result = SessionScanner.Scan(_dir, cache, r => r.Pid != 3);

        Assert.Equal([1], result.Select(r => r.Pid));
    }

    [Fact]
    public void Partial_write_keeps_previous_value_and_deleted_file_drops()
    {
        Write("1.json", """{"pid":1,"kind":"interactive","status":"busy"}""");
        var cache = new Dictionary<string, SessionRecord>();
        Assert.Equal(SessionStatus.Busy, SessionScanner.Scan(_dir, cache, _ => true).Single().Status);

        Write("1.json", "{\"pid\":1,\"kind\":\"inter");
        Assert.Equal(SessionStatus.Busy, SessionScanner.Scan(_dir, cache, _ => true).Single().Status);

        File.Delete(Path.Combine(_dir, "1.json"));
        Assert.Empty(SessionScanner.Scan(_dir, cache, _ => true));
    }

    [Fact]
    public void Store_raises_Changed_only_on_visible_change()
    {
        Write("1.json", """{"pid":1,"kind":"interactive","status":"busy","statusUpdatedAt":1}""");
        using var store = new SessionStore(() => TimeSpan.FromSeconds(2), _dir, _ => true);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        Assert.True(store.Refresh());
        Assert.False(store.Refresh());

        Write("1.json", """{"pid":1,"kind":"interactive","status":"busy","statusUpdatedAt":99}""");
        Assert.False(store.Refresh());

        Write("1.json", """{"pid":1,"kind":"interactive","status":"waiting","waitingFor":"input needed"}""");
        Assert.True(store.Refresh());
        Assert.Equal(2, raised);
    }
}

public class ProcessLivenessTests
{
    [Fact]
    public void Current_process_alive_only_with_matching_start_time()
    {
        var me = System.Diagnostics.Process.GetCurrentProcess();
        var procStart = me.StartTime.ToFileTime();
        var good = new SessionRecord(me.Id, "s", "c", 0, procStart, "interactive", "n", SessionStatus.Idle, null, 0);
        Assert.True(ProcessLiveness.IsAlive(good));
        Assert.False(ProcessLiveness.IsAlive(good with { ProcStart = procStart + 1 }));
        Assert.True(ProcessLiveness.IsAlive(good with { ProcStart = 0 }));
    }

    [Fact]
    public void Exited_process_is_not_alive_even_with_zero_procStart()
    {
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c exit 0") { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit();
        var rec = new SessionRecord(p.Id, "s", "c", 0, 0, "interactive", "n", SessionStatus.Idle, null, 0);
        Assert.False(ProcessLiveness.IsAlive(rec));
    }
}
