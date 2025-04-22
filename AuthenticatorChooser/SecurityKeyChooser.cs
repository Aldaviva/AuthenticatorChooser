namespace AuthenticatorChooser;

public interface SecurityKeyChooser<in WINDOW> {

    void chooseUsbSecurityKey(WINDOW fidoPrompt);

}