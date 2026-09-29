using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ClaudeSessions.Core;

// Minimal UIAutomationClient COM surface, source-generated ([GeneratedComInterface]) so it is
// trim/AOT safe (no built-in COM interop, no System.Windows.Automation which is .NET Framework/WPF
// only). Only the vtable slots we call are typed; earlier slots are placeholders that keep the
// vtable layout correct and are never invoked. Slot order is from UIAutomationClient.h.

[GeneratedComInterface]
[Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
internal partial interface IUIAutomation
{
    void CompareElements0();
    void CompareRuntimeIds1();
    void GetRootElement2();

    void ElementFromHandle(nint hwnd, out IUIAutomationElement element); // 3

    void ElementFromPoint4();
    void GetFocusedElement5();
    void GetRootElementBuildCache6();
    void ElementFromHandleBuildCache7();
    void ElementFromPointBuildCache8();
    void GetFocusedElementBuildCache9();
    void CreateTreeWalker10();
    void ControlViewWalker11();
    void ContentViewWalker12();
    void RawViewWalker13();
    void RawViewCondition14();
    void ControlViewCondition15();
    void ContentViewCondition16();
    void CreateCacheRequest17();

    void CreateTrueCondition(out IUIAutomationCondition condition); // 18
}

[GeneratedComInterface]
[Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
internal partial interface IUIAutomationCondition
{
}

[GeneratedComInterface]
[Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
internal partial interface IUIAutomationElement
{
    void SetFocus0();
    void GetRuntimeId1();
    void FindFirst2();

    void FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray elements); // 3

    void FindFirstBuildCache4();
    void FindAllBuildCache5();
    void BuildUpdatedCache6();
    void GetCurrentPropertyValue7();
    void GetCurrentPropertyValueEx8();
    void GetCachedPropertyValue9();
    void GetCachedPropertyValueEx10();
    void GetCurrentPatternAs11();
    void GetCachedPatternAs12();

    void GetCurrentPattern(int patternId, out nint pattern); // 13

    void GetCachedPattern14();
    void GetCachedParent15();
    void GetCachedChildren16();
    void CurrentProcessId17();

    void CurrentControlType(out int controlType); // 18

    void CurrentLocalizedControlType19();

    void CurrentName([MarshalAs(UnmanagedType.BStr)] out string name); // 20
}

[GeneratedComInterface]
[Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
internal partial interface IUIAutomationElementArray
{
    void Length(out int length); // 0

    void GetElement(int index, out IUIAutomationElement element); // 1
}

[GeneratedComInterface]
[Guid("a8efa66a-0fda-421a-9194-38021f3578ea")]
internal partial interface IUIAutomationSelectionItemPattern
{
    void Select(); // 0
}

/// <summary>Finds tab items in a window's UIA tree and selects one by name.</summary>
internal static class Uia
{
    private const int TreeScopeDescendants = 4;
    private const int UiaTabItemControlTypeId = 50019;
    private const int UiaSelectionItemPatternId = 10010;
    private static readonly Guid ClsidCUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid IidIUIAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private static readonly StrategyBasedComWrappers Wrappers = new();

    public enum SelectOutcome
    {
        Selected,
        NoTabs,
        NotFound,
    }

    /// <summary>One pass: enumerate TabItem descendants of <paramref name="hwnd"/>, select the one named <paramref name="name"/>.</summary>
    public static SelectOutcome TrySelectTab(nint hwnd, string name) =>
        TrySelectTab(hwnd, n => string.Equals(n, name, StringComparison.Ordinal));

    /// <summary>
    /// One pass: select the TabItem whose name satisfies <paramref name="match"/>. Selects only when
    /// exactly one tab matches; several matches are ambiguous and count as <see cref="SelectOutcome.NotFound"/>.
    /// </summary>
    public static SelectOutcome TrySelectTab(nint hwnd, Func<string, bool> match)
    {
        var automation = Create();
        automation.ElementFromHandle(hwnd, out var root);
        automation.CreateTrueCondition(out var all);
        root.FindAll(TreeScopeDescendants, all, out var elements);
        elements.Length(out var count);

        var tabs = 0;
        IUIAutomationElement? hit = null;
        var hits = 0;
        for (var i = 0; i < count; i++)
        {
            elements.GetElement(i, out var el);
            el.CurrentControlType(out var type);
            if (type != UiaTabItemControlTypeId)
            {
                continue;
            }

            tabs++;
            el.CurrentName(out var elName);
            if (match(elName ?? string.Empty))
            {
                hit = el;
                hits++;
            }
        }

        if (hits != 1 || hit is null)
        {
            return tabs == 0 ? SelectOutcome.NoTabs : SelectOutcome.NotFound;
        }

        hit.GetCurrentPattern(UiaSelectionItemPatternId, out var raw);
        if (raw == 0)
        {
            return SelectOutcome.NotFound;
        }

        try
        {
            var pattern = (IUIAutomationSelectionItemPattern)Wrappers.GetOrCreateObjectForComInstance(raw, CreateObjectFlags.None);
            pattern.Select();
        }
        finally
        {
            Marshal.Release(raw);
        }

        return SelectOutcome.Selected;
    }

    private static IUIAutomation Create()
    {
        var clsid = ClsidCUIAutomation;
        var iid = IidIUIAutomation;
        var hr = NativeMethods.CoCreateInstance(in clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, in iid, out var ptr);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            return (IUIAutomation)Wrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }
}
