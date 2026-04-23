using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser;

public class TrayIcon: IDisposable {

    private readonly NotifyIcon       notifyIcon;
    private readonly ToolStripMenuItem enabledMenuItem;

    public bool isEnabled => enabledMenuItem.Checked;

    public TrayIcon(Action onExit) {
        enabledMenuItem = new ToolStripMenuItem("Enabled") {
            Checked     = true,
            CheckOnClick = true
        };

        ToolStripMenuItem exitMenuItem = new("Exit");
        exitMenuItem.Click += (_, _) => onExit();

        ContextMenuStrip contextMenu = new();
        contextMenu.Items.Add(enabledMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        notifyIcon = new NotifyIcon {
            Text             = nameof(AuthenticatorChooser),
            Icon             = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!,
            ContextMenuStrip = contextMenu,
            Visible          = true
        };
    }

    public void Dispose() {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }

}
