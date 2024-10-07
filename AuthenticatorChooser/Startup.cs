using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using Microsoft.Win32;
using NLog;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

// ReSharper disable ClassNeverInstantiated.Global - it's actually instantiated by McMaster.Extensions.CommandLineUtils
// ReSharper disable UnassignedGetOnlyAutoProperty - it's actually assigned by McMaster.Extensions.CommandLineUtils

namespace AuthenticatorChooser;

public class Startup {

    private const string PROGRAM_NAME = nameof(AuthenticatorChooser);

    private static readonly string PROGRAM_VERSION = Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);

    private static Logger? logger;

    [Option(DefaultHelpOptionConvention.DefaultHelpTemplate, CommandOptionType.NoValue)]
    public bool help { get; }

    [Option("--autostart-on-logon", CommandOptionType.NoValue)]
    public bool autostartOnLogon { get; }

    [Option("-l|--log", CommandOptionType.SingleOrNoValue)]
    public (bool enabled, string filename) log { get; }

    [STAThread]
    public static int Main(string[] args) => CommandLineApplication.Execute<Startup>(args);

    public int OnExecute() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Logging.initialize(log.enabled, log.filename);
        logger = LogManager.GetCurrentClassLogger();

        try {
            if (help) {
                showUsage();
            } else if (autostartOnLogon) {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", PROGRAM_NAME, $"\"{Environment.ProcessPath}\"");
            } else {
                using Mutex singleInstanceLock = new(true, $@"Local\{PROGRAM_NAME}_{WindowsIdentity.GetCurrent().User?.Value}", out bool isOnlyInstance);
                if (!isOnlyInstance) return 2;
                try {
                    logger.Info("{name} {version} starting", PROGRAM_NAME, PROGRAM_VERSION);
                    (string name, string marketingVersion, Version version, string arch) os = getOsVersion();
                    logger.Info("Operating system is {name} {marketingVersion} {version} {arch}", os.name, os.marketingVersion, os.version, os.arch);
                    logger.Info("Locales are {userLocale} (user) and {systemLocale} (system)", I18N.userLocaleName, I18N.systemLocaleName);

                    using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
                    windowOpeningListener.windowOpened += (_, window) => SecurityKeyChooser.chooseUsbSecurityKey(window);

                    foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(SecurityKeyChooser.isFidoPromptWindow)) {
                        SecurityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
                    }

                    _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

                    Console.CancelKeyPress += (_, args) => {
                        args.Cancel = true;
                        Application.Exit();
                    };

                    logger.Info("Waiting for Windows Security dialog boxes to open");
                    Application.Run();
                } finally {
                    singleInstanceLock.ReleaseMutex();
                }
            }

            return 0;
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.Error(e, "Uncaught exception");
            MessageBox.Show($"Uncaught exception: {e}", PROGRAM_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        } finally {
            LogManager.Shutdown();
        }
    }

    private static void showUsage() {
        string processFilename = Path.GetFileName(Environment.ProcessPath)!;
        MessageBox.Show(
            $"""
             {processFilename}
                 Runs this program in the background, waiting for FIDO credentials dialog boxes to open and choosing the Security Key option each time.
               
             {processFilename} --autostart-on-logon
                 Registers this program to start automatically every time the current user logs on to Windows.
                 
             {processFilename} --log[=filename]
                 Runs this program in the background like the first example, and logs debug messages to a text file. If you don't specify a filename, it goes to %TEMP%\{PROGRAM_NAME}.log.
               
             {processFilename} --help
                 Shows usage.
                 
             For more information, see https://github.com/Aldaviva/{PROGRAM_NAME}
             Press Ctrl+C to copy this message
             """, $"{PROGRAM_NAME} {PROGRAM_VERSION} usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static (string name, string marketingVersion, Version version, string architecture) getOsVersion() {
        const string NT_CURRENTVERSION_KEY = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        using ManagementObjectSearcher   wmiSearch  = new(new SelectQuery("Win32_OperatingSystem", null, ["Caption", "Version"]));
        using ManagementObjectCollection wmiResults = wmiSearch.Get();
        using ManagementObject           wmiResult  = wmiResults.Cast<ManagementObject>().First();

        return (name: (string) wmiResult["Caption"],
                marketingVersion: Registry.GetValue(NT_CURRENTVERSION_KEY, "DisplayVersion", null) as string ?? string.Empty,
                version: Version.Parse($"{wmiResult["Version"]}.{Registry.GetValue(NT_CURRENTVERSION_KEY, "UBR", 0) as int? ?? 0}"),
                architecture: Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? string.Empty);
    }

}