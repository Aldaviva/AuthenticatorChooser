using ManagedWinapi.Windows;
using NLog;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Input;
using Unfucked;

namespace AuthenticatorChooser;

public class ChromeSecurityKeyChooser: SecurityKeyChooser<AutomationElement> {

    public const string PARENT_WINDOW_CLASS = "Chrome_WidgetWin_1";

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    public void chooseUsbSecurityKey(AutomationElement fidoEl) {
        try {
            if (!fidoEl.Current.Name.Contains("Use a saved passkey for ")) { // TODO localize name
                LOGGER.Trace("Window 0x{hwnd:x} (name: {name}) is not a Chrome passkey prompt", fidoEl.ToHwnd(), fidoEl.Current.Name);
                return;
            }

            AutomationElementCollection buttons = fidoEl.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            AutomationElement? securityKeyButton = buttons.Cast<AutomationElement>().FirstOrDefault(button => button.Current.Name == "Windows Hello or external security key"); // TODO localize name

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