using ManagedWinapi.Windows;
using NLog;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    private const string WINDOW_CLASS_NAME = "Credential Dialog Xaml Host";

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        try {
            if (!isFidoPromptWindow(fidoPrompt)) {
                LOGGER.Trace("Window 0x{hwnd:x} is not a Windows Security window", fidoPrompt.HWnd);
                return;
            }

            if (isAltTabActive()) { // #8
                LOGGER.Debug("Alt+Tab is being held, not interacting with Windows Security window");
                return;
            }

            AutomationElement  fidoEl            = fidoPrompt.toAutomationElement();
            AutomationElement? outerScrollViewer = findOuterScrollViewer(fidoEl);
            if (outerScrollViewer == null || !isPasskeyChoiceWindow(outerScrollViewer)) { // #4
                LOGGER.Debug("Window is not a passkey choice prompt");
                return;
            }

            ICollection<AutomationElement> authenticatorChoices = findAuthenticatorChoices(outerScrollViewer);
            AutomationElement?             securityKeyChoice    = findSecurityKeyChoice(authenticatorChoices);
            if (securityKeyChoice == null) {
                LOGGER.Debug("USB security key is not a choice, skipping");
                return;
            }

            selectSecurityKeyChoice(securityKeyChoice);
            LOGGER.Info("USB security key selected");

            AutomationElement nextButton = findNextButton(fidoEl)!;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                nextButton.SetFocus();
                LOGGER.Info("Shift is pressed, not submitting dialog box");
                return;
            } else if (hasExtraChoice(authenticatorChoices, securityKeyChoice)) {
                nextButton.SetFocus();
                LOGGER.Info("Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), " +
                    "skipping because the user might want to choose it");
                return;
            }

            pressNextButton(nextButton);
            LOGGER.Info("Next button pressed");
        } catch (COMException e) {
            LOGGER.Warn(e, "UI Automation error");
        }
    }

    // Window name and title are localized, so don't match against those
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == WINDOW_CLASS_NAME;

    private static bool isAltTabActive() => SystemWindow.ForegroundWindow.ClassName == "XamlExplorerHostIslandWindow";

    private static AutomationElement? findOuterScrollViewer(AutomationElement fidoWindow) =>
        fidoWindow.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));

    private static bool isPasskeyChoiceWindow(AutomationElement? outerScrollViewer) =>
        outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
            singletonSafePropertyCondition(AutomationElement.NameProperty, false, I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY)))) != null;

    private static ICollection<AutomationElement> findAuthenticatorChoices(AutomationElement outerScrollViewer) {
        Condition idCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList");
        return Retrier.Attempt(_ =>
                outerScrollViewer.FindFirst(TreeScope.Children, idCondition).children().ToList(),
            maxAttempts: 18, // #5: ~5 sec
            delay: attempt => TimeSpan.FromMilliseconds(Math.Min(500, 1 << attempt)));
    }

    private static AutomationElement? findSecurityKeyChoice(ICollection<AutomationElement> authenticatorChoices) {
        IEnumerable<string> securityKeyLabelPossibilities = I18N.getStrings(I18N.Key.SECURITY_KEY);
        return authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, securityKeyLabelPossibilities));
    }

    private static void selectSecurityKeyChoice(AutomationElement securityKeyChoice) =>
        ((SelectionItemPattern) securityKeyChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

    private static AutomationElement? findNextButton(AutomationElement fidoWindow) =>
        fidoWindow.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));

    private static bool hasExtraChoice(ICollection<AutomationElement> authenticatorChoices, AutomationElement securityKeyChoice) =>
        !authenticatorChoices.All(choice => choice == securityKeyChoice || nameContainsAny(choice, I18N.getStrings(I18N.Key.SMARTPHONE)));

    private static void pressNextButton(AutomationElement nextButton) =>
        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

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