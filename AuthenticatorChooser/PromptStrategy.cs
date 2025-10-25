using System.Windows.Automation;

namespace AuthenticatorChooser;

public interface PromptStrategy {

    bool canHandleTitle(string? actualTitle);
    void handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

}