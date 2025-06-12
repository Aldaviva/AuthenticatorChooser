using ManagedWinapi.Windows;

namespace AuthenticatorChooser.WindowOpening;

public interface WindowOpeningListener: IDisposable {

    event EventHandler<SystemWindow>? windowOpened;

}

public class WindowOpeningListenerImpl: WindowOpeningListener {

    public event EventHandler<SystemWindow>? windowOpened;

    private readonly ShellHook shellHook = new ShellHookImpl();

    public WindowOpeningListenerImpl() {
        shellHook.shellEvent += onWindowOpened;
    }

    private void onWindowOpened(object? sender, ShellEventArgs args) {
        if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
            windowOpened?.Invoke(this, new SystemWindow(args.windowHandle));
        }
    }

    public void Dispose() {
        shellHook.shellEvent -= onWindowOpened;
        shellHook.Dispose();
        GC.SuppressFinalize(this);
    }

}