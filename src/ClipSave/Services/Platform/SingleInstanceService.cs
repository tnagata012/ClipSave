using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace ClipSave.Services;

public class SingleInstanceService : IDisposable
{
    private const string DefaultMutexNamePrefix = "Local\\ClipSave_SingleInstance";
    private const string DefaultPipeNamePrefix = "ClipSave_SingleInstancePipe";
    private const string OpenSettingsCommand = "OPEN_SETTINGS";
    private const string UnknownScopeToken = "unknown";

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
        : this(logger, BuildScopedMutexName(), BuildScopedPipeName())
    {
    }

    internal SingleInstanceService(ILogger<SingleInstanceService> logger, string mutexName, string pipeName)
    {
        _logger = logger;
        _mutexName = mutexName;
        _pipeName = pipeName;
    }

    internal static string BuildScopedMutexName(string? userSid = null, int? sessionId = null)
    {
        return $"{DefaultMutexNamePrefix}_{BuildScopeToken(userSid, sessionId)}";
    }

    internal static string BuildScopedPipeName(string? userSid = null, int? sessionId = null)
    {
        return $"{DefaultPipeNamePrefix}_{BuildScopeToken(userSid, sessionId)}";
    }

    private static string BuildScopeToken(string? userSid, int? sessionId)
    {
        var resolvedSid = SanitizeScopeSegment(userSid ?? TryGetCurrentUserSid());
        var resolvedSession = (sessionId ?? TryGetCurrentSessionId())?.ToString() ?? UnknownScopeToken;
        return $"{resolvedSid}_s{resolvedSession}";
    }

    private static string? TryGetCurrentUserSid()
    {
        try
        {
            return WindowsIdentity.GetCurrent().User?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetCurrentSessionId()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.SessionId;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeScopeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UnknownScopeToken;
        }

        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var normalized = new string(chars);

        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? UnknownScopeToken : normalized;
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
