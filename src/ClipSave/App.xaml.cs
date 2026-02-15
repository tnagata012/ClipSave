using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Windows;

namespace ClipSave;

public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;
    private AppLifecycleCoordinator? _lifecycleCoordinator;
    private ILogger<App>? _logger;
    private int _terminatingCrashHandled;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupCrashHandlers();

        try
        {
            _serviceProvider = AppServiceProviderFactory.CreateServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Starting ClipSave");

            _lifecycleCoordinator = new AppLifecycleCoordinator(_serviceProvider, Dispatcher, _logger, Shutdown);
            if (!_lifecycleCoordinator.TryStartPrimaryInstance())
            {
                Shutdown();
                return;
            }

            _logger.LogInformation("ClipSave startup completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred during startup");
            var startupErrorMessage = _lifecycleCoordinator?.LocalizationService?.Format("App_StartupErrorMessage", ex.Message) ??
                                      $"An error occurred during startup:\n{ex.Message}";
            var startupErrorTitle = _lifecycleCoordinator?.LocalizationService?.GetString("App_StartupErrorTitle") ?? "Error";
            System.Windows.MessageBox.Show(
                startupErrorMessage,
                startupErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Shutting down ClipSave");

        _lifecycleCoordinator?.Dispose();
        _lifecycleCoordinator = null;

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serviceProvider = null;

        base.OnExit(e);
    }

    private void SetupCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception("An unknown exception occurred.");
        HandleCrash(exception, "AppDomain.UnhandledException", isTerminating: e.IsTerminating);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        HandleCrash(e.Exception, "DispatcherUnhandledException", isTerminating: true);
        e.Handled = true;
        Shutdown();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleCrash(e.Exception, "TaskScheduler.UnobservedTaskException", isTerminating: false);
        e.SetObserved();
    }

    private void HandleCrash(Exception exception, string source, bool isTerminating)
    {
        try
        {
            if (isTerminating && Interlocked.Exchange(ref _terminatingCrashHandled, 1) == 1)
            {
                _logger?.LogWarning("Skipped duplicate terminating crash handling (Source: {Source})", source);
                return;
            }

            _logger?.LogCritical(exception, "Unhandled exception occurred (Source: {Source}, IsTerminating: {IsTerminating})",
                source, isTerminating);

            if (!isTerminating)
            {
                return;
            }

            var settings = _lifecycleCoordinator?.SettingsService?.Current;
            var dumpPath = CrashDumpService.WriteDump(exception, source, settings);

            if (dumpPath != null)
            {
                _logger?.LogInformation("Wrote crash dump: {DumpPath}", dumpPath);

                System.Windows.MessageBox.Show(
                    FormatLocalizedString("App_CrashDialogMessage", dumpPath),
                    GetLocalizedString("App_CrashDialogTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch
        {
        }
    }

    private string GetLocalizedString(string key)
    {
        return _lifecycleCoordinator?.LocalizationService?.GetString(key) ?? key;
    }

    private string FormatLocalizedString(string key, params object[] args)
    {
        if (_lifecycleCoordinator?.LocalizationService != null)
        {
            return _lifecycleCoordinator.LocalizationService.Format(key, args);
        }

        return args.Length == 0
            ? key
            : string.Format(CultureInfo.InvariantCulture, key, args);
    }
}
