using AuthenticatorChooser.WindowOpening;
using AuthenticatorChooser.Windows11;
using ManagedWinapi.Windows;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NLog;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

// ReSharper disable ClassNeverInstantiated.Global - it's actually instantiated by McMaster.Extensions.CommandLineUtils
// ReSharper disable UnassignedGetOnlyAutoProperty - it's actually assigned by McMaster.Extensions.CommandLineUtils

namespace AuthenticatorChooser;

public class Startup {

    private const string PROGRAM_NAME = nameof(AuthenticatorChooser);

    private static readonly string                  PROGRAM_VERSION = Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);
    private static readonly CancellationTokenSource EXITING_TRIGGER = new();
    public static readonly  CancellationToken       EXITING         = EXITING_TRIGGER.Token;
    private static readonly WindowsIdentity         CURRENT_USER    = WindowsIdentity.GetCurrent();

    private static Logger? logger;

    // #15
    [Option("--skip-all-non-security-key-options", CommandOptionType.NoValue)]
    public bool skipAllNonSecurityKeyOptions { get; }

    // #30
    [Option("--autosubmit-pin-length", CommandOptionType.SingleValue)]
    public int? autosubmitPinLength { get; }

    [Option("--autostart-on-logon", CommandOptionType.NoValue)]
    public bool autostartOnLogon { get; }

    [Option("-l|--log", CommandOptionType.SingleOrNoValue)]
    public (bool enabled, string? filename) log { get; }

    [Option(DefaultHelpOptionConvention.DefaultHelpTemplate, CommandOptionType.NoValue)]
    public bool help { get; }

    [STAThread]
    public static int Main(string[] args) {
        try {
            using var app = new CommandLineApplication<Startup> {
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw
            };
            app.Conventions.UseDefaultConventions();
            return app.Execute(args);
        } catch (CommandParsingException e) {
            MessageBox.Show(e.Message, $"{PROGRAM_NAME} {PROGRAM_VERSION}", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    // ReSharper disable once UnusedMember.Global - it's actually invoked by McMaster.Extensions.CommandLineUtils
    // ReSharper disable once InconsistentNaming - it must be named this, as dictated by McMaster.Extensions.CommandLineUtils, it's not my choice
    public int OnExecute() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Logging.initialize(log.enabled, log.filename);
        logger = LogManager.GetLogger(typeof(Startup).FullName!);

        try {
            if (help) {
                showUsage();
                return 0;
            }

            if (autostartOnLogon && !registerAsStartupProgram()) {
                return 1;
            }

            using Mutex singleInstanceLock = new(true, $@"Local\{PROGRAM_NAME}_{CURRENT_USER.User?.Value}", out bool isOnlyInstance);
            CURRENT_USER.Dispose();
            if (!isOnlyInstance) {
                logger.Warn("Another instance of {program} is already running for this user, this instance is exiting now.", PROGRAM_NAME);
                return 2;
            }

            try {
                logger.Info("{name} {version} starting", PROGRAM_NAME, PROGRAM_VERSION);
                OsVersion os = OsVersion.getCurrent();
                logger.Info("Operating system is {name} {marketingVersion} {version} {arch}", os.name, os.marketingVersion, os.version, os.architecture);
                logger.Info("{Locales are} {locales}", I18N.LOCALE_NAMES.Count == 1 ? "Locale is" : "Locales are", string.Join(", ", I18N.LOCALE_NAMES));

                using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
                WindowsSecurityKeyChooser   securityKeyChooser    = new(new ChooserOptions(skipAllNonSecurityKeyOptions, autosubmitPinLength));

                windowOpeningListener.windowOpened += (_, window) => securityKeyChooser.chooseUsbSecurityKey(window);

                foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(securityKeyChooser.isFidoPromptWindow)) {
                    securityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
                }

                logger.Info("Waiting for Windows Security FIDO dialog boxes to open");

                _ = I18N.getStrings(I18N.Key.SMARTPHONE); // ensure localization is loaded eagerly

                Console.CancelKeyPress += (_, args) => {
                    args.Cancel = true;
                    EXITING_TRIGGER.Cancel();
                    Application.Exit();
                };

                SystemEvents.SessionEnding += onWindowsLogoff;

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
            CURRENT_USER.Dispose();
        }
    }

    private bool registerAsStartupProgram() {
        try {
            string domainAndUsername = CURRENT_USER.Name;

            TaskDefinition scheduledTask = TaskService.Instance.NewTask();
            scheduledTask.RegistrationInfo.Author = "Ben Hutchison";
            scheduledTask.RegistrationInfo.Date   = DateTime.Now;
            scheduledTask.RegistrationInfo.Description =
                $"{PROGRAM_NAME} is a background program that skips the phone pairing option and chooses the USB security key in Windows FIDO/WebAuthn prompts. \n\nThis scheduled task is necessary to start {PROGRAM_NAME} for you on login with elevated permissions, which are required to interact with the Windows 11 FIDO prompts beginning in January 2026. \n\nhttps://github.com/Aldaviva/{PROGRAM_NAME}";
            scheduledTask.Principal.RunLevel                  = TaskRunLevel.Highest; // #44
            scheduledTask.Settings.Enabled                    = true;
            scheduledTask.Settings.ExecutionTimeLimit         = TimeSpan.Zero;
            scheduledTask.Settings.DisallowStartIfOnBatteries = false;
            scheduledTask.Settings.StopIfGoingOnBatteries     = false;
            scheduledTask.Settings.Compatibility              = TaskCompatibility.V2_3;
            scheduledTask.Actions.Add(Environment.ProcessPath!, skipAllNonSecurityKeyOptions ? "--skip-all-non-security-key-options" : null);
            scheduledTask.Triggers.Add(new LogonTrigger { Enabled = true, UserId = domainAndUsername, Delay = TimeSpan.FromSeconds(15) });
            TaskService.Instance.RootFolder.RegisterTaskDefinition($"{PROGRAM_NAME} \u2013 {Environment.UserName}", scheduledTask, TaskCreation.CreateOrUpdate, domainAndUsername, null,
                TaskLogonType.InteractiveToken);

            MessageBox.Show($"{PROGRAM_NAME} is now running in the background, and will also start automatically each time you log in to Windows.", PROGRAM_NAME, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        } catch (Exception e) when (e is not OutOfMemoryException) {
            MessageBox.Show($"Failed to register {PROGRAM_NAME} to start automatically on Windows logon: {e.GetType().Name} {e.Message}", PROGRAM_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
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
                
            {processFilename} --autosubmit-pin-length=$num
                When Windows prompts you for the FIDO PIN for your USB security key, automatically submit the dialog once you have typed a PIN that is $num characters long (minimum 4), instead of you manually pressing Enter. Remember that enough consecutive incorrect submissions (8 on YubiKeys) will permanently block the security key until you reset it and lose all its FIDO credentials, so type with care. This will neither autosubmit PINs when registering a new FIDO credential, changing your PIN, or entering a Windows Hello PIN (which Windows autosubmits without this program's help).
                
            {processFilename} --log[=$filename]
                Runs this program in the background like the first example, and logs debug messages to a text file. If you don't specify $filename, it goes to {Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "%TEMP%", PROGRAM_NAME + ".log")}.
              
            {processFilename} --help
                Shows this usage.
                
            For more information, see https://github.com/Aldaviva/{PROGRAM_NAME}.
            Press Ctrl+C to copy this message.
            """, $"{PROGRAM_NAME} {PROGRAM_VERSION} usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void onWindowsLogoff(object sender, SessionEndingEventArgs args) {
        logger?.Info("Exiting due to Windows session ending for {0}", args.Reason);
        SystemEvents.SessionEnding -= onWindowsLogoff;
        Application.Exit();
    }

}