using ManagedWinapi.Windows;
using System.Windows.Automation;

namespace AuthenticatorChooser;

public abstract class AbstractSecurityKeyChooser<T>: SecurityKeyChooser<T> {

    public abstract void chooseUsbSecurityKey(T fidoPrompt);

    public abstract bool isFidoPromptWindow(SystemWindow window);

    protected static bool nameContainsAny(AutomationElement element, IEnumerable<string> possibleSubstrings) {
        string name = element.Current.Name;
        // #2: in addition to a prefix, there is sometimes also a suffix after the substring
        return possibleSubstrings.Any(possibleSubstring => name.Contains(possibleSubstring, StringComparison.CurrentCulture));
    }

    /// <summary>
    /// <para>Create an <see cref="AndCondition"/> or <see cref="OrCondition"/> for a <paramref name="property"/> from a series of <paramref name="values"/>, which have fewer than 2 items in it.</para>
    /// <para>This avoids a crash in the <see cref="AndCondition"/> and <see cref="OrCondition"/> constructors if the array has size 1.</para>
    /// </summary>
    /// <param name="property">The name of the UI property to match against, such as <see cref="AutomationElement.NameProperty"/> or <see cref="AutomationElement.AutomationIdProperty"/>.</param>
    /// <param name="and"><c>true</c> to make a conjunction (AND), <c>false</c> to make a disjunction (OR)</param>
    /// <param name="values">Zero or more property values to match against.</param>
    /// <returns>A <see cref="Condition"/> that matches the values against the property, without throwing an <see cref="ArgumentException"/> if <paramref name="values"/> has length &lt; 2.</returns>
    protected static Condition singletonSafePropertyCondition(AutomationProperty property, bool and, IEnumerable<string> values) {
        Condition[] propertyConditions = values.Select<string, Condition>(allowedValue => new PropertyCondition(property, allowedValue)).ToArray();
        return propertyConditions.Length switch {
            0 when and => Condition.TrueCondition,
            0          => Condition.FalseCondition,
            1          => propertyConditions[0],
            _ when and => new AndCondition(propertyConditions),
            _          => new OrCondition(propertyConditions)
        };
    }

}