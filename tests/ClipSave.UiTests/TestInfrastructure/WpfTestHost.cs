using System.Windows;
using System.Windows.Threading;

namespace ClipSave.UiTests;

internal static class WpfTestHost
{
    private static readonly object ApplicationLock = new();

    public static void EnsureApplication()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("WPF tests must run on an STA thread.");
        }

        if (Application.Current == null)
        {
            lock (ApplicationLock)
            {
                if (Application.Current == null)
                {
                    _ = new Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }
            }
        }

        EnsureDispatcherSynchronizationContext();
    }

    public static void FlushEvents()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(ExitFrame),
            frame);
        Dispatcher.PushFrame(frame);
    }

    private static object? ExitFrame(object? parameter)
    {
        if (parameter is DispatcherFrame frame)
        {
            frame.Continue = false;
        }

        return null;
    }

    private static void EnsureDispatcherSynchronizationContext()
    {
        if (SynchronizationContext.Current is DispatcherSynchronizationContext)
        {
            return;
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
    }
}
