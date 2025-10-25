using NLog;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public class Win1125H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private const int MIN_PIN_LENGTH = 4; // https://support.yubico.com/hc/en-us/articles/4402836718866-Understanding-YubiKey-PINs#h_01HPHYDEAT97H0AJ4SZ48MWHW4

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1125H2Strategy).FullName!);

    private static readonly Condition LINK_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "Hyperlink"),
        AutomationElement.NameProperty.singletonSafeCondition(false, I18N.getStrings(I18N.Key.CHOOSE_A_DIFFERENT_PASSKEY)));

    private static readonly Condition AUTHENTICATOR_NAME_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
        new PropertyCondition(AutomationElement.HeadingLevelProperty, AutomationHeadingLevel.None));

    public override bool canHandleTitle(string? actualTitle) => I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY)
        .Concat(options.skipAllNonSecurityKeyOptions || options.autoSubmitPinLength >= MIN_PIN_LENGTH ? I18N.getStrings(I18N.Key.SIGN_IN_WITH_A_PASSKEY) : [])
        .Any(expected => expected.Equals(actualTitle, StringComparison.CurrentCulture));

    public override void handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown) {
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
                if (options.autoSubmitPinLength >= MIN_PIN_LENGTH) {
                    autosubmitPin(fidoEl, outerScrollViewer);
                } else {
                    LOGGER.Debug("The current authenticator is already a security key, so there is nothing to do on this dialog");
                }
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

    private void autosubmitPin(AutomationElement fidoEl, AutomationElement outerScrollViewer) {
        CancellationTokenSource windowClosed = new();
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, TreeScope.Element, cleanUp);

        Task.Run(async () => {
            LOGGER.Debug("Waiting for security key PIN prompt to appear");
            AutomationElement? pinField = await outerScrollViewer.WaitForFirstAsync(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsPasswordProperty, true),
                TimeSpan.FromMinutes(3), windowClosed.Token);

            if (pinField != null) {
                Automation.AddAutomationPropertyChangedEventHandler(pinField, TreeScope.Descendants, onPinTyped, ValuePattern.ValueProperty);

                // skipping this current value read seems to also prevent any events from being fired for some reason
                onPinTyped(this, new AutomationPropertyChangedEventArgs(ValuePattern.ValueProperty, null, ((ValuePattern) pinField.GetCurrentPattern(ValuePattern.Pattern)).Current.Value));
                LOGGER.Debug("Found security key PIN prompt, waiting for the user to type {0:N0} characters before submitting it", options.autoSubmitPinLength);
            } else {
                LOGGER.Debug("No security key PIN prompt found");
            }
        }, windowClosed.Token);

        void onPinTyped(object sender, AutomationPropertyChangedEventArgs e) {
            try {
                // LOGGER.Debug("User typed PIN: {0}", typedPin);
                int typedPinLength = ((string) e.NewValue).Length;
                if (typedPinLength == options.autoSubmitPinLength) {
                    LOGGER.Info("Submitting security key PIN prompt because the user typed {0:N0} characters", typedPinLength);
                    cleanUp();
                    AutomationElement okButton = fidoEl.FindFirst(TreeScope.Children, NEXT_BUTTON_CONDITION);
                    ((InvokePattern) okButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                }
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                LOGGER.Error(e);
            }
        }

        void cleanUp(object? sender = null, AutomationEventArgs? e = null) {
            Automation.RemoveAutomationPropertyChangedEventHandler(fidoEl, onPinTyped);
            Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, cleanUp);
            windowClosed.Cancel();
            windowClosed.Dispose();
            if (sender != null) {
                LOGGER.Debug("Security key PIN window closed");
            }
        }
    }

}