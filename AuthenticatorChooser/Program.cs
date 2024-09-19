using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using System.Security.Principal;
using System.Windows.Forms;

namespace AuthenticatorChooser;

internal static class Program {

    private const string PROGRAM_NAME = nameof(AuthenticatorChooser);

    private static readonly IEqualityComparer<string> CASE_INSENSITIVE_COMPARER = StringComparer.InvariantCultureIgnoreCase;

    [STAThread]
    public static int Main(string[] args) {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        if (args.Intersect(["--help", "/help", "-h", "/h", "-?", "/?"], CASE_INSENSITIVE_COMPARER).Any()) {
            showUsage();
        } else if (args.Contains("--autostart-on-logon", CASE_INSENSITIVE_COMPARER)) {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", PROGRAM_NAME, $"\"{Environment.ProcessPath}\"");
        } else {
            using Mutex singleInstanceLock = new(true, $@"Local\{PROGRAM_NAME}_{WindowsIdentity.GetCurrent().User?.Value}", out bool isOnlyInstance);
            if (!isOnlyInstance) return 1;

            using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
            windowOpeningListener.windowOpened += (_, window) => SecurityKeyChooser.chooseUsbSecurityKey(window);

            foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(SecurityKeyChooser.isFidoPromptWindow)) {
                SecurityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
            }

            _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

            Application.Run();
        }

        return 0;
    }

    private static void showUsage() {
        string processFilename = Path.GetFileName(Environment.ProcessPath)!;
        MessageBox.Show(
            $"""
             {processFilename}
                 Runs this program in the background normally, waiting for FIDO credentials dialog boxes to open and choosing the Security Key option each time.
               
             {processFilename} --autostart-on-logon
                 Registers this program to start automatically every time the current user logs on to Windows.
               
             {processFilename} --help
                 Shows usage.
                 
             For more information, see https://github.com/Aldaviva/{PROGRAM_NAME}
             (press Ctrl+C to copy this message)
             """, $"{PROGRAM_NAME} usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}