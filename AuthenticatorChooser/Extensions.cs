using ManagedWinapi.Windows;
using System.Windows.Automation;

namespace AuthenticatorChooser;

public static class Extensions {

    public static IntPtr toHwnd(this AutomationElement element) {
        return new IntPtr(element.Current.NativeWindowHandle);
    }

    public static SystemWindow toSystemWindow(this AutomationElement element) {
        return new SystemWindow(element.toHwnd());
    }

    public static AutomationElement toAutomationElement(this SystemWindow window) {
        return AutomationElement.FromHandle(window.HWnd);
    }

    public static IEnumerable<AutomationElement> children(this AutomationElement parent) {
        return parent.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>();
    }

    /// <summary>Remove null values.</summary>
    /// <returns>Input enumerable with null values removed.</returns>
    public static IEnumerable<T> Compact<T>(this IEnumerable<T?> source) where T: class {
        return source.Where(item => item != null)!;
    }

    /// <summary>Remove null values.</summary>
    /// <returns>Input enumerable with null values removed.</returns>
    public static IEnumerable<T> Compact<T>(this IEnumerable<T?> source) where T: struct {
        return (IEnumerable<T>) source.Where(item => item != null);
    }

}