using ManagedWinapi.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

public static class SecurityKeyChooser {

    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    private const string WINDOW_CLASS_NAME = "Credential Dialog Xaml Host";

    public static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        Console.WriteLine();
        if (!isFidoPromptWindow(fidoPrompt)) {
            Console.WriteLine($"Window 0x{fidoPrompt.HWnd:x} is not a Windows Security window");
            return;
        }

        AutomationElement  fidoEl            = fidoPrompt.toAutomationElement();
        AutomationElement? outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
        AutomationElement? promptTitleEl = outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
            singletonSafePropertyCondition(AutomationElement.NameProperty, false, I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY))));

        if (outerScrollViewer == null || promptTitleEl == null) {
            Console.WriteLine("Window is not a passkey choice prompt");
            return;
        }

        Condition idCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList");
        List<AutomationElement> authenticatorChoices = Retrier.Attempt(_ =>
                outerScrollViewer.FindFirst(TreeScope.Children, idCondition).children().ToList(),
            maxAttempts: 18, // ~5 sec
            delay: attempt => TimeSpan.FromMilliseconds(Math.Min(500, 1 << attempt)));

        IEnumerable<string> securityKeyLabelPossibilities = I18N.getStrings(I18N.Key.SECURITY_KEY);
        if (authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, securityKeyLabelPossibilities)) is not { } securityKeyChoice) {
            Console.WriteLine("USB security key is not a choice, skipping");
            return;
        }

        ((SelectionItemPattern) securityKeyChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Console.WriteLine("USB security key selected");

        AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
            nextButton.SetFocus();
            Console.WriteLine("Shift is pressed, not submitting dialog box");
            return;
        } else if (!authenticatorChoices.All(choice => choice == securityKeyChoice || nameContainsAny(choice, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            nextButton.SetFocus();
            Console.WriteLine("Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), " +
                "skipping because the user might want to choose it");
            return;
        }

        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Console.WriteLine("Next button pressed");
    }

    // name/title are localized, so don't use those
    public static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == WINDOW_CLASS_NAME;

    private static bool nameContainsAny(AutomationElement element, IEnumerable<string?> possibleSubstrings) {
        string name = element.Current.Name;
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