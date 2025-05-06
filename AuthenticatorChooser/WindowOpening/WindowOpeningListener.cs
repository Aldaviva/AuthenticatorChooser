using ManagedWinapi.Windows;
using NLog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Automation;
using ThrottleDebounce;
using Unfucked;

namespace AuthenticatorChooser.WindowOpening;

public interface WindowOpeningListener: IDisposable {

    event EventHandler<SystemWindow>? windowOpened;
    event EventHandler<AutomationElement>? automationElementMaybeOpened;

    Stopwatch mostRecentAutomationEventReceived { get; }

    void listenForOpenedChildAutomationElements(string parentClass);

}

public class WindowOpeningListenerImpl: WindowOpeningListener {

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

    private static readonly TimeSpan AUTOMATION_ELEMENT_THROTTLE = TimeSpan.FromMilliseconds(300);

    public event EventHandler<SystemWindow>? windowOpened;
    public event EventHandler<AutomationElement>? automationElementMaybeOpened;

    public Stopwatch mostRecentAutomationEventReceived { get; } = new();

    private readonly ShellHook                                                shellHook             = new ShellHookImpl();
    private readonly ConcurrentDictionary<string, HashSet<AutomationElement>> watchedParentsByClass = new();
    private readonly RateLimitedAction<StructureChangedEventArgs>             throttledChildStructureChanged;

    public WindowOpeningListenerImpl() {
        shellHook.shellEvent           += onWindowOpened;
        throttledChildStructureChanged =  Throttler.Throttle<StructureChangedEventArgs>(onChildStructureChanged, AUTOMATION_ELEMENT_THROTTLE);
    }

    private void onWindowOpened(object? sender, ShellEventArgs args) {
        if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
            SystemWindow window = new(args.windowHandle);
            windowOpened?.Invoke(this, window);

            if (window.ClassName is { } className && watchedParentsByClass.TryGetValue(className, out HashSet<AutomationElement>? parents) && window.ToAutomationElement() is { } windowEl) {
                lock (parents) {
                    parents.Add(windowEl);
                }
                listenForOpenedChildAutomationElements(windowEl);
            }
        }
    }

    public void listenForOpenedChildAutomationElements(string parentClass) {
        mostRecentAutomationEventReceived.Restart();
        foreach (SystemWindow parent in SystemWindow.FilterToplevelWindows(window => window.ClassName == parentClass)) {
            if (parent.ToAutomationElement() is not { } parentEl) continue;

            listenForOpenedChildAutomationElements(parentEl);
            watchedParentsByClass.GetOrAdd(parentClass, []).Add(parentEl);

            foreach (AutomationElement child in parentEl.Children()) {
                automationElementMaybeOpened?.Invoke(this, child);
            }
        }
    }

    private void listenForOpenedChildAutomationElements(AutomationElement parent) {
        Automation.AddStructureChangedEventHandler(parent, TreeScope.Descendants, onChildStructureChanged);
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, parent, TreeScope.Element, onWindowClosed);
    }

    private void unlistenForOpenedChildAutomationEvents(AutomationElement parent) {
        Automation.RemoveStructureChangedEventHandler(parent, onChildStructureChanged);
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, parent, onWindowClosed);
    }

    private void onChildStructureChanged(object sender, AutomationEventArgs e) {
        mostRecentAutomationEventReceived.Restart();
        LOGGER.Trace("Received {change} event", (e as StructureChangedEventArgs)?.StructureChangeType);
        // For some reason Chromium 134 stopped emitting ChildAdded events and only fires a ChildrenReordered event when the FIDO dialog appears, not sure why
        if (e is StructureChangedEventArgs { StructureChangeType: StructureChangeType.ChildAdded or StructureChangeType.ChildrenBulkAdded or StructureChangeType.ChildrenReordered } args) {
            throttledChildStructureChanged.Invoke(args);
        }
    }

    private void onChildStructureChanged(StructureChangedEventArgs e) {
        // This is stupid. UIA doesn't tell us what element was actually added when the structure changed, so we must rescan all the parents.
        foreach (ISet<AutomationElement> parents in watchedParentsByClass.Values) {
            lock (parents) {
                foreach (AutomationElement parent in parents) {
                    foreach (AutomationElement child in parent.Children()) {
                        automationElementMaybeOpened?.Invoke(this, child);
                    }
                }
            }
        }
    }

    private void onWindowClosed(object sender, AutomationEventArgs e) {
        if (e is WindowClosedEventArgs args) {
            foreach (HashSet<AutomationElement> parents in watchedParentsByClass.Values) {
                lock (parents) {
                    foreach (AutomationElement closedParent in parents.Where(parent => parent.GetRuntimeId().SequenceEqual(args.GetRuntimeId())).ToList()) {
                        parents.Remove(closedParent);
                        unlistenForOpenedChildAutomationEvents(closedParent);
                        LOGGER.Trace("Window closed, stopping listening for it opening child windows");
                    }
                }
            }
        }
    }

    public void Dispose() {
        shellHook.shellEvent -= onWindowOpened;
        shellHook.Dispose();
        throttledChildStructureChanged.Dispose();

        foreach (ISet<AutomationElement> parents in watchedParentsByClass.Values) {
            lock (parents) {
                foreach (AutomationElement parent in parents) {
                    unlistenForOpenedChildAutomationEvents(parent);
                }
                parents.Clear();
            }
        }
        watchedParentsByClass.Clear();

        GC.SuppressFinalize(this);
    }

}