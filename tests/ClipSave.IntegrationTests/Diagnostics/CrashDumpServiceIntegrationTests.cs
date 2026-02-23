using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class CrashDumpServiceIntegrationTests : IDisposable
{
    private readonly string _testDumpPath;
    private readonly ILoggerFactory _loggerFactory;

    public CrashDumpServiceIntegrationTests()
    {
        _testDumpPath = Path.Combine(Path.GetTempPath(), $"ClipSave_Dump_Integration_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDumpPath);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDumpPath))
        {
            try
            {
                Directory.Delete(_testDumpPath, true);
            }
            catch { }
        }

        // Clean up dump files generated during this test run.
        var realDumpDir = CrashDumpService.GetDumpDirectory();
        if (Directory.Exists(realDumpDir))
        {
            var testFiles = Directory.GetFiles(realDumpDir, "crash-*.txt")
                .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-5))
                .ToList();

            foreach (var file in testFiles)
            {
                try { File.Delete(file); } catch { }
            }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    [Spec("SPEC-070-001")]
    [Spec("SPEC-070-002")]
    [Spec("SPEC-070-004")]
    public void CrashDump_WritesFileWithCorrectContent()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception for integration");
        var settings = new AppSettings
        {
            Version = "1.0",
            Save = new SaveSettings { ImageFormat = "png", JpgQuality = 90 },
            Notification = new NotificationSettings()
        };

        // Act
        var dumpPath = CrashDumpService.WriteDump(exception, "Integration_Test", settings);

        // Assert
        dumpPath.Should().NotBeNull();
        File.Exists(dumpPath).Should().BeTrue();

        var content = File.ReadAllText(dumpPath!);
        content.Should().Contain("ClipSave Crash Dump");
        content.Should().Contain("Integration_Test");
        content.Should().Contain("Test exception for integration");
        content.Should().Contain("InvalidOperationException");
        content.Should().Contain("[Settings Snapshot]");
        content.Should().Contain("\"ImageFormat\": \"png\"");
    }

    [Fact]
    public void CrashDump_HandlesNestedExceptions()
    {
        // Arrange
        var innerException = new ArgumentNullException("parameter", "Parameter is null");
        var middleException = new InvalidOperationException("Middle exception", innerException);
        var outerException = new ApplicationException("Application error", middleException);

        // Act
        var dumpPath = CrashDumpService.WriteDump(outerException, "NestedTest", null);

        // Assert
        dumpPath.Should().NotBeNull();
        var content = File.ReadAllText(dumpPath!);

        content.Should().Contain("ApplicationException");
        content.Should().Contain("Application error");
        content.Should().Contain("InvalidOperationException");
        content.Should().Contain("Middle exception");
        content.Should().Contain("ArgumentNullException");
        content.Should().Contain("Parameter is null");
        content.Should().Contain("[Inner Exception (Depth: 1)]");
    }

    [Fact]
    public void CrashDump_HandlesAggregateException()
    {
        // Arrange: create an AggregateException with multiple inner exceptions.
        var exceptions = new Exception[]
        {
            new InvalidOperationException("Task 1 error"),
            new ArgumentException("Task 2 error"),
            new NullReferenceException("Task 3 error")
        };
        var aggregateException = new AggregateException("Multiple errors occurred", exceptions);

        // Act
        var dumpPath = CrashDumpService.WriteDump(aggregateException, "AggregateTest", null);

        // Assert
        dumpPath.Should().NotBeNull();
        var content = File.ReadAllText(dumpPath!);

        content.Should().Contain("AggregateException");
        content.Should().Contain("[Aggregate Exception #1]");
        content.Should().Contain("Task 1 error");
        content.Should().Contain("Task 2 error");
        content.Should().Contain("Task 3 error");
    }

    [Fact]
    public void CrashDump_FileNameFormat()
    {
        // Arrange
        var exception = new Exception("File name test");
        var beforeWrite = DateTime.Now;

        // Act
        var dumpPath = CrashDumpService.WriteDump(exception, "FileNameTest", null);

        // Assert
        dumpPath.Should().NotBeNull();

        var fileName = Path.GetFileName(dumpPath!);
        fileName.Should().StartWith("crash-");
        fileName.Should().EndWith(".txt");

        // Extract and validate the timestamp part in the file name.
        var datePart = fileName.Replace("crash-", "").Replace(".txt", "");
        datePart.Should().MatchRegex(@"^\d{8}-\d{6}-\d{3}$"); // yyyyMMdd-HHmmss-fff
    }

    [Fact]
    public void CrashDump_ContainsSystemInformation()
    {
        // Arrange
        var exception = new Exception("System info test");

        // Act
        var dumpPath = CrashDumpService.WriteDump(exception, "SystemInfoTest", null);

        // Assert
        dumpPath.Should().NotBeNull();
        var content = File.ReadAllText(dumpPath!);

        content.Should().Contain(".NET Version:");
        content.Should().Contain("OS:");
        content.Should().Contain("Architecture:");

        content.Should().Contain("Working Set:");
        content.Should().Contain("Private Memory:");
        content.Should().Contain("GC Heap Size:");

        content.Should().MatchRegex(@"\d+\.\d{2} (B|KB|MB|GB)");
    }

    [Fact]
    public void CrashDump_IncludesStackTrace()
    {
        // Arrange
        Exception? capturedException = null;
        try
        {
            ThrowExceptionWithStackTrace();
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        capturedException.Should().NotBeNull();

        // Act
        var dumpPath = CrashDumpService.WriteDump(capturedException!, "StackTraceTest", null);

        // Assert
        dumpPath.Should().NotBeNull();
        var content = File.ReadAllText(dumpPath!);

        content.Should().Contain("Stack Trace:");
        content.Should().Contain("ThrowExceptionWithStackTrace");
    }

    [Fact]
    public void CrashDump_WorksWithoutSettings()
    {
        // Arrange
        var exception = new Exception("No settings test");

        // Act
        var dumpPath = CrashDumpService.WriteDump(exception, "NoSettingsTest", null);

        // Assert
        dumpPath.Should().NotBeNull();
        var content = File.ReadAllText(dumpPath!);

        content.Should().Contain("No settings test");
        content.Should().NotContain("[Settings Snapshot]");
    }

    [Fact]
    public void CrashDump_MultipleDumpsHaveUniqueNames()
    {
        // Arrange
        var exception = new Exception("Duplicate test");
        var timestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var timestampStep = 0;

        // Act: generate multiple dumps with deterministic timestamps.
        var paths = new List<string?>();
        for (int i = 0; i < 3; i++)
        {
            paths.Add(CrashDumpService.WriteDump(
                exception,
                $"UniqueTest_{i}",
                null,
                () => timestamp.AddMilliseconds(timestampStep++)));
        }

        // Assert
        paths.Should().OnlyContain(p => p != null);
        paths.Distinct().Should().HaveCount(3); // All paths should be unique.
    }

    private static void ThrowExceptionWithStackTrace()
    {
        HelperMethod1();
    }

    private static void HelperMethod1()
    {
        HelperMethod2();
    }

    private static void HelperMethod2()
    {
        throw new InvalidOperationException("Exception for stack trace test");
    }
}

