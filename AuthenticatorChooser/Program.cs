using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using System.Windows.Forms;

namespace AuthenticatorChooser;

internal static class Program {

    [STAThread]
    public static void Main() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
        windowOpeningListener.windowOpened += (_, window) => SecurityKeyChooser.chooseUsbSecurityKey(window);

        foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(SecurityKeyChooser.isFidoPromptWindow)) {
            SecurityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
        }

        _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

        Console.WriteLine();
        Application.Run();
    }

}