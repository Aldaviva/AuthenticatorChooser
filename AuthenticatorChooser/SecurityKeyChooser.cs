using ManagedWinapi.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    private static readonly TimeSpan UI_RETRY_DELAY = TimeSpan.FromMilliseconds(8);

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        if (isFidoPromptWindow(fidoPrompt)) {
            AutomationElement  fidoEl            = fidoPrompt.toAutomationElement();
            AutomationElement? outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
            AutomationElement? promptTitleEl = outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                singleSafePropertyCondition(AutomationElement.NameProperty, I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY))));

            if (outerScrollViewer != null && promptTitleEl != null) {
                List<AutomationElement> listItems = Retrier.Attempt(_ =>
                        outerScrollViewer.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList")).children().ToList(),
                    maxAttempts: 25, delay: _ => UI_RETRY_DELAY);

                if (listItems.FirstOrDefault(listItem => nameContainsAny(listItem, I18N.getStrings(I18N.Key.SECURITY_KEY))) is { } securityKeyButton) {
                    ((SelectionItemPattern) securityKeyButton.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

                    if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift)
                        && listItems.All(listItem => listItem == securityKeyButton || nameContainsAny(listItem, I18N.getStrings(I18N.Key.SMARTPHONE)))) {

                        AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
                        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    } // Otherwise shift key was held down, or prompt contained extra options besides USB security key and pairing a new phone, such as an existing paired phone, PIN, or fingerprint
                }     // Otherwise USB security key was not an option
            }         // Otherwise could not find title, might be a UAC prompt
        }             // Otherwise not a credential prompt, wrong window class name
    }

    // name/title are localized, so don't use those
    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == "Credential Dialog Xaml Host";

    private static bool nameContainsAny(AutomationElement element, IEnumerable<string?> suffices) {
        string name = element.Current.Name;
        return suffices.Any(suffix => suffix != null && name.Contains(suffix, StringComparison.CurrentCulture));
    }

    private static Condition singleSafePropertyCondition(AutomationProperty property, IEnumerable<string> allowedNames) {
        Condition[] propertyConditions = allowedNames.Select<string, Condition>(allowedName => new PropertyCondition(property, allowedName)).ToArray();
        return propertyConditions.Length switch {
            0 => Condition.FalseCondition,
            1 => propertyConditions[0],
            _ => new OrCondition(propertyConditions)
        };
    }

}