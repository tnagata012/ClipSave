using ClipSave.Models;
using ClipSave.Services;
using ClipSave.ViewModels.About;
using ClipSave.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace ClipSave.Infrastructure.Startup;

internal static class AppServiceProviderFactory
{
    internal const int LogRetainedFileCountLimit = 7;
    private const string LogFileNamePattern = "clipsave-{Date}.log";

    public static IServiceProvider CreateServiceProvider()
    {
        return CreateServiceProvider(
            AppDataPaths.GetSettingsFilePath(),
            AppDataPaths.GetLogDirectory());
    }

    internal static IServiceProvider CreateServiceProvider(
        string settingsPath,
        string logDirectory,
        Action<ILoggingBuilder, string, int, LogLevel>? configureFileLogger = null)
    {
        var services = new ServiceCollection();
        var resolvedSettingsPath = ResolveSettingsPath(settingsPath);
        var settingsDirectory = Path.GetDirectoryName(resolvedSettingsPath) ?? AppDataPaths.GetSettingsDirectory();
        var loggingEnabled = ReadLoggingEnabled(resolvedSettingsPath);

        configureFileLogger ??= static (builder, filePath, retainedFileCountLimit, minimumLevel) =>
            builder.AddFile(filePath,
                retainedFileCountLimit: retainedFileCountLimit,
                minimumLevel: minimumLevel);

        services.AddLogging(builder =>
        {
            if (loggingEnabled)
            {
                Directory.CreateDirectory(logDirectory);
                builder.AddConsole();
                builder.AddDebug();
                configureFileLogger(
                    builder,
                    Path.Combine(logDirectory, LogFileNamePattern),
                    LogRetainedFileCountLimit,
                    LogLevel.Debug);
                builder.SetMinimumLevel(LogLevel.Debug);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            }
        });

        services.AddSingleton<SingleInstanceService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<SettingsService>(provider =>
            new SettingsService(
                provider.GetRequiredService<ILogger<SettingsService>>(),
                settingsDirectory));
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<ImageEncodingService>();
        services.AddSingleton<ContentEncodingService>();
        services.AddSingleton<FileStorageService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<ActiveWindowService>();
        services.AddSingleton<SavePipeline>();
        services.AddSingleton<TrayService>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider();
    }

    private static string ResolveSettingsPath(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            return AppDataPaths.GetSettingsFilePath();
        }

        return Path.GetFullPath(settingsPath);
    }

    private static bool ReadLoggingEnabled(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return false;
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SettingsService.CreateJsonOptions());
            return settings?.Advanced?.Logging == true;
        }
        catch
        {
            return false;
        }
    }
}
