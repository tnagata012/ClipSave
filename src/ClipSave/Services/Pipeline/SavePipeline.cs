using ClipSave.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipSave.Services;

internal interface ISaveExecutionGate
{
    bool TryEnter();
    void Exit();
    bool TryDisposeIfIdle();
}

internal sealed class SemaphoreSaveExecutionGate : ISaveExecutionGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool TryEnter()
    {
        return _semaphore.Wait(0);
    }

    public void Exit()
    {
        _semaphore.Release();
    }

    public bool TryDisposeIfIdle()
    {
        if (!_semaphore.Wait(0))
        {
            return false;
        }

        _semaphore.Dispose();
        return true;
    }
}

public class SavePipeline : IDisposable
{
    private readonly ILogger<SavePipeline> _logger;
    private readonly ClipboardService _clipboardService;
    private readonly ContentEncodingService _contentEncodingService;
    private readonly FileStorageService _fileStorageService;
    private readonly NotificationService _notificationService;
    private readonly SettingsService _settingsService;
    private readonly ActiveWindowService _activeWindowService;
    private readonly LocalizationService _localizationService;
    private readonly ISaveExecutionGate _saveExecutionGate;
    private int _disposeRequested;
    private int _activeExecutions;

    public SavePipeline(
        ILogger<SavePipeline> logger,
        ClipboardService clipboardService,
        ContentEncodingService contentEncodingService,
        FileStorageService fileStorageService,
        NotificationService notificationService,
        SettingsService settingsService,
        ActiveWindowService activeWindowService,
        LocalizationService? localizationService = null)
        : this(
            logger,
            clipboardService,
            contentEncodingService,
            fileStorageService,
            notificationService,
            settingsService,
            activeWindowService,
            localizationService,
            new SemaphoreSaveExecutionGate())
    {
    }

    internal SavePipeline(
        ILogger<SavePipeline> logger,
        ClipboardService clipboardService,
        ContentEncodingService contentEncodingService,
        FileStorageService fileStorageService,
        NotificationService notificationService,
        SettingsService settingsService,
        ActiveWindowService activeWindowService,
        LocalizationService? localizationService,
        ISaveExecutionGate saveExecutionGate)
    {
        _logger = logger;
        _clipboardService = clipboardService;
        _contentEncodingService = contentEncodingService;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _activeWindowService = activeWindowService;
        _localizationService = localizationService ?? new LocalizationService(NullLogger<LocalizationService>.Instance);
        ArgumentNullException.ThrowIfNull(saveExecutionGate);
        _saveExecutionGate = saveExecutionGate;
    }

    public async Task<SaveResult> ExecuteAsync()
    {
        if (IsDisposeRequested())
        {
            _logger.LogDebug("Save pipeline execution was ignored because disposal has been requested");
            return SaveResult.CreateBusy();
        }

        try
        {
            if (!_saveExecutionGate.TryEnter())
            {
                _logger.LogDebug("Save pipeline is already running; ignored an additional trigger");
                return SaveResult.CreateBusy();
            }
        }
        catch (ObjectDisposedException) when (IsDisposeRequested())
        {
            _logger.LogDebug("Save pipeline execution was ignored because the execution gate has already been disposed");
            return SaveResult.CreateBusy();
        }

        if (IsDisposeRequested())
        {
            TryReleaseExecutionGateAfterDisposeRequest();
            TryDisposeExecutionGateIfIdle();
            return SaveResult.CreateBusy();
        }

        Interlocked.Increment(ref _activeExecutions);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Starting save pipeline");

            var activeWindow = _activeWindowService.GetActiveWindowInfo();
            if (!TryGetSaveDirectory(activeWindow, out var savePath))
            {
                _logger.LogDebug("Active window is out of save scope");
                return SaveResult.CreateUnsupportedWindow();
            }

            _logger.LogDebug("Resolved save path: {SavePath} ({Kind})", savePath, activeWindow.Kind);

            var content = await _clipboardService.GetContentAsync();
            if (content == null)
            {
                _logger.LogDebug("No saveable clipboard content was found");
                var noContentResult = SaveResult.CreateNoContent();
                NotifyResultSafely(noContentResult);
                return noContentResult;
            }

            _logger.LogDebug("Acquired clipboard content: {Description}", content.GetDescription());

            var settings = CreateSaveSettingsSnapshot(_settingsService.Current.Save);
            if (!settings.IsContentTypeEnabled(content.Type))
            {
                _logger.LogDebug("Saving {ContentType} is disabled", content.Type);
                var disabledResult = SaveResult.CreateContentTypeDisabled(content.Type);
                return disabledResult;
            }

            var result = await ExecuteEncodeAndSaveWithFallbackAsync(content, settings, savePath);

            if (result.Kind == SaveResultKind.Success && result.FilePath != null)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogInformation("Save pipeline completed: {ContentType} -> {FilePath} (Elapsed: {Elapsed}ms)",
                    result.ContentType ?? content.Type, result.FilePath, elapsed);
            }

            NotifyResultSafely(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during save pipeline execution");
            var errorResult = SaveResult.CreateFailure(_localizationService.GetString("SavePipeline_GenericFailure"));
            NotifyResultSafely(errorResult);
            return errorResult;
        }
        finally
        {
            TryReleaseExecutionGateAfterExecution();

            if (Interlocked.Decrement(ref _activeExecutions) == 0 && IsDisposeRequested())
            {
                TryDisposeExecutionGateIfIdle();
            }
        }
    }

    private async Task<SaveResult> EncodeAndSaveAsync(ClipboardContent content, SaveSettings settings, string savePath)
    {
        var (encodedData, extension) = _contentEncodingService.Encode(content, settings);

        _logger.LogDebug("Encoded content ({Extension}, {Size} bytes)",
            extension.ToUpperInvariant(), encodedData.Length);

        if (!_fileStorageService.HasEnoughSpace(savePath, encodedData.Length))
        {
            var errorMessage = _localizationService.GetString("SavePipeline_DiskSpaceInsufficient");
            _logger.LogError(errorMessage);
            return SaveResult.CreateFailure(errorMessage);
        }

        var filePath = await _fileStorageService.SaveFileAsync(
            encodedData,
            savePath,
            extension,
            FileNamingPolicy.CreateOptions(settings));

        return SaveResult.CreateSuccess(filePath, content.Type);
    }

    private async Task<SaveResult> ExecuteEncodeAndSaveWithFallbackAsync(
        ClipboardContent content,
        SaveSettings settings,
        string savePath)
    {
        if (content is not ImageContent)
        {
            return await EncodeAndSaveAsync(content, settings, savePath);
        }

        var (canRunOnBackground, contentForProcessing) = PrepareForBackgroundProcessing(content);
        if (!canRunOnBackground)
        {
            return await EncodeAndSaveAsync(contentForProcessing, settings, savePath);
        }

        try
        {
            return await Task.Run(() => EncodeAndSaveAsync(contentForProcessing, settings, savePath));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Background image processing failed; retrying on the UI thread");
            return await EncodeAndSaveAsync(contentForProcessing, settings, savePath);
        }
    }

    private (bool CanRunOnBackground, ClipboardContent Content) PrepareForBackgroundProcessing(ClipboardContent content)
    {
        if (content is ImageContent imageContent)
        {
            try
            {
                var detachedImage = CreateThreadSafeImageSnapshot(imageContent.Image);
                _logger.LogDebug("Detached image for background processing ({Width}x{Height}, {Format})",
                    detachedImage.PixelWidth, detachedImage.PixelHeight, detachedImage.Format);
                return (true, new ImageContent(detachedImage));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to detach image; processing on the UI thread");
                return (false, content);
            }
        }

        return (true, content);
    }

    private bool TryGetSaveDirectory(ActiveWindowResult activeWindow, out string savePath)
    {
        savePath = string.Empty;

        if (activeWindow.Kind == ActiveWindowKind.Other)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeWindow.FolderPath))
        {
            _logger.LogWarning("Failed to resolve save path: Kind={Kind}", activeWindow.Kind);
            return false;
        }

        savePath = activeWindow.FolderPath;
        return true;
    }

    internal bool TryResolveSaveDirectoryForTest(ActiveWindowResult activeWindow, out string savePath)
    {
        return TryGetSaveDirectory(activeWindow, out savePath);
    }

    private static SaveSettings CreateSaveSettingsSnapshot(SaveSettings source)
    {
        return new SaveSettings
        {
            ImageEnabled = source.ImageEnabled,
            TextEnabled = source.TextEnabled,
            MarkdownEnabled = source.MarkdownEnabled,
            JsonEnabled = source.JsonEnabled,
            CsvEnabled = source.CsvEnabled,
            ImageFormat = source.ImageFormat,
            JpgQuality = source.JpgQuality,
            FileNamePrefix = source.FileNamePrefix,
            IncludeTimestamp = source.IncludeTimestamp
        };
    }

    private static BitmapSource CreateThreadSafeImageSnapshot(BitmapSource source)
    {
        var normalizedSource = source;
        var format = normalizedSource.Format;

        // CopyPixels requires a concrete pixel format. BPP=0 formats must be normalized first.
        if (format.BitsPerPixel <= 0)
        {
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = normalizedSource;
            converted.DestinationFormat = PixelFormats.Pbgra32;
            converted.EndInit();
            normalizedSource = converted;
            format = normalizedSource.Format;
        }

        var width = normalizedSource.PixelWidth;
        var height = normalizedSource.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Image dimensions are invalid.");
        }

        var bitsPerPixel = format.BitsPerPixel;
        var stride = checked((width * bitsPerPixel + 7) / 8);
        var buffer = new byte[checked(stride * height)];
        normalizedSource.CopyPixels(buffer, stride, 0);

        var dpiX = normalizedSource.DpiX > 0 ? normalizedSource.DpiX : 96;
        var dpiY = normalizedSource.DpiY > 0 ? normalizedSource.DpiY : 96;

        var snapshot = BitmapSource.Create(
            width,
            height,
            dpiX,
            dpiY,
            format,
            normalizedSource.Palette,
            buffer,
            stride);

        if (!snapshot.IsFrozen && snapshot.CanFreeze)
        {
            snapshot.Freeze();
        }

        return snapshot;
    }

    private void NotifyResultSafely(SaveResult result)
    {
        try
        {
            _notificationService.NotifyResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification");
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposeRequested, 1);

        if (Volatile.Read(ref _activeExecutions) == 0)
        {
            TryDisposeExecutionGateIfIdle();
        }

        GC.SuppressFinalize(this);
    }

    private void TryReleaseExecutionGateAfterExecution()
    {
        try
        {
            _saveExecutionGate.Exit();
        }
        catch (ObjectDisposedException) when (IsDisposeRequested())
        {
            _logger.LogDebug("Skipped save execution gate release because pipeline disposal is in progress");
        }
    }

    private void TryReleaseExecutionGateAfterDisposeRequest()
    {
        try
        {
            _saveExecutionGate.Exit();
        }
        catch (ObjectDisposedException) when (IsDisposeRequested())
        {
            _logger.LogDebug("Skipped save execution gate release after disposal request");
        }
    }

    private void TryDisposeExecutionGateIfIdle()
    {
        try
        {
            if (!_saveExecutionGate.TryDisposeIfIdle())
            {
                return;
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool IsDisposeRequested()
    {
        return Volatile.Read(ref _disposeRequested) == 1;
    }
}
