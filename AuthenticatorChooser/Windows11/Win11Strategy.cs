using NLog;
using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

public abstract class Win11Strategy(ChooserOptions options): PromptStrategy {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win11Strategy).FullName!);

    protected ChooserOptions options { get; } = options;

    public virtual AutomationElement? findDesiredChoice(AutomationElement outerScrollViewer, IReadOnlyCollection<AutomationElement> authenticatorChoices) {
        return authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.SECURITY_KEY)));
    }

    public abstract void preselectChoice(AutomationElement desiredChoice, bool isShiftDown);

    public virtual bool submitChoice(AutomationElement desiredChoice, IReadOnlyList<AutomationElement> authenticatorChoices, bool isShiftDown) {
        if (isShiftDown) {
            LOGGER.Info("Shift is pressed, not submitting dialog box");
            return false;
        } else if (!options.skipAllNonSecurityKeyOptions && !authenticatorChoices.All(choice => choice == desiredChoice || choice.nameContainsAny(I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            LOGGER.Info(
                "Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), skipping because you might want to choose it. You may override this behavior with --skip-all-non-security-key-options.");
            return false;
        } else {
            return true;
        }
    }

}