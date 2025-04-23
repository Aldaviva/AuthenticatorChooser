using ManagedWinapi.Windows;
using System.Collections.Concurrent;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.WindowOpening;

public interface WindowOpeningListener: IDisposable {

    event EventHandler<SystemWindow>? windowOpened;
    event EventHandler<AutomationElement>? automationElementMaybeOpened;

    void listenForOpenedChildAutomationElements(string parentClass);

}

public class WindowOpeningListenerImpl: WindowOpeningListener {

    public event EventHandler<SystemWindow>? windowOpened;
    public event EventHandler<AutomationElement>? automationElementMaybeOpened;

    private readonly ConcurrentDictionary<string, ConcurrentBag<AutomationElement>> watchedParentsByClassName = new();
    private readonly ShellHook                                                      shellHook                 = new ShellHookImpl();

    public WindowOpeningListenerImpl() {
        shellHook.shellEvent += onWindowOpened;
    }

    private void onWindowOpened(object? sender, ShellEventArgs args) {
        if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
            SystemWindow window = new(args.windowHandle);
            windowOpened?.Invoke(this, window);

            if (window.ClassName is { } className && watchedParentsByClassName.TryGetValue(className, out ConcurrentBag<AutomationElement>? parents) && window.ToAutomationElement() is { } windowEl) {
                parents.Add(windowEl);
                listenForOpenedChildAutomationElements(windowEl);
            }
        }
    }

    public void listenForOpenedChildAutomationElements(string parentClass) {
        foreach (SystemWindow parent in SystemWindow.FilterToplevelWindows(window => window.ClassName == parentClass)) {
            if (parent.ToAutomationElement() is not { } parentEl) continue;

            listenForOpenedChildAutomationElements(parentEl);
            watchedParentsByClassName.GetOrAdd(parentClass, []).Add(parentEl);

            foreach (AutomationElement child in parentEl.Children()) {
                automationElementMaybeOpened?.Invoke(this, child);
            }
        }
    }

    private void listenForOpenedChildAutomationElements(AutomationElement parent) {
        Automation.AddStructureChangedEventHandler(parent, TreeScope.Descendants, onChildStructureChanged);
    }

    private void onChildStructureChanged(object sender, AutomationEventArgs e) {
        if (e is StructureChangedEventArgs { StructureChangeType: StructureChangeType.ChildAdded or StructureChangeType.ChildrenBulkAdded }) {
            // This is stupid. UIA doesn't tell us what element was actually added when the structure changed, so we must rescan all the parents.
            foreach (ConcurrentBag<AutomationElement> parents in watchedParentsByClassName.Values) {
                foreach (AutomationElement parent in parents) {
                    foreach (AutomationElement child in parent.Children()) {
                        automationElementMaybeOpened?.Invoke(this, child);
                    }
                }
            }
        }
    }

    public void Dispose() {
        shellHook.shellEvent -= onWindowOpened;
        shellHook.Dispose();

        foreach (ConcurrentBag<AutomationElement> parents in watchedParentsByClassName.Values) {
            while (parents.TryTake(out AutomationElement? parent)) {
                Automation.RemoveStructureChangedEventHandler(parent, onChildStructureChanged);
            }
        }

        GC.SuppressFinalize(this);
    }

}