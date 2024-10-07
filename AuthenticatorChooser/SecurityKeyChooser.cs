using ManagedWinapi.Windows;
using NLog;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    private const string WINDOW_CLASS_NAME  = "Credential Dialog Xaml Host";
    private const string ALT_TAB_CLASS_NAME = "XamlExplorerHostIslandWindow";

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        try {
            if (!isFidoPromptWindow(fidoPrompt)) {
                LOGGER.Trace("Window 0x{hwnd:x} is not a Windows Security window", fidoPrompt.HWnd);
                return;
            }

            if (SystemWindow.ForegroundWindow.ClassName == ALT_TAB_CLASS_NAME) { // #8
                LOGGER.Debug("Alt+Tab is being held, not interacting with Windows Security window");
                return;
            }

            AutomationElement  fidoEl            = fidoPrompt.toAutomationElement();
            AutomationElement? outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
            if (outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                    singletonSafePropertyCondition(AutomationElement.NameProperty, false, I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY)))) == null) { // #4
                LOGGER.Debug("Window is not a passkey choice prompt");
                return;
            }

            Condition           credentialsListIdCondition    = new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList");
            IEnumerable<string> securityKeyLabelPossibilities = I18N.getStrings(I18N.Key.SECURITY_KEY);
            ICollection<AutomationElement> authenticatorChoices = Retrier.Attempt(_ =>
                    outerScrollViewer.FindFirst(TreeScope.Children, credentialsListIdCondition).children().ToList(),
                maxAttempts: 18, // #5: ~5 sec
                delay: attempt => TimeSpan.FromMilliseconds(Math.Min(500, 1 << attempt)));
            AutomationElement? securityKeyChoice = authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, securityKeyLabelPossibilities));
            if (securityKeyChoice == null) {
                LOGGER.Debug("USB security key is not a choice, skipping");
                return;
            }

            ((SelectionItemPattern) securityKeyChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            LOGGER.Info("USB security key selected");

            AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"))!;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                nextButton.SetFocus();
                LOGGER.Info("Shift is pressed, not submitting dialog box");
                return;
            } else if (!authenticatorChoices.All(choice => choice == securityKeyChoice || nameContainsAny(choice, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
                nextButton.SetFocus();
                LOGGER.Info("Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), " +
                    "skipping because the user might want to choose it");
                return;
            }

            ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Info("Next button pressed");
        } catch (COMException e) {
            LOGGER.Warn(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        }
    }

    // Window name and title are localized, so don't match against those
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == WINDOW_CLASS_NAME;

    // private static bool isAltTabWindow(SystemWindow window) => window.ClassName == ALT_TAB_CLASS_NAME;

    private static bool nameContainsAny(AutomationElement element, IEnumerable<string?> possibleSubstrings) {
        string name = element.Current.Name;
        // #2: in addition to a prefix, there is sometimes also a suffix after the substring
        return possibleSubstrings.Any(possibleSubstring => possibleSubstring != null && name.Contains(possibleSubstring, StringComparison.CurrentCulture));
    }

    /// <summary>
    /// <para>Create an <see cref="AndCondition"/> or <see cref="OrCondition"/> for a <paramref name="property"/> from a series of <paramref name="values"/>, which have fewer than 2 items in it.</para>
    /// <para>This avoids a crash in the <see cref="AndCondition"/> and <see cref="OrCondition"/> constructors if the array has size 1.</para>
    /// </summary>
    /// <param name="property">The name of the UI property to match against, such as <see cref="AutomationElement.NameProperty"/> or <see cref="AutomationElement.AutomationIdProperty"/>.</param>
    /// <param name="and"><c>true</c> to make a conjunction (AND), <c>false</c> to make a disjunction (OR)</param>
    /// <param name="values">Zero or more property values to match against.</param>
    /// <returns>A <see cref="Condition"/> that matches the values against the property, without throwing an <see cref="ArgumentException"/> if <paramref name="values"/> has length &lt; 2.</returns>
    private static Condition singletonSafePropertyCondition(AutomationProperty property, bool and, IEnumerable<string> values) {
        Condition[] propertyConditions = values.Select<string, Condition>(allowedValue => new PropertyCondition(property, allowedValue)).ToArray();
        return propertyConditions.Length switch {
            0 => and ? Condition.TrueCondition : Condition.FalseCondition,
            1 => propertyConditions[0],
            _ => and ? new AndCondition(propertyConditions) : new OrCondition(propertyConditions)
        };
    }

}