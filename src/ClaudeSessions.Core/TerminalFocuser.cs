using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ClaudeSessions.Core;

public enum FocusResult
{
    /// <summary>Marker tab found and selected; window brought forward.</summary>
    TabSelected,

    /// <summary>No tab strip (single tab); only the window was brought forward.</summary>
    WindowOnly,

    /// <summary>Tabs exist but the marker never showed up; only the window was brought forward.</summary>
    MarkerNotFound,

    AttachFailed,
    NoWindow,
    Error,
}

public readonly record struct FocusOutcome(FocusResult Result, bool IsForeground);

/// <summary>
/// Focuses the Windows Terminal tab hosting a Claude Code session. Everything runs under one
/// static lock on a dedicated STA thread because AttachConsole/FreeConsole are process-wide.
/// NEVER use System.Console in this process: it would touch the console we attach to.
/// </summary>
[SupportedOSPlatform("windows")]
public static unsafe class TerminalFocuser
{
    private const int MarkerAttempts = 4;

    private static readonly object Gate = new();
    private static bool _ctrlHandlerSet;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnCtrl(uint type) => 1;

    public static FocusOutcome Focus(int pid)
    {
        var sw = Stopwatch.StartNew();
        FocusOutcome outcome = new(FocusResult.Error, false);
        var thread = new Thread(() => outcome = FocusOnThread(pid)) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        DiagLog.WriteLine($"Focus pid={pid}: result={outcome.Result} foreground={outcome.IsForeground} elapsedMs={sw.ElapsedMilliseconds}");
        return outcome;
    }

    private static FocusOutcome FocusOnThread(int pid)
    {
        lock (Gate)
        {
            nint hwnd = 0;
            var result = FocusResult.Error;
            try
            {
                // 1. Register a handler that swallows control events while attached. Covers Ctrl+C/Break;
                // CTRL_CLOSE_EVENT may still terminate the process after the handler returns. Accepted
                // residual risk: we are attached for only ~100 ms.
                if (!_ctrlHandlerSet)
                {
                    _ctrlHandlerSet = NativeMethods.SetConsoleCtrlHandler((nint)(delegate* unmanaged[Stdcall]<uint, int>)&OnCtrl, true);
                }

                // 2. Attach to the session's console.
                NativeMethods.FreeConsole();
                if (!NativeMethods.AttachConsole((uint)pid))
                {
                    return new FocusOutcome(FocusResult.AttachFailed, false);
                }

                // 3. Console window -> owning top-level (Windows Terminal) window.
                var console = NativeMethods.GetConsoleWindow();
                hwnd = console == 0 ? 0 : NativeMethods.GetAncestor(console, NativeMethods.GA_ROOTOWNER);
                if (hwnd == 0)
                {
                    result = FocusResult.NoWindow;
                    return new FocusOutcome(result, false);
                }

                // 4. Marker title -> UIA tab.
                result = SelectTabByMarker();
            }
            catch (Exception ex)
            {
                DiagLog.WriteLine($"Focus pid={pid}: exception {ex}");
                result = FocusResult.Error;
            }
            finally
            {
                // 5. Always detach.
                NativeMethods.FreeConsole();
            }

            // 6. Bring the window forward (even if the tab step failed).
            var fg = hwnd != 0 && BringToFront(hwnd);
            return new FocusOutcome(result, fg);
        }
    }

    private static unsafe string GetTitle()
    {
        var buf = new char[2048];
        fixed (char* p = buf)
        {
            var n = NativeMethods.GetConsoleTitle(p, (uint)buf.Length);
            return new string(p, 0, (int)Math.Min(n, (uint)buf.Length));
        }
    }

    private static FocusResult SelectTabByMarker()
    {
        var hwnd = NativeMethods.GetAncestor(NativeMethods.GetConsoleWindow(), NativeMethods.GA_ROOTOWNER);
        var old = GetTitle();
        var marker = "cs-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            NativeMethods.SetConsoleTitle(marker);
            var noTabRounds = 0;
            for (var attempt = 0; attempt < MarkerAttempts; attempt++)
            {
                // A running Claude rewrites the title (spinner); re-assert the marker every round.
                if (attempt > 0)
                {
                    Thread.Sleep(50);
                    NativeMethods.SetConsoleTitle(marker);
                }

                var outcome = Uia.TrySelectTab(hwnd, marker);
                switch (outcome)
                {
                    case Uia.SelectOutcome.Selected:
                        return FocusResult.TabSelected;
                    case Uia.SelectOutcome.NoTabs when ++noTabRounds >= 2:
                        return FocusResult.WindowOnly;
                }
            }

        }
        finally
        {
            NativeMethods.SetConsoleTitle(old);
        }

        // A busy Claude rewrites its title on every spinner frame and keeps overwriting the marker.
        // Fall back to the title itself, spinner stripped, accepted only when exactly one tab matches.
        var wanted = TabTitle.Normalize(old);
        if (wanted.Length > 0)
        {
            var byTitle = Uia.TrySelectTab(hwnd, name => TabTitle.Normalize(name) == wanted);
            if (byTitle == Uia.SelectOutcome.Selected)
            {
                return FocusResult.TabSelected;
            }
        }

        return FocusResult.MarkerNotFound;
    }

    private static bool BringToFront(nint hwnd)
    {
        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        NativeMethods.SwitchToThisWindow(hwnd, true);

        if (NativeMethods.GetForegroundWindow() != hwnd)
        {
            var fgThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
            var ours = NativeMethods.GetCurrentThreadId();
            var attached = fgThread != 0 && fgThread != ours && NativeMethods.AttachThreadInput(fgThread, ours, true);
            try
            {
                NativeMethods.SetForegroundWindow(hwnd);
                NativeMethods.BringWindowToTop(hwnd);
            }
            finally
            {
                if (attached)
                {
                    NativeMethods.AttachThreadInput(fgThread, ours, false);
                }
            }
        }

        return NativeMethods.GetForegroundWindow() == hwnd;
    }
}
