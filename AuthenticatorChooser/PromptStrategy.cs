using System.Windows.Automation;

namespace AuthenticatorChooser;

public interface PromptStrategy {

    void submitChoice(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

}