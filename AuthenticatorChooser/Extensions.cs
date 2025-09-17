using System.Windows.Automation;

namespace AuthenticatorChooser;

public static class Extensions {

    public static bool nameContainsAny(this AutomationElement element, IEnumerable<string> possibleSubstrings) {
        string name = element.Current.Name;
        // #2: in addition to a prefix, there is sometimes also a suffix after the substring
        return possibleSubstrings.Any(possibleSubstring => name.Contains(possibleSubstring, StringComparison.CurrentCulture));
    }

}