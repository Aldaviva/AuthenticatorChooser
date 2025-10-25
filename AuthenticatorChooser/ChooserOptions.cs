using System.Diagnostics;

namespace AuthenticatorChooser;

public readonly record struct ChooserOptions(bool skipAllNonSecurityKeyOptions, int? autoSubmitPinLength) {

    public Stopwatch overallStopwatch { get; } = new();

}