using AuthenticatorChooser.WindowOpening;
using ManagedWinapi.Windows;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using Microsoft.Win32;
using NLog;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Automation;
using System.Windows.Forms;
using Unfucked;

// ReSharper disable ClassNeverInstantiated.Global - it's actually instantiated by McMaster.Extensions.CommandLineUtils
// ReSharper disable UnassignedGetOnlyAutoProperty - it's actually assigned by McMaster.Extensions.CommandLineUtils

namespace AuthenticatorChooser;

public class Startup {

    private const string PROGRAM_NAME = nameof(AuthenticatorChooser);

    // https://support.microsoft.com/en-us/topic/september-26-2023-kb5030310-os-build-22621-2361-preview-363ac1ae-6ea8-41b3-b3cc-22a2a5682faf
    // ReSharper disable once InconsistentNaming - the version is literally called 22H2
    private static readonly Version WINDOWS_11_22H2_MOMENT4 = new(10, 0, 22621, 2361);
    private static readonly string  PROGRAM_VERSION         = Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);

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
        logger = LogManager.GetCurrentClassLogger();

        try {
            if (help) {
                showUsage();
            } else if (autostartOnLogon) {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", PROGRAM_NAME, $"\"{Environment.ProcessPath}\"");
                MessageBox.Show($"{PROGRAM_NAME} will now start automatically each time you log in to Windows.", PROGRAM_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                using Mutex singleInstanceLock = new(true, $@"Local\{PROGRAM_NAME}_{WindowsIdentity.GetCurrent().User?.Value}", out bool isOnlyInstance);
                if (!isOnlyInstance) {
                    logger.Warn("Another older instance of {program} is already running for this user, this new instance is exiting now.", PROGRAM_NAME);
                    return 2;
                }

                try {
                    logger.Info("{name} {version} starting", PROGRAM_NAME, PROGRAM_VERSION);
                    OsVersion os = OsVersion.create();
                    logger.Info("Operating system is {name} {marketingVersion} {version} {arch}", os.name, os.marketingVersion, os.version, os.architecture);
                    logger.Info("Locales are {locales}", string.Join(", ", I18N.LOCALE_NAMES));

                    using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();

                    bool hasOsBluetoothCtap = os.version >= WINDOWS_11_22H2_MOMENT4;
                    if (hasOsBluetoothCtap) {
                        WindowsSecurityKeyChooser securityKeyChooser = new() { skipAllNonSecurityKeyOptions = skipAllNonSecurityKeyOptions };

                        windowOpeningListener.windowOpened += (_, window) => securityKeyChooser.chooseUsbSecurityKey(window);

                        foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(securityKeyChooser.isFidoPromptWindow)) {
                            securityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
                        }

                        logger.Info("Waiting for Windows Security FIDO dialog boxes to open");
                    } else {
                        ChromiumSecurityKeyChooser securityKeyChooser     = new();
                        SynchronizationContext     synchronizationContext = new WindowsFormsSynchronizationContext(); // Checking keyboard state must run on the UI thread

                        windowOpeningListener.automationElementMaybeOpened += (_, child) => synchronizationContext.Post(_ => securityKeyChooser.chooseUsbSecurityKey(child), null);
                        windowOpeningListener.listenForOpenedChildAutomationElements(ChromiumSecurityKeyChooser.PARENT_WINDOW_CLASS);

                        foreach (AutomationElement chromeChildEl in SystemWindow.FilterToplevelWindows(securityKeyChooser.isFidoPromptWindow)
                                     .SelectMany(chrome => chrome.ToAutomationElement()?.Children() ?? [])) {
                            securityKeyChooser.chooseUsbSecurityKey(chromeChildEl);
                        }

                        logger.Info("Waiting for Chromium FIDO dialog boxes to open");
                    }

                    _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

                    Console.CancelKeyPress += (_, args) => {
                        args.Cancel = true;
                        Application.Exit();
                    };

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
                 Runs this program in the background, waiting for FIDO credential dialog boxes to open and choosing the Security Key option each time.
               
             {processFilename} --autostart-on-logon
                 Registers this program to start automatically every time the current user logs on to Windows.
                 
             {processFilename} --skip-all-non-security-key-options
                 Chooses the Security Key option even if there are other valid options, such as an already-paired phone, or Windows Hello PIN or biometrics. By default, without this option, this program will only choose the Security Key if the sole other option is pairing a new phone. This is an aggressive behavior, so if it skips an option you need, remember that you can hold Shift when the FIDO prompt appears if you need to choose a different option.
                 
             {processFilename} --log[=filename]
                 Runs this program in the background like the first example, and logs debug messages to a text file. If you don't specify a filename, it goes to {Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "%TEMP%", PROGRAM_NAME + ".log")}.
               
             {processFilename} --help
                 Shows usage.
                 
             For more information, see https://github.com/Aldaviva/{PROGRAM_NAME}.
             Press Ctrl+C to copy this message.
             """, $"{PROGRAM_NAME} {PROGRAM_VERSION} usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}