using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AuthenticatorChooser.WindowOpening;

public interface ShellHook: IDisposable {

    event EventHandler<ShellEventArgs>? shellEvent;

}

public partial class ShellHookImpl: Form, ShellHook {

    private const string USER32 = "user32.dll";

    public event EventHandler<ShellEventArgs>? shellEvent;

    private readonly uint subscriptionId;

    public ShellHookImpl() {
        subscriptionId = registerWindowMessage("SHELLHOOK");
        registerShellHookWindow(Handle);
    }

    protected override void WndProc(ref Message message) {
        if (message.Msg == subscriptionId) {
            shellEvent?.Invoke(this, new ShellEventArgs(shellEvent: (ShellEventArgs.ShellEvent) message.WParam.ToInt32(), windowHandle: message.LParam));
        }

        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing) {
        deregisterShellHookWindow(Handle);
        base.Dispose(disposing);
    }

    [LibraryImport(USER32, EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint registerWindowMessage(string lpString);

    [LibraryImport(USER32, EntryPoint = "DeregisterShellHookWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool deregisterShellHookWindow(IntPtr hWnd);

    [LibraryImport(USER32, EntryPoint = "RegisterShellHookWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool registerShellHookWindow(IntPtr hWnd);

}

public class ShellEventArgs(ShellEventArgs.ShellEvent shellEvent, IntPtr windowHandle): EventArgs {

    public readonly ShellEvent shellEvent   = shellEvent;
    public readonly IntPtr     windowHandle = windowHandle;

    public enum ShellEvent {

        HSHELL_WINDOWCREATED       = 1,
        HSHELL_WINDOWDESTROYED     = 2,
        HSHELL_ACTIVATESHELLWINDOW = 3,
        HSHELL_WINDOWACTIVATED     = 4,
        HSHELL_GETMINRECT          = 5,
        HSHELL_REDRAW              = 6,
        HSHELL_TASKMAN             = 7,
        HSHELL_LANGUAGE            = 8,
        HSHELL_ACCESSIBILITYSTATE  = 11,
        HSHELL_APPCOMMAND          = 12

    }

}