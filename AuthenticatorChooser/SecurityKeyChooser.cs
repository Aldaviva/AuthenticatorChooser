using ManagedWinapi.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    private static readonly TimeSpan UI_RETRY_DELAY = TimeSpan.FromMilliseconds(8);

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        if (!isFidoPromptWindow(fidoPrompt)) {
            // Window is not a Windows Security window
            return;
        }

        AutomationElement  fidoEl            = fidoPrompt.toAutomationElement();
        AutomationElement? outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
        AutomationElement? promptTitleEl = outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
            new OrCondition(I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).Select<string, Condition>(s => new PropertyCondition(AutomationElement.NameProperty, s)).ToArray())));

        if (outerScrollViewer == null || promptTitleEl == null) {
            // Window is not a passkey reading prompt
            return;
        }

        List<AutomationElement> listItems = Retrier.Attempt(_ =>
                outerScrollViewer.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList")).children().ToList(),
            maxAttempts: 25, delay: _ => UI_RETRY_DELAY);

        if (listItems.FirstOrDefault(listItem => nameContainsAny(listItem, I18N.getStrings(I18N.Key.SECURITY_KEY))) is not { } securityKeyButton) {
            // USB security key is not a choice, skipping
            return;
        }

        ((SelectionItemPattern) securityKeyButton.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
            // Shift is pressed, not submitting dialog box
            return;
        } else if (!listItems.All(listItem => listItem == securityKeyButton || nameContainsAny(listItem, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            // Dialog box has a choice that isn't smartphone or USB security key (such as PIN or biometrics), skipping because the user might want to choose it
            return;
        }

        AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static bool nameContainsAny(AutomationElement element, IEnumerable<string?> suffices) {
        string name = element.Current.Name;
        return suffices.Any(suffix => suffix != null && name.Contains(suffix, StringComparison.CurrentCulture));
    }

    // name/title are localized, so don't use those
    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == "Credential Dialog Xaml Host";

}