using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using Microsoft.Win32;
using NLog;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

// ReSharper disable ClassNeverInstantiated.Global - it's actually instantiated by McMaster.Extensions.CommandLineUtils
// ReSharper disable UnassignedGetOnlyAutoProperty - it's actually assigned by McMaster.Extensions.CommandLineUtils

namespace AuthenticatorChooser;

public class Startup {

    private const string PROGRAM_NAME = nameof(AuthenticatorChooser);

    private static readonly string PROGRAM_VERSION = Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);

    private static Logger? logger;

    // #15
    [Option("--skip-all-non-security-key-options", CommandOptionType.NoValue)]
    public bool skipAllNonSecurityKeyOptions { get; }

    [Option("--autostart-on-logon", CommandOptionType.NoValue)]
    public bool autostartOnLogon { get; }

    [Option("-l|--log", CommandOptionType.SingleOrNoValue)]
    public (bool enabled, string? filename) log { get; }

    [Option(DefaultHelpOptionConvention.DefaultHelpTemplate, CommandOptionType.NoValue)]
    public bool help { get; }

    [STAThread]
    public static int Main(string[] args) => CommandLineApplication.Execute<Startup>(args);

    // ReSharper disable once UnusedMember.Global - it's actually invoked by McMaster.Extensions.CommandLineUtils
    // ReSharper disable once InconsistentNaming - it must be named this, as dictated by McMaster.Extensions.CommandLineUtils, it's not my choice
    public int OnExecute() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Logging.initialize(log.enabled, log.filename);
        logger = LogManager.GetLogger(typeof(Startup).FullName);

        try {
            if (help) {
                showUsage();
                return 0;
            }

            if (autostartOnLogon) {
                registerAsStartupProgram();
            }

            using Mutex singleInstanceLock = new(true, $@"Local\{PROGRAM_NAME}_{WindowsIdentity.GetCurrent().User?.Value}", out bool isOnlyInstance);
            if (!isOnlyInstance) {
                logger.Warn("Another older instance of {program} is already running for this user, this new instance is exiting now.", PROGRAM_NAME);
                return 2;
            }

            try {
                logger.Info("{name} {version} starting", PROGRAM_NAME, PROGRAM_VERSION);
                OsVersion os = OsVersion.getCurrent();
                logger.Info("Operating system is {name} {marketingVersion} {version} {arch}", os.name, os.marketingVersion, os.version, os.architecture);
                logger.Info("{Locales are} {locales}", I18N.LOCALE_NAMES.Count == 1 ? "Locale is" : "Locales are", string.Join(", ", I18N.LOCALE_NAMES));

                using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
                WindowsSecurityKeyChooser   securityKeyChooser    = new() { skipAllNonSecurityKeyOptions = skipAllNonSecurityKeyOptions };

                windowOpeningListener.windowOpened += (_, window) => securityKeyChooser.chooseUsbSecurityKey(window);

                foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(securityKeyChooser.isFidoPromptWindow)) {
                    securityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
                }

                logger.Info("Waiting for Windows Security FIDO dialog boxes to open");

                _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

                Console.CancelKeyPress += (_, args) => {
                    args.Cancel = true;
                    Application.Exit();
                };

                Application.Run();
            } finally {
                singleInstanceLock.ReleaseMutex();
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

    private void registerAsStartupProgram() {
        StringBuilder autostartCommand = new();
        autostartCommand.Append('"').Append(Environment.ProcessPath).Append('"');
        if (skipAllNonSecurityKeyOptions) {
            autostartCommand.Append(' ').Append("--skip-all-non-security-key-options");
        }

        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", PROGRAM_NAME, autostartCommand.ToString());
        MessageBox.Show($"{PROGRAM_NAME} is now running in the background, and will also start automatically each time you log in to Windows.", PROGRAM_NAME, MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void showUsage() {
        string processFilename = Path.GetFileName(Environment.ProcessPath)!;
        MessageBox.Show(
            $"""
             {processFilename}
                 Runs this program in the background, waiting for FIDO credential dialog boxes to open and choosing the Security Key option each time.
               
             {processFilename} --autostart-on-logon
                 Registers this program to start automatically every time the current user logs on to Windows, and also leaves it running in the background like the first example.
                 
             {processFilename} --skip-all-non-security-key-options
                 Forces this program to choose the Security Key option even if there are other valid options, such as an already-paired phone or Windows Hello PIN or biometrics. By default, without this option, it will only choose the Security Key if the sole other option is pairing a new phone. This is an aggressive behavior, so if it skips an option you need, remember that you can hold Shift when the FIDO prompt appears to temporarily disable this program and manually choose a different option.
                 
             {processFilename} --log[=filename]
                 Runs this program in the background like the first example, and logs debug messages to a text file. If you don't specify a filename, it goes to {Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "%TEMP%", PROGRAM_NAME + ".log")}.
               
             {processFilename} --help
                 Shows this usage.
                 
             For more information, see https://github.com/Aldaviva/{PROGRAM_NAME}.
             Press Ctrl+C to copy this message.
             """, $"{PROGRAM_NAME} {PROGRAM_VERSION} usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}