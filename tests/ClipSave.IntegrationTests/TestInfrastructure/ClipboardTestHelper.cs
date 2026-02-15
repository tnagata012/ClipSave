using ClipSave.Models;
using ClipSave.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipSave.IntegrationTests;

internal static class ClipboardTestHelper
{
    public static Task<ClipboardContent?> GetContentAsync(ILogger<ClipboardService> logger, Action setClipboardAction)
    {
        return RunStaAsync(async () =>
        {
            try
            {
                setClipboardAction();
                Clipboard.Flush();
                var clipboardService = new ClipboardService(logger);
                return await clipboardService.GetContentAsync().ConfigureAwait(true);
            }
            finally
            {
                Clipboard.Clear();
            }
        });
    }

    public static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Task<T> RunStaAsync<T>(Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        var result = await action().ConfigureAwait(true);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                });

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
