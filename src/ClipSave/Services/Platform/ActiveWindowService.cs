using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipSave.Services;

public enum ActiveWindowKind
{
    Other,
    Desktop,
    Explorer
}

public record ActiveWindowResult(ActiveWindowKind Kind, string? FolderPath);

public class ActiveWindowService
{
    private readonly ILogger<ActiveWindowService> _logger;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public ActiveWindowService(ILogger<ActiveWindowService> logger)
    {
        _logger = logger;
    }

    public ActiveWindowResult GetActiveWindowInfo()
    {
        try
        {
            var hWnd = GetForegroundWindow();

            if (hWnd == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to get foreground window");
                return new ActiveWindowResult(ActiveWindowKind.Other, null);
            }

            var className = GetWindowClassName(hWnd);
            _logger.LogDebug("Active window class name: {ClassName}", className);

            if (IsDesktopClassName(className))
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (TryNormalizeExistingDirectoryPath(desktopPath, out var normalizedDesktopPath))
                {
                    _logger.LogDebug("Detected desktop window: {Path}", normalizedDesktopPath);
                    return new ActiveWindowResult(ActiveWindowKind.Desktop, normalizedDesktopPath);
                }

                _logger.LogWarning("Failed to resolve desktop path: {Path}", desktopPath);
                return new ActiveWindowResult(ActiveWindowKind.Other, null);
            }

            if (IsExplorerClassName(className))
            {
                var explorerPath = GetExplorerPath(hWnd);
                if (!TryNormalizeExistingDirectoryPath(explorerPath, out var normalizedExplorerPath))
                {
                    _logger.LogWarning("Failed to resolve Explorer path (possibly non-file-system folder): {Path}", explorerPath);
                    return new ActiveWindowResult(ActiveWindowKind.Other, null);
                }

                _logger.LogDebug("Detected Explorer window: {Path}", normalizedExplorerPath);
                return new ActiveWindowResult(ActiveWindowKind.Explorer, normalizedExplorerPath);
            }

            _logger.LogDebug("Active window is out of save scope: {ClassName}", className);
            return new ActiveWindowResult(ActiveWindowKind.Other, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while resolving active window");
            return new ActiveWindowResult(ActiveWindowKind.Other, null);
        }
    }

    private string GetWindowClassName(IntPtr hWnd)
    {
        var className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString();
    }

    private static bool IsDesktopClassName(string className)
    {
        return className == "Progman" || className == "WorkerW";
    }

    private static bool IsExplorerClassName(string className)
    {
        return className == "CabinetWClass" || className == "ExploreWClass";
    }

    internal static bool TryNormalizeExistingDirectoryPath(string? candidatePath, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var path = candidatePath.Trim();

        // Shell namespace paths (e.g., ::{GUID}) are not file-system directories.
        if (path.StartsWith("::", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                return false;
            }

            normalizedPath = fullPath;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string? GetExplorerPath(IntPtr targetHwnd)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
            {
                _logger.LogWarning("Failed to resolve Shell.Application type");
                return null;
            }

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                _logger.LogWarning("Failed to create Shell.Application instance");
                return null;
            }

            try
            {
                dynamic? windows = null;
                try
                {
                    windows = shell.Windows();
                    foreach (dynamic window in windows)
                    {
                        try
                        {
                            if (window.HWND == (long)targetHwnd)
                            {
                                string? path = window.Document?.Folder?.Self?.Path;
                                return path;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                        finally
                        {
                            ReleaseComObjectIfNeeded(window);
                        }
                    }
                }
                finally
                {
                    ReleaseComObjectIfNeeded(windows);
                }
            }
            finally
            {
                ReleaseComObjectIfNeeded(shell);
            }

            return null;
        }
        catch (COMException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Explorer path (COM error)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Explorer path");
            return null;
        }
    }

    private static void ReleaseComObjectIfNeeded(object? comObject)
    {
        try
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch
        {
        }
    }
}
