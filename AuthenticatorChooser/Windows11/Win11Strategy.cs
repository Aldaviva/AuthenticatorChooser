using NLog;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public abstract class Win11Strategy(ChooserOptions options): PromptStrategy {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win11Strategy).FullName!);

    private static readonly Condition CHOICES_LIST_CONDITION = new PropertyCondition(AutomationElement.ClassNameProperty, "ListView");

    protected ChooserOptions options { get; } = options;

    public abstract void submitChoice(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

    protected bool shouldSkipSubmission(AutomationElement desiredChoice, IEnumerable<AutomationElement> authenticatorChoices, bool isShiftDown) {
        if (isShiftDown) {
            LOGGER.Info("Shift is pressed, not submitting dialog box");
            return true;
        } else if (!options.skipAllNonSecurityKeyOptions && !authenticatorChoices.All(choice => choice == desiredChoice || choice.nameContainsAny(I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            LOGGER.Info(
                "Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), skipping because you might want to choose it. You may override this behavior with --skip-all-non-security-key-options.");
            return true;
        } else {
            return false;
        }
    }

    protected static IReadOnlyCollection<AutomationElement>? findAuthenticatorChoices(AutomationElement outerScrollViewer) {
        IReadOnlyList<AutomationElement>? authenticatorChoices =
            outerScrollViewer.WaitForFirst(TreeScope.Children, CHOICES_LIST_CONDITION, el => el.Children().ToList(), TimeSpan.FromMinutes(1), Startup.EXITING);
        if (authenticatorChoices == null) {
            LOGGER.Warn("Could not find authenticator choices after retrying for 1 minute. Giving up and not automatically selecting Security Key.");
        }
        return authenticatorChoices;
    }

    protected static AutomationElement? getSecurityKeyChoice(IEnumerable<AutomationElement> authenticatorChoices) {
        return authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.SECURITY_KEY)));
    }

}