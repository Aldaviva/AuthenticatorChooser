using NLog;
using System.Windows.Automation;
using Unfucked;
using Condition = System.Windows.Automation.Condition;

namespace AuthenticatorChooser.Windows11;

public abstract class Win11Strategy(ChooserOptions options): PromptStrategy {

    protected const int MIN_PIN_LENGTH = 4; // https://support.yubico.com/hc/en-us/articles/4402836718866-Understanding-YubiKey-PINs#h_01HPHYDEAT97H0AJ4SZ48MWHW4

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win11Strategy).FullName!);

    private static readonly   Condition CHOICES_LIST_CONDITION = new PropertyCondition(AutomationElement.ClassNameProperty, "ListView");
    protected static readonly Condition NEXT_BUTTON_CONDITION  = new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton");

    protected ChooserOptions options { get; } = options;

    public abstract bool canHandleTitle(string? actualTitle);
    public abstract Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

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

    protected static async Task<IReadOnlyCollection<AutomationElement>?> findAuthenticatorChoices(AutomationElement outerScrollViewer, CancellationToken ct = default) {
        using CancellationTokenSource stopFinding = CancellationTokenSource.CreateLinkedTokenSource(App.Current.exiting, ct);
        IReadOnlyList<AutomationElement>? authenticatorChoices =
            await outerScrollViewer.WaitForFirstAsync(TreeScope.Children, CHOICES_LIST_CONDITION, el => Task.FromResult(el.Children().ToList()), TimeSpan.FromSeconds(30), stopFinding.Token);
        if (authenticatorChoices == null) {
            LOGGER.Warn("Could not find authenticator choices after retrying for 1 minute. Giving up and not automatically selecting Security Key.");
        }
        return authenticatorChoices;
    }

    protected static AutomationElement? getSecurityKeyChoice(IEnumerable<AutomationElement> authenticatorChoices) {
        return authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.SECURITY_KEY)));
    }

    protected static async Task<AutomationElement?> findPinField(AutomationElement outerScrollViewer, CancellationToken ct) =>
        await outerScrollViewer.WaitForFirstAsync(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsPasswordProperty, true),
            TimeSpan.FromMinutes(3), ct);

    protected void autosubmitPin(AutomationElement fidoEl, AutomationElement outerScrollViewer, AutomationElement? pinField = null) {
        CancellationTokenSource windowClosed = new();
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, TreeScope.Element, cleanUp);

        Task.Run(async () => {
            LOGGER.Debug("Waiting for security key PIN prompt to appear");
            pinField ??= await findPinField(outerScrollViewer, windowClosed.Token);

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