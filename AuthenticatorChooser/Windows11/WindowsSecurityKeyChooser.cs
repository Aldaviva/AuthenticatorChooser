using ManagedWinapi.Windows;
using NLog;
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

    private static readonly Condition TITLE_CONDITION = new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock");

    private PromptStrategy? strategy;

    public override void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        options.overallStopwatch.Restart();
        try {
            if (!isFidoPromptWindow(fidoPrompt)) {
                LOGGER.Trace("Window 0x{hwnd:x} is not a Windows Security window", fidoPrompt.HWnd);
                return;
            } else if (SystemWindow.ForegroundWindow.ClassName == ALT_TAB_CLASS_NAME) { // #8
                LOGGER.Debug("Alt+Tab is being held, not interacting with Windows Security window");
                return;
            }

            AutomationElement? fidoEl = fidoPrompt.ToAutomationElement();
            if (fidoEl?.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer")) is not { } outerScrollViewer) {
                LOGGER.Debug("Window is not a passkey choice prompt because it does not have a ScrollViewer child");
                return;
            }

            LOGGER.Trace("Found outerScrollViewer, looking for dialog icon and title");

            // #36: detect OS version using caption icon automation ID instead of title, which collides between versions in 19% of languages
            if (strategy == null) {
                // Icon appears with neither AutomationElement.FindFirst nor Inspect's UIA Content or Control Views, only Raw View
                string? captionIconAutomationId = TreeWalker.RawViewWalker.GetFirstChild(fidoEl).Current.AutomationId;
                strategy = captionIconAutomationId switch {
                    "WindowLogo"         => new Win1123H2Strategy(options),
                    "WindowSecurityLogo" => new Win1125H2Strategy(options),
                    _                    => null
                };

                if (strategy == null) {
                    LOGGER.Error("Unsupported OS dialog version, Windows Security dialog box caption icon has unrecognized automation ID {id}", captionIconAutomationId);
                    return;
                }
            }

            // #21: title not rendered immediately
            if (outerScrollViewer.WaitForFirst(TreeScope.Children, TITLE_CONDITION, TimeSpan.FromSeconds(5), Startup.EXITING) is not { } titleLabel) {
                LOGGER.Debug("Window is not a passkey choice prompt because there is no TextBlock child of the ScrollViewer after retrying for {0:N3}", options.overallStopwatch.Elapsed.TotalSeconds);
                return;
            }

            string? actualTitle = titleLabel.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

            if (!strategy.canHandleTitle(actualTitle)) {
                LOGGER.Debug("Window is not a passkey choice prompt because the first TextBlock child of the ScrollViewer has the name {actual}", actualTitle);
                return;
            } else {
                LOGGER.Trace("Window 0x{hwnd:x} is a Windows Security window", fidoPrompt.HWnd);
            }

            bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            strategy.handleWindow(actualTitle!, fidoEl, outerScrollViewer, isShiftDown);

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