using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace AuthenticatorChooser;

internal static class Logging {

    private static readonly SimpleLayout MESSAGE_FORMAT = new(
        " ${level:format=FirstCharacter:lowercase=true} | ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} | ${logger:shortName=true:padding=-25} | ${message:withException=true:exceptionSeparator=\n}");

    private static readonly LogLevel LOG_LEVEL = LogLevel.Debug;

    public static void initialize(bool enableFileAppender, string? logFilename) {
        logFilename = logFilename != null ? Environment.ExpandEnvironmentVariables(logFilename) : Path.Combine(Path.GetTempPath(), Path.ChangeExtension(nameof(AuthenticatorChooser), ".log"));

        LoggingConfiguration logConfig = new();

        if (enableFileAppender) {
            logConfig.AddRule(LOG_LEVEL, LogLevel.Fatal, new FileTarget("fileAppender") {
                Layout   = MESSAGE_FORMAT,
                FileName = logFilename
            });
        }

        logConfig.AddRule(LOG_LEVEL, LogLevel.Fatal, new ConsoleTarget("consoleAppender") {
            Layout                 = MESSAGE_FORMAT,
            DetectConsoleAvailable = true
        });

        LogManager.Configuration = logConfig;
    }

}