using ManagedWinapi.Windows;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;

namespace AuthenticatorChooser;

/*
 * Chromium localized strings come from generated_resources_*.xtb in https://chromium.googlesource.com/chromium/src/+/refs/heads/main/chrome/app/resources/
 */
public class ChromiumSecurityKeyChooser: AbstractSecurityKeyChooser<AutomationElement> {

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public const string PARENT_WINDOW_CLASS = "Chrome_WidgetWin_1";

    public Stopwatch? mostRecentAutomationEventFired { get; set; }

    private static readonly Condition SECURITY_KEY_BUTTON_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
        singletonSafePropertyCondition(AutomationElement.NameProperty, false, I18N.getStrings(I18N.Key.WINDOWS_HELLO_OR_EXTERNAL_SECURITY_KEY)));

    public override void chooseUsbSecurityKey(AutomationElement fidoEl) {
        try {
            if (!nameContainsAny(fidoEl, I18N.getStrings(I18N.Key.USE_A_SAVED_PASSKEY_FOR))) {
                return;
            }

            AutomationElement? securityKeyButton = fidoEl.FindFirst(TreeScope.Descendants, SECURITY_KEY_BUTTON_CONDITION);

            if (securityKeyButton == null) {
                LOGGER.Warn("Could not find security key button in FIDO dialog box");
            } else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                LOGGER.Info("Shift is pressed, not submitting dialog box");
            } else {
                ((InvokePattern) securityKeyButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                LOGGER.Debug("Security key button pressed after {0:N3} sec", mostRecentAutomationEventFired?.Elapsed.TotalSeconds);
            }
        } catch (COMException e) {
            LOGGER.Warn(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        } catch (ElementNotEnabledException) { }
    }

    public override bool isFidoPromptWindow(SystemWindow window) => window.ClassName == PARENT_WINDOW_CLASS;

}