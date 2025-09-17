using System.Windows.Automation;

namespace AuthenticatorChooser;

public interface PromptStrategy {

    AutomationElement? findDesiredChoice(AutomationElement outerScrollViewer, IReadOnlyCollection<AutomationElement> authenticatorChoices);

    void preselectChoice(AutomationElement desiredChoice, bool isShiftDown);

    /// <summary>
    /// Reply to the prompt
    /// </summary>
    /// <param name="desiredChoice">Which element to choose</param>
    /// <param name="authenticatorChoices">All possible choices</param>
    /// <param name="isShiftDown">If the user held Shift to not submit the dialog</param>
    /// <returns><c>true</c> if the dialog is being submitted, or <c>false</c> if the dialog is being left open</returns>
    bool submitChoice(AutomationElement desiredChoice, IReadOnlyList<AutomationElement> authenticatorChoices, bool isShiftDown);

}