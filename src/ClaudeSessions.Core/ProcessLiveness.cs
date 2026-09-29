namespace ClaudeSessions.Core;

/// <summary>PID-reuse-safe liveness: the process creation FILETIME must equal the file's procStart (when recorded) and the process must not have exited.</summary>
public static class ProcessLiveness
{
    public static bool IsAlive(SessionRecord s)
    {
        var h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)s.Pid);
        if (h == 0)
        {
            return false;
        }

        try
        {
            if (!NativeMethods.GetProcessTimes(h, out var creation, out var exit, out _, out _))
            {
                return false;
            }

            // A non-zero exit time means the process is gone (a handle can outlive it).
            if (exit.ToInt64() != 0)
            {
                return false;
            }

            // procStart == 0: the file did not record it, so an openable, running process is accepted.
            return s.ProcStart == 0 || creation.ToInt64() == s.ProcStart;
        }
        finally
        {
            NativeMethods.CloseHandle(h);
        }
    }
}
