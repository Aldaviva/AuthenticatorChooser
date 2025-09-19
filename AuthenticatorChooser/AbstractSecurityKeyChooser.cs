using ManagedWinapi.Windows;

namespace AuthenticatorChooser;

public abstract class AbstractSecurityKeyChooser<T>: SecurityKeyChooser<T> {

    public abstract void chooseUsbSecurityKey(T fidoPrompt);

    public abstract bool isFidoPromptWindow(SystemWindow window);

}