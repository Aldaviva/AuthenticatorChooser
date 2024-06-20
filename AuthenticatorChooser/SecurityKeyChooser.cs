using ManagedWinapi.Windows;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    private static readonly TimeSpan UI_RETRY_DELAY = TimeSpan.FromMilliseconds(8);

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!isFidoPromptWindow(fidoPrompt)) {
            Console.WriteLine($"Window 0x{fidoPrompt.HWnd:x} is not a Windows Security window");
            return;
        }

        Console.WriteLine($"Found FIDO prompt window (HWND=0x{fidoPrompt.HWnd:x}) after {stopwatch.ElapsedMilliseconds:N0} ms");
        AutomationElement fidoEl = fidoPrompt.toAutomationElement();
        Console.WriteLine($"Converted window to AutomationElement after {stopwatch.ElapsedMilliseconds:N0} ms");

        AutomationElement outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));

        AutomationElement promptTitleEl = outerScrollViewer.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
            new OrCondition(I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).Select<string, Condition>(s => new PropertyCondition(AutomationElement.NameProperty, s)).ToArray())));
        if (promptTitleEl == null) {
            Console.WriteLine("Window is not a passkey reading prompt");
            return;
        } else {
            Console.WriteLine($"Window is the passkey prompt after {stopwatch.ElapsedMilliseconds:N0} ms");
        }

        List<AutomationElement> listItems = Retrier.Attempt(_ =>
                outerScrollViewer.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList")).children().ToList(), // ClassName=ListView
            maxAttempts: 25, delay: _ => UI_RETRY_DELAY, beforeRetry: () => Console.WriteLine("No list found, retrying"));
        Console.WriteLine($"Found list of authenticator choices after {stopwatch.ElapsedMilliseconds:N0} ms");

        if (listItems.FirstOrDefault(listItem => nameContainsAny(listItem, I18N.getStrings(I18N.Key.SECURITY_KEY))) is not { } securityKeyButton) {
            Console.WriteLine("USB security key is not a choice, skipping");
            return;
        }

        Console.WriteLine($"Prompted for credential type after {stopwatch.ElapsedMilliseconds:N0} ms");
        ((SelectionItemPattern) securityKeyButton.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Console.WriteLine($"USB key selected after {stopwatch.ElapsedMilliseconds:N0} ms");

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
            Console.WriteLine("Shift is pressed, not submitting dialog box");
            return;
        } else if (!listItems.All(listItem => listItem == securityKeyButton || nameContainsAny(listItem, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            Console.WriteLine("Dialog box has a choice that isn't smartphone or USB security key (such as PIN or biometrics), skipping because the user might want to choose it");
            return;
        }
        Console.WriteLine($"Checked Shift key and other auth options after {stopwatch.ElapsedMilliseconds:N0} ms");

        AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
        Console.WriteLine($"Found Next button after {stopwatch.ElapsedMilliseconds:N0} ms");
        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        stopwatch.Stop();
        Console.WriteLine($"Next button clicked after {stopwatch.ElapsedMilliseconds:N0} ms");
    }

    private static bool nameContainsAny(AutomationElement element, IEnumerable<string?> suffices) {
        string name = element.Current.Name;
        return suffices.Any(suffix => suffix != null && name.Contains(suffix, StringComparison.CurrentCulture));
    }

    // name/title are localized, so don't use those
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == "Credential Dialog Xaml Host";

}