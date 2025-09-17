using ManagedWinapi.Windows;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public class WindowsSecurityKeyChooser(ChooserOptions options): AbstractSecurityKeyChooser<SystemWindow> {

    // #4: unfortunately, this class name is shared with the UAC prompt, detectable when desktop dimming is disabled
    private const string WINDOW_CLASS_NAME  = "Credential Dialog Xaml Host";
    private const string ALT_TAB_CLASS_NAME = "XamlExplorerHostIslandWindow";

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(WindowsSecurityKeyChooser).FullName!);

    private static readonly Condition TITLE_CONDITION               = new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock");
    private static readonly Condition CREDENTIALS_LIST_ID_CONDITION = new PropertyCondition(AutomationElement.ClassNameProperty, "ListView");

    private ChooserOptions options = options;

    public override void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        Stopwatch overallStopwatch = Stopwatch.StartNew();
        options = options with { overallStopwatch = overallStopwatch };
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
            if (outerScrollViewer == null) {
                LOGGER.Debug("Window is not a passkey choice prompt because it does not have a ScrollViewer child");
                return;
            } else {
                LOGGER.Trace("Found outerScrollViewer, looking for dialog title");
            }
            options = options with { fidoEl = fidoEl! };

            // #21: title not rendered immediately
            if (outerScrollViewer.WaitForFirst(TreeScope.Children, TITLE_CONDITION, TimeSpan.FromSeconds(5)) is not { } titleLabel) {
                LOGGER.Debug("Window is not a passkey choice prompt because there is no TextBlock child of the ScrollViewer after retrying for {0:N3}", overallStopwatch.Elapsed.TotalSeconds);
                return;
            }

            PromptStrategy strategy;
            string?        actualTitle = titleLabel.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
            if (hasTitle(I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY).Concat(options.skipAllNonSecurityKeyOptions ? I18N.getStrings(I18N.Key.SIGN_IN_WITH_A_PASSKEY) : []))) {
                strategy = new Win1125H2Strategy(options);
            } else if (hasTitle(I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).Concat(options.skipAllNonSecurityKeyOptions ? I18N.getStrings(I18N.Key.MAKING_SURE_ITS_YOU) : []))) {
                strategy = new Win1123H2Strategy(options);
            } else {
                LOGGER.Debug("Window is not a passkey choice prompt because the first TextBlock child of the ScrollViewer has the name {actual}", actualTitle);
                return;
            }

            bool hasTitle(IEnumerable<string> expectedTitles) => expectedTitles.Any(expected => expected.Equals(actualTitle, StringComparison.CurrentCulture));

            LOGGER.Trace("Window 0x{hwnd:x} is a Windows Security window", fidoPrompt.HWnd);

            Stopwatch authenticatorChoicesStopwatch = Stopwatch.StartNew();
            // #5, #11: power series backoff, max=500 ms per attempt, ~1 minute total
            if (outerScrollViewer.WaitForFirst(TreeScope.Children, CREDENTIALS_LIST_ID_CONDITION, el => el.Children().ToList(), TimeSpan.FromMinutes(1)) is { } authenticatorChoices) {
                LOGGER.Trace("Found authenticator choices after retrying for {0:N3} sec", authenticatorChoicesStopwatch.Elapsed.TotalSeconds);
            } else {
                LOGGER.Warn("Could not find authenticator choices after retrying for {0:N3} sec due to the following exception. Giving up and not automatically selecting Security Key.",
                    authenticatorChoicesStopwatch.Elapsed.TotalSeconds);
                return;
            }

            // bool isLocalWindowsHelloTpmPrompt = false;

            /*AutomationElement? desiredChoice = authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, I18N.getStrings(I18N.Key.SECURITY_KEY)));
            if (desiredChoice == null && skipAllNonSecurityKeyOptions && version <= PromptVersion.WIN11_23H2) {
                desiredChoice                = authenticatorChoices.FirstOrDefault(choice => nameContainsAny(choice, I18N.getStrings(I18N.Key.USE_ANOTHER_DEVICE))); // #15
                isLocalWindowsHelloTpmPrompt = desiredChoice != null;
            }*/

            AutomationElement? desiredChoice = strategy.findDesiredChoice(outerScrollViewer, authenticatorChoices);

            if (desiredChoice == null) {
                LOGGER.Debug("Desired choice not found, skipping");
                return;
            }

            bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            strategy.preselectChoice(desiredChoice, isShiftDown);

            /*if ((!isLocalWindowsHelloTpmPrompt || !isShiftDown) && version <= PromptVersion.WIN11_23H2) {
                ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                LOGGER.Info("{choice} selected {0:N3} sec after the dialog appeared", isLocalWindowsHelloTpmPrompt ? "Use another device" : "USB security key", overallStopwatch.Elapsed.TotalSeconds);
            }*/

            /*if (isLocalWindowsHelloTpmPrompt) {
                return;
            }*/

            strategy.submitChoice(desiredChoice, authenticatorChoices, isShiftDown);

            // AutomationElement? nextButton = fidoEl!.FindFirst(TreeScope.Children, NEXT_BUTTON_CONDITION);
            // if (nextButton == null) {
            //     LOGGER.Error("Could not find Next button in Windows Security dialog box, skipping this dialog box instance");
            // } else if (isShiftDown) {
            //     nextButton.SetFocus();
            //     LOGGER.Info("Shift is pressed, not submitting dialog box");
            // } else if (!skipAllNonSecurityKeyOptions && !authenticatorChoices.All(choice => choice == desiredChoice || nameContainsAny(choice, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            //     nextButton.SetFocus();
            //     LOGGER.Info(
            //         "Dialog box has a choice that is neither pairing a new phone nor USB security key (such as an existing phone, PIN, or biometrics), skipping because you might want to choose it. You may override this behavior with --skip-all-non-security-key-options.");
            // } else if (version >= PromptVersion.WIN11_25H2) {
            //     ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            //     LOGGER.Info("Choice selected {0:N3} sec after dialog appeared", overallStopwatch.Elapsed.TotalSeconds);
            // } else {
            //     ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            //     LOGGER.Info("Next button pressed {0:N3} sec after dialog appeared", overallStopwatch.Elapsed.TotalSeconds);
            // }
        } catch (ElementNotAvailableException e) {
            LOGGER.Error(e, "Element in Windows Security dialog box disappeared before this program could interact with it, skipping this dialog box instance");
        } catch (COMException e) {
            LOGGER.Error(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        } catch (Exception e) when (e is not OutOfMemoryException) {
            LOGGER.Error(e, "Uncaught exception while handling Windows Security dialog box, skipping it");
        }
    }

    // Window name and title are localized, so don't match against those
    public override bool isFidoPromptWindow(SystemWindow window) => window.ClassName == WINDOW_CLASS_NAME;

}