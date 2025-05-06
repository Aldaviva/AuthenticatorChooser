using Microsoft.Win32;
using System.Management;

namespace AuthenticatorChooser;

/// <param name="name">Microsoft Windows 11 Pro</param>
/// <param name="marketingVersion">24H2</param>
/// <param name="version">10.0.26100.3775 (major version is 10 on Windows 11)</param>
/// <param name="architecture">AMD64</param>
internal readonly record struct OsVersion(string name, string marketingVersion, Version version, string architecture) {

    private const string NT_CURRENTVERSION_KEY = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static OsVersion getCurrent() {
        using ManagementObjectSearcher   wmiSearch  = new(new SelectQuery("Win32_OperatingSystem", null, ["Caption", "Version"]));
        using ManagementObjectCollection wmiResults = wmiSearch.Get();
        using ManagementObject           wmiResult  = wmiResults.Cast<ManagementObject>().First();

        return new OsVersion(
            name: (string) wmiResult["Caption"],
            marketingVersion: Registry.GetValue(NT_CURRENTVERSION_KEY, "DisplayVersion", null) as string ?? string.Empty,
            version: Version.Parse($"{wmiResult["Version"]}.{Registry.GetValue(NT_CURRENTVERSION_KEY, "UBR", 0) as int? ?? 0}"),
            architecture: Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? string.Empty);
    }

}