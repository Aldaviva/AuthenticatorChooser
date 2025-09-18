using System.Diagnostics;

namespace AuthenticatorChooser;

public readonly record struct ChooserOptions(bool skipAllNonSecurityKeyOptions) {

    public Stopwatch overallStopwatch { get; init; } = null!;

}