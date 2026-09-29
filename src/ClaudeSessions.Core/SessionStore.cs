namespace ClaudeSessions.Core;

/// <summary>
/// Polls the sessions directory with a PeriodicTimer and raises <see cref="Changed"/> only when
/// something visible changed (see <see cref="SessionChangeDetector"/>).
/// </summary>
public sealed class SessionStore : IDisposable
{
    private readonly string _directory;
    private readonly Func<TimeSpan> _interval;
    private readonly Func<SessionRecord, bool> _isAlive;
    private readonly Dictionary<string, SessionRecord> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private IReadOnlyList<SessionRecord> _current = [];
    private Task? _loop;

    public SessionStore(Func<TimeSpan> interval, string? directory = null, Func<SessionRecord, bool>? isAlive = null)
    {
        _interval = interval;
        _directory = directory ?? DefaultDirectory();
        _isAlive = isAlive ?? ProcessLiveness.IsAlive;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SessionRecord> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions");

    public void Start()
    {
        _loop ??= Task.Run(RunAsync);
    }

    /// <summary>One synchronous scan; returns true if it raised <see cref="Changed"/>.</summary>
    public bool Refresh()
    {
        var next = SessionScanner.Scan(_directory, _cache, _isAlive);
        bool changed;
        lock (_gate)
        {
            changed = !SessionChangeDetector.VisiblyEqual(_current, next);
            _current = next;
        }

        if (changed)
        {
            try
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                DiagLog.WriteLine($"SessionStore.Changed handler failed: {ex}");
            }
        }

        return changed;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(Sanitize(_interval()));
        try
        {
            do
            {
                try
                {
                    Refresh();
                }
                catch (Exception ex)
                {
                    DiagLog.WriteLine($"SessionStore.Refresh failed: {ex}");
                }

                var wanted = Sanitize(_interval());
                if (wanted != timer.Period)
                {
                    timer.Period = wanted;
                }
            }
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static TimeSpan Sanitize(TimeSpan t) => t < TimeSpan.FromMilliseconds(500) ? TimeSpan.FromSeconds(2) : t;
}
