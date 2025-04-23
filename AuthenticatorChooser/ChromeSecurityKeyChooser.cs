using AuthenticatorChooser.Resources;
using ManagedWinapi.Windows;
using NLog;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;

namespace AuthenticatorChooser;

public class ChromeSecurityKeyChooser: SecurityKeyChooser<AutomationElement> {

    public const string PARENT_WINDOW_CLASS = "Chrome_WidgetWin_1";

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public void chooseUsbSecurityKey(AutomationElement fidoEl) {
        try {
            if (!fidoEl.Current.Name.Contains(LocalizedStrings.useASavedPasskeyFor)) {
                return;
            }

            AutomationElementCollection buttons = fidoEl.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            AutomationElement? securityKeyButton = buttons.Cast<AutomationElement>().FirstOrDefault(button => button.Current.Name == LocalizedStrings.windowsHelloOrExternalSecurityKey);

            if (securityKeyButton == null) {
                LOGGER.Debug("Could not find security key button in FIDO dialog box");
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                securityKeyButton.SetFocus();
                LOGGER.Info("Shift is pressed, not submitting dialog box");
                return;
            }

            ((InvokePattern) securityKeyButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Debug("Clicked security key button");
        } catch (COMException e) {
            LOGGER.Warn(e, "UI Automation error while selecting security key, skipping this dialog box instance");
        }
    }

    public static bool isChromeWindow(SystemWindow window) => window.ClassName == PARENT_WINDOW_CLASS;

}