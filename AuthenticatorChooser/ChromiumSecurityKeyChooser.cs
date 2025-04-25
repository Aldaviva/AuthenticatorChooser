using AuthenticatorChooser.Resources;
using ManagedWinapi.Windows;
using NLog;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;

namespace AuthenticatorChooser;

/*
 * Chromium localized strings come from generated_resources_*.xtb in https://chromium.googlesource.com/chromium/src/+/refs/heads/main/chrome/app/resources/
 */
public class ChromiumSecurityKeyChooser: AbstractSecurityKeyChooser<AutomationElement> {

    public const string PARENT_WINDOW_CLASS = "Chrome_WidgetWin_1";

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public override void chooseUsbSecurityKey(AutomationElement fidoEl) {
        try {
            if (!nameContainsAny(fidoEl, I18N.getStrings(I18N.Key.USE_A_SAVED_PASSKEY_FOR))) {
                return;
            }

            AutomationElement? securityKeyButton = fidoEl.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>()
                .FirstOrDefault(button => button.Current.Name == LocalizedStrings.windowsHelloOrExternalSecurityKey);

            if (securityKeyButton == null) {
                LOGGER.Debug("Could not find security key button in FIDO dialog box");
            } else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                securityKeyButton.SetFocus();
                LOGGER.Info("Shift is pressed, not submitting dialog box");
            } else {
                ((InvokePattern) securityKeyButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                LOGGER.Debug("Clicked security key button");
            }
        } catch (COMException e) {
            LOGGER.Warn(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        }
    }

    public override bool isFidoPromptWindow(SystemWindow window) => window.ClassName == PARENT_WINDOW_CLASS;

}