using Dark.Net;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AuthenticatorChooser;

public partial class MainWindow {

    public MainWindow() {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);
        DarkNet.Instance.SetWindowThemeWpf(this, Theme.Auto);
        hideMinimizeAndMaximizeButtons();
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongA")]
    private static partial int GetWindowLong(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongA")]
    private static partial int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    private const int GWL_STYLE      = -16;
    private const int WS_MAXIMIZEBOX = 0x10000;
    private const int WS_MINIMIZEBOX = 0x20000;

    private void hideMinimizeAndMaximizeButtons() {
        nint windowHandle        = new WindowInteropHelper(this).Handle;
        int  existingWindowStyle = GetWindowLong(windowHandle, GWL_STYLE);
        _ = SetWindowLong(windowHandle, GWL_STYLE, existingWindowStyle & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);
    }

}