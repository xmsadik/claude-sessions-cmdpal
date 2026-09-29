using System;
using ClaudeSessions.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeSessions;

/// <summary>Click / Enter on a session: focus its terminal tab, then close the palette.</summary>
internal sealed partial class FocusSessionCommand : InvokableCommand
{
    private readonly int _pid;

    public FocusSessionCommand(SessionRecord session, string name = "Focus")
    {
        _pid = session.Pid;

        // Command.Id must be non-empty for dock items too.
        Id = $"ClaudeSessions.focus.{session.Pid}";
        Name = name;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            var outcome = TerminalFocuser.Focus(_pid);
            if (outcome.Result is FocusResult.AttachFailed or FocusResult.NoWindow)
            {
                return CommandResult.ShowToast("Couldn't reach this session's terminal (running as administrator?)");
            }
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"Invoke pid={_pid} failed: {ex}");
        }

        return CommandResult.Dismiss();
    }
}
