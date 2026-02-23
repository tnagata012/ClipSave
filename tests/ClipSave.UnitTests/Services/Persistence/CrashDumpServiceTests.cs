using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class CrashDumpServiceTests : IDisposable
{
    private readonly string _testDumpPath;

    public CrashDumpServiceTests()
    {
        _testDumpPath = Path.Combine(Path.GetTempPath(), $"ClipSave_DumpTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDumpPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDumpPath))
        {
            try
            {
                Directory.Delete(_testDumpPath, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void BuildDumpContent_ContainsBasicInfo()
    {
        var exception = new InvalidOperationException("Test exception");
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 45);

        var content = CrashDumpService.BuildDumpContent(exception, "TestSource", timestamp, null);

        content.Should().Contain("ClipSave Crash Dump");
        content.Should().Contain("2024-01-15 10:30:45");
        content.Should().Contain("TestSource");
    }

    [Fact]
    public void BuildDumpContent_ContainsExceptionDetails()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("Test exception message");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("InvalidOperationException");
        content.Should().Contain("Test exception message");
        content.Should().Contain("Stack Trace");
    }

    [Fact]
    public void BuildDumpContent_ContainsInnerException()
    {
        var innerException = new ArgumentException("Inner exception");
        var exception = new InvalidOperationException("Outer exception", innerException);
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("Outer exception");
        content.Should().Contain("Inner exception");
        content.Should().Contain("ArgumentException");
    }

    [Fact]
    public void BuildDumpContent_ContainsAggregateExceptionDetails()
    {
        var exceptions = new Exception[]
        {
            new InvalidOperationException("Exception 1"),
            new ArgumentException("Exception 2"),
            new NullReferenceException()
        };
        var aggregateException = new AggregateException("Aggregate exception", exceptions);
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(aggregateException, "Test", timestamp, null);

        content.Should().Contain("Exception 1");
        content.Should().Contain("Exception 2");
        content.Should().Contain("Aggregate exception");
    }

    [Fact]
    public void BuildDumpContent_ContainsSystemInfo()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("Version:");
        content.Should().Contain(".NET Version:");
        content.Should().Contain("OS:");
        content.Should().Contain("Architecture:");
    }

    [Fact]
    public void BuildDumpContent_ContainsMemoryInfo()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("Working Set:");
        content.Should().Contain("Private Memory:");
        content.Should().Contain("GC Heap Size:");
    }

    [Fact]
    public void BuildDumpContent_ContainsSettingsInfo_WhenProvided()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.Now;
        var settings = new AppSettings
        {
            Save = new SaveSettings { ImageFormat = "png", JpgQuality = 85 },
            Notification = new NotificationSettings()
        };

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, settings);

        content.Should().Contain("[Settings Snapshot]");
        content.Should().Contain("\"ImageFormat\": \"png\"");
        content.Should().Contain("\"JpgQuality\": 85");
    }

    [Fact]
    public void BuildDumpContent_OmitsSettings_WhenNull()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().NotContain("[Settings Snapshot]");
    }

    [Fact]
    public void BuildDumpContent_ContainsHeaderAndFooter()
    {
        var exception = new Exception("Test");
        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("================================================================================");
        content.Should().Contain("End of Dump");
    }

    [Fact]
    public void BuildDumpContent_HandlesNullStackTrace()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("Test");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var timestamp = DateTime.Now;

        var content = CrashDumpService.BuildDumpContent(exception, "Test", timestamp, null);

        content.Should().Contain("Stack Trace:");
    }

    [Fact]
    public void WriteDump_CreatesFile()
    {
        var exception = new InvalidOperationException("Test exception");

        var dumpPath = CrashDumpService.WriteDump(exception, "Test", null);

        dumpPath.Should().NotBeNull();
        File.Exists(dumpPath).Should().BeTrue();

        var content = File.ReadAllText(dumpPath!);
        content.Should().Contain("Test exception");

        if (dumpPath != null && File.Exists(dumpPath))
        {
            File.Delete(dumpPath);
        }
    }

    [Fact]
    public void WriteDump_FileNameContainsTimestamp()
    {
        var exception = new Exception("Test");
        var beforeWrite = DateTime.Now;

        var dumpPath = CrashDumpService.WriteDump(exception, "Test", null);

        dumpPath.Should().NotBeNull();
        var fileName = Path.GetFileName(dumpPath!);
        fileName.Should().StartWith("crash-");
        fileName.Should().EndWith(".txt");

        if (dumpPath != null && File.Exists(dumpPath))
        {
            File.Delete(dumpPath);
        }
    }
}
