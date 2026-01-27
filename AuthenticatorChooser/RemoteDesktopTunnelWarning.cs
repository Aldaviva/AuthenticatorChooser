using Microsoft.Win32;
using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Security.Principal;

namespace AuthenticatorChooser;

public interface RemoteDesktopTunnelWarning: IDisposable {

    bool isMonitoring { get; set; }
    bool isDialogAllowed { get; set; }

}

public class RemoteDesktopTunnelWarningImpl: RemoteDesktopTunnelWarning {

    private readonly RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Ben Hutchison\\AuthenticatorChooser", RegistryKeyPermissionCheck.ReadWriteSubTree);

    private EventLogWatcher? logWatcher;

    private const string SHOW_TUNNEL_WARNING = "Show RDP Server FIDO Tunnel Warning";

    public bool isMonitoring {
        get;
        set {
            if (field != value) {
                if (value) {
                    if (isDialogAllowed) {
                        string currentUserSid = WindowsIdentity.GetCurrent().User!.Value;
                        string query          = $"*[System[Provider/@Name=\"Microsoft-Windows-WebAuthN\" and EventID=1050 and Security/@UserID=\"{SecurityElement.Escape(currentUserSid)}\"]]";
                        logWatcher                    =  new EventLogWatcher(new EventLogQuery("Microsoft-Windows-WebAuthN/Operational", PathType.LogName, query));
                        logWatcher.EventRecordWritten += onWebAuthnRpcRequest;
                        logWatcher.Enabled            =  true;
                    }
                } else {
                    logWatcher?.Enabled = false;
                    logWatcher          = null;
                }
                field = value;
            }
        }
    }

    private void onWebAuthnRpcRequest(object? sender, EventRecordWrittenEventArgs e) {
        throw new NotImplementedException();
    }

    public bool isDialogAllowed {
        get => Convert.ToBoolean(registryKey.GetValue(SHOW_TUNNEL_WARNING) as int? ?? 1);
        set => registryKey.SetValue(SHOW_TUNNEL_WARNING, Convert.ToInt32(value));
    }

    public void Dispose() {
        logWatcher?.EventRecordWritten -= onWebAuthnRpcRequest;
        logWatcher?.Dispose();
        logWatcher = null;
        registryKey.Dispose();
        GC.SuppressFinalize(this);
    }

}