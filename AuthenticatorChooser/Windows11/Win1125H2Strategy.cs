using NLog;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public class Win1125H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1125H2Strategy).FullName!);

    private static readonly Condition LINK_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "Hyperlink"),
        AutomationElement.NameProperty.singletonSafeCondition(false, I18N.getStrings(I18N.Key.CHOOSE_A_DIFFERENT_PASSKEY)));

    private static readonly Condition AUTHENTICATOR_NAME_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
        new PropertyCondition(AutomationElement.HeadingLevelProperty, AutomationHeadingLevel.None));

    public override void submitChoice(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown) {
        if (I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY).Contains(actualTitle, StringComparer.CurrentCulture)) {
            if (findAuthenticatorChoices(outerScrollViewer) is not { } authenticatorChoices) return;

            if (getSecurityKeyChoice(authenticatorChoices) is not { } desiredChoice) {
                LOGGER.Debug("Desired choice not found, skipping");
                return;
            }

            if (!shouldSkipSubmission(desiredChoice, authenticatorChoices, isShiftDown)) {
                ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                LOGGER.Info("Choice selected {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
            }
        } else {
            /*
             * The choice to use a non-TPM passkey was moved from the list to a separate link in 25H2.
             * Here, the user wants to skip all non-security-key options, and this prompt is one of the authenticator challenges like entering a TPM PIN or plugging in a security key
             */
            if (outerScrollViewer.WaitForFirst(TreeScope.Children, AUTHENTICATOR_NAME_CONDITION) is not { } authenticatorNameEl) {
                LOGGER.Debug("Could not find name of the current authenticator while trying to skip a non-security-key option, ignoring dialog");
                return;
            }

            if (I18N.getStrings(I18N.Key.SECURITY_KEY).Contains(authenticatorNameEl.Current.Name, StringComparer.CurrentCulture)) {
                LOGGER.Debug("The current authenticator is already a security key, so there is nothing to do on this dialog");
                return;
            }

            if (outerScrollViewer.FindFirst(TreeScope.Children, LINK_CONDITION) is not { } chooseADifferentPasskeyLink) {
                LOGGER.Warn("Could not find 'Choose a different passkey' link in dialog");
                return;
            }

            ((InvokePattern) chooseADifferentPasskeyLink.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Info("Requested list of all authenticators {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
        }
    }

}