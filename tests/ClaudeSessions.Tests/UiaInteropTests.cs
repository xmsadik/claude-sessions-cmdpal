using System.Diagnostics;
using ClaudeSessions.Core;
using Xunit;

namespace ClaudeSessions.Tests;

public class UiaInteropTests
{
    /// <summary>
    /// Smoke test for the hand-declared UIA vtable slots: enumerate a real Windows Terminal window
    /// with a marker that cannot exist. Passes silently when no terminal is running.
    /// </summary>
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Enumerates_terminal_tabs_without_com_errors()
    {
        var wt = Process.GetProcessesByName("WindowsTerminal").FirstOrDefault(p => p.MainWindowHandle != 0);
        if (wt is null)
        {
            return;
        }

        Uia.SelectOutcome? outcome = null;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                outcome = Uia.TrySelectTab(wt.MainWindowHandle, "cs-does-not-exist-" + Guid.NewGuid().ToString("N"));
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        Assert.Null(error);
        Assert.NotEqual(Uia.SelectOutcome.Selected, outcome);
    }
}
