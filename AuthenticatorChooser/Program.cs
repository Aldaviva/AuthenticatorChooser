using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Input;
using ThrottleDebounce;

namespace AuthenticatorChooser;

internal static class Program {

    private static readonly TimeSpan UI_RETRY_DELAY = TimeSpan.FromMilliseconds(8);

    [STAThread]
    public static void Main() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
        windowOpeningListener.windowOpened += (_, window) => chooseUsbSecurityKey(window);

        foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(isFidoPromptWindow)) {
            chooseUsbSecurityKey(fidoPromptWindow);
        }

        Console.WriteLine();
        Application.Run();
    }

    private static void chooseUsbSecurityKey(SystemWindow fidoPrompt) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!isFidoPromptWindow(fidoPrompt)) {
            Console.WriteLine($"Window 0x{fidoPrompt.HWnd:x} is not a Windows Security window");
            return;
        }

        Console.WriteLine($"Found FIDO prompt window (HWND=0x{fidoPrompt.HWnd:x}) after {stopwatch.ElapsedMilliseconds:N0} ms");
        AutomationElement fidoEl = fidoPrompt.toAutomationElement();
        Console.WriteLine($"Converted window to AutomationElement after {stopwatch.ElapsedMilliseconds:N0} ms");

        AutomationElement outerScrollViewer = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
        if (outerScrollViewer.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Sign in with your passkey")) == null) { // not localized by Windows
            Console.WriteLine("Window is not a passkey reading prompt");
            return;
        }

        Console.WriteLine($"Window is the passkey prompt after {stopwatch.ElapsedMilliseconds:N0} ms");

        List<AutomationElement> listItems = Retrier.Attempt(_ =>
                outerScrollViewer.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "CredentialsList")).children().ToList(), // ClassName=ListView
            maxAttempts: 25, delay: _ => UI_RETRY_DELAY, beforeRetry: () => Console.WriteLine("No list found, retrying"));
        Console.WriteLine($"Found list of authenticator choices after {stopwatch.ElapsedMilliseconds:N0} ms");

        if (listItems.FirstOrDefault(listItem => nameEndsWithAny(listItem, I18N.getStrings(I18N.Key.SECURITY_KEY))) is not { } securityKeyButton) {
            Console.WriteLine("USB security key is not a choice, skipping");
            return;
        }

        Console.WriteLine($"Prompted for credential type after {stopwatch.ElapsedMilliseconds:N0} ms");
        ((SelectionItemPattern) securityKeyButton.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Console.WriteLine($"USB key selected after {stopwatch.ElapsedMilliseconds:N0} ms");

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
            Console.WriteLine("Shift is pressed, not submitting dialog box");
            return;
        } else if (!listItems.All(listItem => listItem == securityKeyButton || nameEndsWithAny(listItem, I18N.getStrings(I18N.Key.SMARTPHONE)))) {
            Console.WriteLine("Dialog box has a choice that isn't smartphone or USB security key (such as PIN or biometrics), skipping because the user might want to choose it");
            return;
        }

        AutomationElement nextButton = fidoEl.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
        ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        stopwatch.Stop();
        Console.WriteLine($"Next button clicked after {stopwatch.ElapsedMilliseconds:N0} ms");
    }

    private static bool nameEndsWithAny(AutomationElement element, IEnumerable<string?> suffices) {
        string name = element.Current.Name;
        return suffices.Any(suffix => suffix != null && name.EndsWith(suffix, StringComparison.CurrentCulture));
    }

    // name/title are localized, so don't use those
    private static bool isFidoPromptWindow(SystemWindow window) => window.ClassName == "Credential Dialog Xaml Host";

}