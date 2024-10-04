using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.MessageTemplates;
using NLog.Targets;
using System.Text;

namespace AuthenticatorChooser;

internal static class Logging {

    private static readonly SimpleLayout MESSAGE_FORMAT = new(
        " ${level:format=FirstCharacter:lowercase=true} | ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} | ${logger:shortName=true:padding=-18} | ${message:withException=true:exceptionSeparator=\n}");

    public static void initialize(bool enableFileAppender, string? logFilename) {
        logFilename ??= Path.Combine(Path.GetTempPath(), Path.ChangeExtension(nameof(AuthenticatorChooser), ".log"));

        LoggingConfiguration logConfig = new();
        ServiceRepository    services  = logConfig.LogFactory.ServiceRepository;
        services.RegisterService(typeof(IValueFormatter), new UnfuckedValueFormatter((IValueFormatter) services.GetService(typeof(IValueFormatter))!));

        if (enableFileAppender) {
            logConfig.AddRule(LogLevel.Trace, LogLevel.Fatal, new FileTarget("fileAppender") {
                Layout          = MESSAGE_FORMAT,
                FileName        = logFilename,
                CleanupFileName = true
            });
        }

        logConfig.AddRule(LogLevel.Trace, LogLevel.Fatal, new ConsoleTarget("consoleAppender") {
            Layout                 = MESSAGE_FORMAT,
            DetectConsoleAvailable = true
        });

        LogManager.Configuration = logConfig;
    }

    /// <summary>
    /// When logging strings to NLog using structured logging, don't surround them with quotation marks, because it looks stupid
    /// </summary>
    /// <param name="parent">Built-in <see cref="ValueFormatter"/></param>
    private class UnfuckedValueFormatter(IValueFormatter parent): IValueFormatter {

        public bool FormatValue(object value, string format, CaptureType captureType, IFormatProvider formatProvider, StringBuilder builder) {
            switch (value) {
                case string s:
                    builder.Append(s);
                    return true;
                case StringBuilder s:
                    builder.Append(s);
                    return true;
                case ReadOnlyMemory<char> s:
                    builder.Append(s);
                    return true;
                default:
                    return parent.FormatValue(value, format, captureType, formatProvider, builder);
            }
        }

    }

}