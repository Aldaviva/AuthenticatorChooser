using NLog;
using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

public class Win1123H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1123H2Strategy).FullName!);

    private static readonly Condition NEXT_BUTTON_CONDITION = new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton");

    private bool isLocalWindowsHelloTpmPrompt;

    /**
     * If we're on the TPM dialog, and the user wants to absolutely always use security keys, then we just selected "Use another device" to see the list of all authenticator choices, so the dialog is closing because we selected something, so don't do anything else with the soon to be nonexistant dialog.
     * Otherwise, perform common checks like holding Shift and stopping if there are other options.
     * Finally, click Next.
     */
    public override void submitChoice(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown) {
        /*
         * If the TPM contains a passkey for this RP, Windows will ask for your fingerprint/PIN/face, and you have to select "Use another device" and click Next to see all the authenticator choices.
         * #5, #11: power series backoff, max=500 ms per attempt, ~1 minute total
         */
        if (findAuthenticatorChoices(outerScrollViewer) is not { } authenticatorChoices) {
            LOGGER.Warn("Could not find authenticator choices after retrying for 1 minute. Giving up and not automatically selecting Security Key.");
            return;
        }

        AutomationElement? desiredChoice = getSecurityKeyChoice(authenticatorChoices);
        if (desiredChoice == null && options.skipAllNonSecurityKeyOptions) {
            desiredChoice                = authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.USE_ANOTHER_DEVICE))); // #15
            isLocalWindowsHelloTpmPrompt = desiredChoice != null;
        }

        if (desiredChoice == null) {
            LOGGER.Debug("Desired choice not found, skipping");
            return;
        }

        /*
         * Select the desired choice in preparation to click Next later.
         * However, if we're both on the TPM credential screen (PIN/fingerprint/face) AND the user is holding Shift to not submit the dialog, then don't select it, because that would submit the "Use another device" choice to move from the TPM credential dialog to the authenticator choice list dialog.
         */
        if (!(isLocalWindowsHelloTpmPrompt && isShiftDown)) {
            ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        }

        if (isLocalWindowsHelloTpmPrompt) {
            // do nothing because the prompt either has already closed or will remain open due to Shift being held down
        } else if (fidoEl.FindFirst(TreeScope.Children, NEXT_BUTTON_CONDITION) is not { } nextButton) {
            LOGGER.Error("Could not find Next button in Windows Security dialog box, skipping this dialog box instance");
        } else if (shouldSkipSubmission(desiredChoice, authenticatorChoices, isShiftDown)) {
            // do nothing
        } else {
            ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Info("Next button pressed {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
        }
    }

}