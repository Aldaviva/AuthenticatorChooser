using ManagedWinapi.Windows;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;
using ThrottleDebounce;
using Unfucked;

namespace AuthenticatorChooser;

public class WindowsSecurityKeyChooser: AbstractSecurityKeyChooser<SystemWindow> {

    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    private const string WINDOW_CLASS_NAME  = "Credential Dialog Xaml Host";
    private const string ALT_TAB_CLASS_NAME = "XamlExplorerHostIslandWindow";

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(WindowsSecurityKeyChooser).FullName);

    public bool skipAllNonSecurityKeyOptions { get; init; }

    public override void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        Stopwatch overallStopwatch = Stopwatch.StartNew();
        try {
            if (!isFidoPromptWindow(fidoPrompt)) {
                LOGGER.Trace("Window 0x{hwnd:x} is not a Windows Security window", fidoPrompt.HWnd);
                return;
            }

            if (SystemWindow.ForegroundWindow.ClassName == ALT_TAB_CLASS_NAME) { // #8
                LOGGER.Debug("Alt+Tab is being held, not interacting with Windows Security window");
                return;
            }

            AutomationElement? fidoEl            = fidoPrompt.ToAutomationElement();
            AutomationElement? outerScrollViewer = fidoEl?.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
            if (outerScrollViewer?.FindFirst(TreeScope.Children, new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
                    singletonSafePropertyCondition(AutomationElement.NameProperty, false,
                        I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).Concat(skipAllNonSecurityKeyOptions ? I18N.getStrings(I18N.Key.MAKING_SURE_ITS_YOU) : [])))) == null) { // #4, #15
                LOGGER.Debug("Window is not a passkey choice prompt");
                return;
            }

            LOGGER.Trace("Window 0x{hwnd:x} is a Windows Security window", fidoPrompt.HWnd);

            Condition credentialsListIdCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList");

            Stopwatch                      authenticatorChoicesStopwatch = Stopwatch.StartNew();
            ICollection<AutomationElement> authenticatorChoices;
            try {
                // #5, #11: power series backoff, max=500 ms per attempt, ~1 minute total
                authenticatorChoices = Retrier.Attempt(_ => outerScrollViewer.FindFirst(TreeScope.Children, credentialsListIdCondition).Children().ToList(),
                    maxAttempts: 127, delay: Retrier.Delays.Power(TimeSpan.FromMilliseconds(1), max: TimeSpan.FromMilliseconds(500)));
                LOGGER.Trace("Found authenticator choices after {0:N3} sec", authenticatorChoicesStopwatch.Elapsed.TotalSeconds);
            } catch (Exception e) when (e is not OutOfMemoryException) {
                LOGGER.Warn(e, "Could not find authenticator choices after retrying for {0:N3} sec due to the following exception. Giving up and not automatically selecting Security Key.",
                    authenticatorChoicesStopwatch.Elapsed.TotalSeconds);
                return;
            }

            bool isLocalWindowsHelloTpmPrompt = false;

            AutomationElement? desiredChoice = authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, I18N.getStrings(I18N.Key.SECURITY_KEY)));
            if (desiredChoice == null && skipAllNonSecurityKeyOptions) {
                desiredChoice                = authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, I18N.getStrings(I18N.Key.USE_ANOTHER_DEVICE))); // #15
                isLocalWindowsHelloTpmPrompt = desiredChoice != null;
            }

            if (desiredChoice == null) {
                LOGGER.Debug("Desired choice not found, skipping");
                return;
            }

            bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (!(isLocalWindowsHelloTpmPrompt && isShiftDown)) {
                ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                LOGGER.Info("{choice} selected after {0:N3} sec", isLocalWindowsHelloTpmPrompt ? "Use another device" : "USB security key", overallStopwatch.Elapsed.TotalSeconds);
            }

            if (isLocalWindowsHelloTpmPrompt) {
                return;
            }

            AutomationElement? nextButton = fidoEl!.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
            if (nextButton == null) {
                LOGGER.Error("Could not find Next button in Windows Security dialog box, skipping this dialog box instance");
            } else if (isShiftDown) {
                nextButton.SetFocus();
                LOGGER.Info("Shift is pressed, not submitting dialog box");
            } else if (!skipAllNonSecurityKeyOptions && !authenticatorChoices.All(choice => choice == desiredChoice || nameContainsAny(choice, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
                nextButton.SetFocus();
                LOGGER.Info(
                    "Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), skipping because the user might want to choose it. You may override this behavior with --skip-all-non-security-key-options.");
            } else {
                ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                LOGGER.Info("Next button pressed after {0:N3} sec", overallStopwatch.Elapsed.TotalSeconds);
            }
        } catch (COMException e) {
            LOGGER.Error(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        }
    }

    // Window name and title are localized, so don't match against those
    public override bool isFidoPromptWindow(SystemWindow window) => window.ClassName == WINDOW_CLASS_NAME;

}