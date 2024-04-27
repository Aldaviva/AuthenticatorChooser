using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using SimWinInput;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Input;
using ThrottleDebounce;
using Cursor = System.Windows.Forms.Cursor;
using Point = System.Drawing.Point;

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

        try {
            Thread.Sleep(100);
            const nint TOP     = 0;
            const nint TOPMOST = -1;
            fidoPrompt.TopMost = true;
            SetWindowPos(fidoPrompt.HWnd, TOP, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE);
            SetWindowPos(fidoPrompt.HWnd, TOPMOST, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE);
            Foregrounder.Foregrounder.BringToForeground(fidoPrompt.HWnd);

            Thread.Sleep(100);
            RECT  fidoWindowPosition = fidoPrompt.Position;
            int   clickPositionX     = (int) (fidoWindowPosition.Left + fidoWindowPosition.Width / 2.0);
            int   clickPositionY     = (int) (fidoWindowPosition.Top + fidoWindowPosition.Height / 2.0);
            Point oldMousePosition   = Cursor.Position;
            SimMouse.Click(MouseButtons.Left, clickPositionX, clickPositionY);
            SimMouse.Act(SimMouse.Action.MoveOnly, oldMousePosition.X, oldMousePosition.Y);
            Console.WriteLine($"Set window 0x{fidoPrompt.HWnd:x} to be always on top");
        } catch (Exception e) when (e is not OutOfMemoryException) {
            Console.WriteLine("Failed to set credential window to be always on top");
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags flags);

}

[Flags]
internal enum SetWindowPosFlags: uint {

    SWP_ASYNCWINDOWPOS = 0x4000,
    SWP_DEFERERASE     = 0x2000,
    SWP_DRAWFRAME      = 0x20,
    SWP_HIDEWINDOW     = 0x80,
    SWP_NOACTIVATE     = 0x10,
    SWP_NOCOPYBITS     = 0x100,
    SWP_NOMOVE         = 0x2,
    SWP_NOOWNERZORDER  = 0x200,
    SWP_NOREDRAW       = 0x8,
    SWP_NOSENDCHANGING = 0x400,
    SWP_NOSIZE         = 0x1,
    SWP_NOZORDER       = 0x4,
    SWP_SHOWWINDOW     = 0x40

}