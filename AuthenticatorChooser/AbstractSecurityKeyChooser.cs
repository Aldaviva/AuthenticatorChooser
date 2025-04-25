using ManagedWinapi.Windows;
using System.Windows.Automation;

namespace AuthenticatorChooser;

public abstract class AbstractSecurityKeyChooser<T>: SecurityKeyChooser<T> {

    public abstract void chooseUsbSecurityKey(T fidoPrompt);

    public abstract bool isFidoPromptWindow(SystemWindow window);

    protected static bool nameContainsAny(AutomationElement element, IEnumerable<string?> possibleSubstrings) {
        string name = element.Current.Name;
        // #2: in addition to a prefix, there is sometimes also a suffix after the substring
        return possibleSubstrings.Any(possibleSubstring => possibleSubstring != null && name.Contains(possibleSubstring, StringComparison.CurrentCulture));
    }

}