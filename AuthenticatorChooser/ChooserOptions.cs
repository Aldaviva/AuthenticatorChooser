using System.Diagnostics;
using System.Windows.Automation;

namespace AuthenticatorChooser;

public readonly record struct ChooserOptions(bool skipAllNonSecurityKeyOptions) {

    public AutomationElement fidoEl { get; init; } = null!;
    public Stopwatch overallStopwatch { get; init; } = null!;

}