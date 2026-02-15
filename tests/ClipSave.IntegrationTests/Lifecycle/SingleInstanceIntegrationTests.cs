using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class SingleInstanceIntegrationTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<SingleInstanceService> _services = [];
    private readonly string _testId = Guid.NewGuid().ToString("N");

    public SingleInstanceIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        foreach (var service in _services)
        {
            service.Dispose();
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    [Spec("SPEC-000-006")]
    public void TryAcquireOrNotify_SecondInstance_ReturnsFalse()
    {
        var first = CreateService();
        first.TryAcquireOrNotify().Should().BeTrue();

        var second = CreateService();
        var started = second.TryAcquireOrNotify();

        started.Should().BeFalse();
    }

    [Fact]
    [Spec("SPEC-000-007")]
    public async Task TryAcquireOrNotify_SecondInstance_RaisesSecondInstanceLaunched()
    {
        var first = CreateService();
        first.TryAcquireOrNotify().Should().BeTrue();

        var launched = new TaskCompletionSource<bool>();
        first.SecondInstanceLaunched += (_, _) => launched.TrySetResult(true);

        var second = CreateService();
        second.TryAcquireOrNotify();

        var completed = await Task.WhenAny(launched.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(launched.Task, "secondary instance should notify the primary instance");
        (await launched.Task).Should().BeTrue();
    }

    private SingleInstanceService CreateService()
    {
        var mutexName = $"Local\\ClipSave_Integration_SingleInstance_{_testId}";
        var pipeName = $"ClipSave_Integration_SingleInstance_{_testId}";
        var service = new SingleInstanceService(
            _loggerFactory.CreateLogger<SingleInstanceService>(),
            mutexName,
            pipeName);
        _services.Add(service);
        return service;
    }
}
