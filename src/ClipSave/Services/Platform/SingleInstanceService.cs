using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Pipes;

namespace ClipSave.Services;

public class SingleInstanceService : IDisposable
{
    private const string DefaultMutexName = "Global\\ClipSave_SingleInstance";
    private const string DefaultPipeName = "ClipSave_SingleInstancePipe";
    private const string OpenSettingsCommand = "OPEN_SETTINGS";

    private readonly ILogger<SingleInstanceService> _logger;
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _pipeCts;
    private bool _disposed;

    public event EventHandler? SecondInstanceLaunched;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
        : this(logger, DefaultMutexName, DefaultPipeName)
    {
    }

    internal SingleInstanceService(ILogger<SingleInstanceService> logger, string mutexName, string pipeName)
    {
        _logger = logger;
        _mutexName = mutexName;
        _pipeName = pipeName;
    }

    public bool TryAcquireOrNotify()
    {
        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            _ownsMutex = createdNew;

            if (createdNew)
            {
                _logger.LogInformation("Started as primary instance");
                StartPipeServer();
                return true;
            }

            _logger.LogInformation("Detected existing instance; notifying it to open settings window");
            NotifyExistingInstance();

            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create mutex; aborting startup");
            return false;
        }
    }

    private void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        _ = ListenForClientsAsync(_pipeCts.Token);
    }

    private async Task ListenForClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(_pipeServer);
                var command = await reader.ReadLineAsync(cancellationToken);

                if (command == OpenSettingsCommand)
                {
                    _logger.LogDebug("Received settings-open request from secondary instance");
                    SecondInstanceLaunched?.Invoke(this, EventArgs.Empty);
                }

                _pipeServer.Disconnect();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pipe server error occurred");
            }
            finally
            {
                _pipeServer?.Dispose();
                _pipeServer = null;
            }
        }
    }

    private void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 3000);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(OpenSettingsCommand);

            _logger.LogDebug("Sent open-settings command to existing instance");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out while connecting to existing instance");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify existing instance");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _pipeCts?.Cancel();
        _pipeCts?.Dispose();
        _pipeCts = null;

        _pipeServer?.Dispose();
        _pipeServer = null;

        if (_ownsMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Release can throw when the current thread does not own the mutex.
            }
        }

        _mutex?.Dispose();
        _mutex = null;

        _logger.LogInformation("Disposed SingleInstanceService");
        GC.SuppressFinalize(this);
    }

}
