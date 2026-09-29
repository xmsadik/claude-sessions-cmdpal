using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClaudeSessions.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ClaudeSessions.Notifications;

/// <summary>
/// Shows a Windows toast per <see cref="SessionNotice"/> via the WinRT
/// <see cref="ToastNotificationManager"/> (same approach as SysPulse's AlertNotifier: no COM
/// activator, no click action). Never throws back into the poll loop; after the first display
/// failure (identity/policy) toasts stay off for the life of the process.
/// </summary>
internal sealed partial class SessionNotifier
{
    private const string ToastGroup = "claude-sessions";

    private volatile bool _disabled;

    public void Notify(IReadOnlyList<SessionNotice> notices)
    {
        if (notices.Count == 0 || _disabled)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            foreach (var n in notices)
            {
                Show(n);
            }
        });
    }

    private void Show(SessionNotice n)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            var s = n.Session;
            var folder = SessionPresentation.FolderName(s.Cwd);
            var title = string.IsNullOrWhiteSpace(s.Name) ? folder : s.Name;
            var body = n.Kind == SessionNoticeKind.Waiting
                ? $"{SessionPresentation.Indicator(SessionStatus.Waiting)} {SessionPresentation.StatusText(s)} · {folder}"
                : $"✅ Finished after {SessionPresentation.Duration(n.Worked)} · {folder}";

            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textNodes = xml.GetElementsByTagName("text");

            // DOM text nodes escape "&", "<" etc. themselves, so a path or name can't break the markup.
            textNodes.Item(0).AppendChild(xml.CreateTextNode(title));
            textNodes.Item(1).AppendChild(xml.CreateTextNode(body));

            var toast = new ToastNotification(xml)
            {
                // One toast per session: a newer notice replaces the older one.
                Tag = s.Pid.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Group = ToastGroup,
            };

            // Packaged identity: CreateToastNotifier() with no AUMID argument.
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        catch (Exception ex)
        {
            _disabled = true;
            DiagLog.WriteLine($"Toast display failed, disabling toasts for this session: {ex}");
        }
    }
}
