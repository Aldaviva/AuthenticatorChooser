using NLog;
using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

public class Win1125H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1125H2Strategy).FullName!);

    private static readonly PropertyCondition LINK_CONDITION = new(AutomationElement.ClassNameProperty, "Hyperlink");

    /*
     * The choice to use a non-TPM passkey was moved from the list to a separate link in 25H2
     */
    public override AutomationElement? findDesiredChoice(AutomationElement outerScrollViewer, IReadOnlyCollection<AutomationElement> authenticatorChoices) {
        AutomationElement? desiredChoice = base.findDesiredChoice(outerScrollViewer, authenticatorChoices);
        if (desiredChoice == null && options.skipAllNonSecurityKeyOptions &&
            outerScrollViewer.FindFirst(TreeScope.Children, LINK_CONDITION) is { } chooseADifferentPasskeyLink &&
            chooseADifferentPasskeyLink.nameContainsAny(I18N.getStrings(I18N.Key.CHOOSE_A_DIFFERENT_PASSKEY))) {
            desiredChoice = chooseADifferentPasskeyLink;
        }
        return desiredChoice;
    }

    /*
     * Selecting choices in 25H2 automatically submits them, so don't select anything in case Shift is held down or more choices are present, in case the user doesn't want the dialog to be submitted automatically.
     */
    public override void preselectChoice(AutomationElement desiredChoice, bool isShiftDown) { }

    /*
     * Security key choice is selectable, but the "Choose a different passkey" link is invokable
     */
    public override bool submitChoice(AutomationElement desiredChoice, IReadOnlyList<AutomationElement> authenticatorChoices, bool isShiftDown) {
        if (!base.submitChoice(desiredChoice, authenticatorChoices, isShiftDown)) {
            return false;
        } else if (desiredChoice.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selectable)) {
            ((SelectionItemPattern) selectable).Select();
        } else {
            ((InvokePattern) desiredChoice.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        LOGGER.Info("Choice selected {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
        return true;
    }

}